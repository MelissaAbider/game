from functools import lru_cache
from pathlib import Path

from pydantic_settings import BaseSettings, SettingsConfigDict

ENV_FILE = Path(__file__).with_name(".env")


class Settings(BaseSettings):
    gemini_api_key: str = ""
    gradium_api_key: str = ""
    gemini_model: str = "gemini-3.8-flash"
    gemini_image_model: str = "gemini-3.1-flash-image"
    gradium_voice_id: str = "YTpq7expH9539ERJ"

    model_config = SettingsConfigDict(env_file=ENV_FILE, env_file_encoding="utf-8", extra="ignore")


@lru_cache
def get_settings() -> Settings:
    return Settings()
