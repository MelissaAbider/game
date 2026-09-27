from fastapi import APIRouter

from backend.ai.intent_parser import IntentParser
from backend.ai.schemas import InterpretRequest, IntentResponse

router = APIRouter(prefix="/api", tags=["intent"])
parser = IntentParser()

@router.post("/interpret", response_model=IntentResponse)
async def interpret(request: InterpretRequest) -> IntentResponse:
    return await parser.parse(request.transcript, request.world_state)
