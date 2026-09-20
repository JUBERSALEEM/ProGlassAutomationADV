# ProGlass WPF audit

**Date:** 2026-09-20  
**Scope:** `ProGlassAutomationADV` (net8.0-windows, UseWPF)  
**Branch (remote):** `arena/01a0bb84-proglassautomationadv` @ `2a5e104`  
**This clone HEAD:** `c919443` (behind remote; dirty C# tree not in the UI commit)

**Verdict:** Static XAML/GDS restyle for in-scope ERP views is in. App is **not** build/test/runtime verified here.

---

## Working (static / code)

| Area | Evidence |
|---|---|
| Project shape | `ProGlassAutomation.csproj` — `net8.0-windows` + WPF |
| XAML inventory | Guard **337** files, **489** GDS keys — **OK** |
| Shared visual system | GDS last-win, `ErpPageRoot`, Field.*, list chrome, KPI 36 |
| In-scope restyle | Lists, JO/DN/TI shells, reports, production filter/stations |
| Token dialect | `TextSecondaryBrush`, `FontFamily.Icon`, type scale |
| Competing `SurfacePrimaryStyle` | **0** on in-scope Views (sole card chrome kept on ~24) |
| Input Height 40 | **0** leftover in-scope |
| Height 38 filters/buttons | Densified / snapped to 36 |
| Domain tests exist | **~219** files, **~1727** `[Fact]` — PI, JO, TI, DN, opt, production, VAT, security, theme, settings nav |

## Not working / not proven

| Area | Status |
|---|---|
| **Build** | **NOT EXECUTED** — no .NET SDK in this environment |
| **Unit tests** | **NOT EXECUTED** — 1727 facts unrun here |
| **Runtime / UI** | **NOT EXECUTED** — no live WPF session |
| **CI** | No `.github/workflows` |
| Settings / Ios26 / About | Still Ios26* language — **not** GDS restyle |
| Printable invoice | Deferred — not restyled |
| Personal (GbNight) | Deferred — not restyled |
| AiChat 38×38 avatar, inquiry 38×38 glyph | Left on purpose (not KPI) |
| Generator KPI `Typography.Label` BasedOn | Left on purpose |
| This sandbox vs remote | Clone **behind** `2a5e104`; extra uncommitted C# (opt/identity/theme) **not** in the UI SHA |

## Module snapshot (code present, runtime unknown)

| Module | Code | UI restyle | Run? |
|---|---|---|---|
| Login / first-run | Yes | Partial (White on auth kept) | Unknown |
| PI / costing / P&L | Yes | In-scope GDS | Unknown |
| JO / DN / TI | Yes | Lists + detail shells | Unknown |
| Inquiry / statements / reports | Yes | Registers + filters | Unknown |
| Production stations | Yes | Density; nested controls **no** ErpPageRoot | Unknown |
| Sheet store / company / user | Yes | In-scope GDS | Unknown |
| Optimization | Yes | Tabs densified | Unknown |
| Settings / About | Yes | **Ios26 — open** | Unknown |
| Print preview (printable) | Yes | **Open** | Unknown |

## Risks

1. Redesign never compiled against WPF — resource-key misses would only show at load.
2. Clone/remote split + dirty C# can hide or revert UI work if committed from the wrong tree.
3. Settings/About still look like a different app than ERP.
4. No CI → regressions in PI pricing, wastage, JO/P&L, MaterialKey, opt identity are unguarded in this environment.

## Recommendation

1. Restore/sync to **`2a5e104`** before any new UI commit.  
2. On a machine with SDK: `dotnet build` then targeted tests (PI, opt, production, theme).  
3. Visual pass: lists → JO/DN/TI → reports → production.  
4. Settings/Ios26/print only after that green.  
5. Do not treat this audit as production-verified.
