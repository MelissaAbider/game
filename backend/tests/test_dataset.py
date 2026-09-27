import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from ai.intent_parser import local_fallback_parse


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
            "tags": ["door", "exit", "red door"],
        },
        {
            "id": "blue_terminal",
            "type": "terminal",
            "label": "blue terminal",
            "color": "blue",
            "x": 638,
            "y": 374,
            "tags": ["terminal", "computer", "console", "screen", "blue thing"],
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

FORMULATION_DATASET = [
    ("Go right.", "MOVE_RIGHT"),
    ("Move to the right.", "MOVE_RIGHT"),
    ("Head right.", "MOVE_RIGHT"),
    ("Walk toward the right side.", "MOVE_RIGHT"),
    ("Go that way.", "MOVE_RIGHT"),
    ("Move over there.", "MOVE_RIGHT"),
    ("Go left.", "MOVE_LEFT"),
    ("Head left.", "MOVE_LEFT"),
    ("Walk to the left side.", "MOVE_LEFT"),
    ("Run left.", "MOVE_LEFT"),
    ("Jump.", "JUMP"),
    ("Jump over the laser.", "JUMP_OVER"),
    ("Hop over the laser obstacle.", "JUMP_OVER"),
    ("Get over that beam.", "JUMP_OVER"),
    ("Run to the red door.", "MOVE_TO"),
    ("Head toward the red door.", "MOVE_TO"),
    ("Go to the door.", "MOVE_TO"),
    ("Move to the exit.", "MOVE_TO"),
    ("Keep going toward the door.", "MOVE_TO"),
    ("Go to the blue terminal.", "MOVE_TO"),
    ("Get closer to that computer.", "MOVE_TO"),
    ("Move toward the console.", "MOVE_TO"),
    ("Go to the blue thing.", "MOVE_TO"),
    ("Use the blue thing.", "MOVE_TO"),
    ("Activate the computer.", "MOVE_TO"),
    ("Touch the blue console.", "MOVE_TO"),
    ("Press the terminal.", "MOVE_TO"),
    ("Go to the blue terminal and activate it.", "MOVE_TO"),
    ("Run toward the door but stop before the laser.", "MOVE_TO"),
    ("Stop before that laser.", "MOVE_TO"),
    ("Jump over the laser, go to the terminal and use it.", "JUMP_OVER"),
    ("Run right.", "MOVE_RIGHT"),
    ("Please head right.", "MOVE_RIGHT"),
    ("Can you go over there?", "MOVE_RIGHT"),
    ("Crouch.", "CROUCH"),
    ("Duck under the beam.", "CROUCH"),
    ("Crouch under the laser and stand back up afterwards.", "CROUCH"),
    ("Run to the computer and use it.", "MOVE_TO"),
    ("Get closer to the screen.", "MOVE_TO"),
    ("Go through the red door.", "MOVE_TO"),
]


def test_forty_plus_formulations_have_expected_primary_action() -> None:
    assert len(FORMULATION_DATASET) >= 40
    for phrase, expected_primary_action in FORMULATION_DATASET:
        result = local_fallback_parse(phrase, WORLD_STATE)
        assert result.actions, phrase
        assert result.actions[0].type == expected_primary_action


def test_autopilot_requests_are_rejected() -> None:
    for phrase in ["Solve the level for me.", "Beat the game."]:
        result = local_fallback_parse(phrase, WORLD_STATE)
        assert result.requires_clarification
        assert not result.actions
