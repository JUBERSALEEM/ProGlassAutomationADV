# Crystal Engine Audit — scroll splash, heading legibility, engine health

**Date:** 15 Oct 2026 · **Scope:** `website/` only · **Status:** FINDINGS ONLY —
no code changed by this audit. Phases in §6 await approval.

**Method.** Read in full: `src/app/crystal-engine.css` (all 1,636 lines),
`src/components/glass/GlassLife.tsx`, `src/lib/glassLife.ts`,
`src/app/[locale]/developer/page.tsx` (the page in the witness screenshots),
plus the heading/label definitions in `globals.css` and the label census
across `src/`. Cascade outcomes proven by selector specificity + source
order. Contrast computed with the WCAG formula from the shipped token
values against the backend composites §Y measured (disc cores, disc+lamp
stack, and those seen through a midnight pane: scrim 0.38 + lift 0.05).
Findings verified at HEAD `e0979a4` (§AA) — they are independent of the
motion-system pass.

---

## 1 · Verdict

The engine's architecture is sound (isolation, fallback lanes, one-edge
law, text-on-top seating all hold). Three real defects remain, and the
first one fully explains the user's screenshot witness:

1. **The §Z scroll-rest is defeated on touch by a CSS cascade tie** — the
   one liquid disc that still animates on phones never actually pauses
   while scrolling. Colour keeps swimming behind every card. (§2·A1)
2. **Small heading labels (`.eyebrow`, `.mono-label`, raw micro-labels)
   sit below the contrast floor exactly where the room is brightest** —
   2.4–4.5:1 at the lamp/disc stacks, and because the liquid drifts, any
   given label rotates in and out of legibility. That is the "~10% of
   headings blurred or not visible" witness. (§3)
3. **Two chrome surfaces still sample the backdrop on touch** (`.nav-sheet`,
   `.glass-bar`), missed by every film census so far. (§2·A2–A3)

## 2 · Scroll splash & bounce

### A1 · CRITICAL — §Z's pause loses the cascade on coarse pointers

The pause (§Z, crystal-engine.css:1409):

```css
[data-appearance="crystal"] .liquid-scene[data-liquid-rest="1"] span {
  animation-play-state: paused;
}
```

Specificity: three class-level selectors + one element = **(0,3,1)**.

The touch lane's disc-1 rule (§S, crystal-engine.css:1584, *later in the
file*):

```css
[data-appearance="crystal"] .liquid-scene span:nth-child(1) {
  animation: liquid-drift-a 20s ease-in-out -9s infinite alternate;
}
```

Also **(0,3,1)** (attribute + class + pseudo-class + element). Equal
specificity → source order decides → the `animation` **shorthand** wins,
and a shorthand resets `animation-play-state` to `running`. On every
touch device, the brand disc (64vmax, the largest and brightest mover)
therefore **keeps drifting during scroll**, behind midnight panes whose
0.05 lift + 0.38 scrim still show ~57% of it, unblurred (content panes
sample no backdrop since §O — the colour is *sharp*). On fine pointers
§Z works (no competing shorthand after line 1409), which is why desktop
scroll rests and phone scroll splashes. `html.glass-econ` (0,3,2) still
outranks both, so weak-device stilling is unaffected.

The §Z regression guards pinned the pause rule's *presence*, not its
cascade victory — the exact blind spot this bug lived in.

### A2 · MEDIUM — `.nav-sheet` still samples on touch

§O's chrome set (crystal-engine.css:794) gives `.nav-sheet` a 14px
backdrop blur. The coarse film lane (§S/§X/§Z) filmed `.nav-glass`, its
`::before`, `.nav-pill`, `.menu-card`, `thead`, inputs, `.studio-sheet`
— never `.nav-sheet`. The mobile nav sheet (Navbar.tsx:774) is a live
sampler whenever the sheet is open over a scrolling page.

### A3 · LOW — `.glass-bar` still samples on touch

16px blur (crystal-engine.css:899), absent from the film lane. Today it
is used only on the `/sys/crystal` showcase page, so the cost is
confined — a census gap, not a field defect.

### A4 · MEDIUM (perceptual) — entrance rise reads as bounce on phones

Cards still enter with a 12px rise + fade (both reveal lanes; §AA capped
their delays, not the geometry). During a fling this is a continuous
stream of rising cards — motion the eye reads as bounce layered on the
scroll itself. Fine pointers can afford it; touch cannot.

### A5 · NOTE — fixed-layer viewport jitter (platform)

`.liquid-scene` and the nav are `position: fixed`. Mobile browsers
resize the visual viewport as URL chrome hides/shows mid-scroll, and
fixed layers can bob a few px with it. Soft 64vmax gradients hide most
of it; it is platform behaviour, listed for completeness, not a code
defect we can remove.

### Cleared by the read (not suspects)

- `GlassLife.tsx` / `glassLife.ts`: two attribute writes per gesture at
  most, passive listener, fine-pointer-gated catch light, no React state,
  no per-scroll work. The §P doctrine holds in code.
- `--glass-surge` / `--glass-drift`: writers were deleted in §P; the
  registered properties rest at 0. Still water, no per-frame restyle.
- `body::after` wallpaper: `position: fixed` (globals.css:1166),
  three static gradients — painted once, composited.
- `LogSurface`/preview panes (the screenshot windows): no backdrop
  filters of their own.
- `.nav-glass` + `::before`, `.nav-pill`, `.menu-card`, `thead`, form
  wells, `.studio-sheet`: filmed on coarse (§S/§X/§Z) — verified present.
- Reveal observers: one-shot, never reset on scroll-back (§AA verified).

## 3 · Heading & label legibility (Midnight Crystal)

The failing pattern is the small caps label tier — `.eyebrow`
(brand-300, 10px), `.mono-label` (text-3, 10px), and raw utility
micro-labels (`font-mono text-2xs … text-text-faint`, `text-text-faint`
stage/date/source labels; `text-text-faint` alone appears 453× site-wide
including microcopy). Big headings are white and fine; Daylight is fine
(dark ink tokens on light). Computed contrast (WCAG; backends: LAMP =
brigh
...[truncated 8184 chars]