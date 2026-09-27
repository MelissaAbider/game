import json
import re

from google import genai
from google.genai import types
from pydantic import ValidationError

from backend.ai.model_rules import INTENT_INTERPRETER_SYSTEM_INSTRUCTION
from backend.ai.schemas import GameAction, IntentResponse
from backend.config import get_settings
from backend.game.validators import validate_intent


class IntentParser:
    def __init__(self) -> None:
        self.settings = get_settings()
        self.client = (
            genai.Client(api_key=self.settings.gemini_api_key)
            if self.settings.gemini_api_key
            else None
        )

    async def parse(self, transcript: str, world_state: dict) -> IntentResponse:
        if is_emergency_stop(transcript):
            return IntentResponse(
                actions=[GameAction(type="STOP")],
                confidence=1,
                interpretation="Stop immediately.",
                echo="Command cancelled.",
            )

        if not self.client:
            return validate_intent(local_fallback_parse(transcript, world_state), world_state)

        prompt = {
            "player_transcript": transcript,
            "world_state": world_state,
            "allowed_actions": world_state.get("allowedActions", []),
            "response_rules": {
                "confidence_threshold": 0.7,
                "max_actions": 8,
                "no_autopilot": "Do not solve the whole room from a vague request.",
            },
        }

        try:
            response = await self.client.aio.models.generate_content(
                model=self.settings.gemini_model,
                contents=json.dumps(prompt),
                config=types.GenerateContentConfig(
                    system_instruction=INTENT_INTERPRETER_SYSTEM_INSTRUCTION,
                    response_mime_type="application/json",
                    response_schema=IntentResponse,
                    temperature=0.15,
                ),
            )
        except Exception:
            return validate_intent(local_fallback_parse(transcript, world_state), world_state)

        try:
            parsed = IntentResponse.model_validate_json(response.text or "{}")
        except ValidationError:
            parsed = IntentResponse(
                actions=[],
                confidence=0,
                interpretation="Invalid structured output.",
                requires_clarification=True,
                clarification="I couldn't interpret that.",
                echo="I couldn't interpret that.",
            )

        return validate_intent(parsed, world_state)


def is_emergency_stop(text: str) -> bool:
    return bool(re.match(r"^\s*(stop|wait|cancel|freeze|hold on)[\s.!?]*$", text, re.I))


def local_fallback_parse(transcript: str, world_state: dict) -> IntentResponse:
    text = transcript.lower()
    actions: list[GameAction] = []

    door = find_object(world_state, "door", "red")
    terminal = find_object(world_state, "terminal", "blue")
    laser = find_object(world_state, "hazard", "laser")
    hazard_mentioned = "laser" in text or "beam" in text or "hazard" in text

    if is_emergency_stop(text):
        actions.append(GameAction(type="STOP"))
    elif "solve" in text or "finish the level" in text:
        return IntentResponse(
            actions=[],
            confidence=0.42,
            interpretation="Player asked for autopilot.",
            requires_clarification=True,
            clarification="Be more specific.",
            echo="Be more specific.",
        )
    else:
        if "left" in text and "door" not in text:
            actions.append(GameAction(type="MOVE_LEFT", speed="run" if "run" in text else "walk"))
        if ("right" in text or "over there" in text or "that way" in text) and not any(
            word in text for word in ["door", "terminal", "computer", "console", "thing", "laser", "beam"]
        ):
            actions.append(GameAction(type="MOVE_RIGHT", speed="run" if "run" in text else "walk"))
        if "stop before" in text and hazard_mentioned and laser:
            actions.append(
                GameAction(
                    type="MOVE_TO",
                    target=laser["id"],
                    speed="run" if "run" in text else "walk",
                    stop_offset=88,
                )
            )
        elif hazard_mentioned and ("jump" in text or "over" in text or "hop" in text) and laser:
            actions.append(GameAction(type="JUMP_OVER", target=laser["id"]))
        if any(word in text for word in ["terminal", "computer", "console", "blue thing", "screen"]) and terminal:
            actions.append(GameAction(type="MOVE_TO", target=terminal["id"], speed="run" if "run" in text else "walk"))
            if any(word in text for word in ["activate", "use", "interact", "press", "touch"]):
                actions.append(GameAction(type="INTERACT", target=terminal["id"]))
        if ("door" in text or "exit" in text) and door and not ("stop before" in text and hazard_mentioned):
            actions.append(GameAction(type="MOVE_TO", target=door["id"], speed="run" if "run" in text else "walk"))
        if text.strip() in {"jump", "jump."}:
            actions.append(GameAction(type="JUMP"))
        if "crouch" in text or "duck" in text:
            actions.append(GameAction(type="CROUCH", duration_ms=900))
            if "stand" in text:
                actions.append(GameAction(type="STAND"))

    if not actions:
        return IntentResponse(
            actions=[],
            confidence=0.35,
            interpretation="Ambiguous local fallback parse.",
            requires_clarification=True,
            clarification="I didn't understand that.",
            echo="I didn't understand that.",
        )

    return IntentResponse(
        actions=actions[:8],
        confidence=0.78,
        interpretation=f"Local fallback parsed: {transcript}",
        echo="Command accepted.",
    )


def find_object(world_state: dict, object_type: str, tag_or_color: str) -> dict | None:
    for item in world_state.get("visibleObjects", []):
        tags = " ".join(item.get("tags", []))
        if item.get("type") == object_type and (
            item.get("color") == tag_or_color or tag_or_color in tags or tag_or_color in item.get("label", "")
        ):
            return item
    return None
