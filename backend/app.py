import logging
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from backend.config import get_settings
from backend.ai.design_assets import ASSET_DIR
from backend.routes.design import router as design_router
from backend.routes.intent import router as intent_router
from backend.routes.voice import router as voice_router
from backend.routes.session import router as session_router

settings = get_settings()

logging.basicConfig(
    level=logging.DEBUG,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s"
)
logger = logging.getLogger("echoshift")

app = FastAPI(
    title="EchoShift Lab Backend",
    description="Voice STT/TTS bridge and Gemini structured intent parser.",
    version="0.1.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # the Unity client (desktop or phone on the LAN) calls from anywhere
    allow_credentials=False,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(session_router)
app.include_router(design_router)
app.include_router(intent_router)
app.include_router(voice_router)
ASSET_DIR.mkdir(parents=True, exist_ok=True)
app.mount("/assets", StaticFiles(directory=ASSET_DIR), name="assets")


@app.get("/")
async def root() -> dict:
    return {
        "project": "EchoShift Lab",
        "status": "ready",
        "model": settings.gemini_model,
        "image_model": settings.gemini_image_model,
    }


@app.get("/api/health")
async def health() -> dict:
    return {
        "ok": True,
        "gemini_configured": bool(settings.gemini_api_key),
        "gradium_configured": bool(settings.gradium_api_key),
    }
