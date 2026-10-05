# Agent Chat (Phase L)

The typed conversational surface for the ProGlass Agent.

## Why it exists

Phase F built the entire agent engine — intent detection, tools,
permissions, teaching, memory, security, providers — but
`ProGlassAgent.HandleAsync` had **exactly one caller**: the voice
pipeline. There was no way to type to the agent.

That made the standing requirement — *"a real local agent that learns from
teaching in chat"* — **unreachable in practice**. A taught rule could be
approved or revoked in Settings, but it could never be *proposed*
conversationally, because there was no conversation.

## What it adds — and what it deliberately does not

**Adds:** a surface. Nothing else.

**Does not add:** any capability, permission, or bypass. Every message
goes through the same `HandleAsync` pipeline as the voice path, and hits
the same gates in the same order:

```
sanitize → intent → teach/approve/query/navigate
         → tool gate: registered? → permission → confirmation → execute
```

A tool the agent could not run by voice, it cannot run by chat.

## Files

| File | Role |
|------|------|
| `Agent/UI/AgentChatViewModel.cs` | Transcript, send/clear/speak commands, busy + status state |
| `Agent/UI/AgentChatView.xaml` | Bubbles, transcript scroller, composer |
| `Agent/UI/AgentChatView.xaml.cs` | Enter-to-send, auto-scroll |

Hosted in `MainWindow.xaml` as a right-hand overlay, toggled by the
top-bar robot button (`ToggleAgentChatCommand`) and gated on
`IsAgentChatOpen`.

## Behaviour

- **Enter** sends. Shift+Enter is deliberately ignored so it can become a
  newline later without changing this contract.
- **SPEAK** captures one utterance via the existing Whisper pipeline and
  runs it as a normal turn.
- **CLEAR** clears *the transcript only*. Saved memory, taught rules and
  knowledge are untouched — those have their own explicit resets in
  Settings and must never be cleared as a side effect.
- Transcript is capped at 200 messages (display hygiene). The agent's own
  conversation memory is separately capped and session-only.

## Failure posture

The agent failing must never break the ERP. Every path is wrapped:

| Situation | What the user sees |
|-----------|--------------------|
| Agent cannot be built | "The agent could not start… Everything else in ProGlass is unaffected." |
| `HandleAsync` returns null | "I did not get a reply from the agent." |
| Unhandled exception | "Something went wrong handling that. Nothing was changed." |
| Voice unavailable | "Voice is not available on this machine." |

No stack trace ever reaches the UI, and no failure is reported as an
empty success.

## Layout notes

Two rules observed here, learned from the Settings overlap defect:

1. The transcript `ScrollViewer` sets
   `HorizontalScrollBarVisibility="Disabled"`. At the default (`Auto`) WPF
   measures content at **infinite width**, which makes `TextWrapping`
   inert and runs bubbles off-screen.
2. This control does **not** assign its own root `DataContext`. Doing that
   in `SettingsAgentSection` is precisely what re-targeted the parent's
   `Visibility` binding and made three settings pages render on top of one
   another. The host supplies the view-model.

## Still not built

- No streaming/partial responses — replies appear whole.
- Shift+Enter does not yet insert a newline.
- The transcript is not persisted across restarts (by design: conversation
  is session-only).
