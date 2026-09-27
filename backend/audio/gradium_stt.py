import asyncio
import logging
from contextlib import suppress
from typing import Awaitable, Callable

from backend.config import get_settings

logger = logging.getLogger("echoshift.gradium.stt")

TextHandler = Callable[[str], Awaitable[None]]
FinalHandler = Callable[[str], Awaitable[None]]
ErrorHandler = Callable[[str], Awaitable[None]]


class GradiumSTTSession:
    def __init__(self, on_text: TextHandler, on_final: FinalHandler, on_error: ErrorHandler | None = None) -> None:
        self.settings = get_settings()
        self.on_text = on_text
        self.on_final = on_final
        self.on_error = on_error
        self.queue: asyncio.Queue[bytes | None] = asyncio.Queue()
        self.segments: list[str] = []
        self._finished = asyncio.Event()

    async def send_audio(self, chunk: bytes) -> None:
        await self.queue.put(chunk)

    async def finish(self) -> None:
        await self.queue.put(None)
        await self._finished.wait()

    async def run(self) -> None:
        if not self.settings.gradium_api_key:
            await self.on_final("")
            self._finished.set()
            return

        try:
            import gradium

            client = gradium.client.GradiumClient(api_key=self.settings.gradium_api_key)
            async with client.stt_realtime(
                model_name="default",
                input_format="pcm_24000",
                json_config={"language": "en", "delay_in_frames": 10},
                wait_for_ready_on_start=True,
            ) as stt:
                sender = asyncio.create_task(self._sender(stt))
                receiver = asyncio.create_task(self._receiver(stt))
                # The receiver owns completion: it returns once Gradium confirms the flush.
                # Give it a grace period after the sender is done so the final words arrive.
                await sender
                try:
                    await asyncio.wait_for(receiver, timeout=2.5)
                except asyncio.TimeoutError:
                    logger.warning("[GRADIUM STT] No flush confirmation, emitting partial transcript.")
                    receiver.cancel()
                    with suppress(asyncio.CancelledError):
                        await receiver
                    await self.on_final(" ".join(self.segments).strip())
        except Exception as exc:
            logger.warning("[GRADIUM STT] Falling back after STT error: %s", exc)
            if self.on_error:
                with suppress(Exception):
                    await self.on_error(str(exc).splitlines()[0][:200] if str(exc) else "STT error")
            with suppress(Exception):
                await self.on_final(" ".join(self.segments).strip())
        finally:
            self._finished.set()

    async def _sender(self, stt) -> None:
        flush_id = 1
        while True:
            chunk = await self.queue.get()
            if chunk is None:
                await stt.send_flush(flush_id=flush_id)
                with suppress(Exception):
                    await stt.send_eos()
                return
            await stt.send_audio(chunk)

    async def _receiver(self, stt) -> None:
        async for msg in stt:
            msg_type = msg.get("type")
            if msg_type == "text":
                text = msg.get("text", "")
                if text:
                    self.segments.append(text)
                    await self.on_text(text)
            elif msg_type == "flushed":
                await self.on_final(" ".join(self.segments).strip())
                return
            elif msg_type == "end_of_stream":
                await self.on_final(" ".join(self.segments).strip())
                return
