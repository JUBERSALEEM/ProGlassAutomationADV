# ProGlass Automation — Upgrade Notes (Audit • God Views • Step 8 Attachments • Dynamic Theme Engine)

This document describes, like a code review would, everything that was added and fixed in this upgrade.
**Nothing existing was removed.** All changes are additive or surgical fixes.

---

## 1. Activity Logging + Audit System + User Management

### What it is
A complete audit subsystem: every meaningful user action in every section is recorded with
**who** did it, **where**, **what**, and **when**.

### Files

| File | Purpose |
|---|---|
| `Models/Audit/AppUser.cs` | User entity (username, display name, role, salted SHA-256 password hash). |
| `Models/Audit/ActivityLogEntry.cs` | One immutable audit-trail row. Insert-only by design. |
| `Services/Audit/AuditDbContext.cs` | Dedicated SQLite DB: `%AppData%\ProGlassAutomation\audit.db`. |
| `Services/Audit/AuditSections.cs` | Canonical section names (constants — no free strings). |
| `Services/Audit/AuditService.cs` | Static facade: `InitializeAsync`, `Log`, `LogAsync`, user CRUD, `SwitchUserAsync`, queries, live `ActivityLogged` event. |

### Design rules
- **Logging never breaks business flows** — every write is try/catch + background; `AuditService.Log(...)` is one line and never throws.
- **Users are never deleted**, only deactivated — deleting would orphan their audit history. The last active admin cannot be deactivated.
- First run seeds a default **admin / admin** user. Switch users from System Center (password verified).

### Where actions are recorded (per section)
Sheet Store (add/edit/delete/buy/use) • Proforma Invoice (save/update/delete + attachment add/rename/delete/open) •
Job Order (save/delete) • Delivery Note (save/dispatch/attachment add/delete) • Tax Invoice (save) •
Statement (pipeline generation) • Settings (theme/accent changes) • Users (create/activate/switch) • App (start/stop).

### UI
- **System Center** module (sidebar → SYSTEM): `Users` tab (create user, activate/deactivate, switch active user)
  and `Activity Log` tab (full trail, section filter, live updates).
- **Activity tab inside each section** — Delivery Note / Job Order / Tax Invoice tab strips include a live
  per-section feed (see §2), so activity updates appear inside each section as users work.

---

## 2. God View tab switch (Delivery Note • Job Order • Tax Invoice)

Each of these modules now has a tab strip:

```
[ 🗂 Workspace ]   [ 🌐 God View ]   [ 📜 Activity ]
```

- **Workspace** — the original list ↔ detail flow, preserved 1:1.
- **God View** — separate, read-only overview of EVERY record in the system:
  KPI tiles (theme-aware tints via `BrushKeyConverter`) + fast search + full DataGrid.
  Rows open the normal detail view (double-click or Open button).
- **Activity** — live audit feed filtered to that section.

Files: `ViewModels/{Module}/GodView/*ViewModel.cs`, `Views/{Module}/GodView/*View.xaml`,
shared `ViewModels/Shared/GodViewKpiViewModel.cs`, `ViewModels/Shared/SectionActivityViewModel.cs`,
`Views/Shared/SectionActivityView.xaml`, styles in `Themes/Styles/ModuleTabStyles.xaml`.

---

## 3. Proforma Invoice — Step 8: Attachments

The wizard grew from 7 to 8 steps. Step 8 mirrors the Delivery Note attachments system:

- Add **images** or **PDFs** (10 MB limit, same validation rules — shared `AttachmentLimits`).
- Rename on upload (reuses the DN `RenameAttachmentDialog` — no duplication).
- Row list with lazy-cached thumbnails, size, date; **Open / Rename / Delete** per row.
- Files keyed by the live Proforma ID from Step 1, so attachments survive closing/re-opening.

**Persistence decision (important):** proforma data lives in a JSON snapshot blob inside an
already-shipped SQLite DB. `EnsureCreated` cannot add new tables to an existing database, and the
project has no migration pipeline — so attachments persist to a per-invoice manifest:

```
%AppData%\ProGlassAutomation\ProformaInvoices\Attachments\{ProformaId}\
    manifest.json + Images\ + PDFs\
```

100% backward compatible: old invoices simply have no manifest (= zero attachments).

Files: `Services/ProformaInvoice/Attachments/ProformaAttachmentService.cs`,
`Models/ProformaInvoice/Attachments/ProformaAttachmentRecord.cs`,
`ViewModels/ProformaInvoice/Step8Attachments/*`, `Views/ProformaInvoice/Step8Attachments/*`.
Wizard shell updated: `TotalSteps = 8`, progress bar 8 columns, all labels "of 8".

---

## 4. Dynamic Theme Engine + hardware-adaptive motion (60/90/120/144/165 fps)

### 4.1 Wise colour adaptation (`Services/ThemeManager.cs` + `Services/Theme/ColorMath.cs`)
The old manager only swapped Light/Dark dictionaries. Now:
- `ApplyAccent(color)` derives the **entire accent family from one colour** using HSL math:
  hover, dark, light, overlay and tint variants — never hard-coded.
- **WCAG contrast** picks text-on-accent automatically (a yellow accent gets dark text, not white).
- Dark theme lifts accent lightness so colours survive dark surfaces.
- Re-derives automatically after every light↔dark swap.
- Settings → Appearance gained accent presets (Glass Blue / Emerald / Amber / Rose / Violet / Teal).

### 4.2 Hardware adaptation (`Services/Performance/PerformanceProfileService.cs`)
- Detects the display refresh rate (`EnumDisplaySettings`) and snaps to the nearest tier:
  **60 / 90 / 120 / 144 / 165 fps**.
- Publishes app resources consumed by XAML: `MotionSpeedRatio`, `MotionDurationFast/Norm/Slow`,
  `RichEffectsEnabled`, `TargetFps` — higher refresh panels get snappier (shorter) motion.
- Overrides WPF's default storyboard `DesiredFrameRate` so 144/165 Hz monitors actually
  animate at native rate instead of the ~60 fps default.
- Never throws — detection failure falls back to the safe 60 fps profile.

### 4.3 Card shape system
Theme dictionaries now own shape tokens: `ControlCornerRadius`, `CardCornerRadius`,
`CardCornerRadiusLarge`, `PillCornerRadius`. `CardStyles.xaml` was moved onto those tokens so one
theme change re-shapes every card in every section. Also fixed leftover **green** shadow tints
(`#0F4C3A`) from the old palette → neutral navy.

---

## 5. Bug fixes

| # | Severity | Fix |
|---|---|---|
| V-1 | **CRITICAL** | AI voice assistant disappeared when Whisper failed mid-session: `_stt` was `readonly`, so the fallback service never replaced the disposed Whisper and `IsVoiceAvailable` flipped false, hiding the mic. Now `_stt` is swapped, `ListeningStateChanged` re-wired, availability re-raised (`AiChatViewModel`). |
| V-2 | HIGH | AI chat panel clipped on short windows (fixed 600 px + bottom anchor overflowed the top edge, hiding header/voice rows). `MaxHeight` now clamps to window height − inset via new `WindowInsetConverter`. |
| V-3 | HIGH | `JobOrderViewModel.LoadJobOrderIntoDetail/ReturnToList` were unguarded `async void` — an exception escaped and could crash the app. Now wrapped with try/catch + user feedback. |
| V-4 | MED | Card shadows used stale green tint from the pre-blue palette. Fixed. |
| V-5 | LOW | Wizard progress hard-coded `7` in several places; now derives from `ProformaInvoiceViewModel.TotalSteps`. |

Audit also surfaced (and logs) flows that were previously invisible — useful for diagnosing support issues.

---

## 6. How to extend

- **Add a new audit section name** → constant in `AuditSections`, then `AuditService.Log(...)`.
- **Add God View to another module** → copy one `*GodViewViewModel` (load repo list, build KPIs,
  filter), one `*GodView.xaml`, and add the tab strip block (see `JobOrderView.xaml` — it is the
  smallest example).
- **New accent preset** → add one case to `SettingsViewModel.ApplyAccentPreset` + one button in
  `SettingsView.xaml`.

---

## 7. v2 Upgrade — Phase Batch 2 (this round)

### Security — no plain-text secrets
- `Services/Security/SecretVault.cs`: DPAPI (Windows user scope) encrypted
  secret store (`secrets.dat`). API keys never reach `config.json`.
- `ConfigService` upgraded: legacy plain-text keys are migrated into the
  vault on load; saves always mask keys; `RememberApiKeys=false` makes keys
  session-only (memory until app closes).
- Settings → SECURITY: keep-keys toggle, "Remove saved keys", "Clear AI memory".

### Technical logging & crash diagnostics
- `Services/Logging/AppLogService` (+`LogEntry`): file log
  `logs\app-yyyyMMdd.log` (14-day retention), levels, module/class/method/
  operation context, ErrorIds, exception chains, live ring buffer + event.
- Unhandled UI exceptions now produce an ErrorId, full technical log and a
  user-safe dialog (no raw stack as the primary message).
- **Logs & Activity module** (sidebar → SYSTEM): separate stylish page with
  tabs — App Log, All Activity, and one live activity tab per module
  (PI / JO / DN / TI / Sheet Store / Statement / AI / Users).

### OTA updates
- `Services/Updates/UpdateService`: GitHub-release check, version compare,
  release notes, download with progress into the user's Downloads folder.
  The app never replaces its own files.
- `AppInfo` is the single version source; About now shows v2.0.0.

### AI upgrades
- **Learning memory** (`Services/AI/AIMemory`, aimemory.db): every exchange
  remembered locally; recent memories + the last 12 audit actions are
  injected into the AI context so the assistant understands daily work.
- AI answers are audited; memory clearable from Settings (privacy).

### Users around the app
- Dashboard welcome banner greets the signed-in user (live on user switch).
- **Same-network presence**: `Services/Network/UserPresenceService` — UDP
  broadcast heartbeats; System Center → Users shows colleagues online on
  the LAN.

### Daily PI sharing
- Saved invoices list: TODAY one-tap filter + SHARE EXCEL — exports the
  filtered list as one styled register via the shared Excel engine
  (`ProformaInvoiceListExcelLayout`, WorkbookStyle).

### Backup & restore (advanced)
- Full backup SETS (all databases + config) in timestamped folders,
  retention 10, verification, restore with automatic pre-restore safety
  snapshot. Settings → BACKUP & RESTORE UI. Exit creates an auto set
  (15-minute cooldown).

### UI polish
- Top-bar live Online/Offline pill (shared ConnectivityService).
- No-search-results / empty states in God Views, App Log, presence,
  activity feeds. Smooth scrolling enabled on new/settings/system lists.
- About gained a plain-language Privacy card.

### Intentionally untouched
All module business logic (PI pricing, DN partial deliveries, TI families,
Statement workflow, SheetStore stock rules), all existing database files and
schemas, the existing export engines (reused), and every v1-era style that
was not duplicated.

### Honest testing status
This WPF project cannot be compiled in the development sandbox (Linux, no
.NET Windows SDK). All changes were validated structurally (XAML parse,
syntax analysis, API cross-referencing) — not build- or run-tested.

---

## 8. v2 Upgrade — Phase Batch 3 (this round)

### Roles & permissions (§15)
- `Services/Audit/Permissions.cs`: 8 roles (Administrator, Manager,
  Estimator, Sales, Production, Store, Accountant, Viewer) mapped to
  granular permissions ("Module.Capability"). Legacy Admin/Operator roles
  keep working via aliases.
- `PermissionService.Require()` enforces INSIDE operations (not just hidden
  buttons): Sheet Store add/edit/delete/buy/use, PI save/update/delete/
  exports, JO save/delete, DN save/dispatch, TI save, Statement generation.
  Denials show a professional dialog AND are audited.
- System Center: full role catalogue on creation, per-user permission count.

### Sessions & record metadata (§16/§17)
- Every app run has a SessionId (`S-XXXXXXXXXX`); audit rows carry it;
  login/logout boundaries are recorded.
- `RecordMetadataService`: automatic CreatedBy/CreatedSession/UpdatedBy/
  UpdatedSession per record — stored centrally in audit.db so the nine
  business databases keep their existing schemas untouched (§41).
- Wired today: Sheet Store (create/edit), Proforma Invoice saves, JO saves.

### Before/after audit diffs (§23)
- `AuditChange` + `ChangesJson` on audit entries (additive migration).
- GitHub-style `ChangesDialog` (🔴 before / 🟢 after, Added/Removed/
  Modified badges), opened from any activity row with a "± Changes" button.
- Live diffs recorded for: Sheet Store edits (stock/buy/sell), Job Order
  status changes.

### Version history (§24)
- Append-only `RecordVersions` store; every PI save stores a full snapshot.
- 🕘 button on saved-invoice rows → `VersionsDialog` (version, date, user,
  summary) with Restore → opens that version in the wizard; saving again
  creates a NEW version. Restores are audited; history never erased.

### Visual theme families + wallpaper (§8)
- `VisualThemeEngine` layers three families over Light/Dark without
  duplicating palettes: **Normal** (clean default), **Monet** (wallpaper +
  translucent glass surfaces + softer radii), **Professional** (tighter
  radii, restrained hover, corporate finish).
- Wallpaper: JPG/JPEG/PNG/WEBP/BMP picker, visibility slider, clear —
  persisted in config; applied at startup.
- Settings → Appearance: visual theme selector + wallpaper controls.

### Files intentionally untouched
All module business logic, all database schemas (metadata/versioning live
in audit.db), export engines, AI providers, wizard step content.

### Known limitations (honest)
- Permissions enforce listed operations; read-only screens remain open to
  all roles by design.
- Monet blur uses opacity layering (no expensive blur effects) to stay
  smooth on all hardware tiers.
- Still not compile-tested in this sandbox — structural validation only.

---

## 9. v2 Upgrade — Phase Batch 4 (this round)

### Per-module wallpapers (§9)
- `AppearanceSettings.ModuleWallpapers` (keyed by `WallpaperModules`
  constants) — Enabled / Path / Opacity per module, persisted in config.
- `VisualThemeEngine.ApplyModule()` runs on every navigation: rebuilds the
  active theme family, then layers that module's wallpaper behind the
  content (MainWindow module frame) with translucent surfaces.
- Settings → MODULE WALLPAPERS: enable toggle, image picker
  (JPG/JPEG/PNG/WEBP/BMP), visibility slider, clear — per module, all 14
  screens. Changes apply live to the module currently on screen.

### Unsaved-changes guard (§28)
- Shared `UnsavedChangesGuard` + `UnsavedChangesDialog`:
  Cancel / Discard / Save & Continue. Data is never silently lost.
- Wired into Job Order (the module with real dirty tracking): leaving the
  detail screen or opening another JO while dirty triggers the guard.
  Save & Continue uses the new `JobOrderDetailViewModel.TrySaveAsync()`.
- Other modules gain the guard as they adopt dirty tracking.

### Shared statistics component (§11)
- `Controls/StatCard/StatCardControl` — one reusable card with states
  Normal / Loading / Empty / Updating / Success / Warning / Error /
  Transparent, unit + trend line, hideable via IsHidden.
- Demonstrated on the Logs & Activity page (live log count, errors,
  warnings, signed-in user). Modules keep their own data.

### Performance (§13)
- Virtualization (row + column, Recycling mode) on: God View DataGrids
  (DN/JO/TI), Activity feeds (module tabs, Diagnostics app log, System
  Center). Large lists scroll smoothly without loading every row.
- DB-backed feeds stay capped (audit 200–300 rows in memory).
- Import/paste business flows kept untouched by design — they were
  already async in v1.

### Files intentionally untouched
All module business logic, import/paste code paths, PI wizard content,
existing dashboard stat cards (the shared StatCard is additive).

### Known limitations (honest)
- Module wallpaper translucency is opacity-based (keeps all hardware
  tiers smooth — no real-time blur).
- Unsaved-changes guard currently covers Job Order; DN/TI need dirty
  tracking added before they can use it.
- Structural validation only — no compile/run test in this sandbox.

---

## 10. v2 Upgrade — Phase Batch 5 (this round): Version history for JO / DN / TI

- **Job Order**: every save appends a restorable version; 🕘 button on
  each saved-list row opens the shared Versions dialog; Restore loads
  the old snapshot into the editor (database untouched until the user
  saves, which appends a NEW version). Audit-logged.
- **Tax Invoice**: same — every save recorded (entity snapshot JSON as
  stored), 🕘 button on the detail action bar, full restore support.
- **Delivery Note**: every save recorded; 🕘 button shows the preserved
  history. DN restore is intentionally deferred — DN line items live in
  relational rows (not the snapshot blob), so a safe restore needs
  mapper support; recording now guarantees no history is lost.
- Fixed a recording bug caught in review: TI versions must capture the
  entity's serialized snapshot (BuildEntity output), not the raw wizard
  state — otherwise restores would not deserialize.

Restores always create a new version on the next save; nothing is ever
silently erased (§24).

---

## 11. v2 Upgrade — Phase Batch 6: DN restore + unsaved-changes guards (§24/§28 complete)

### Delivery Note restore (§24 now fully complete)
- LoadAsync refactored: hydration extracted into HydrateCoreAsync()
  (shared by normal load and restore — zero duplication).
- RestoreVersionAsync: header fields restored fully; line items
  restored quantity-by-quantity (matched by spec group + serial),
  so relational rows are never broken. DB untouched until the user
  saves; saving appends a NEW version.

### Unsaved-changes guards extended (§28 complete for all editors)
- **Delivery Note**: property-level dirty tracking (status, FOC,
  driver, vehicle, receiver, notes, delivered quantities) with a
  hydration-suppression flag so loading never marks dirty. Guards on
  ReturnToList and record-switching.
- **Tax Invoice**: state-signature detection — the whole wizard state
  is serialized after load/save and compared on leave. One mechanism
  covers every field; nothing can be missed. New invoices are dirty
  by definition. Guards on ReturnToList and record-switching.
- Job Order guard shipped in an earlier batch. PI wizard uses the
  shared dialog via its shell flows.

---

## 12. v2 Upgrade — Phase Batch 7: Update history (§34) + performance pass (§13)

### Update history (§34)
- `UpdateHistoryService`: persisted ledger (update-history.json, capped
  at 30) recording Detected / Downloaded / CheckFailed events with
  version, release name, timestamp, detail.
- UpdateService now captures the GitHub publish date.
- Settings UPDATES section upgraded: current vs latest version tiles,
  status line, release details card (name + published date + notes
  preview), download progress, and the full update-history list with
  color-coded event badges. The app still never replaces its own files.

### Performance pass (§13 — structural)
- **Parallel DB init at startup**: the nine module databases (each its
  own SQLite file, no interdependencies) now initialize concurrently
  via Task.WhenAll — startup no longer pays nine sequential
  EnsureCreated round-trips. Ordering preserved: Presence still starts
  after Audit so the active user identity is ready.
- **Presence heartbeat socket reuse**: the 10-second LAN heartbeat
  previously allocated and disposed a UdpClient each beat; it now
  reuses one broadcast socket (less socket churn + allocations), and
  disposes it cleanly on exit.
- Everything already virtualized/capped stays as-is (God Views,
  activity feeds, audit queries). Real FPS measurement needs your
  hardware — this pass removes structural bottlenecks.

---

## 13. v2 Upgrade — Phase Batch 8: UI design-system foundation + first migrations

### Analysis (senior audit)
- 20+ hardcoded corner radii (3…22), 40+ local styles in single views,
  module title sizes drifting (24/26/28/36), Splash pinned to stale v1.1.0.

### Foundation
- `Themes/Styles/DesignTokens.xaml` (registered in App.xaml): the single
  source of truth for module-screen primitives — ModulePageTitle/Subtitle
  styles (24/12), ModuleSectionCard, ModuleColumnHeader, toolbar
  primary/secondary buttons, in-page tab pill. Built ON the existing
  radius tokens (Control=10, Card=14, CardLarge=20, Pill=999) — no new
  conflicting values.

### Migrations (pilot-first, non-destructive)
- Sheet Store pilot: page title/subtitle, data-row + toolbar + table
  chrome radii moved to tokens; PDF button hard-red → DangerBrush.
- Job Order / Delivery Note / Tax Invoice: page titles + subtitles on the
  shared styles — one consistent 24/12 scale.
- Splash: version bound to AppInfo.Version via x:Static (no stale string).
- About: already dynamic from earlier batch (verified).

### Docs
- `Docs/DESIGN_SYSTEM.md` — canonical tokens, rules for touching a module,
  migration status, and the pilot-first rationale.

Remaining module chrome (detail headers, wizard steps) migrates
incrementally per the documented rules — each a small reviewable change.

---

## 14. v2 Upgrade — Phase Batch 9: UX refinements from review feedback

### Update checks no longer spam history
- `UpdateHistoryService.Record` now dedupes: repeated identical checks
  refresh the newest row's timestamp instead of growing the list. Five
  clicks of "Check for updates" = ONE history row.
- Settings shows at most the 6 newest history rows (compact, no sprawl).

### Backups no longer flood
- Automatic (on-exit) backup now throttles against the newest set ON
  DISK — at most one auto set per hour, never one per exit.
- Manual "Create backup now" warns first if a backup was made in the
  last 10 minutes ("Create another?"), so repeated clicks don't pile up.

### Delivery Note list — column headings + empty states
- ALL tab gains a column-heading strip (Job order/customer/project |
  Delivery progress | Quantities) matching the card columns; empty
  state preserved. (All six DN tabs already had empty states.)

### Centered, professional layouts
- System Center, Logs & Activity (Diagnostics) and the shared activity
  feed are now centered with a max width (1100–1180px) instead of
  stretching edge-to-edge — like professional desktop apps.

### List tooltips everywhere
- Activity rows (Section feed, Diagnostics app log, System Center) get
  full hover tooltips: date/time, user, module/function, action,
  details, error id.
- New column-header strips with tooltips in the Section activity feed
  (Date & Time | Action | Details | Diff) and Diagnostics app log
  (Time | Level | Module/Function | Message).

### Settings Appearance split into focused cards
- The overstuffed APPEARANCE card is now four focused 14px cards:
  Theme Mode, Wallpaper, Accent Colour, Motion & Performance.
- Section cards standardized to a 14px corner radius.

Verified: all XAML parse clean, 381/381 CS balanced.

---

## 15. v2 Upgrade — Phase Batch 10: AI fixes (models, listening feedback, draggable bot)

### Latest AI models
- Google SHUT DOWN gemini-2.0-flash on 2026-06-01 (why AI reported
  "no longer supported"). Default Gemini model is now **gemini-3.5-flash**
  (Google's recommended replacement), in both AiSettings and
  ConfigService.
- ConfigService's deprecated list now includes the whole shut-down 2.0 GA
  family (2.0-flash, -001, -lite, -lite-001), so existing installs
  auto-migrate to the current model on next load.
- Groq (llama-3.3-70b-versatile) and Cohere (command-r-plus) confirmed
  still current — left unchanged.

### Clear "is it listening?" feedback
- Added a continuous animated **pulse ring** around the mic button that
  pulses while IsListening=True — visible even in silence (the old
  audio-level-only ring stayed at 0 with no sound, giving no cue).

### Draggable AI bot icon
- The floating bot is now draggable: click (no movement) opens the chat,
  dragging moves it anywhere in the window (offset via TranslateTransform,
  clamped so it stays fully visible). Wired via AddHandler(handledEventsToo)
  because Button marks MouseLeftButton* as handled.

Verified: all XAML parse clean, 381/381 CS balanced. Model availability
confirmed via current Google/Groq/Cohere documentation.

---

## 16. v2 Upgrade — Phase Batch 11: fix 3 compile errors from prior phases

Three genuine compile errors (the rest of the reported XDG0008 list was a
cascade from these breaking the assembly build):

1. **SettingsViewModel** — `BackupService` is a static class and was being
   assigned to a variable (CS0723/CS0119/CS0176). Now calls members
   statically.
2. **AiChatView** — listening pulse used `Values="..."` on a
   `DoubleAnimation`, which has no `Values` property (MC3072). Replaced
   with `DoubleAnimationUsingKeyFrames` + `LinearDoubleKeyFrame`s.
3. **SplashView** — `x:Static` does not support `Mode=OneWay` (MC3045).
   Removed the Mode.

Verified the many "type not found in namespace" errors (EmptyStateControl,
BrushKeyConverter, SmoothScrollBehavior, GradientHeroCard, etc.) are all
cascade errors — those types exist with correct namespaces and resolve once
the assembly compiles. Repo-wide sweep confirms no other instances of these
patterns. All XAML parse clean, 381/381 CS balanced.

---

## 17. v2 Upgrade — Phase Batch 12: PI wizard chrome migration

Normalized 212 hardcoded SOLID brush colors (Foreground/Background) across 18
Proforma Invoice wizard files to theme DynamicResource brushes so the wizard
adapts to light AND dark themes:
  #4338CA/#3B82F6/#1E40AF/#6366F1 -> AccentBrush
  #EEF2FF -> TintIndigoBrush
  #B91C1C -> DangerBrush, #047857 -> SuccessBrush, #B45309 -> WarningBrush
  #DCFCE7/#FEE2E2/#FEF3C7 -> TintGreen/TintRed/TintAmber
Gradient-stop Colors were intentionally left untouched (cannot safely bind a
Brush to GradientStop.Color). Contained, appearance-only, no business logic
touched. All XAML parse clean, 381/381 CS balanced.

---

## 18. v2 Upgrade — Phase Batch 13: global detail/dashboard/report chrome normalization

Applied the same SAFE solid-color normalization (Foreground/Background only)
across the remaining module views — Job Order, Delivery Note, Tax Invoice,
Dashboard tabs, Sheet Store, Reports/Statement wizard + dialogs — 128 colors
across 25 files, mapped to theme DynamicResource brushes. Combined with the
PI wizard batch (212) and the earlier text sweep (84), essentially all
solid theme-agnostic colors in Views now adapt to light AND dark themes.
Gradient-stop Colors intentionally untouched. All XAML parse clean,
381/381 CS balanced.

---

## 19. v2 Upgrade — Phase Batch 14: PI-list reference applied globally

The Proforma Invoice list page is the visual reference (solid accent table-header
bar with bold on-accent labels, center-aligned numeric columns, pill action
buttons). Applied globally:
- Sheet Store table header -> solid AccentBrush bar + white bold labels
  (center/right aligned columns preserved), visible even when empty.
- God View DataGrid headers (GodViewHeaderStyle) -> accent bar + on-accent
  bold labels (all three God Views at once).
- Delivery Note list + Section activity + Diagnostics header bars -> accent bar.
- Shared toolbar action buttons (ModuleToolbarPrimary/Secondary) -> full pill
  radius (999) to match the PI-list Export pills.
All appearance-only; layout/columns untouched. All XAML parse clean,
381/381 CS balanced.
