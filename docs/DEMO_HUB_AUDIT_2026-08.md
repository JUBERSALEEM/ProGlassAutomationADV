# Demo Hub audit + recording UX hardening — August 2026

Response to the user finding:

> "Demo hub resetting last saved video resolution and mic or speaker
> access it should be fixed with update do not restore. Also improve
> clarity and make demo hub more advanced more synced more explained
> and update with latest changes so user didn't have to interact with
> any mouse click etc demo hub do everything automatic also when video
> stop same time it close it keeps running even video recorded. Also
> include view folder where screen recording is saved. Make demo
> recorder hub small right now too big."

Plus a cross-check on the previous licensing hardening pass — one
production-corrupting test-pollution bug I introduced and fixed.

## Cross-check licensing findings (bug I missed)

**Bug X-1 (P1 test pollution)**: My `Aug 2026 industrial-hardening`
commit added `TrialLedger.Heartbeat()` + `TrialLedger.Read()` calls
inside `LicenseService.Initialize()`. The existing
`LicenseServiceTests` fixture only overrode `TestDataFolderOverride`,
not `TrialLedger.TestProgramDataOverride` — so running the license
test suite would silently write "trial issued" markers to the real
`%ProgramData%\ProGlassAutomation\` and pollute the developer's actual
auto-trial state.

**Fix** (2 places):
- `LicenseService.Initialize` — auto-propagates
  `TestDataFolderOverride` into `TrialLedger.TestProgramDataOverride`
  and sets `SkipRegistryAnchorForTests = true` when running under a
  test data-folder override. Idempotent — respects a test that
  already set a different path.
- `Tests/Licensing/LicenseServiceTests` fixture — explicit save/restore
  of the ledger overrides as defence in depth.

**Bug X-2 (subtle)**: `TrialLedger.Read()` called `RegistryAnchor.Read()`
unconditionally — so a stale registry key from a previous developer
session gave false-positive "issued" verdicts even inside test hosts.
`Read()` now consults `SkipRegistryAnchorForTests` on the read side
too (previously only the write paths honoured it).

## Demo Hub — 5 fixes

### 1 · Persist recording settings across restarts ✅

**Symptom**: Every launch reset quality to 1080p, fps to 30, and
re-armed both audio toggles. The last-picked values only lived in
`UserScreenRecordService.Shared._settings` (in-memory).

**Fix**:
- New `RecordingPreferences` class in `AppConfig`:
  `QualityHeight`, `Fps`, `CaptureLiveAudio`, `CaptureMicrophone`,
  `Supersample`, `AutoCloseHudAfterSave`.
- `UserScreenRecordService` hydrates from
  `App.Config.Recording` on first construction via a new
  `LoadPersistedSettings()` helper.
- Every `Settings` setter now calls `PersistSettings()` which
  writes back to `AppConfig` and saves the config file. Best-effort:
  a save failure logs a warning but does not gate recording.
- `ConfigService.Load` gains a `config.Recording ??= new()` line so
  old `config.json` files hydrate to defaults without null-ref.

**Only reset on updates**: yes — the JSON round-trip is stable across
minor releases; only a schema break would trigger the `??=` fallback.

### 2 · Auto-close the recorder overlay after "SAVED" ✅

**Symptom**: The blocking `MessageBox.Show` in
`DemoRecordingChrome.ShowSaved` froze the UI thread until the user
clicked OK. The user reported: "keeps running even video recorded."

**Fix**:
- `DemoRecordingChrome.ShowSaved` now:
  - Removes the blocking MessageBox.
  - Sets a compact toast on the HUD (`"12:34 · 350 frames · <name>.mp4"`).
  - Wires an `EnableOpenFolder(path)` call so the HUD's new folder
    button (see fix #4) points at the just-saved file.
  - Starts a 4-second `DispatcherTimer` and closes the HUD
    when it fires — respecting `AutoCloseHudAfterSave` (default
    true).

### 3 · Compact HUD ✅

**Before**: 38×38 px buttons, 20-pt time, 14×10 padding. Roughly
280 px wide.
**After**: 28×28 buttons, 15-pt time, 10×6 padding. Roughly 180 px
wide — closer to the phone-recorder pill the user asked for.

### 4 · One-click "Open recording folder" ✅

- New folder-icon button on the HUD, visible only after `SAVED`.
  Uses `explorer.exe /select,"<path>"` to highlight the just-saved
  file in Explorer.
- New `UserScreenRecordService.OpenRecordingsFolder()` method +
  `ScreenRecordPanelViewModel.OpenRecordingsFolderCommand` — bound
  to the "OPEN RECORDINGS" button in the top-bar recorder popup
  (already existed as a stub; now works).

### 5 · Demo Catalog — new module coverage ✅

New workflow entries with rich narration:
- **Settings — Features** (4 narration beats covering Session card,
  Proforma toggles, Inquiry toggles).
- **Settings — License** (3 beats covering trial policy + anti-tamper
  ledger).
- **About** — enriched narration beat.
- **Settings** entry — 2 beats instead of the previous 1-liner.

Every added module uses the same `PresentAsync` pipeline, so the
agent voice narrates each beat, waits for TTS to finish, and only
then advances — the exact fix for the user's "screen module view
is not syncing with the work in demo videos" complaint. The 30-day
trial policy is now voiced explicitly.

## Tests added

`Tests/Agent/RecordingPreferencesPersistenceTests.cs` — **4 tests**:
- Defaults are sensible
- Full JSON round-trip preserves every field
- Legacy config without the property hydrates to null (documenting
  the shape ConfigService.Load then upgrades)
- Serialised config carries the recording block

Repo hermetic test count: **122 → 126** (+4).

## Deferred (documented, not shipped)

- **Full narration re-sync pass** across every workflow file (~13
  workflows) — each would need per-beat review to enrich the voice.
  Ship the pattern in Settings/Features/License first, follow-up
  ticket for the other 10.
- **True zero-mouse Auto pilot** — the user's "do everything automatic"
  ask. Right now the agent still needs the user to click GENERATE
  once. A cron-style auto-generate-on-launch would need a new
  `AppConfig.Recording.AutoStartOnLaunch` flag and a startup hook.
  Not this pass.
- **HUD position persistence** — the HUD remembers no position
  between recordings today. Sensible follow-up but out of scope.

## Files touched

| File | Change |
| ---- | ------ |
| `Services/AI/AIConfig/AppConfig.cs` | + `RecordingPreferences` class + `Recording` property |
| `Services/AI/AIConfig/ConfigService.cs` | `??=` hydration for `Recording` |
| `Services/Recording/UserScreenRecordService.cs` | Hydrate from + persist to `AppConfig.Recording`; new `OpenRecordingsFolder()` |
| `ViewModels/Shared/ScreenRecordPanelViewModel.cs` | + `OpenRecordingsFolderCommand` |
| `Agent/Demo/DemoRecordingChrome.cs` | Non-blocking SAVED path + auto-close timer |
| `Views/Shared/RecordOverlayWindow.xaml{,.cs}` | Compact chrome + `EnableOpenFolder` + folder button |
| `Agent/Demo/DemoWorkflowCatalog.cs` | Enriched Settings / Features / License / About narration |
| `Services/Licensing/LicenseService.cs` | Auto-propagate `TestDataFolderOverride` into `TrialLedger` (cross-check fix) |
| `Services/Licensing/TrialLedger.cs` | Honour `SkipRegistryAnchorForTests` on Read (cross-check fix) |
| `Tests/Licensing/LicenseServiceTests.cs` | Explicit ledger override save/restore |
| `Tests/Agent/RecordingPreferencesPersistenceTests.cs` | **NEW** · 4 tests |
