import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from ai.intent_parser import is_emergency_stop, local_fallback_parse


WORLD_STATE = {
    "player": {"x": 100, "y": 400, "facing": "right", "grounded": True},
    "visibleObjects": [
        {
            "id": "red_door",
            "type": "door",
            "label": "red door",
            "color": "red",
            "x": 836,
            "y": 366,
            "tags": ["exit", "red door"],
        },
        {
            "id": "blue_terminal",
            "type": "terminal",
            "label": "blue terminal",
            "color": "blue",
            "x": 638,
            "y": 374,
            "tags": ["computer", "console", "screen", "blue thing"],
        },
        {
            "id": "laser_01",
            "type": "hazard",
            "label": "red laser",
            "color": "red",
            "x": 382,
            "y": 386,
            "tags": ["laser", "beam", "hazard"],
        },
    ],
}


def test_emergency_stop_fastpath() -> None:
    assert is_emergency_stop("STOP!")
    result = local_fallback_parse("stop", WORLD_STATE)
    assert [action.type for action in result.actions] == ["STOP"]
    assert result.confidence == 0.78


def test_interact_terminal_sequence() -> None:
    result = local_fallback_parse("Go to the computer and activate it", WORLD_STATE)
    assert [action.type for action in result.actions] == ["MOVE_TO", "INTERACT"]
    assert result.actions[0].target == "blue_terminal"


def test_stop_before_laser_sequence() -> None:
    result = local_fallback_parse("Run toward the red door but stop before the laser", WORLD_STATE)
    assert [action.type for action in result.actions] == ["MOVE_TO"]
    assert result.actions[0].target == "laser_01"
    assert result.actions[0].stop_offset == 88
