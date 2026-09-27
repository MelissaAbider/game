from typing import Dict

from fastapi import APIRouter
from pydantic import BaseModel

router = APIRouter(prefix="/api/session", tags=["session"])

class SessionMetrics(BaseModel):
    session_id: str
    number_of_voice_commands: int = 0
    successful_commands: int = 0
    failed_commands: int = 0
    clarification_count: int = 0
    deaths: int = 0
    average_ai_latency_ms: float = 0.0
    level_completion_time_seconds: float = 0.0

# In-memory storage for local sessions
_sessions: Dict[str, SessionMetrics] = {}

@router.get("/metrics/{session_id}", response_model=SessionMetrics)
async def get_metrics(session_id: str):
    if session_id not in _sessions:
        _sessions[session_id] = SessionMetrics(session_id=session_id)
    return _sessions[session_id]

@router.post("/metrics/{session_id}/record")
async def record_command(session_id: str, success: bool, latency_ms: float, clarification: bool = False):
    if session_id not in _sessions:
        _sessions[session_id] = SessionMetrics(session_id=session_id)
    s = _sessions[session_id]
    s.number_of_voice_commands += 1
    if success:
        s.successful_commands += 1
    else:
        s.failed_commands += 1
    if clarification:
        s.clarification_count += 1
    
    # Update running average latency
    s.average_ai_latency_ms = (
        (s.average_ai_latency_ms * (s.number_of_voice_commands - 1) + latency_ms) 
        / s.number_of_voice_commands
    )
    return s
