from fastapi import APIRouter, Query
from pydantic import BaseModel, Field

from backend.ai.design_assets import (
    GAME_IDS,
    generate_assets,
    generate_custom_texture,
    generate_key_art,
    home_payload,
    theme_payload,
)

router = APIRouter(prefix="/api/design", tags=["design"])


@router.get("/theme")
async def get_theme() -> dict:
    return theme_payload()


@router.get("/assets")
async def get_assets(ensure: bool = Query(default=True)) -> dict:
    """Return the asset manifest. With ensure=true, generates any missing asset first."""
    if ensure:
        return await generate_assets()
    return theme_payload()


@router.post("/assets/regenerate")
async def regenerate_assets(ids: list[str] | None = None) -> dict:
    return await generate_assets(ids, force=True)


@router.post("/regenerate")
async def regenerate_theme() -> dict:
    return await generate_key_art(force=True)


class TextureRequest(BaseModel):
    material: str = Field(min_length=1, max_length=60)


@router.post("/texture")
async def texture(request: TextureRequest) -> dict:
    """On-demand seamless texture for an object the player conjured with words."""
    return await generate_custom_texture(request.material)


@router.post("/game")
async def game_art(force: bool = Query(default=False)) -> dict:
    """Paints (or repaints with force=true) the Unity game art: room backdrops, robot poses and obstacle props."""
    return await generate_assets(GAME_IDS, force=force)


@router.get("/home")
async def home(ensure: bool = Query(default=True)) -> dict:
    """Home screen art (Nano Banana): menu backdrop + serious and funny hero, with visor/eye boxes for animation."""
    return await home_payload(ensure)
