# ProGlass AI Platform — Architecture Documentation (Phase 21)

## Overview
Central unified AI layer for the ProGlassAutomation ERP.

## Key Abstractions
- `IAIOrchestrator` — unified surface for chat, voice, agent tasks.
- `IAIModelProvider` — provider-independent AI model interface.
- `IAIModelRouter` — intelligent routing by task category.
- `IAAgentTaskEngine` — multi-step agent execution (plan-observe-verify-correct).
- `AgentTaskMemory` — task/project execution history.
- `AgentTypedValidation` — typed parameters, authorization, timeout, cancellation, audit.
- `AgentLiveInteraction` — controlled application interaction with confirmation gates.

## Provider Routing
Task categories: Conversation, VoiceCommand, ERPQuery, Reasoning, Coding, LongContext, DevelopmentAgent, ScreenUnderstanding.

## Security
- `AgentSecurityService` preserved (sanitize, redact, egress gate).
- `AgentConfigurationService` safe defaults (LocalOnly=true, no consent by default).

## Memory
- Session conversation (50 turns, RAM only).
- Approved persistent facts (`agent.db`).
- Task/project memory (`AgentTaskMemory`).

## Fallback
- Cloud unavailable -> local provider.
- Agent unavailable -> ERP continues without interruption.

## Integration Points
- `AgentHost` exposes `AI` (`IAIOrchestrator`) and `CommandRouter` (`AIUnifiedCommandRouter`).
- `AgentVoicePipeline` remains the voice entry point.
- `AgentToolRegistry` remains the tool registry; enhanced by `AgentEnhancedRegistry`.

## Testing
- `MockAIProvider` available for tests.
- `AIRegressionChecklist` defines verification items per phase.
