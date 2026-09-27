from backend.ai.schemas import GameAction, IntentResponse
from backend.game.action_schema import ALLOWED_ACTIONS, TARGETED_ACTIONS


def visible_object_ids(world_state: dict) -> set[str]:
    return {item.get("id") for item in world_state.get("visibleObjects", []) if item.get("id")}


def validate_intent(intent: IntentResponse, world_state: dict) -> IntentResponse:
    ids = visible_object_ids(world_state)
    validated: list[GameAction] = []

    for action in intent.actions:
        if action.type not in ALLOWED_ACTIONS:
            continue
        if action.type == "BUILD" and action.target and action.target not in ids:
            # Build placement falls back to the subject's surroundings when the anchor is unknown.
            action.target = None
        if action.type in TARGETED_ACTIONS and action.target not in ids:
            continue
        if action.target and action.target not in ids and action.type not in {"INTERACT", "USE"}:
            continue
        validated.append(action)

    if intent.actions and not validated:
        return IntentResponse(
            actions=[],
            confidence=0.25,
            interpretation=intent.interpretation,
            requires_clarification=True,
            clarification="Which object?",
            echo="Which object?",
        )

    intent.actions = validated
    return intent
