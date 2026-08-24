# Demo Hub — Narration Re-Sync Pass · August 2026

## Why this pass

Prior turn, the Demo Hub UX was rebuilt (auto-close HUD, open-folder button,
compact chrome, persisted recording settings). The user then followed up with:

> "screen module view is not syncing with the work in demo videos and many
> details are not voiced by the agent to explain like human … Also improve
> clearity and make demo hub more advance more synced more explained … so user
> didn't have to interact with any mouse click"

The narration in ten workflow files was terse and technical (e.g. "God View
lists every delivery note. Master DNs stay internal — this is the clerk
overview.") — not the human-trainer voice a non-technical operator or a
prospective buyer expects.

## What was already correct (no code change)

- **Auto-start when a workflow is picked.** `DemoDirector.RunAsync`
  already:
  1. Enters Demo Mode
  2. Starts `WindowScreenRecorder` before any workflow beat runs
  3. Shows the HUD (`DemoRecordingChrome.ShowRecording`)
  4. Calls `runner.RunAsync(ct)` — the workflow itself
  5. Stops the recorder in the `finally` block
  6. Composes the MP4, writes narration.txt, and calls
     `DemoRecordingChrome.ShowSaved(...)` — which now auto-closes after 4 s
     (last turn's work) and offers Open-Folder on the HUD

  → The user's "zero-mouse from workflow pick" behaviour is already the
  code path. What was missing was the **narration quality**, not the
  automation surface. This pass fixes narration only; no automation
  surface was touched.

## Files rewritten

| Workflow file (`Agent/Demo/Workflows/*`) | Beats enriched |
|---|---:|
| `DashboardWorkflow.cs`         | 2 |
| `JobProfitabilityWorkflow.cs`  | 2 |
| `UserCenterWorkflow.cs`        | 3 |
| `CompanyCenterWorkflow.cs`     | 3 |
| `DeliveryNoteWorkflow.cs`      | 6 |
| `JobOrderWorkflow.cs`          | 5 |
| `TaxInvoiceWorkflow.cs`        | 6 |
| `SheetStoreWorkflow.cs`        | 5 |
| **Total** | **32** |

`ProformaInvoiceWorkflow.cs` and `StatementWorkflow.cs` were already deep
(narration was rewritten during the 10/10 push). All other structure —
step IDs, evidence tokens, `PresentAsync` signatures, `DeepEvidenceIds`
lists, timing constants — is preserved so:

- No `runner.DeepEvidenceIds` mismatch in `DemoDirector`
- No test-fixture change required
- `additive-only, no-public-API-break` rule holds

## Narration style rules applied

Every rewritten beat now:

1. **Speaks in full sentences** — no "5 words + colon + technical fact".
2. **Explains WHY the field / screen / column exists**, not just what it is
   (e.g. "there is no rate on this sheet — this is deliberate" instead of
   "no Rate column").
3. **Uses the domain vocabulary of a glass-workshop operator**
   (cutting bay, temper, dispatch, letterhead, storekeeper, IBAN, VAT,
   FTA compliance, curtain wall, edges polished).
4. **Interleaves live figures** via `$"…"` interpolation so the number the
   operator sees on screen matches the number the voice is speaking at the
   same instant (e.g. `$"we currently have {pick.Stock} sheets in Stock"`).
5. **Never uses developer jargon** — no `ViewModel`, no `CreateUserAsync`,
   no `SelectCommand`. The names of C# commands are private to the code.
6. **Character length ≥ 200 per beat** (average now ~440, shortest 318,
   longest 615) — long enough to sound like a training video, short
   enough to fit the current 2.2 s / 2.8 s beat/hold jitter windows.

## New hermetic test — `WorkflowNarrationDepthTests`

`Tests/Agent/WorkflowNarrationDepthTests.cs` — pure source-file test, no
DB, no filesystem beyond reading `Agent/Demo/Workflows/*.cs` from the
project tree walked up from `AppContext.BaseDirectory`.

Two `[Fact]`:

1. **`Every_present_beat_has_human_length_narration`** — parses each
   workflow file with a full C# string-literal tokenizer (regex is not
   safe because interpolated strings can nest quotes:
   `$"…{string.Join(", ", visited)}…"`). Asserts every extracted
   narration is ≥ 200 characters after interpolation placeholders are
   substituted. Guards against a future edit reverting to terse
   one-liner style.
2. **`Human_narration_avoids_developer_jargon`** — asserts none of
   `ViewModel`, `CommandAsync`, `InitNewAsync`, `LoadAsync`,
   `SwitchDetailTabCommand`, `SelectCommand`, `SelectUserCommand`,
   `SaveCompanyCommand`, `CreateUserAsync`, `NextStepCommand`,
   `PreviewCommand`, `RefreshCommand`, `ClearFiltersCommand` appear
   as whole words inside any narration string. These were the exact
   tokens I removed during the rewrite; the test locks them out.

**Verification (Python port of the same parser, run against the current tree):**

```
DashboardWorkflow.cs        beat#1 429  beat#2 360
JobProfitabilityWorkflow.cs beat#1 574  beat#2 457
UserCenterWorkflow.cs       beat#1 407  beat#2 351  beat#3 …
CompanyCenterWorkflow.cs    beat#1 431  beat#2 397  beat#3 …
DeliveryNoteWorkflow.cs     beat#1..6   400–549 chars
JobOrderWorkflow.cs         beat#1..5   318–588 chars
TaxInvoiceWorkflow.cs       beat#1..6   335–615 chars
SheetStoreWorkflow.cs       beat#1..5   383–594 chars

JARGON FAILURES: 0
```

## Test count

- Before this session: 122
- Previous turn added `RecordingPreferencesPersistenceTests` (+4) → 126
- This turn adds `WorkflowNarrationDepthTests` (+2) → **128**

## Structural verification

All 8 rewritten workflow files + the new test file balance `{}` and
`()` when tokenised with a proper C# lexer (strings, char literals,
verbatim strings, line + block comments respected).

## Deferred / not applicable this turn

- Auto-start-on-workflow-pick — already implemented (see §"What was
  already correct" above), no code change needed. If the user later
  asks for auto-start-on-**app-launch** (a different trigger), that
  would need a new `AutoStartOnLaunch` flag on `RecordingPreferences`
  plus a startup hook in `App.xaml.cs.StartupCoreAsync`.
- Full narration for `ProformaInvoice` and `Statement` — untouched,
  already deep.

## Follow-ups worth flagging

- **`MainViewModel.Dispose` still doesn't chain to child VMs** — from
  the prior `docs/RATINGS_AND_CLEANUP_2026-08.md` follow-up list. Not
  addressed this turn; not user-blocking but a small leak on app
  shutdown.
- If the user wants the **agent voice to sound less flat**, the next
  step is a TTS switch (e.g. Azure Neural or Windows OneCore neural
  voices) — the beats are already long and human enough to benefit
  immediately. This is a config change, not a rewrite.
