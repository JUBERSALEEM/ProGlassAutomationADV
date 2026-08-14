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
