import asyncio
import json
import logging
from contextlib import suppress

from fastapi import APIRouter, WebSocket, WebSocketDisconnect
from starlette.websockets import WebSocketDisconnect as StarletteWebSocketDisconnect

from backend.audio.gradium_stt import GradiumSTTSession
from backend.audio.gradium_tts import stream_tts

router = APIRouter()
logger = logging.getLogger("echoshift.voice")


# Gradium allows only a few concurrent STT sessions per key. The game only ever needs one,
# so a new push-to-talk always evicts the previous session instead of leaking it.
_active_stt: asyncio.Task | None = None
MAX_SESSION_S = 30
IDLE_TIMEOUT_S = 8


@router.websocket("/ws/stt")
async def stt_socket(websocket: WebSocket) -> None:
    global _active_stt
    await websocket.accept()

    async def on_text(text: str) -> None:
        await websocket.send_json({"type": "text", "text": text})

    async def on_final(transcript: str) -> None:
        await websocket.send_json({"type": "final", "transcript": transcript})

    async def on_error(message: str) -> None:
        await websocket.send_json({"type": "error", "message": message})

    if _active_stt and not _active_stt.done():
        logger.info("[STT] evicting previous session")
        _active_stt.cancel()
        with suppress(asyncio.CancelledError, Exception):
            await _active_stt

    session = GradiumSTTSession(on_text=on_text, on_final=on_final, on_error=on_error)
    runner = asyncio.create_task(session.run())
    _active_stt = runner
    loop = asyncio.get_running_loop()
    deadline = loop.time() + MAX_SESSION_S

    try:
        while True:
            remaining = deadline - loop.time()
            if remaining <= 0:
                logger.info("[STT] max session length reached, finishing")
                await session.finish()
                break
            try:
                message = await asyncio.wait_for(websocket.receive(), timeout=min(IDLE_TIMEOUT_S, remaining))
            except asyncio.TimeoutError:
                logger.info("[STT] client idle, finishing session")
                await asyncio.wait_for(session.finish(), timeout=8)
                break
            if message.get("type") == "websocket.disconnect":
                break
            if message.get("bytes") is not None:
                await session.send_audio(message["bytes"])
                continue
            if message.get("text"):
                data = json.loads(message["text"])
                if data.get("type") == "end":
                    await asyncio.wait_for(session.finish(), timeout=10)
                    break
    except (WebSocketDisconnect, StarletteWebSocketDisconnect, RuntimeError) as exc:
        logger.info("[STT] client disconnected: %s", exc)
    except Exception:
        logger.exception("[STT] socket failure")
    finally:
        if not runner.done():
            runner.cancel()
        with suppress(asyncio.CancelledError, Exception):
            await runner
        if _active_stt is runner:
            _active_stt = None
        with suppress(Exception):
            await websocket.close()


@router.websocket("/ws/tts")
async def tts_socket(websocket: WebSocket) -> None:
    await websocket.accept()
    try:
        raw = await websocket.receive_text()
        text = json.loads(raw).get("text", "")
        async for chunk in stream_tts(text):
            await websocket.send_bytes(chunk)
        await websocket.send_json({"type": "end"})
    except WebSocketDisconnect:
        return
    except Exception:
        logger.exception("[TTS] socket failure")
        with suppress(Exception):
            await websocket.send_json({"type": "end"})
