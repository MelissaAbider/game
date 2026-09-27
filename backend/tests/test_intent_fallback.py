import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from ai.intent_parser import is_emergency_stop, local_fallback_parse


WORLD_STATE = {
    "player": {"x": 126, "y": 390, "facing": "right", "grounded": True},
    "allowedActions": [
        "MOVE_LEFT",
        "MOVE_RIGHT",
        "MOVE_TO",
        "JUMP",
        "JUMP_OVER",
        "CROUCH",
        "STAND",
        "WAIT",
        "STOP",
        "INTERACT",
        "USE",
        "FOLLOW",
        "AVOID",
    ],
    "visibleObjects": [
        {
            "id": "red_door",
            "type": "door",
            "label": "red door",
            "color": "red",
            "x": 836,
            "y": 366,
            "tags": ["door", "exit", "red door", "right door"],
        },
        {
            "id": "blue_terminal",
            "type": "terminal",
            "label": "blue terminal",
            "color": "blue",
            "x": 638,
            "y": 374,
            "tags": ["terminal", "computer", "console", "screen", "blue thing", "control panel"],
        },
        {
            "id": "laser_01",
            "type": "hazard",
            "label": "red laser",
            "color": "red",
            "x": 382,
            "y": 386,
            "tags": ["laser", "hazard", "beam", "obstacle"],
        },
    ],
}


COMMAND_CASES = [
    ("Go right.", ["MOVE_RIGHT"]),
    ("Move to the right.", ["MOVE_RIGHT"]),
    ("Head right.", ["MOVE_RIGHT"]),
    ("Walk toward the right side.", ["MOVE_RIGHT"]),
    ("Go that way.", ["MOVE_RIGHT"]),
    ("Move over there.", ["MOVE_RIGHT"]),
    ("Go left.", ["MOVE_LEFT"]),
    ("Head left.", ["MOVE_LEFT"]),
    ("Walk to the left side.", ["MOVE_LEFT"]),
    ("Run left.", ["MOVE_LEFT"]),
    ("Jump.", ["JUMP"]),
    ("Jump over the laser.", ["JUMP_OVER"]),
    ("Hop over the laser obstacle.", ["JUMP_OVER"]),
    ("Get over that beam.", ["JUMP_OVER"]),
    ("Run to the red door.", ["MOVE_TO"]),
    ("Head toward the red door.", ["MOVE_TO"]),
    ("Go to the door.", ["MOVE_TO"]),
    ("Move to the exit.", ["MOVE_TO"]),
    ("Keep going toward the door.", ["MOVE_TO"]),
    ("Go to the blue terminal.", ["MOVE_TO"]),
    ("Get closer to that computer.", ["MOVE_TO"]),
    ("Move toward the console.", ["MOVE_TO"]),
    ("Go to the blue thing.", ["MOVE_TO"]),
    ("Use the blue thing.", ["MOVE_TO", "INTERACT"]),
    ("Activate the computer.", ["MOVE_TO", "INTERACT"]),
    ("Touch the blue console.", ["MOVE_TO", "INTERACT"]),
    ("Press the terminal.", ["MOVE_TO", "INTERACT"]),
    ("Go to the blue terminal and activate it.", ["MOVE_TO", "INTERACT"]),
    ("Run toward the door but stop before the laser.", ["MOVE_TO"]),
    ("Stop before that laser.", ["MOVE_TO"]),
    ("Jump over the laser, go to the terminal and use it.", ["JUMP_OVER", "MOVE_TO", "INTERACT"]),
    ("Run right.", ["MOVE_RIGHT"]),
    ("Please head right.", ["MOVE_RIGHT"]),
    ("Can you go over there?", ["MOVE_RIGHT"]),
    ("Crouch.", ["CROUCH"]),
    ("Duck under the beam.", ["CROUCH"]),
    ("Crouch under the laser and stand back up afterwards.", ["CROUCH", "STAND"]),
    ("Run to the computer and use it.", ["MOVE_TO", "INTERACT"]),
    ("Get closer to the screen.", ["MOVE_TO"]),
    ("Go through the red door.", ["MOVE_TO"]),
]


def test_forty_command_formulations_have_expected_first_actions():
    assert len(COMMAND_CASES) >= 40
    for text, expected in COMMAND_CASES:
        result = local_fallback_parse(text, WORLD_STATE)
        assert not result.requires_clarification, text
        assert [action.type for action in result.actions] == expected


def test_emergency_stop_is_privileged():
    for text in ["Stop", "cancel.", "WAIT", "freeze"]:
        assert is_emergency_stop(text)


def test_autopilot_request_requires_clarification():
    result = local_fallback_parse("Solve the level.", WORLD_STATE)
    assert result.requires_clarification
    assert not result.actions
