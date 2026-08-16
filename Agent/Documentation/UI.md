# ProGlass Agent — UI

## Surface

Exactly **one** new page: Settings → **AI & Agent** (Segoe MDL2 `E8F2`).

The page owns its own `AgentSettingsViewModel`, so the existing
`SettingsViewModel` gains only an `IsAgentPage` flag and the `"Agent"` key.

## Cards

| Card | Contents |
|---|---|
| **Privacy** | LOCAL ONLY toggle (default ON) · cloud consent toggle · plain-English summary · allowed hosts · hardware advice |
| **Credentials** | Provider combo (gemini / groq / cohere) · PasswordBox · STORE / REMOVE |
| **Taught phrases** | Rule list with status, APPROVE / REVOKE per row, CLEAR ALL |
| **Memory & knowledge** | Export / import (file dialogs) · CLEAR MEMORY · CLEAR KNOWLEDGE · data paths |
| **Status** | Result of the last action |

## Design compliance

- **No emoji.** Segoe MDL2 glyphs only: `E8F2 E72E E74E E74D E72C E73E E78C E896`
- All colour through `DynamicResource` theme brushes
- Global pill button styles: `OutlinedPillButton`, `SuccessPillButton`,
  `DangerButton`, `RowSuccessButton`, `RowDangerButton`
- Section styles merged from `Views/Settings/SettingsSectionStyles.xaml`
- MVVM — the only code-behind is the PasswordBox handler, matching the
  existing AI section's pattern (a secret must not live in a bindable
  property)

## Privacy UX principle

The posture must be readable at a glance and impossible to change by
accident. Local-Only is one explicit toggle; cloud consent is a **second**
explicit toggle; turning Local-Only back on **revokes** consent.

## Lazy build

`AgentHost.Ensure()` and knowledge seeding run in this page's `Loaded`
handler. Until the user opens it, the agent costs the application nothing.
