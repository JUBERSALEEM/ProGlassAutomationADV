# Senior Analysis — App-Wide Hardcoded Card Audit

**Date:** 2026-09-02
**Branch:** `arena/01a06070-proglassautomationadv`
**Scope:** every `.xaml` in the shipped app (Tests/Tools/Licensing excluded)

---

## 0. Executive summary

Three real defects found and fixed. Two of them were **functional bugs
hiding behind a cosmetic complaint** — finished features that no user
could reach.

| # | Defect | Impact | Status |
|---|---|---|---|
| 1 | Property-element `DynamicResource` on the Overview hero card | Card rendered **fully transparent** | **Fixed** |
| 2 | Reports hub rail: 6 hand-copied cards, VM list ignored | **VAT Submission report unreachable** | **Fixed** |
| 3 | Overview `DeliveryTiles` computed but never rendered | 5 delivery KPIs silently discarded | **Fixed** |

Two further areas were investigated and **deliberately left alone**;
the reasoning is in §5, because "why I did not change this" matters as
much as the diffs.

---

## 1. The transparent card

You pointed at the Overview dashboard's first card — the Factory Pulse
hero, next to the factory button. It set its brush as a **property
element**:

```xml
<Border.Background>
    <DynamicResource ResourceKey="DashHeroBlueBrush"/>
</Border.Background>
```

`DynamicResource` is a **markup extension**. In attribute position WPF
creates a deferred reference that resolves at runtime. In *property
element* position it does not: WPF constructs a
`DynamicResourceExtension` **object** and assigns that object to
`Background`. A `Border` cannot paint with it, so the card rendered
**fully transparent** — exactly what you saw.

The resource itself was never the problem: `DashHeroBlueBrush` is
defined in both `LightTheme.xaml` and `DarkTheme.xaml`.

```xml
<!-- fixed -->
Background="{DynamicResource DashHeroBlueBrush}"
```

**Swept the whole codebase for the pattern: this was the only site.**

### Why the validator missed it

This is well-formed XML and the key exists, so neither the new X0 parse
rule nor the resource-resolution rules could flag it. It is a *semantic*
WPF rule. Detection now exists as the one-line grep in §4.

---

## 2. Reports hub rail — hardcoded cards hiding a broken feature

`Views/Reports/SalesReportView.xaml` contained **six hand-copied
`<Button>` blocks**, ~26 lines each, differing only in icon, title,
subtitle and key — **163 lines** of near-duplicate markup.

Meanwhile `SalesReportViewModel` already declared a `NavItems`
collection describing exactly those cards. **Nothing was bound to it.**
It was dead code.

The two had drifted, and that drift is the actual bug:

> `NavItems` lists **seven** reports. The view rendered **six**.
> `VatSubmission` has a localized title, a registered view model and a
> working navigation target — **but no button could reach it.**

A fully implemented VAT report was invisible to users. That is the cost
of hardcoded cards: the data and the view disagree, and nothing tells
you.

**Fix:** one `ItemsControl` over `NavItems`. 163 lines → 81. Adding a
report is now one line in the view model.

### Localization was the trap — and it is handled

The hand-written labels used `{DynamicResource Nav.Overview}`, which WPF
**re-resolves automatically** when the language dictionary is swapped.
Binding literal strings from a view model would have **silently dropped
translation** — the rail would stay English while the rest of the app
switched to Arabic. A refactor that quietly breaks i18n is worse than
the duplication it removes.

So the fix keeps translation working end to end:

1. `NavItems` carries resource **keys** (`TitleKey`, `SubtitleKey`), not
   text.
2. A new `ResourceKeyToStringConverter` resolves them at bind time.
3. `SalesReportViewModel` subscribes to
   `LocalizationService.LanguageChanged` and re-raises `NavItems`, which
   forces every binding — and therefore every converter call — to
   re-evaluate on a live language switch.

Unknown keys render **the key itself**, not blank. A label reading
`Nav.Overview` is an obvious, greppable bug; an empty card is a mystery.

All 15 keys were verified present in `en-US.xaml`.

### Contrast was preserved, not assumed

The active pill fills with `AccentBrush`. The old markup flipped the
label to white with a per-`TextBlock` `DataTrigger` in **every copied
block**.

A naive template would have kept `Foreground="{DynamicResource
PrimaryForegroundBrush}"` as a **local value**, and a local value
**beats** the inherited `TextElement.Foreground` — leaving dark text on
a dark accent fill. The template therefore sets the default via a
`Style` setter and flips it with a trigger, so the active card cannot
render unreadable.

---

## 3. Overview dashboard — KPIs computed and thrown away

`OverviewDashboardViewModel` builds **four** tile collections:
`SalesTiles`, `ProductionTiles`, `InventoryTiles`, `DeliveryTiles`.

The view rendered **three**. Every dashboard refresh computed five
delivery KPIs (Preparing, Dispatched, Delivered Today, Total Delivered,
Pending Balance) that no one could see.

Added the fourth card using the **same** `SoftKpiTileTemplate` and
`SoftKpiRowPanel` as the other three — a new card with **zero** new
hardcoded tile markup.

---

## 4. Global sweeps run

| Check | Result |
|---|---|
| Property-element `DynamicResource` (the transparency bug) | **1 found, fixed** — no others |
| Surface brushes pointing at a **non-existent** key | **0** — no other invisible cards |
| Literal `#RRGGBB` on themed properties | 37, all intentional (print fidelity, splash, login) |
| Card-shaped `Border` with no `Background` and no `Style` | 53, all intentional (trigger-driven, translucent by design) |
| VM card/nav collections **never bound** in XAML | 89 scanned → **7 unbound**, all reviewed |

Reusable one-liner for the transparency class of bug:

```bash
grep -rn '<DynamicResource ResourceKey=' --include=*.xaml .
```

---

## 5. What I did NOT change, and why

You asked for *no hardcoded cards to remain*. Two areas match the
pattern but I stopped short, because converting them would trade a
cosmetic issue for real regression risk **with no compiler available to
verify**.

**`SettingsFeaturesSection.xaml` — 47 toggle tiles.**
Superficially identical, but each binds a **distinct named property**
(`ShowCompanyTrn`, `ShowBankName`, …). Making them data-driven needs an
indexer or 47-entry descriptor plus rework of how settings persist. They
already share `SettingsTileStyle` and are fully theme- and
localization-correct — the duplication is structural, not a defect.

**Statement Generator stepper — 4 badge blocks (~65 lines each).**
The strongest remaining candidate, but the per-step state
(`IsStep1Active`, `IsStep1Complete`, `CanNavigateToStep1`, ×4) is spread
across three partial files with **20 `OnPropertyChanged` fan-out sites**
driving the gating. Converting it means restructuring that notification
graph. A subtle mistake would break wizard navigation — a worse outcome
than duplicated markup.

Both are genuinely worth doing. I recommend doing them **after a green
build**, one at a time, so each is verifiable. I did not want to repeat
the pattern of shipping a large refactor that only static checks had
seen.

---

## 6. Verification

- Every touched `.xaml` parses as well-formed XML.
- `tools/xaml_validate.py` → **CLEAN**, 588 app-scope keys (+6).
- All 15 nav resource keys resolve in `en-US.xaml`.
- All 6 referenced brushes exist in **both** Light and Dark themes.
- `StringEqualsMultiConverter` returns `bool`, matching
  `DataTrigger Value="True"`.
- Brace/paren balance and BOM unchanged on every edited file.

**Still no compiler in this environment.** The C# added here is small
(one converter, one event subscription, one model change) and was
checked against real signatures — `LocalizationService.Instance`,
`LanguageChanged`, and `protected OnPropertyChanged` were each verified
in source rather than assumed. The build remains the source of truth.
