# step1.c.11 — Bento Grid Engine (Phase 01 correction)

Date: **2026-10-20**. Phase 01 correction inside step1, not a new feature and not Claymorphism.

## What the source now does

- **Engine:** `Theme/ThemeBento/step1.e.11.BentoGrid.cs` — native WPF `Panel`, equal-height rows, gutter, explicit spans, packed reflow below `BentoCompactWidth`, `FillAvailable` for viewport mosaics.
- **One studio:** `ThemeStudioView` is a single fill mosaic (no extra hero bar): settings 1×4, compact preview 2×3, signals/controls/tokens, gallery 3×1.
- **Business Reference:** the page *is* the mosaic (no title strip): hero 2×2, pulse 2×2, analytics 4×5 with unequal KPIs 2+1+1, work/review 2×2, handoff 4×1.
- **Shell:** the window is a BentoGrid: brand, Studio/Reference, Light/Dark as row-0 tiles; page content spans the remaining cells. Tab headers are collapsed.
- **Workflow:** owner may keep `windows-wpf.yml` in `.github/workflows/`. Agents must not move it back to `cols/ci`. The App cannot publish workflow files; the owner’s placement is authoritative.

## Still not claimed

Windows restore/build/STA/visual inspection are not done in this environment. No accessibility certification. No PR without two separate confirmations.
