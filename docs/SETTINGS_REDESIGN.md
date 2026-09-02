# Settings Module v2 — Redesign Report

**Date:** 2026-08-15 · **Branch:** `arena/019fff44-proglassautomationadv`

## What was wrong (v1 audit)

| # | Issue | Severity |
|---|-------|----------|
| 1 | Single 2019-line `SettingsView.xaml` — every card stacked on one endless scroll; no navigation | Structure |
| 2 | 6 local button styles duplicated the global `Styles/ButtonStyles.xaml` design system (`DangerPillButtonStyle`, `DeletePillButtonStyle`, `PillActionButtonStyle` were already dead code) | Duplication |
| 3 | Code-behind handled 3 PasswordBoxes + Whisper card clicks in the shell — content and logic tangled in one file | Structure |
| 4 | Emoji glyphs in headers/cards/buttons (☀ 🔐 🤖 📊 🔵 ⚡ 🟣 🎤 📦 🗄 ✕ 🎨 🎙 🟣) — violates the Segoe MDL2 icon standard | UI |
| 5 | `HorizontalAlignment="Center"` scattered on pill buttons (button-standard violation) | UI |
| 6 | Right column (2/5 of the width) wasted on decorative info cards while settings cramped into 3/5 | UI |
| 7 | Inline hand-rolled ✕ button template for module-wallpaper rows | Duplication |
| 8 | Provider card border styling copy-pasted 3× (Gemini/Groq/Cohere) | Duplication |

## New architecture

```
Views/Settings/
├── SettingsView.xaml              ~260 lines — shell: nav rail + section hosts
├── SettingsView.xaml.cs           ~25 lines  — pure InitializeComponent
├── SettingsSectionStyles.xaml     shared ResourceDictionary (cards, typography,
│                                  toggle, password box, chips, nav styles)
└── Sections/
    ├── SettingsAppearanceSection  theme mode · wallpaper · accent colour ·
    │                              motion & performance · module wallpapers
    ├── SettingsAiSection          master toggle · data access · model & tuning ·
    │                              Gemini/Groq/Cohere providers · security
    ├── SettingsVoiceSection       Whisper model picker · status card
    ├── SettingsUpdatesSection     OTA check / download / install · history ledger
    ├── SettingsDataSection        backup & restore · sample data
    └── SettingsAboutSection       version card · 4 knowledge cards
```

Page switching reuses the proven TI/DN/JO detail pattern:
`SettingsViewModel.SettingsPage` + `Is{Page}Page` flags +
`SwitchSettingsPageCommand` — sections are visibility-switched, never
destroyed, so state (typed keys, downloads, selections) survives switching.

## Bug & quality fixes shipped

- All emoji replaced with **Segoe MDL2 Assets** glyphs (E790, E8F2, E72E,
  E720, E706, E9F9, E7B8, E968, E73E, E72C, E946, E713).
- Provider logos → neutral coloured dots (blue/amber/violet) — no emoji.
- All action buttons now use the global pill styles; `HorizontalAlignment="Center"` removed everywhere.
- Module-wallpaper delete → `RowDangerButton` (E711 glyph).
- 3× duplicated provider-card styling → one `ProviderCardBaseStyle` + BasedOn.
- 6 dead styles deleted; live styles moved verbatim into the shared dict.
- Updates hint text now reflects the Velopack reality (installed builds
  update in place).
- Content width capped at 780px per page (industrial settings standard).

## Safety verification performed

- minidom well-formedness: **8/8 XAML files OK**
- Resource-closure audit (every `StaticResource` resolvable): **0 missing**
- Binding preservation: **0 lost** (99 → 112; +13 = nav command/flags + version card)
- C# brace balance on all touched code-behinds/VM: **depth 0**
- Emoji scan on new files: **0 found**
- No external references to removed handlers/named elements
- `SmoothScrollBehavior` re-attached to every section ScrollViewer (parity)

## VM changes (additive only)

`SettingsViewModel`: `SettingsPage`, `IsAppearancePage`, `IsAiPage`,
`IsVoicePage`, `IsUpdatesPage`, `IsDataPage`, `IsAboutPage`,
`SwitchSettingsPageCommand`. No existing property, command or behaviour
was modified.
