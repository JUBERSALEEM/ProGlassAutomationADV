# 🪟 ProGlass Automation

> Purpose-built desktop software for glass production workflow — WPF (.NET 8) application with MVVM architecture, modular features, and export/reporting tools.

---

## Quick links

- Project: https://github.com/JUBERSALEEM/ProGlassAutomationADV
- **Upgrade notes (audit system, God Views, Step 8 attachments, dynamic theme engine, bug fixes): `Docs/UPGRADE_NOTES.md`**
- Docs: `docs/` (see `docs/Chat5`, `docs/Chat5-9`, `docs/Chat6`)
- New helper documents:
  - BUG_FIXES.md — high-priority defects to fix
  - PERFORMANCE_OPTIMIZATION.md — performance & smoothness checklist
  - UI_UX_GUIDELINES.md — professional UI/UX style guide
  - MASTER_PROMPTS.md — master prompts for developer chat tasks
  - PROJECT_STRUCTURE.md — canonical project tree (top-level)

---

## What changed (update)

This repository was audited and several helper documents were added to help triage, fix, and optimize the project. Files added/updated:

- `BUG_FIXES.md` — prioritized bug list by module for step-by-step fixes.
- `PERFORMANCE_OPTIMIZATION.md` — concrete checklist and techniques to reach stable 60/90/120/144 Hz UI.
- `UI_UX_GUIDELINES.md` — professional UI/UX rules, accessibility, and style recommendations.
- `MASTER_PROMPTS.md` — ready-to-use prompts for automated / human-assisted bug fixing and PR drafting.
- `PROJECT_STRUCTURE.md` — up-to-date top-level project structure and key files.
- `docs/Chat6/README.md` — new chat6 folder (placeholder for next-phase docs and transcripts).

---

## Short project overview

ProGlass Automation is a Windows desktop application for managing glass production workflows: inventory (Sheet Store), proforma invoices (8-step wizard incl. attachments), job orders, delivery notes, tax invoices, statements and exports (PDF/Excel/CSV). The app uses a modular ViewModel-first MVVM pattern; each module is loaded into a shell `MainWindow` ContentControl via DataTemplate bindings.

Highlights of the current build:
- **Audit & users** — every user action is recorded (`audit.db`); create/switch users in System Center; live Activity tabs inside Delivery Note, Job Order and Tax Invoice.
- **God View tabs** — DN / JO / TI modules switch between Workspace, an all-records God View, and Activity.
- **Dynamic theme engine** — accent colours are derived (HSL + WCAG contrast), card shapes are token-driven, and motion adapts to the display refresh tier (60/90/120/144/165 fps).

### Stack
- Language: C# targeting .NET 8
- UI: WPF (XAML, MVVM)
- ORM / DB: EF Core + SQLite (multiple DB files per domain)
- Notable libs: ClosedXML (Excel), CsvHelper (CSV), QuestPDF (PDF), Whisper.net (optional local STT), NAudio (audio capture)

---

## How to use the new helper files

- Start with `BUG_FIXES.md`: address Critical -> High issues in order. Each item includes file references.
- Use `PERFORMANCE_OPTIMIZATION.md` when optimizing UI for smoothness and batching work off the UI thread.
- Follow `UI_UX_GUIDELINES.md` to harmonize styles and ensure accessibility.
- Use `MASTER_PROMPTS.md` when collaborating with assistants or bots to prepare patches, PRs, or code reviews.
- The file `PROJECT_STRUCTURE.md` lists top-level folders and important files for onboarding and navigation.

---

## Run the app (developer)

Prereqs: Windows 10/11, .NET 8 SDK, Visual Studio 2022/2023 with .NET desktop workload.

```bash
# Clone
git clone https://github.com/JUBERSALEEM/ProGlassAutomationADV.git
cd ProGlassAutomationADV

# Open solution in Visual Studio and run (F5)
start ProGlassAutomation.sln
```

When running, the app creates local SQLite files under `%AppData%/ProGlassAutomation/` (for each module). Back these up before experimenting with schema changes.

---

## Where to find important modules (top-level)

See `PROJECT_STRUCTURE.md` for a canonical tree and the exact file names for each module.

---

## Contributing

1. Read `BUG_FIXES.md` and pick a high-priority issue.
2. Create a branch `fix/<short-description>` and implement the fix.
3. Run the app, add automated tests where feasible, and ensure exports still work.
4. Open a PR with a clear description referencing the issue in `BUG_FIXES.md`.

---

<div align="center">
  <strong>ProGlass Automation</strong> — Built with ❤️ by Juber Saleem
</div>
