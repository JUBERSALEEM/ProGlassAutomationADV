# SPLASH — 10 FRAME MOCKUPS + 100% VERIFICATION TABLE (2026-09-19)

**Purpose:** ten mockup images of the splash (`Views/Splash/SplashView.xaml`),
one per REAL state the code produces. Every element in every mockup maps to
code — nothing invented. The images (AI-generated) are the visual guide;
**this table + the code are the 100% reference**, and the running app is the
final check.

**Mockups:** `/home/user/splash_mockups/splash-01..10.png` (kept OUT of the
repository, per the no-mockups-in-code rule).

**Honest scope:** images are NOT pixel-exact (AI generation). They are
100% *implementable* — every colour, size, text string, position, opacity
state and transition below is taken from the code. Compare app → table →
image. If app matches the table, the image is a match.

---

## 1. Source of truth (read from code this round)

- `Views/Splash/SplashView.xaml` (844 lines) — full layout, brushes, ambience
- `Views/Splash/SplashView.xaml.cs` (805 lines) — entrance choreography,
  dots timer, terms/login/first-run gates, fade-out
- `Themes/LightTheme.xaml` / `Themes/DarkTheme.xaml` — exact colour tokens
- `Themes/Styles/GlobalDesignSystem.xaml` — font sizes, radii, Chip.Primary,
  Button.Primary/Secondary
- `Themes/Languages/en-US.xaml` — every splash/login string
- `Views/Login/LoginPanel.xaml`, `Views/Login/FirstRunSetupPanel.xaml`
- `Services/Updates/AppInfo.cs` — version `2.0.1`

### Palette (exact, from theme files)

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
WindowBg@72% → AccentDark@100% (`PageBg`). Plus: constellation
DrawingBrush tile 340×300 @ 35% opacity; orb 520×520 top-right
(margin 0,-160,-160,0) AccentLight→Accent→transparent; orb 440×440
bottom-left (margin -140,0,0,-140) Accent→AccentDark→transparent;
hero glow 620×620 centre (margin 0,-40,0,0).

### Typography / geometry tokens

`FontSize.Display=32` · `FontSize.H1=24` · `FontSize.Title=14` ·
`FontSize.Body=13` · `Radius.XSmall=3` · `Radius.Medium=10` ·
`Radius.XLarge=16` · `Radius.Full=12` (logo tile is a 12px rounded SQUARE,
not a circle — the 999-capsule was removed globally).

### Entrance timeline (SplashView.xaml.cs `SplashView_Loaded`)

| t (ms) | Element | Animation |
|---|---|---|
| 0–400 / 100–500 / 200–600 | top chips L/C/R | fade 0→1, 400ms each |
| 100–600 | logo | fade 0→1 + scale 0.92→1 (CubicEase Out) |
| 450–900 | wordmark | fade 0→1 + slide Y 12→0 |
| 750–1150 | tagline | fade 0→1 |
| 1000–1450 | module tiles (whole panel) | fade 0→1, 450ms |
| 700–2600 | progress fill | width 0→track (≈320) 1900ms EaseInOut |
| 950–1300 | status text + dots | fade 0→1; dots cycle 350ms: ""→"."→".."→"..." |
| t=0 (no animation) | footer pill | visible from first frame |
| ambient (Window.Triggers) | hero glow breathe 7s; orb drifts 19/23/26/21s; orbit ring 48s linear |

**16 module tiles** (code order): Dashboard, Sheet Store, Specifications,
Inquiry, Proforma, Job Order, Tax Invoice, Production, Optimization,
Dispatch, Statements, Reports, Profitability, VAT, Master Data, System —
132×88, margin 8 (16px gap), 16px radius, glass fill
`#33FFFFFF→#14808080→#0D000000`, 40×40 badge (accent-light→accent-dark
gradient) with MDL2 glyph, label 13px semibold. WrapPanel MaxWidth 1216 →
**8 per row, 2 rows** at any normal width.

**Strings (en-US, exact):** chips `MADE IN INDIA` / `ENTERPRISE · v2.0.1` /
`OFFLINE · SECURE` · tagline `Made in India for the UAE glass industry` ·
status `Starting workspace` · footer `© 2026 ProGlass Automation` + `·  v`
+ `2.0.1` · terms title `TERMS OF USE` · intro `United Arab Emirates ·
Please read before continuing` · accept text `I have read and accept these
terms, and I accept responsibility for the accuracy of the records I
produce.` · buttons `DECLINE AND EXIT` (150×42) / `ACCEPT AND CONTINUE`
(190×42, **disabled @45% opacity until checkbox ticked**) · login
`ProGlass Automation` / `Made in India · for the UAE glass industry` /
`Username` / `Password` / `Remember username` / `Forgot password?` /
`Skip login until update` + `Auto sign-in on this device. Resets on the
next app update.` / `SIGN IN` (44px) / `Exit` / `Version 2.0.1` · first-run
`Welcome to ProGlass` / `Create the first administrator account` /
`ProGlass needs an administrator to get started. Choose a username and
password — you will use them to sign in right after.` / `Username` /
`Display name` / `Password` / `Confirm password` / `Create administrator` /
`Closing the splash will exit the app — ProGlass cannot run without a user.`

---

## 2. The 10 frames (image ↔ code ↔ check)

| # | Image | Code state | Reproduce at runtime |
|---|---|---|---|
| 1 | `splash-01-stage-ignition.png` | t=0: stage only — all content `Opacity="0"` defaults; footer already visible | First ~100ms of boot (or pause with a debugger breakpoint on `SplashView_Loaded`) |
| 2 | `splash-02-trust-frame.png` | t≈650ms: 3 chips visible, logo settled (100%), wordmark ~45% mid-fade | ~0.65s into boot |
| 3 | `splash-03-brand-lockup.png` | t≈1000ms: wordmark 100%, tagline ~55%, progress just started (~8%), status fading | ~1.0s into boot |
| 4 | `splash-04-module-grid.png` | t≈1450ms: 16 tiles ~55% fade-in, 2×8 grid, progress ~40%, dots ".." | ~1.45s into boot |
| 5 | `splash-05-workspace-ready.png` | t≈2600ms steady state: everything 100%, progress 100%, dots "..." | Just before `FadeOutAndClose` (breakpoint on `FadeOutAndClose`) |
| 6 | `splash-06-dark-theme-ready.png` | Same state as #5, Dark theme tokens | Same moment, with app saved theme = Dark (Settings → Appearance) |
| 7 | `splash-07-terms-gate-unchecked.png` | `ShowTermsGate()`: full-page terms, checkbox empty, Continue disabled @45% | Fresh machine / delete `%APPDATA%\ProGlassAutomation\terms-accepted.txt` |
| 8 | `splash-08-terms-gate-checked.png` | Same + checkbox ticked → `IsContinueEnabled=true` → Continue full accent | Tick the box on screen |
| 9 | `splash-09-sign-in-gate.png` | `RunLoginAsync()` (auto-sign-in unavailable): PosterHost hidden, 420px LoginPanel on stage | Normal boot with existing user (no skip-login armed) |
| 10 | `splash-10-first-run-setup.png` | `RunFirstRunSetupAsync()`: 460px FirstRunSetupPanel on stage | Fresh install (empty Users table) |

### Per-frame acceptance checklist

**All frames (1–6):** stage gradient direction (warm top-left → deep-blue
bottom-right corner); both orbs partially off-screen (top-right bigger);
footer pill `© 2026 ProGlass Automation · v2.0.1` bottom-centre; no taskbar
(window is `WindowStyle=None, Topmost, Maximized, ShowInTaskbar=False`);
no window title bar anywhere.

**1:** zero text/logo/icons; ONLY atmosphere. If any UI element is visible
before its fade start time → regression.

**2:** exactly 3 chips, edge-to-edge row (left / centre / right); logo tile
is a 12px rounded SQUARE (not circle) with hexagon + `PGA`; dashed elliptical
orbit ring squashed horizontally; wordmark visibly semi-transparent (~45%).

**3:** wordmark two-tone split exactly after "ProGlass " (ink) /
"Automation" (cerulean); tagline centred under it; progress track thin (3px)
with only a left sliver; status text present, NO dots yet (first dot ≈1.3s).

**4:** tiles exactly 16 in 2 rows of 8, identical size/spacing; badge icon
centred above label; progress ~40%; status shows two dots.

**5/6:** progress bar fully filled (no gap at right end); three dots; dark
frame uses the dark palette table above (verify the bottom-right corner is
sky-blue, not deep blue — that's the AccentDark stop difference).

**7:** card max-width 980, 16px radius, translucent; 9 numbered terms with
bold leads; bottom bar spans full width with top hairline; DECLINE outlined
/ CONTINUE solid; **CONTINUE at 45% opacity** (the key state).

**8:** checkbox solid blue + white tick; CONTINUE crisp full-opacity accent.

**9:** card 420 wide, single-column; field order Username → Password →
remember/forgot row → skip-login box (toggle OFF, gray) → SIGN IN 44px →
Exit link → Version; NO skip-login pill armed state, NO error panel, NO
status strip (all Hidden while idle).

**10:** card 460 wide (wider than login); 4 fields; hint line under Password;
button text `Create administrator` (NOT "Sign in"); closing-hint line
present; version at bottom.

---

## 3. Verification status

- **STATIC VERIFIED** — every value in §1 read from the files listed above
  (no assumption, no script verdict).
- **IMAGES** — generated from the §1 values; visual guide only, not
  pixel-exact (AI rendering). Discrepancy rule: **table + code win**.
- **RUNTIME** — NOT RUN here (no .NET SDK). The checklist in §2 is the
  100% check on your machine; report any frame that disagrees with its
  table row and I'll fix it at source.
