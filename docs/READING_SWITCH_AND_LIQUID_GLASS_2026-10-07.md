# Reading switch, richer sweep, liquid glass — 2026-10-07

Website only (`website/`). Branch `arena/ff328a9c-proglassautomationadv`.
Commits `bb85214`, `bd64122`.

## Two controls, because there were two questions

The top panel had one control doing two jobs: a visitor who wanted a lighter
page had to open a colour studio and choose a family, and a visitor who liked
the family had to find it again every time the reading changed. They are now
separate, because they are separate questions:

| Control | Question | Component |
| --- | --- | --- |
| Reading — Light · System · Dark | how is this page lit? | `src/components/layout/ThemeModeSwitch.tsx` |
| Palette — the family swatch | what is it coloured with? | `src/components/layout/ThemeToggle.tsx` |

The reading switch is a three-segment group (`role="group"`, segments carry
`aria-pressed`) whose thumb is one absolutely-positioned element translated by
`activeIndex * 100%` — so the segments never move and the thumb never re-lays
out. The palette trigger is a popover of family cards that changes the family
and nothing else; its footer still opens the full studio. Both call
`useThemeGuard`, so the same engine drives them.

The old icon-only light/dark button between them is gone.

### Panel budget

The row is a fixed height (`--nav-height`, 4.25rem) inside a 96rem shell, and
`lg` (1024px) is the tight breakpoint. Accommodations, cheapest first:

* reading-switch segments are `w-10`, narrowing to `lg:w-8 xl:w-9`;
* the palette's text label is `xl:inline` — below that the swatch and the
  chevron carry it;
* the ghost "See the app" shortcut is `max-xl:hidden`. It is the first child of
  the Product menu, so the page is still one hover away and the shortcut
  returns as soon as the row has room.

On a 360px phone the row is language · palette · menu; the reading switch joins
at `sm` (`hidden sm:flex`), where it fits beside the brand.

## The engine

`src/lib/useTheme.ts` was rewritten rather than patched:

* **State is read back from the DOM.** All three attributes (`data-theme`,
  `data-theme-mode`, `data-appearance`) are the source of truth and are read
  with `getAttribute`, so React state cannot disagree with what the page is
  wearing.
* **`color-scheme` follows the resolved value**, which is what fixes native
  controls, scrollbars and form fields in the dark reading.
* **The chrome colour is not hard-coded.** It is read from the live `--ink-950`
  at the moment of the switch and written to a single authoritative
  `<meta name="theme-color">`; the extra tags Next renders for
  `prefers-color-scheme` are collapsed into it (their `media` cleared) so a
  browser cannot pick the wrong one.
* **Changes cross-fade.** A real change sets `data-theme-switch="on"` for
  300ms; that attribute drives background/border/colour/box-shadow transitions
  on the named surfaces (page, card tiers, float/menu cards, nav glass, nav
  pill, button). It is scoped to named classes, never `*`, and is skipped
  entirely under `prefers-reduced-motion`.
* **`prefers-color-scheme` is watched only while the mode is `system`**, and a
  cross-tab `storage` event keeps a second tab in step.
* Exports: `applyTheme`, `applyAppearance`, `setThemeMode`, `setAppearance`,
  `cycleThemeMode`, `nextThemeMode`, `useThemeGuard`, `useThemeMode`,
  `useAppearance`, `useResolvedTheme`, `useTheme`.

The studio's System plate no longer imitates a shade with white-to-black; it is
split in the site's own two page colours, and a line under the plates prints
what the system currently resolves to — the one thing a plate cannot show,
because `System` is a deferral rather than a colour.

## The sweep, left to right

One token family, five stops, three worked images — all family tokens, so a
family change re-lights the whole thing:

```css
--sweep-1: rgb(var(--brand-700) / .62);   /* midnight */
--sweep-2: rgb(var(--brand-500) / .34);
--sweep-3: rgb(var(--glass-cyan) / .16);
--sweep-4: rgb(var(--glass-rose) / .12);
--sweep-5: rgb(var(--brand-600) / .42);
```

| Image | Where it is used |
| --- | --- |
| `--sweep-image` | page ambient (`body::after` layer 5) and `.band-rich` |
| `--sweep-edge` | hairlines: `rule-violet`, `.nav-glass::after`, dividers |
| `--sweep-panel` | nav glass and nav pill fills |

`.band-rich` keeps its two corner pools and lays the sweep over them, so a band
reads as lit from one side rather than tinted — the same reading left to right
that the page ambient has behind it. The primary button's tint is read the same
way: accent ×1.3 at 0%, accent at 44%, cyan ×0.7 at 100%.

Daylight uses the same stops at roughly half the alpha.

## Crystal is liquid glass

`[data-appearance="crystal"]` is now a material, not a transparency setting:

* **fills** `.34 / .46 / .84` (daylight `.46 / .6 / .92`), separated from
  `--surface-alpha-*` so the other three families are untouched;
* **blur** 26px at 200% saturation (daylight 28px at 185%);
* **`--glass-frost`** — the corner catch-light: one tight white ellipse at the
  top-left, one cyan/indigo ellipse at the bottom-right, both scaled by
  `--surface-specular`. `none` in the other three families.
* The ellipse radius is deliberately *smaller* than the card (72%/84%, daylight
  88%/96%). A wash that spans the surface is a tint: it lifts the middle of the
  card, which is where the running text sits and where the contrast floor is
  measured. Confining it to the corners keeps the catch-light where the eye
  looks for the material's edge and out of the way where a paragraph is read.
* **Bevel** is asymmetric — `inset 1px 1px 1px` white at the top-left,
  `inset -1px -1px 2px` ink at the bottom-right — over the existing luminous
  ring and edge insets, on `.surface`, `.surface-raised` and `.plate`.
* The scrim (.34 dark / .5 daylight) plus fill lands at roughly 0.57 of the old
  darkening, so the documented 4.5:1 text floor still holds in all four modes.

## Verification

```
npx tsc --noEmit                      clean
npm run lint                          clean
npm run build                         398/398
python3 scripts/audit-site.py         0 failures / 0 warnings
curl sweep of built routes            390/391 at 200   (the 391st is _global-error)
```

Built-CSS checks: `--sweep-1` present, `--glass-frost` present, the
`data-theme-switch` block emitted, `body::after` carries ten layers, and the
`max-xl:hidden` rule lands as `@media not all and (min-width:1280px)`.

No black was introduced; the two `ring-black/20` rings in the palette popover
became `ring-white/25`, and the studio's mode plates use the site's own two
page colours. No base palette token changed value, so the rendered plates
(`npm run assets:plates`) are still current and were not re-rendered.
