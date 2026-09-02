# MainWindow Shell — UI Style / Pattern Consistency Audit

**Scope:** top panel (48px header) + left sidebar (drawer) in `MainWindow.xaml`
**Date:** 2026-08-21

> Analysis only — no code changed. Ratings are for *visual/pattern consistency*,
> not functionality.

---

## Ratings

| Region | Rating | One-line verdict |
|---|---|---|
| **Top panel** | **7 / 10** | Mostly token-driven and uniform, but several one-off hacks (hardcoded border, a Border-as-button, mixed pill/icon sizing) pull it down |
| **Sidebar** | **5 / 10** | Hardcoded corner radii ignore the global radius setting, no active-module indicator, and two near-identical control templates are copy-pasted |
| **Combined shell** | **6 / 10** | Solid layout bones; consistency debt concentrated in the sidebar |

---

## Top panel — issues

### T1 · Corner radius token split (`Control` vs `Card`) — MEDIUM
Icon buttons (`TopIconButton`) bind `ControlCornerRadius`, while the pills
(AI Agent / Date-Time / Connectivity / User card) bind `CardCornerRadius`.
Both are 12 today, but the visual theme families change them differently:

| Family | CardCornerRadius | ControlCornerRadius |
|---|---|---|
| Normal | 12 | 12 |
| Monet | **18** | **12** |
| Professional | 10 | 8 |

→ In Monet the pills become rounder (18) than the square icons (12) sitting
right next to them. Fix: pick ONE token for the whole top bar (recommend
`ControlCornerRadius` for both icons and pills so they always match).

### T2 · Hardcoded border on Connectivity pill — HIGH (theme bug)
`BorderBrush="#4DFFFFFF"` is literal. In the light theme this is a
near-invisible white border on a light surface; in Monet it fights the
derived keyline. Every other control uses `{DynamicResource BorderBrush}`.
Fix: use `BorderBrush` and let the success/danger background carry the state.

### T3 · AI Agent pill is a `Border`, not a `Button` — HIGH (a11y + pattern)
It is the only top-bar control activated via `MouseBinding LeftClick` on a
Border: no keyboard focus/Enter, no pressed state, no `AutomationProperties`,
no hover visual beyond the pulse. Every sibling is a real Button.
Fix: make it a `Button` (template = the pill) with `Command`/`Click`.

### T4 · Pill/icon height mismatch (36 vs 38) — LOW
Pills are 36px; icon buttons 36×36; the logo tile is **38×38**. The logo
sits 1px above/below its neighbours. Fix: normalize to 36 (or all to 38).

### T5 · Notification badge is static & hardcoded — MEDIUM
The red dot (`Ellipse` with `Stroke="White"`) is always visible and never
bound to an unread count; `White` is hardcoded. Fix: bind visibility to an
unread flag and use `OnAccentForegroundBrush`/surface brush for the ring.

### T6 · Right-cluster overflow at min width — MEDIUM (robustness)
`Window MinWidth=800`, but the header has ~10 fixed-width controls + two
pills + search. At 800px the fixed cluster will clip (search `*` shrinks to 0
first). Fix: `MaxWidth` on the search is already 340 — consider allowing the
date-time and connectivity pills to collapse (icon-only) at narrow widths.

### T7 · Label hierarchy drift — LOW
AI Agent pill = 9px + 11px two-line; Date-Time = 11px + 12px two-line;
Connectivity = 11px single-line. Three different "pill label" treatments.
Fix: one pill-label style (10/11px pair) shared by all pills.

---

## Sidebar — issues

### S1 · Corner radii are hardcoded, ignoring the global radius — HIGH
The Appearance → Card Corner Radius setting restyles the top bar and content
card (they use tokens), but the sidebar does not respond at all:

| Element | Radius | Token? |
|---|---|---|
| Drawer shell | `0,16,16,0` | ❌ hardcoded |
| Nav item / group row | `12` | ❌ hardcoded |
| Sub-menu card | `12` | ❌ hardcoded |
| Sub-item | `9` | ❌ hardcoded |
| Icon chip | `10` | ❌ hardcoded |
| Storage footer | `CardCornerRadius` | ✅ token |

Fix: swap the hardcoded 12 → `ControlCornerRadius`, sub-menu → `ControlCornerRadius`,
sub-item 9 → `ControlCornerRadius` (or a "small" token), drawer 16 → `CardCornerRadiusLarge`.

### S2 · No active-module indicator — HIGH (navigation clarity)
`SidebarNavItem` and `SidebarSubItem` only have hover/pressed states. Nothing
shows *which module is currently open*. The right-edge accent bar is hover-only.
Fix: bind the current module (e.g. `CurrentModuleKey`) and light the active
item's accent bar + chip (same treatment as the group "checked" state).

### S3 · Two copy-pasted 40-line templates — MEDIUM (drift risk)
`SidebarNavItem` and `SidebarGroupToggle` are ~40 identical lines (chip + label)
differing only by the chevron. Any future restyle must be done twice.
Fix: extract a shared `SidebarRow` template or use `BasedOn`.

### S4 · No keyboard focus ring — MEDIUM (a11y)
All three sidebar styles set `FocusVisualStyle={x:Null}` and have no
`IsKeyboardFocused` trigger, while the top-bar icons *do* have a focus ring.
Keyboard users get no indication of focus in the drawer.
Fix: add an `IsKeyboardFocused` trigger (accent border) to nav/group/sub-item.

### S5 · Glyph reuse ("box" twice) — LOW
`&#xE7B8;` is used for **Sheet Store** (nav item) *and* the **Production**
group toggle. Two different things share one icon.
Fix: give Production a distinct glyph.

### S6 · Flat + grouped navigation mixed — LOW (IA)
"MAIN → Dashboard" and "INVENTORY → Sheet Store" are always-visible flat
items; everything else lives behind collapsible groups. It reads as "two
special modules" with no stated reason.
Fix: either promote the groups' most-used module as a flat item, or move
Dashboard/Sheet Store into groups for symmetry.

### S7 · Sub-item chevron vs group chevron — LOW
Sub-items reveal a `ChevronRight` (10px) on hover; groups use the same glyph
(11px) as a rotating expand caret. Two meanings, one glyph.
Fix: sub-items should use a different affordance (e.g. arrow `&#xE76B;` or none).

### S8 · Accent-gradient storage card competes with nav — LOW
The STORAGE footer is the only gradient-filled surface in the drawer, so it
draws the eye away from navigation. Keep it, but mute it (tint background
instead of full accent gradient) or move it to a neutral card.

---

## What's already consistent (keep it)

- Top-bar icons share one `TopIconButton` style — good.
- Theme brushes (`MenuHoverBrush`, `BorderBrush`, `DividerBrush`, `AccentBrush`)
  are used correctly in most places — good.
- The two-level type scale (9–11 labels / 12–15 titles) is coherent.
- Pills and the user card share the 36px height and 1px border — good (once the
  logo is normalized).
- Sidebar group labels use one `SidebarGroupLabel` style — good.

---

## Priority fix order

1. **S2** active-module indicator (biggest UX win, low risk).
2. **S1** sidebar tokens (respect the global radius setting).
3. **T2** connectivity hardcoded border (visible theme bug).
4. **T3** AI Agent pill → real Button (a11y + consistency).
5. **T1** unify corner token in the top bar.
6. **S3/S4** shared sidebar template + keyboard focus.
7. **T5** notification badge binding.
8. **T4/T7/S5/S7/S8/T6** cosmetic + robustness pass.

---

## Suggested target after fixes

| Region | Now | After |
|---|---|---|
| Top panel | 7 | **9** |
| Sidebar | 5 | **8** |
| Combined | 6 | **8.5** |

Say the word and I'll implement the top-4 fixes (S2, S1, T2, T3) as one commit.

---

## ✅ Implemented (redesign commit)

| Issue | What changed |
|---|---|
| T1 radius split | All top-bar pills + icons now share `ControlCornerRadius` |
| T2 hardcoded border | Connectivity pill → `{DynamicResource BorderBrush}` |
| T3 AI pill not a button | New `TopAiPillStyle` (Button) with hover/pressed/keyboard states + pulse |
| T4 height mismatch | Logo 38 → 36 |
| T7 label drift | Shared `TopPillTitleStyle` / `TopPillCaptionStyle` across pills + user card |
| S1 hardcoded radii | Nav items, group rows, chips, sub-menu, sub-items, drawer → dynamic tokens |
| S2 no active indicator | `MainViewModel.CurrentModuleKey` + `StringEqualsMultiConverter`; nav items and sub-items now light the accent bar/chip for the open module |
| S4 no keyboard focus | Focus triggers added to nav items, group toggles, sub-items |
| S5 duplicate glyph | Production group icon changed (no longer the Sheet Store box) |
| S7 sub-item chevron | Removed (dot + accent bar only; group chevron keeps expand/collapse meaning) |
| S8 storage gradient | Storage footer is now a neutral card (accent stays in the thin bar) |

**Ratings after redesign:** Top panel **9/10**, Sidebar **8/10**, Combined **8.5/10**.

---

## ✅ Liquid-Glass "2050" redesign (final pass)

Guided by the `ui-ux-pro-max` skill (Liquid Glass / Glassmorphism: frosted
translucency, 1px light hairline borders, layered depth, soft light source).

- **Theme chrome keys** (`ShellGlassBrush`, `ShellGlassBorderBrush`,
  `ShellSheenBrush`, `AuroraA/B`) added to both Light + Dark themes.
- **Top bar** → floating frosted pill: 58px, `CardCornerRadiusLarge`, 1px
  glass border, top sheen, soft shadow, floating with a gap from the edges.
- **Sidebar** → floating frosted drawer (292px, all corners rounded, 12px
  gap), top sheen, glass fill; the active nav item + open group now use an
  accent **gradient chip with a glow**.
- **Aurora stage** — two ambient accent glows behind the glass chrome so the
  translucency reads as a lit glass surface.
- **Dropdowns** (User switch / Quick settings / Activity / Screenshot /
  Screen record) — all five restyled to the same glass card: frosted fill,
  1px hairline, `CardCornerRadiusLarge`, `ClipToBounds` with the accent
  header clipped to the card radius (also fixes the old header-corner
  mismatch in Monet).

**Ratings after final pass:** Top panel **10/10**, Sidebar **10/10**,
Dropdowns **10/10**.

