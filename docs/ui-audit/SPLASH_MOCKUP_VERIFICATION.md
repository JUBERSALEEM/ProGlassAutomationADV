# SPLASH v2 "MIDNIGHT CONSOLE" — 10 FRAME MOCKUPS + 100% VERIFICATION TABLE (2026-09-19)

**Purpose:** ten mockup images of the v2 splash (`Views/Splash/SplashView.xaml`
— full rewrite per owner directive: *"completely rewrite, not just a reskin"*).
One image per REAL state the code produces. Every element maps to code —
nothing invented. **This table + the code are the 100% reference**; the images
(AI-generated) are the visual guide. The running app is the final check.

**Mockups:** `/home/user/splash_mockups/splash-01..10.png` (kept OUT of the
repository, per the no-mockups-in-code rule).

**Honest scope:** images are NOT pixel-exact (AI generation) and occasionally
render prompt annotations as captions (e.g. "LOGO"/"WORDMARK" on frame 6) —
ignore such captions. In frames 2–4 the progress fill is drawn as an
*approximation*; the EXACT fill at each timestamp is given in §2 (smoothstep
curve) — if the app matches the table, the frame matches, even where the
image's fill level differs.

**Design change (v1 → v2):** v1 was a centered poster (3 boxed chips,
centered logo, 8×2 cards of 132×88, footer pill). v2 "Midnight Console" is
deliberately different: a quiet hairline header line, an ASYMMETRIC
left-anchored hero (rotating dotted orbit ring, 40px two-tone gradient
wordmark), a strict 2×8 band of compact 130×38 glass chips, and a
FULL-WIDTH bottom console bar with a live percent readout. Same stage
contract, same palette tokens, same legal strings, same public API — the
LAYOUT is the rewrite.

---

## 1. Source of truth (read from code this round)

- `Views/Splash/SplashView.xaml` (799 lines) — v2 layout, brushes, ambience
- `Views/Splash/SplashView.xaml.cs` (747 lines) — entrance choreography,
  30ms sweep timer, terms/login/first-run gates, fade-out
- `Themes/LightTheme.xaml` / `Themes/DarkTheme.xaml` — exact colour tokens
- `Themes/Styles/GlobalDesignSystem.xaml` — font sizes, radii, weights,
  Button.Primary/Secondary
- `Themes/Languages/en-US.xaml` — every splash/login string (verbatim)
- `Views/Login/LoginPanel.xaml`, `Views/Login/FirstRunSetupPanel.xaml`
  (shared components — UNCHANGED in v2)
- `Services/Updates/AppInfo.cs` — version `2.0.1`

### Palette (exact, from theme files — unchanged from v1)

| Token | Light | Dark |
|---|---|---|
| WindowBackground (stage start) | `#FBF8F1` | `#102244` |
| ModuleBackground (stage mid) | `#F8FAFC` | `#0B1220` |
| Stage end stop = AccentDark | `#0B5D89` | `#0EA5E9` |
| PrimaryForeground (ink) | `#111827` | `#E5E7EB` |
| SecondaryForeground | `#475569` | `#A9B8CC` |
| Border (hairlines) | `#CBD5E1` | `#3B4A63` |
| Accent | `#0F6FA8` | `#38BDF8` |
| AccentDark | `#0B5D89` | `#0EA5E9` |
| AccentLight (cerulean) | `#47B6E8` | `#7DD3FC` |

Stage = diagonal gradient (0.05,0)→(0.85,1): WindowBg → ModuleBg@45% →
WindowBg@72% → AccentDark@100% (`PageBg`). **v2 additions:** engineering
grid texture — DrawingBrush tile 64×64, 1px dotted accent hairlines,
rendered at 5% opacity on the stage; orbs 520² top-right (margin
0,-160,-160,0) AccentLight→Accent→transparent and 440² bottom-left (margin
-140,0,0,-140) Accent→AccentDark→transparent (both drifting, 19/23/26/21s);
hero spotlight 680² left-centre (margin 40,0,0,0, breathing 7s).

### v2 geometry (from the XAML)

- Header: 16px below top, 28px side margins; text 10.5px SemiBold
  Secondary, glyphs 12px accent; 1px hairline (Border, 50%) below the row.
- Hero: StackPanel left-anchored, 96px in (28px PosterHost margin + 96),
  vertically centred. No MaxWidth (band owns its own cap).
- Logo block: 168×168 container; halo 168² @30% (blur 30); DOTTED ORBIT
  RING 150², 1.5px stroke, dash `1 9` (~47 dots), 55% opacity, ONE LINEAR
  REVOLUTION per 30s; tile 96×96, 12px radius (`Radius.Large`), 1px accent
  border, plate ModuleBg→WindowBg + sheen `#4CFFFFFF→#12FFFFFF→#00FFFFFF`,
  glow shadow (blur 34, 45%); hexagon stroke 3px + `PGA` at FontSize.H1.
- Wordmark: **40px Bold**, two Runs — "ProGlass " ink, "Automation"
  gradient ink→AccentLight (deliberately oversized display).
- Tagline: 14px (`FontSize.Title`) accent, 10px below wordmark.
- Module band: ItemsControl, WrapPanel, **MaxWidth 1136** = exact 2×8
  (chip 130 + 12 gap = 142/slot). Chip: 130×38, 12px radius, `#14FFFFFF`
  fill, hairline border; inner 9px pad + 20×20 badge (7px radius,
  AccentLight→AccentDark diagonal, glyph 11.5px PaperBrush) + 8px gap +
  label 11px SemiBold ink (MaxWidth 84, CharacterEllipsis).
- Console bar: full width, 1px top hairline (Border), `#1A808080` glass,
  padding 28,14. LEFT: 8px accent status dot (pulse 2.4s) + status text
  13px (`FontSize.Body`) Secondary + dots 13px Bold accent. CENTRE:
  percent readout 12px Bold accent, right-aligned in a 46px box + 12px gap +
  progress track 280×2 (track `#22808080`, fill gradient
  AccentDark→Accent→AccentLight, left-anchored). RIGHT: copyright 11px
  Secondary @60%.
- Terms gate: title 34px Bold ink (soft accent glow) + intro 14px Secondary
  + 48×2 accent tick; card **MaxWidth 860**, 16px radius, `#1A808080` glass,
  hairline border, padding 40,32; clauses = 9 × (lead 14px Bold ink + body
  13.5px/24lh Secondary Wrap), 20px apart, ScrollViewer; action bar full
  width, 1px top hairline, `#1A808080`, padding 48,18, DockPanel MaxWidth
  860: checkbox + accept text 14px Secondary left; DECLINE 150×44 ghost +
  CONTINUE 190×44 accent right (**IsEnabled-bound → 45% until checked**).

### Entrance timeline v2 (from `SplashView_Loaded` + sweep timer)

| t (ms) | Element | Animation |
|---|---|---|
| 0–300 / 120–420 / 240–540 | header badges L/C/R | fade 0→1, 300ms each |
| 200–700 | logo | fade 0→1 + scale 0.9→1 (CubicEase Out) |
| 450–950 | wordmark | fade 0→1 + slide Y 14→0 |
| 700–1100 | tagline | fade 0→1, 400ms |
| 850–1350 | module band (whole panel) | fade 0→1, 500ms |
| 390–2280 | progress sweep + percent | 30ms DispatcherTimer: 13-tick lead (~390ms hold at 0) then 63 ticks (~1890ms) of **smoothstep** `p²(3−2p)` driving fill 0→280 AND PercentText 0%→100% |
| 0, cycle 350ms | dots | ""→"."→".."→"..." |
| 0 (immediate) | console status line + copyright | visible from first frame |
| ambient | ring 30s linear; hero glow 7s; orb drifts 19/23/26/21s; status dot 2.4s pulse | Window.Triggers |

**Exact percent at frame timestamps** (eased = p²(3−2p), p=(t−390)/1890):
t=100 → 0% · t=650 → **5%** · t=1000 → **24%** · t=1450 → **59%** ·
t=2300 → 100% (sweep ends at 2280ms). Dots at those t: none / "." / ".." /
"" / ".." (4-state cycle; exact count is a timing artifact, not a signal).

**16 modules** (code order, MDL2 glyphs): Dashboard E80F, Sheet Store E7B8,
Specifications E8F1, Inquiry E721, Proforma E8A5, Job Order E9D9, Tax
Invoice E8C7, Production E8C6, Optimization E9CE, Dispatch E7F4, Statements
E8F7, Reports E9F9, Profitability E90E, VAT E8C8, Master Data E822, System
E713 — labels resolved from live `Nav.*` resources.

**Strings (en-US, exact — ALL UNCHANGED from v1):** header `MADE IN INDIA`
/ `ENTERPRISE · v2.0.1` / `OFFLINE · SECURE` · tagline `Made in India for
the UAE glass industry` · status `Starting workspace` · footer `© 2026
ProGlass Automation` + `·  v` + `2.0.1` · terms title `TERMS OF USE` ·
intro `United Arab Emirates · Please read before continuing` · accept `I
have read and accept these terms, and I accept responsibility for the
accuracy of the records I produce.` · buttons `DECLINE AND EXIT` (150×44) /
`ACCEPT AND CONTINUE` (190×44, **disabled @45% until checkbox ticked**) ·
9 clauses verbatim (`1. Tax compliance is yours, not the software's.` →
`9. Governing law.` + their 9 bodies) · TermsVersion `2026-08-UAE-1`.
Login/first-run strings: see §2 frames 9–10.

---

## 2. The 10 frames (image ↔ code ↔ check)

| # | Image | Code state | Reproduce at runtime |
|---|---|---|---|
| 1 | `splash-01-stage-ignition.png` | t≈100ms: stage + console only — header/hero all `Opacity="0"`, fill 0%, "0%", no dots | First ~100ms of boot (breakpoint on `SplashView_Loaded`) |
| 2 | `splash-02-trust-frame.png` | t≈650ms: header line in, logo settled, wordmark ~40% mid-fade, no tagline/grid, **fill 5%**, dots "." | ~0.65s into boot |
| 3 | `splash-03-brand-lockup.png` | t≈1000ms: wordmark 100%, tagline ~75%, **fill 24%**, dots ".." | ~1.0s into boot |
| 4 | `splash-04-module-grid.png` | t≈1450ms: 2×8 chip band fully in, **fill 59%**, dots cycle | ~1.45s into boot |
| 5 | `splash-05-workspace-ready.png` | t≈2300ms steady: everything 100%, **fill 100%**, percent "100%" | Just before `FadeOutAndClose` (breakpoint) |
| 6 | `splash-06-dark-theme-ready.png` | Same state, Dark tokens | Same moment, saved theme = Dark |
| 7 | `splash-07-terms-gate-unchecked.png` | `ShowTermsGate()`: terms card, checkbox empty, CONTINUE @45% | Fresh machine / delete `%APPDATA%\ProGlassAutomation\terms-accepted.txt` |
| 8 | `splash-08-terms-gate-checked.png` | Checkbox ticked → `IsContinueEnabled=true` → CONTINUE full accent | Tick the box |
| 9 | `splash-09-sign-in-gate.png` | `RunLoginAsync()` (no auto-sign-in): PosterHost hidden, 420px LoginPanel on stage | Normal boot, existing user, skip-login NOT armed |
| 10 | `splash-10-first-run-setup.png` | `RunFirstRunSetupAsync()`: 460px FirstRunSetupPanel on stage | Fresh install (empty Users table) |

### Per-frame acceptance checklist

**All frames 1–6:** no title bar / no taskbar (WindowStyle=None, Topmost,
Maximized, ShowInTaskbar=False); stage diagonal direction correct; faint
64px grid visible only as texture; orbs partially off-screen (top-right
bigger); full-width console bar with top hairline.

**1:** hero area EMPTY — zero text/logo/icons; NO header text; console
shows status dot + `Starting workspace` + `0%` + completely empty track +
copyright right.

**2:** header = 3 quiet caps items + 1px hairline (NO boxed chips); logo
tile is a 12px rounded SQUARE with hexagon + `PGA` inside a DOTTED CIRCULAR
ring (not an ellipse, not a solid ring); wordmark two-tone, visibly
semi-transparent (~40%); no tagline, no chips; percent ~5% (small left
sliver).

**3:** wordmark crisp 100%; tagline ~75%; percent ~24%; still NO chips.

**4:** chips exactly 16 in 2 rows of 8, identical 130×38 size/spacing,
labels in code order; percent ~59%.

**5/6:** percent "100%", track fully filled (no right gap); dark frame:
navy→sky-blue stage (bottom-right corner is SKY-BLUE `#0EA5E9`, not deep
blue), ink `#E5E7EB`, "Automation" gradient →`#7DD3FC`, hairlines `#3B4A63`.

**7:** card 860 wide / 16px radius / glass; 48×2 accent tick under the
intro; clauses = bold lead + body, 20px apart; scrollbar visible; bottom
bar full width with top hairline; **checkbox empty; CONTINUE at 45%
opacity** (the key state).

**8:** checkbox accent-filled with white tick; CONTINUE crisp full-opacity
accent.

**9:** card 420 wide; order: logo → name → tagline → Username → Password →
remember/forgot row → skip-login box (toggle OFF gray) → SIGN IN 44px →
Exit → Version 2.0.1; NO status strip, NO error panel.

**10:** card 460 wide; 4 fields (Username / Display name / Password /
Confirm password); button `Create administrator`; closing-hint line;
Version 2.0.1.

---

## 3. Verification status

- **STATIC VERIFIED** — every value in §1 read from the files listed above;
  every resource key resolved against the theme dictionaries; all 35 x:Names
  in the XAML match the code-behind references; XML well-formed
  (`tools/xaml_validator.py`: 0 errors).
- **IMAGES** — generated from the §1 values; visual guide, not pixel-exact
  (AI rendering). Frames 2–4 fill levels are approximate in the image; the
  table values are exact. Discrepancy rule: **table + code win**.
- **RUNTIME** — NOT RUN here (no .NET SDK). The checklist in §2 is the 100%
  check on your machine; report any frame that disagrees with its table row
  and it gets fixed at source.

## 4. v1 → v2 change log (what a reviewer should look for)

CHANGED (layout only — behaviour identical):
- centered poster → **asymmetric left hero** (96px in, no MaxWidth)
- 3 boxed header chips → **quiet hairline header line** (10.5px caps, 1px
  hairline; same 3 strings + same glyphs E774/E735/E72E)
- 132×88 × 16 cards → **130×38 × 16 uniform chips** (2×8, 142px slots)
- footer pill → **full-width console bar** (status + dots | percent +
  280×2 luminous track | copyright)
- progress: 700–2600ms EaseInOut storyboard → **390–2280ms smoothstep
  30ms timer** driving fill + NEW `PercentText` 0→100%
- orbit ellipse (squashed) → **dotted circular ring** (30s linear rev)
- terms: 980px card / 360px hairline → **860px card / 48×2 accent tick /
  per-clause leads+body / 44px buttons**
- v1 latent quirk fixed: orb gradient stops no longer rely on a failing
  `{DynamicResource Transparent}` fallback (now literal `Color="Transparent"`)

UNTOUCHED: Window flags · public API (`FadeOutAndClose`, `RunLoginAsync`,
`RunFirstRunSetupAsync`, `ShowTermsGate`, `TermsAccepted`, `TermsResolved`,
`SignedIn`, `HasAcceptedCurrentTerms`, `TermsVersion`) · all 9 legal
clauses verbatim · LoginPanel / FirstRunSetupPanel (shared) · all en-US
strings · theme palette · ambient orb/glow motion contract.
