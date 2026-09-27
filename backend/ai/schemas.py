from typing import Any, Literal
from pydantic import BaseModel, Field, model_validator

ActionType = Literal[
    "MOVE_LEFT",
    "MOVE_RIGHT",
    "MOVE_FORWARD",
    "MOVE_BACK",
    "TURN_LEFT",
    "TURN_RIGHT",
    "TURN_AROUND",
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
    "BUILD",
    "ECHO",
    "RECYCLE",
]

Speed = Literal["walk", "run"]

# Objects the player can conjure with words in the 3D client (each has an energy cost there).
BuildKind = Literal["bridge", "stairs", "ramp", "platform", "crate", "shield", "light"]


class GameAction(BaseModel):
    type: ActionType
    target: str | None = None
    speed: Speed | None = None
    distance: int | None = Field(default=None, ge=10, le=600)
    stop_offset: int | None = Field(default=None, ge=0, le=240)
    duration_ms: int | None = Field(default=None, ge=100, le=10000)
    condition: str | None = None
    build_kind: BuildKind | None = None
    material: str | None = Field(default=None, max_length=60)

    @model_validator(mode="after")
    def target_required_for_targeted_actions(self) -> "GameAction":
        if self.type in {"MOVE_TO", "JUMP_OVER", "FOLLOW", "AVOID"} and not self.target:
            raise ValueError(f"{self.type} requires target")
        if self.type == "BUILD" and not self.build_kind:
            raise ValueError("BUILD requires build_kind")
        return self


class IntentResponse(BaseModel):
    actions: list[GameAction] = Field(default_factory=list, max_length=8)
    confidence: float = Field(ge=0, le=1)
    interpretation: str = ""
    requires_clarification: bool = False
    clarification: str | None = None
    echo: str | None = None


class InterpretRequest(BaseModel):
    transcript: str = Field(min_length=1, max_length=500)
    world_state: dict[str, Any]
