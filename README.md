<div align="center">

# 🪟 ProGlass Automation

### Professional Glass Production & Business Workflow Platform

**WPF • .NET 8 • C# • MVVM • SQLite • EF Core**

<br>

![Version](https://img.shields.io/badge/version-2.0.0-blue?style=for-the-badge)
![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![C%23](https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=csharp&logoColor=white)
![SQLite](https://img.shields.io/badge/Database-SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)
![EF Core](https://img.shields.io/badge/ORM-EF%20Core-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![License](https://img.shields.io/badge/license-Private-success?style=for-the-badge)

<br>

### V1 FOUNDATION → V2 PROFESSIONAL PLATFORM

<br>

[📦 Repository](https://github.com/JUBERSALEEM/ProGlassAutomationADV)
•
[📖 Upgrade Notes](Docs/UPGRADE_NOTES.md)
•
[🎨 Design System](Docs/DESIGN_SYSTEM.md)
•
[🗺️ Roadmap](Docs/ROADMAP.md)
•
[🔀 Open Pull Request](https://github.com/JUBERSALEEM/ProGlassAutomationADV/pulls)

</div>

---

## 🧭 What is ProGlass Automation?

ProGlass Automation is a professional Windows desktop application built specifically around glass fabrication, estimation, production, documentation, inventory, delivery, invoicing and reporting workflows — from the first proforma invoice to the final tax invoice and statement.

This branch delivers an **incremental V1 → V2 upgrade**. Every change was layered on top of the working V1 application — **nothing that already works was removed**. Each phase was additive, reviewed and validated structurally.

| Document | Purpose |
|---|---|
| [`Docs/UPGRADE_NOTES.md`](Docs/UPGRADE_NOTES.md) | Full per-phase V1 → V2 changelog |
| [`Docs/DESIGN_SYSTEM.md`](Docs/DESIGN_SYSTEM.md) | Visual tokens, brushes & migration rules |
| [`Docs/ROADMAP.md`](Docs/ROADMAP.md) | Phased plan from upgraded V1 to shippable professional product |

---

## ✅ V2 — Delivered

- 👤 Centralized user identity, roles & operation-based permissions
- 🕒 Sessions, automatic created/updated metadata (user + session)
- 📝 User activity log · 🧰 application log · 🧩 module diagnostics
- 🔎 Audit database with GitHub-style **before → after** diffs
- 🕐 Per-document **version history** + restore-as-new-version
- 🔐 **Secrets vault** (Windows DPAPI) — API keys never stored in plain text
- 💾 Full backup sets + safe restore · 🔄 OTA update check (never self-replaces)
- 🎨 Theme-adaptive design system (`DesignTokens.xaml`) · 🖼️ module wallpapers
- ⚡ Adaptive performance profiles (60–165 Hz displays)
- 🤖 AI assistant: current Gemini models, listening pulse, draggable bot, AI memory
- 🧾 8-step Proforma Invoice wizard with attachments & saved-invoice history
- 🌍 God View tabs for Job Order, Delivery Note and Tax Invoice

## 🔄 V2 — Roadmap

- 🌸 Monet / 💎 Professional theme refinement & deeper module-wallpaper tuning
- 🧩 Remaining module-by-module UI migration (pilot-first, one module at a time)
- 📦 Installer packaging (app binary → Program Files; user data stays in AppData)
- 🧪 On-device visual verification pass per module

## 🔄 Development Principle

**Inspect → Understand → Reuse → Refactor → Upgrade → Test → Stabilize**

The project avoids unnecessary rewrites, duplicate services, duplicate styles and unnecessary generic abstractions. See [Final Principles](#-final-principles).

---

# 🚀 V1 → V2

V2 is not a blind rewrite. The existing V1 application already contains working business modules:

- 📊 Dashboard · 📦 Sheet Store · 🧾 Proforma Invoice · 🏭 Job Order
- 🚚 Delivery Note · 🧮 Tax Invoice · 📈 Sales Report · 📑 Statement Generator
- ✂️ Optimization · 🧰 Logs · 👥 Activity Sessions

The V2 work upgrades the existing application infrastructure and modules progressively — shared core first, then module chrome, then module internals.

## 🧭 Platform Overview

```
    🪟 PROGLASS AUTOMATION
             │
    ┌────────┼────────┐
    │        │        │
    ▼        ▼        ▼
   💼              📊
 BUSINESS PRODUCTION REPORTING
    │        │        │
    └────────┼────────┘
             │
             ▼
        🧠 SHARED CORE
             │
    ┌────────┼────────┐
    │        │        │
    ▼        ▼        ▼
   👤       🎨
  USERS      UI      LOGS
    │        │        │
    ▼        ▼        ▼
  ROLES    THEMES   ACTIVITY
 SESSIONS WALLPAPER AUDIT
 PERMISSIONS STATS   ERRORS
             │
             ▼
        ⚡ PERFORMANCE
          60–165+ Hz
```

---

# 🧩 Application Modules

| Icon | Module | V1 | V2 |
|---|---|---|---|
| 📊 | Dashboard | 🟢 | ✅ Upgraded (theme-adaptive) |
| 📦 | Sheet Store | 🟢 | ✅ Upgraded (accent table header, themed chrome) |
| 🧾 | Proforma Invoice | 🟢 | ✅ 8-step wizard + attachments + history |
| 🏭 | Job Order | 🟢 | ✅ Upgraded + God View + version history |
| 🚚 | Delivery Note | 🟢 | ✅ Upgraded + God View + version history |
| 🧮 | Tax Invoice | 🟢 | ✅ Upgraded + God View + version history |
| 📈 | Sales Report | 🟢 | ✅ Upgraded |
| 📑 | Statement Generator | 🟢 | ✅ Upgraded |
| ✂️ | Optimization | 🟢 | 🔲 Placeholder for V2 redesign |
| 🧾 | Logs & Activity | ➕ | ✅ New module (app log + activity + diagnostics) |
| 👥 | System Center | ➕ | ✅ New module (users, roles, permissions, sessions) |
| ⚙️ | Settings | 🟢 | ✅ Split cards, wallpapers, updates, privacy |

Existing V1 modules are preserved and improved, never blindly replaced.

### 🧾 Proforma Invoice — 8-step wizard

`Step1 Company Setup → Step2 Client Profile → Step3 Project Specs → Step4 Pricing Estimator → Step5 Data Grid → Step6 Other Charges → Step7 Print Preview → Step8 Attachments`

Step 8 attaches supporting files to the invoice; the saved-invoice list, grand-total summary and printable invoice all reuse the shared document infrastructure. Unsaved-changes guards protect navigation away from dirty steps.

### 🌍 God View

Job Order, Delivery Note and Tax Invoice each provide a God View tab — a single operational overview of the document pipeline with themed header bars (`GodViewHeaderStyle`).

---

# 🎨 V2 Visual System

A shared visual system with professional styles, built on `Themes/Styles/DesignTokens.xaml` (corner radii 10 / 14 / 20 / 999, motion-duration tokens, type scale, theme brushes).

| Theme | Character | Status |
|---|---|---|
| ◻️ **Normal** | Clean, simple, practical — the default | ✅ |
| 🌸 **Monet** | Transparent surfaces, soft cards, wallpaper visibility, subtle depth, smooth transitions | ✅ (refinement ongoing) |
| 💎 **Professional** | Strong hierarchy, clear contrast, structured cards, minimal noise | ✅ (refinement ongoing) |

~430 hard-coded solid colors across the app were migrated to theme brushes, so every theme re-skins the whole application from one place.

### 🎯 Design Principle

**Shared style system + individual module personality.** The Proforma-Invoice list is the visual reference: accent table-header bars, on-accent labels, pill action buttons, center-aligned numeric columns — applied app-wide.

---

# 🖼️ Module Wallpapers ✅

Each module can carry its own wallpaper, managed from **Settings → Appearance / Module Wallpapers**, plus a global wallpaper with opacity control. Supported formats: `JPG` `JPEG` `PNG` `WEBP` `BMP`. Transparent card surfaces keep the wallpaper visible while content stays readable.

---

# 🧱 Shared UI System ✅

Reusable components for repeated patterns — buttons (pill), cards, statistics, tables, forms, navigation, dialogs, loading/progress, typography, wallpaper surfaces, animations.

```
Controls/
├── AutoCompleteTextBox/   # accent-highlighted autocomplete input
├── EmptyState/            # consistent "nothing here yet" pattern
├── GradientHeroCard/      # hero surface for dashboards
├── MiniBarChart/          # compact charting
├── Sparkline/             # inline trend lines
└── StatCard/              # reusable statistics card

Themes/Styles/
├── DesignTokens.xaml      # radii, motion, typography, brushes
├── CardStyles.xaml        # card surfaces
├── PillStyles.xaml        # pill buttons & badges
├── IconBadgeStyles.xaml   # iconography badges
├── ModuleTabStyles.xaml   # module tab chrome
└── TypographyStyles.xaml  # type scale
```

Views stay thin and easy to modify; tokens live in one place.

---

# 📊 Smart Statistics ✅

`StatCardControl` — reusable, hideable statistics with well-defined states:

`Normal · Loading · Updating · Progress · Success · Warning · Error · Empty · Hidden`

Statistics are data, not decoration: every card answers a real operational question (stock levels, pending documents, backup age, …).

---

# ⚡ Adaptive Performance ✅

`PerformanceProfileService` detects the display refresh rate (60 / 90 / 120 / 144 / 165 Hz) and scales the animation workload to match:

- Scaled motion-duration tokens instead of one-size-fits-all animation rates
- Virtualized lists for large inventories and document grids
- Background processing for heavy work; background PDF generation
- Smooth scrolling; visual effects never slow the app

**Performance comes before unnecessary visual effects.**

---

# 👤 User Identity ✅

Centralized identity: user id, name, role, session id, login time, current date/time (live in the top bar). Records automatically capture created/updated user + session — **users never type system metadata**.

---

# 🔐 Roles & Permissions ✅

Roles: **Administrator · Manager · Estimator · Sales · Production · Store · Accountant · Viewer**

Permissions are operation-based (`ProformaInvoice.Edit`, `SheetStore.Delete`, …) and validated **by the operation itself**, not just by hiding buttons — so a disabled UI cannot bypass a rule.

---

# 🕒 Automatic Metadata ✅

Applicable records maintain `CreatedAt / CreatedBy / CreatedSession` and `UpdatedAt / UpdatedBy / UpdatedSession` automatically via `RecordMetadataService`. Every important change carries a timestamp and a user identity.

---

# 📝 Logs & Activity ✅

Three systems answer three different questions:

| System | Question | Where |
|---|---|---|
| 🧰 **Application Log** | What went wrong technically? | `logs/app-*.log` — startup/shutdown, info/warning/error/exception, DB/file/import/export/PDF/update events. Crash reporter writes `crash-latest.txt` with Error IDs |
| 📝 **Activity Log** | What did the user do? | Meaningful operations only — login/logout, create/edit/delete, import/export, print, approve, restore, settings change — with timestamp, user, role, module, action, entity, session. No UI noise |
| 🔎 **Audit Log** | What changed, exactly? | Dedicated `audit.db` with GitHub-style **before → after** visualization: 🔴 BEFORE / 🟢 AFTER, side-by-side, with Added / Removed / Modified / Unchanged states |

The **Logs & Activity** module surfaces all three; `Views/Diagnostics/` exposes module-level diagnostics.

---

# 🕐 Version History ✅

Important documents (PI / JO / DN / TI) maintain version history via `VersionHistoryService`. Any older version can be restored **as a new version** — the restore itself creates a new audit event, so history is never rewritten or lost.

---

# 📄 Dynamic Document Support ✅

Shared CSV / Excel / PDF infrastructure (ClosedXML, CsvHelper, QuestPDF). Module-specific business rules stay in the owning module; only the mechanics are shared.

---

# 📐 Common Glass Data ✅

`W1 H1 W2 H2 Width Height Quantity SQM LM Thickness GlassType UnitPrice Amount Tax Reference Description`

These fields are reused wherever they represent the same concept; specialized logic stays in its own module.

---

# 🤖 AI Assistant ✅

- **Current models**: default `gemini-3.5-flash` (Google's replacement for the retired 2.0-flash), with automatic config migration away from deprecated model families; Groq and Cohere providers stay current
- 🔐 **Secrets vault** — provider API keys are encrypted with Windows DPAPI, never stored in plain text
- 💫 **Listening pulse ring** while the assistant listens/processes
- 🖱️ **Draggable FloatingBot** — stays usable over any module, including popups and dropdowns
- 🧠 **AI memory** for context across conversations
- Assistant UI is fully theme-aware

---

# 🛡️ Protected Operations ✅

Delete / permanent delete / restore / replace / overwrite / reset / destructive import / database replacement are all protected by:

confirmation dialogs · permission validation · activity logging · audit capture · unsaved-changes guards · duplicate detection

---

# ❌ Professional Error Experience ✅

Errors never silently disappear. Every failure produces:

clear user message · unique **Error ID** · technical details · one-click copy · automatic log entry

---

# 💾 Data Protection & Backup ✅

- Data lives under **`%AppData%/ProGlassAutomation/`** — per-user, upgrade-safe, never inside Program Files
- `BackupService` maintains **backup sets**: automatic backup cadence (~hourly), manual-backup warning window (~10 minutes), and `TimeSinceLastBackup()` surfaced in the UI
- Restore always takes a **safety snapshot first**
- *Program Files is reserved for the application binary (installer scope), not user data*

---

# ⚙️ Settings Architecture ✅

Settings is organized as focused cards:

**Appearance** (theme / background / wallpaper / animation / performance) · **Module Wallpapers** · **User** · **Roles & Permissions** · **Backup** · **Updates** · **Diagnostics** · **About** · **Privacy**

---

# 🔄 Update System ✅

`UpdateService` performs OTA **checks** (current vs latest version, release notes, status) and keeps a de-duplicated update history. Updates never silently replace user data, and the application never self-replaces.

---

# 🗂️ Project Structure

```
ProGlassAutomationADV/
├── App.xaml(.cs)               # startup, splash, theme bootstrap
├── MainWindow.xaml(.cs)        # shell: card sidebar, live top-bar clock
├── Controls/                   # reusable UI components
├── Converters/                 # value converters
├── Behaviors/                  # interaction behaviors
├── Models/                     # entities incl. Models/Audit
├── Services/
│   ├── Audit/                  # audit db, metadata, version history
│   ├── Updates/                # AppInfo (v2.0.0), update service
│   ├── Theme/                  # ThemeManager, ColorMath, VisualThemeEngine
│   ├── Performance/            # PerformanceProfileService
│   ├── AI/                     # assistant, config, secrets vault
│   ├── Database/               # EF Core context + DatabaseBackup
│   └── Export/                 # shared CSV/Excel/PDF infrastructure
├── Themes/Styles/              # DesignTokens + shared styles
├── ViewModels/                 # MVVM view models
├── Views/
│   ├── SystemCenter/           # users / roles / permissions / sessions
│   ├── Diagnostics/            # diagnostics & module health
│   ├── ProformaInvoice/        # 8-step wizard + history
│   ├── JobOrder/ DeliveryNote/ TaxInvoice/   # + God Views
│   └── …                       # remaining V1 modules
├── Docs/                       # UPGRADE_NOTES, DESIGN_SYSTEM
└── Resources/                  # icon source assets
```

---

# 📏 Development Rules

1. **Analyze before touching** — inspect the existing implementation before modifying it.
2. **Additive upgrades** — layer V2 on top of working V1; never delete what works.
3. **No duplicate services or styles** — reuse the shared one, or extend it.
4. **Module logic stays in its module** — shared infrastructure holds mechanics only.
5. **Theme everything** — new UI uses theme brushes and `DesignTokens`, never hard-coded colors.
6. **Traceability** — every important action is logged, audited and timestamped.
7. **Validate structurally before shipping** — XAML must parse, C# must balance; visual verification happens on-device.

### 🚫 Anti-patterns (explicitly avoided)

- Blind rewrites of working modules
- Generic “do-everything” systems where a focused implementation is cleaner
- Hard-coded colors / duplicated styles outside the theme system
- Silent failures without Error IDs
- User data stored in Program Files
- Storing API keys in plain text

---

# 🪜 Migration Order

```
1. Shared core (identity, audit, logging, tokens)        ✅
2. App chrome (shell, sidebar, splash, top bar)          ✅
3. Global color → brush normalization                    ✅
4. Module chrome (headers, tables, toolbars)             ✅
5. Module internals (pilot-first, one module at a time)  🔄
6. Installer packaging + on-device verification          🔲
```

---

# ✅ Quality Gate

A change is only considered done when:

- it compiles cleanly on `net8.0-windows`
- XAML parses; no binding or resource errors introduced
- V1 behavior is preserved (additive diff)
- theme brushes/tokens are used instead of literals
- the operation is logged/audited where applicable
- the README / docs reflect the new state

---

# 🗺️ V2 Roadmap

```
    🚀 PROGLASS V2
          │
    ┌─────┴─────┐
    ▼           ▼
  🧠 CORE     🎨 UI        ✅ delivered
    │           │
    ▼           ▼
  🧩 MODULE UPGRADE        ✅ business modules preserved
    │
    ▼
  🧪 TESTING / ⚡ OPTIMIZE  🔲 on-device verification
    │
    ▼
  📦 RELEASE               🔲 installer packaging
```

---

# 🏁 V2 Definition

| Area | Status |
|---|---|
| 🧱 Architecture | ✅ Clean, additive |
| 🎨 UI | ✅ Modern, theme-adaptive |
| 🌈 Themes | ✅ Normal + Monet + Professional |
| 🖼️ Wallpaper | ✅ Module-aware |
| 📊 Statistics | ✅ Reusable, hideable |
| ⚡ Performance | ✅ Adaptive (60–165 Hz) |
| 👤 Users / 🔐 Permissions | ✅ Centralized, role-based |
| 📝 Activity / 🔎 Audit | ✅ Traceable, before/after |
| 🕐 History | ✅ Timestamped, versioned |
| 📄 Documents | ✅ CSV / Excel / PDF |
| 🤖 AI Assistant | ✅ Current models, secure keys |
| 🛡️ Safety | ✅ Protected operations |
| 💾 Data | ✅ Preserved, backed up |
| 🔄 Updates | ✅ Professional workflow |

---

# 🌟 Core Philosophy

```
    ┌──────────────────────────────┐
    │       🪟 PROGLASS            │
    │        AUTOMATION            │
    └──────────────┬───────────────┘
                   │
          ┌────────┼────────┐
          ▼        ▼        ▼
        🧠 SMART  🎨 BEAUTIFUL  ⚡ FAST
          │        │        │
          └────────┼────────┘
                   ▼
             🔐 TRUSTWORTHY → 📝 TRACEABLE → 🧩 MODULAR → 💾 RELIABLE
```

# 📌 Final Principles

> **Make reusable things reusable. Keep specific things specific.**
> **Preserve what already works. Improve what needs improvement.**
> **Do not create generic systems where they provide no real benefit.**
> **Do not duplicate code when a clean shared implementation is appropriate.**
> **Do not blindly overwrite existing modules.**
> **Every important action should be traceable.**
> **Every important change should have a timestamp and user identity.**
> **Every important error should be diagnosable.**
> **Performance comes before unnecessary visual effects.**

---

<div align="center">

### V1 FOUNDATION → V2 PROFESSIONAL PLATFORM

**Build once. Improve continuously. Preserve what works.**

<br>

`WPF` • `.NET 8` • `C#` • `MVVM` • `SQLite` • `EF Core` • `v2.0.0`

<br>

### Built with ❤️ by Juber Saleem

</div>
