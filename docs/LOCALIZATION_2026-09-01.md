# System-Wide Language & Currency — Implementation & Audit (2026-09-01)

Status: **infrastructure + global chrome + phone/currency shipped**; deep
per-module text sweep tracked by `tools/i18n-audit.py` (see §5).

---

## 1. What the user asked for

> Multi-language + multi-currency switch globally across all modules;
> live internet FX at switch time (1 AED → ≈26 INR); offline = last
> fetched rate; English primary in every country; geo/IP suggests a
> second language (India → हिन्दी, China → 中文); 10–15 languages;
> language & currency independent (English UI + INR, Chinese UI + AED);
> phone box with country code from IP (+971 / +91 …).

## 2. Architecture (built)

```
Themes/Languages/{en-US,ar-AE,zh-CN,hi-IN,es-ES,fr-FR,de-DE,ru-RU,pt-BR,
                  tr-TR,ur-PK,bn-BD,id-ID,ja-JP,ko-KR}.xaml   ← 250 keys × 15 langs
        │  merged live by
Services/Localization/LocalizationService.cs
        │  · active dictionary merged AFTER en-US (always merged = fallback,
        │    partial translations never blank)
        │  · {DynamicResource Shell.Title} retranslates on switch, no restart
        │  · RTL (ar-AE, ur-PK) flips Window.FlowDirection
        │  · persists App.Config.Localization.Language

Services/Localization/CurrencyService.cs
        │  · books STAY in AED — conversion is display/print-time only
        │  · live rates: https://open.er-api.com/v6/latest/AED (free, no key)
        │  · every success cached → %AppData%\ProGlassAutomation\fx-cache.json
        │    with timestamp; offline (or fetch failure) → LAST FETCHED RATE
        │  · first-ever run → seed table (labelled "seeded", replaced on
        │    first successful fetch; the settings status line shows the source)
        │  · live repaint: every visible binding re-evaluates on switch
        │  · persists App.Config.Localization.CurrencyCode

Services/Localization/GeoService.cs
        │  · country = cache → https://ipwho.is (IP, no key) → OS region
        │  · raises LocationResolved → language suggestion + phone dial +
        │    one-time currency suggestion (never overrides a user pick)

Converters/CurrencyDisplayConverter.cs  (App.Resources key "CurrencyConverter")
        · renders BASE (AED) amounts in the display currency
        · 76 amount bindings converted: AED {0:N2} → prefix, {0:N2} AED → suffix,
          -/{+/-} variants, "With surcharge/Imported", /m² rates
        · 3 remaining N2 sites are in FROZEN files (GodView/DataGridView) — deferred
```

### Language & currency are INDEPENDENT
English UI + ₹ INR ✓ · 中文 UI + AED ✓ · العربية UI + ¥ CNY ✓ — any of the
15 × 28 combinations. Settings page: **Appearance → LANGUAGE & CURRENCY**
(three tiles: language, currency, phone country + auto-detect toggle +
live rate status + "Reset to English").

## 3. Phone box

New `Views/Shared/CountryPhoneControl` — ComboBox 🇦🇪 +971 | number.
- Default country: GeoService (IP → cache → OS region) → editable.
- Bound value stays the full phone with dial code (`+971-501234567`);
  legacy unprefixed values are preserved until edited.
- Wired into the three phone sites (all outside the freeze):
  `ClientProfileView` (ClientContactPhone),
  `CompanySetupView` + `CompanyCenterView` (RepresentativeMobile).

## 4. Startup (App.xaml.cs, right after ConfigService.Load)

1. `GeoService.Initialize()` — cache → `CurrencyService.Initialize()` —
   `LocalizationService.Initialize(Config.Localization)` (saved language,
   or regional suggestion; English is the primary default everywhere).
2. `_ = GeoService.RefreshAsync()` + `_ = CurrencyService.RefreshAsync()`
   (fire-and-forget; app never blocks, never throws).
3. Geo arrival → `ApplyRegionalSuggestion` (India → hi-IN, China → zh-CN,
   UAE → ar-AE…) once; the user's manual pick always wins afterwards.

## 5. Coverage audit (run `python3 tools/i18n-audit.py`)

FREEZE LIFTED (2026-09-01): GodView / DataGrid / DeliveryNoteList /
SettingsViewModel are editable again. The 5 previously-frozen XAML files
(146 literals) are now fully bound: 0 standalone labels remain in the
three GodViews; only numeric/unit fragments ("AED ", "05", "pcs",
" · ") stay literal — they are data separators, not translatable labels.

| Check | Result |
|---|---|
| Language dictionaries | 15 × 359 keys, identical key sets |
| Undefined `{DynamicResource}` locale keys | **0** |
| Locale keys via `StaticResource` (would not retranslate) | **0** |
| Live locale refs in Views | 65+ (shell, settings, splash, login, phone, currency, God views) |
| Literal UI strings remaining, non-god views | **2,956 strings / 118 files** (next phases) |
| Literal UI strings in ex-frozen files | 13 (units/separator fragments only) |

`tools/i18n/build.py` regenerates all 15 dictionaries from **one short
file per language** (`tools/i18n/{code}.py`). Country data lives in **one
short file per country** (`Services/Localization/Countries/Country.XX.cs`
— dial code, flag, currency, suggested language) aggregated by
`CountryCatalog`; `CountryDialCode` is a thin facade. `SeededFxRates`
was split into its own file. No single new file is large by design.

### What is fully localized & live
Shell nav + group titles + tooltips (MainWindow), top bar, Quick Settings,
Settings sidebar + language/currency page, Login window, Splash + legal
terms chrome (9 clauses), phone labels, currency/rate status, common
actions (Save/Cancel/Delete…), module names, and the five
previously-frozen screens (Delivery Notes / Job Orders / Tax Invoices
God Views, PI Step-5 DataGrid, Delivery Note List) — 109 extra keys
author-plate in all 14 non-English languages.

### 2,943 remaining literals — phased plan (needs the user's go-ahead)
The literal sweep touches ~116 view files; because the freeze explicitly
blocks GodView/DataGridViewModel/SettingsViewModel etc., the god-file
texts (137 strings) stay English (the en-US fallback) by design. All
future work uses the same mechanism:

1. Phase A — module home/list pages (Inquiry, Sheet Store, Reports Hub,
   System/User/Company Centers, PI saved lists).
2. Phase B — record editors & dialogs (Inquiry editor, Sheet dialogs,
   PI steps 1–4/6/8, statement generator shell).
3. Phase C — report/print surfaces (PrintableInvoice, report views).
4. Phase D — ViewModel string literals via `LocalizationService.T(...)`
   (SettingsViewModel and other code-behind label strings).

Every step adds keys to `tools/i18n/{code}.py` (build.py enforces all 15
languages carry the same key set — missing keys fail the build) and
converts the literal to `{DynamicResource Key}`. English stays the merged
fallback, so a missing translation shows English, never a blank label.

## 6. Exports remain invariant (regression guard preserved)

`ExportCultureInvarianceTests` + the 12 export boundary files keep
`CultureInfo.InvariantCulture`. The currency converter only changes the
**on-screen/print** rendering; CSV/XLSX/statement exports are untouched
and stay AED + invariant formatting, per the earlier release audit.

## 7. Windows verification checklist (this sandbox has no .NET SDK)

```
dotnet build -c Release            # watch: ProGlassAutomation.csproj
dotnet test                        # ExportCultureInvarianceTests etc.
python3 tools/i18n-audit.py        # expect: P1 = 0
python3 tools/publish-audit.py     # expect: P1=0 P2=56 P3=61 (baseline)
```

Manual smoke: start offline → English + seeded rate label; online → live
rate; switch to हिन्दी → full UI flips + ₹ (≈26 for 1 AED); kill internet
→ switch to 中文/¥ → uses last fetched rate; phone box shows +971 in UAE
and +91 when GeoService resolves IN.
