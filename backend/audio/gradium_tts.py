import asyncio
import base64
import logging
from collections.abc import AsyncIterator

from backend.config import get_settings

logger = logging.getLogger("echoshift.gradium.tts")


async def stream_tts(text: str) -> AsyncIterator[bytes]:
    settings = get_settings()
    if not settings.gradium_api_key or not text.strip():
        return

    try:
        import gradium

        client = gradium.client.GradiumClient(api_key=settings.gradium_api_key)
        async with client.tts_realtime(
            voice_id=settings.gradium_voice_id,
            output_format="wav",
            model_name="default",
        ) as tts:
            async def sender() -> None:
                await tts.send_text(text[:220])
                await tts.send_eos()

            sender_task = asyncio.create_task(sender())
            async for msg in tts:
                msg_type = msg.get("type")
                if msg_type == "audio":
                    audio = msg.get("audio", b"")
                    if isinstance(audio, str):
                        yield base64.b64decode(audio)
                    else:
                        yield audio
                elif msg_type == "end_of_stream":
                    break
            await sender_task
    except Exception as exc:
        logger.warning("[GRADIUM TTS] Skipping speech after TTS error: %s", exc)
        return
