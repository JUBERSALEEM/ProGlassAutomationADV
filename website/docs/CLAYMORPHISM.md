# CLAYMORPHISM.md — design spec for the Clay appearance family

Source: the site's owner (2026-10-12), with reference boards (soft-desktop
control set, analytics dashboard, shop, fitness, music player). The Clay
family in `src/app/globals.css` follows these tokens verbatim. **Do not
improvise colors or shadows.**

## 1. Color tokens

```
Backgrounds
  --bg-app:      #F7F5FB   tinted near-white, never pure #FFF
  --bg-raised:   #FFFFFF   only for floating sheets over --bg-app
Surfaces (the clay itself)
  --surface-lavender: #B8A6FF   primary cards, hero blocks
  --surface-sky:      #9EC9F0   info, secondary cards
  --surface-mint:     #7DD4A8   success, positive stats
  --surface-peach:    #FFB59E   warm cards, highlights
  --surface-pink:     #F5B8D0   accent, playful cards
  --surface-lemon:    #F5D77A   attention rows, badges
Actions & status
  --action-primary:   #FF7A6B   coral: confirm, start, play
  --action-on:        #FFFFFF   label on coral
  --status-active:    #7DD4A8   green pill
  --status-pending:   #F5D77A   yellow pill
  --status-archived:  #C9B5FF   muted lavender pill
Text
  --text-strong:  #2E2A3F   headings, never pure black
  --text-muted:   #6B6580   captions, secondary labels
```

Rule: one saturated `--action-*` per screen as the hero CTA.
Surfaces carry the mood; actions carry the verb.

## 2. Radius tokens (chunky is the brand)

```
--r-card:   28px   range 24–32 by card size
--r-button: 18px   range 16–20
--r-chip:   999px  pills are fully round
--r-input:  16px
--r-icon:   20px   squircle app-icon tiles
```

Never drop below 16px on interactive elements, or the soft shadow reads as a
flat drop-shadow instead of moulded clay.

## 3. Spacing (8pt grid)

4 / 8 / 12 / 16 / 24 / 32 / 48 · card padding 24–28 · sibling gap ≥16 ·
tap target 48×48 min, button height 52–56.

## 4. The shadow recipe (the whole look lives here)

Every clay surface = 3 stacked shadows, all keyed to its own hue:

```
box-shadow:
  8px 8px 24px rgba(H, 0.35),            colored drop, SE
  -8px -8px 24px rgba(255,255,255,0.6),  light lift, NW
  inset 4px 4px 8px rgba(255,255,255,0.5),   top-left sheen
  inset -6px -6px 12px rgba(H, 0.30);    bottom-right depth
```

H = the surface's OWN color darkened ~15%, never gray.
lavender → rgba(140,108,255,·) · coral → rgba(255,90,75,·).
Light always from top-left; depth pools bottom-right.

Elevation: L0 flat tint, no shadow · L1 resting card, full recipe ·
L2 floating sheet, same recipe with offset 12 / blur 32.
Do not stack clay on clay beyond L2.

## 5. Component recipes

- **Button (primary):** bg `--action-primary`, white bold 16px label,
  radius `--r-button`, height 54, recipe keyed to coral.
  `:active` → `scale(.97)` + offsets halve (the squish: it sinks).
- **Button (secondary):** bg `--surface-lavender` or white, ink label,
  same geometry, shadow at 0.25 strength.
- **Card:** a `--surface-*` fill, `--r-card`, padding 24–28, full recipe.
- **Chip:** radius 999, padding 6×14, height 28–32, shadow half strength.
- **Toggle:** track 52×32 pill; off = inset well, on = sky; white thumb.
- **Input:** bg `--bg-app`, `--r-input`, INSET shadows only (well).
- **Bottom nav bar:** white, lifted by one clay shadow; active = coral.
- **App-icon tile:** squircle `--r-icon`, single surface fill, full recipe.

## 6. Typography

Rounded sans — Quicksand, Nunito, Outfit, Fredoka (site ships **Nunito**).
Weights 500/600/700 · h1 28–32, h2 22, body ≥16, caption 13 ·
letter-spacing 0.01em body / 0 headings · line-height 1.4 / 1.2.

## 7. Do

- Match every shadow color to its surface hue.
- Keep light source top-left on the entire screen.
- Exactly one `--action-*` CTA per view.
- Give each clay element 16px+ of breathing room.
- Pair chunky radii with chunky bold type.

## 8. Don't

- Gray/neutral shadows — kills the moulded feel instantly.
- Radius under 16 on buttons — reads as flat-with-shadow.
- Pure #FFF page background — erases the soft tinted glow.
- Skipping the inset top-left sheen — surface looks stuck flat.
- Stacking clay on clay 3+ deep — turns to mush.
- Sharp grotesk fonts (Inter, Helvetica) fighting the round mood.
- Multiple saturated CTAs competing on one screen.

## 9. When to use / avoid

Use: onboarding, kids & education, friendly fintech, savings apps, 3D landing
pages, empty states, celebratory moments.
Avoid: dense data tables, analytics dashboards, enterprise tools, anything
text-heavy or high-density.

## Site implementation notes

- The engine is `src/app/clay-engine.css`, imported **after** `globals.css`
  in `src/app/[locale]/layout.tsx`. Everything it rules is scoped to
  `[data-appearance="clay"]`; globals.css holds zero clay rules — the other
  appearance families cannot see this layer. Its five layers: §0 channels,
  §1 utility floor (bg-*/border-*/shadow-* interception), §2 named surfaces,
  §3 interactive grammar, §4 semantic chip tints, §5 midnight deltas.
- Daylight component classes do not change; the floor re-voices the utility
  inventory itself (~350 bg-ink-* fills, ~500 border-line* hairlines).

- Midnight keeps the same recipe on dark plum grounds (drop hue deepens to
  `18 10 38`); daylight is the spec's composition verbatim.
- `--font-nunito` self-hosted woff2 (SIL OFL, via @fontsource-variable/nunito);
  `[data-appearance="clay"]` re-roots `--font-sans`.
- The old hard `7px 9px 0 0` offset plate was deleted — pressed-paper
  vocabulary, the reason the family read brutalist, not clay.
- §AC (top panel & menus): every nav item is a resting clay pill-card
  and every dropdown/sheet/language row a separate round card, so the
  bar and its menus never read as bare text or flat rows; the sliding
  indicator retires inside the bar (the demo `.navclay` keeps its).
  The first §AC ship was rejected on sight and the rejection was
  correct: three stacked causes, all found on the built CSS. (1) §15's
  flat-segment strip (`background-color: transparent !important;
  box-shadow: none !important` on everything inside `.nav-pill`)
  silently annulled every card rule — the items' winning background
  measured `#0000` while the suites stayed green, because they probe
  text contrast, never the card against its panel. §15 is amended:
  the strip is retired (recorded in its header), its reading colours
  stay (scoped off the current/open item, which wears its own
  lavender card with the pressed plum ink). (2) The §9.7 utility-pane
  sweep outranked the menu-link recipe; the menu selectors now carry
  the structural chain that outranks it. (3) The faces were one
  shallow step off their panels with a one-sided drop; cards now take
  `--menu-face` (mode-aliased ladder step: daylight card-face,
  midnight data-face) under the family's measured outer mould (SE
  drop at half strength + NW lift at 1.3× `--clay-lift` — 0.13 at
  midnight, where the engine's own token note says the mould begins
  to read), rounder corners (20px), and a 10px card rhythm. Panel
  sizing is content-fit: the dropdown wrapper takes 19rem for short
  menus and 22rem for long ones with a viewport guard, the Clay pill
  pads 6px/5px, and the dropdown panel insets 10px so the first/last
  item cards clear the panel's curve. Verification is now a measured
  separation bench (clay-paint §AC): winning card face vs winning
  panel face steps 1.37:1 (midnight) / 1.30:1 (daylight) for menu
  cards and 1.67:1 / ≥1.25:1 for bar items, with the ≥0.13 midnight
  NW lift asserted on the winning shadow — computed from the built
  stylesheet, not eyeballed. Side effect, disclosed: theme-studio
  option buttons inside the bar were victims of the same §15 strip
  and now receive the §9.7 sweep's card mould.
