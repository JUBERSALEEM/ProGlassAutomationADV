# ui-ux-pro-max — install and audit findings

Skill installed from `nextlevelbuilder/ui-ux-pro-max-skill` and vendored
into `.claude/skills/ui-ux-pro-max/` so it travels with the repo.

Searchable locally, no network needed after install:

```bash
python3 .claude/skills/ui-ux-pro-max/scripts/search.py "<query>" --domain <ux|chart|color|typography|style>
python3 .claude/skills/ui-ux-pro-max/scripts/search.py "<query>" --stack wpf
python3 .claude/skills/ui-ux-pro-max/scripts/search.py "<query>" --design-system
```

It ships **56 WPF-specific rules** (`data/stacks/wpf.csv`) plus 119 UX
guidelines, 25 chart types and 192 product palettes.

---

## What the skill recommends for this product

Query: `"dashboard analytics enterprise" --design-system`

| Field | Value |
|---|---|
| Style | **Data-Dense Dashboard** |
| Keywords | multiple charts/widgets, data tables, KPI cards, **minimal padding**, grid layout, space-efficient, maximum data visibility |
| Best for | business intelligence, financial analytics, enterprise reporting |
| Primary | `#1E40AF` · Secondary `#3B82F6` · Accent `#D97706` |
| Effects | hover tooltips, row highlighting, smooth filter animation |
| Avoid | ornate design, no filtering |

The existing direction — dense cards, hairline borders, small caps
labels, large tabular numerals — matches this profile. The palette also
matches: `AccentBrush` in both themes is already in the `#1E40AF`/
`#3B82F6` family, so no colour change was warranted.

---

## Audit of the shipped dashboards against the skill

### Violations found and FIXED

**1. Chart not keyboard operable** — priority 1 (Accessibility, CRITICAL)
and the "Compact Control Semantics" guideline: *"Do not reveal the only
action on hover."*
The trend chart exposed every value through hover alone, so a keyboard
user got nothing. Now `Focusable`, with Left/Right to walk the series,
Home/End for the ends, Escape to dismiss, a visible focus ring, and
`AutomationProperties.Name` updated per point so a screen reader
announces the focused value. Mouse and keyboard share one
`ShowIndicator` method, so the two paths cannot drift.

**2. Animation ignored the OS reduced-motion setting** — priority 7
lists "no reduced-motion" as an anti-pattern.
`Animate="True"` now also yields to `SystemParameters.ClientAreaAnimation`,
so a user who has disabled animation system-wide gets none.

### Checked and already compliant

- **No emoji as icons** (priority 4 anti-pattern) — 0 emoji remain.
- **Semantic colour tokens, no raw hex in components** (priority 6) —
  0 non-shadow hardcoded colours in dashboard markup.
- **Meaning not conveyed by colour alone** (priority 10) — the active
  module row uses a rail, surface, filled chip and bold text, not just
  colour; alerting KPIs use an icon plus colour.
- **Resource dictionaries over inline values** (WPF Styling rule) —
  `Styles/DashboardStyles.xaml`.
- **Explicit styles with `x:Key` + `BasedOn`** (WPF Styling rule) —
  used throughout the dashboard styles.
- **Async for long operations** (WPF Performance, High) — all loads go
  through `async LoadCoreAsync`.
- **`DependencyProperty` for custom-control binding targets** (WPF
  Performance) — `TrendChartControl` uses DPs throughout.

### Considered and deliberately NOT applied

**Virtualization on the activity feeds** — WPF Performance rule, High.
I applied `VirtualizingPanel.IsVirtualizing` to the four feed
`ItemsControl`s, then reverted it. Reason: a plain `ItemsControl`
defaults to a non-virtualizing `StackPanel`, so the attached properties
are inert without also replacing the `ItemsPanel`; and each feed already
sits inside a page-level `ScrollViewer`, where setting
`CanContentScroll="True"` would switch that scroller to item-based
scrolling and break the existing smooth-scroll behaviour.

The feeds are also bounded at 20 rows by `GetRecentRowsAsync`, so there
is no list long enough to virtualize. Applying the rule literally here
would have added inert markup and a scrolling regression. Worth
revisiting only if a feed becomes unbounded.

**`StaticResource` over `DynamicResource` for theme values** — WPF
Styling rule. Deliberately inverted in this project: `ThemeManager`
swaps the theme dictionary at *runtime*, so `StaticResource` would
freeze every surface on whichever theme was active at startup. That was
the original "few boxes dynamic, few not" bug. The rule assumes static
themes; this app does not have them.

---

## Standing usage

Before any further dashboard UI work, query the skill first:

```bash
python3 .claude/skills/ui-ux-pro-max/scripts/search.py "<concern>" --stack wpf
```

and treat results as recommendations to verify against this codebase,
not instructions that override its established patterns.

---

## Website pass — what the skill changed (2026-10-10)

The audit above covered the WPF application; the marketing site had not
been run through the skill until this pass. Queries and the changes they
drove:

| Query | Top result | Applied |
|---|---|---|
| `SaaS manufacturing premium marketing dark immersive --design-system` | Pattern: Scroll-Triggered Storytelling · Style: Liquid Glass | Crystal (the site's liquid-glass family, parity with the app's Crystal Fluid) became the first-run default family — previously the default was Clay. Stored visitor choices still win. |
| `glassmorphism dark premium --domain style` | Glassmorphism: blur 10–20px, translucent 10–30%, 1px light border, z-depth | Crystal token strength raised (specular .26→.32, edge alpha .30→.34, tint .16→.19); the tiered translucency/blur/saturation system itself already predates the pass. |
| `premium entrance reveal stagger --domain gsap` | Scroll Reveal: 300–400ms, y 12, expo/power out, start "top 90%" | `.reveal` retuned 700ms/18px → 380ms/12px; the 90% trigger (rootMargin `-10%`) was already in place. |
| `midnight violet ice cyan dark premium --domain color` | Violet #7C3AED + cyan #0891B2 on #0F172A | No change warranted — the site's violet+cyan ramps already sit in this family. |

Not applied (with reasons): the design-system light palette (site is a
dark-first twin of a dark-first app; Daylight mode keeps its own
engineered tokens), the Calistoga display pairing (adding a third family
of webfonts to replace Inter/Source Serif 4 would be a font swap for
novelty, and Inter is the application's own face — see `fontFamily` in
`[locale]/layout.tsx`; the skill's own pairing table lists Inter for this
product class), split-text headline animation (one DOM node per
character in 16 locales, measured gain over the existing gradient sweep:
none).
