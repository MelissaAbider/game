from backend.ai.model_rules import ECHO_PERSONALITY_RULES


def echo_rules() -> str:
    return ECHO_PERSONALITY_RULES


def deterministic_echo_reply(event: str) -> str:
    replies = {
        "accepted": "Command accepted.",
        "unlocked": "Sector unlocked.",
        "denied": "Access denied.",
        "ambiguous": "Which object?",
        "failed": "I couldn't interpret that.",
        "cancelled": "Command cancelled.",
    }
    return replies.get(event, "Interesting.")
