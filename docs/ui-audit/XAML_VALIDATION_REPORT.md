# XAML Safety Validation Report (Phase 17 gate)

**Generated:** 2026-09-18 · **Files checked:** 331 · **ERROR 0 / WARN 22 / NOTE 3**

**Legend:** W1 malformed · W2 dup x:Name · W3 dup x:Key · W4 dup attribute · W5 static declared-after-use · W6 static unresolved in parse scope · W7 dynamic unresolved & not runtime-published · W8 invalid numeric syntax · W9 Min>Max · W10 single-child violation · W11 Items+ItemsSource · W12 grid out-of-bounds (auto-extends) · W14 missing code-behind · W15 FindName missing · W16 unauthorized static on runtime token · W17 hardcoded hex in view. W3 duplicate x:Key is WARN (WPF last-wins shadowing, latent). W7 covers keys published at runtime by Services (ThemeManager, PrintTokenService, …).

**Honesty gate:** static analysis only. It cannot prove runtime layout, visual-tree construction or the actual WPF parse of merged dictionaries. **BUILD / RUNTIME verification: NOT RUN** (no .NET SDK in this environment) — do not treat this report as build or runtime proof.

## ERROR (0)

_none_

## WARN (22)

- `W5` **Controls/EmptyState/EmptyStateControl.xaml** — StaticResource 'IconFont' declared after first use in file
- `W5` **Controls/StatCard/StatCardControl.xaml** — StaticResource 'IconFont' declared after first use in file
- `W5` **Themes/Styles/GlobalDesignSystem.xaml** — StaticResource 'Tab.Inactive' declared after first use in file
- `W7` **Views/About/Sections/DeveloperSection.xaml** — DynamicResource 'Ios26TileBadgeBrush' declared nowhere & not runtime-published
- `W7` **Views/About/Sections/DeveloperSection.xaml** — DynamicResource 'Ios26TileBadgeBrush' declared nowhere & not runtime-published
- `W7` **Views/About/Sections/DeveloperSection.xaml** — DynamicResource 'Ios26TileBadgeBrush' declared nowhere & not runtime-published
- `W5` **Views/Diagnostics/DiagnosticsView.xaml** — StaticResource 'EmojiToGlyph' declared after first use in file
- `W5` **Views/Diagnostics/DiagnosticsView.xaml** — StaticResource 'EmojiToGlyph' declared after first use in file
- `W7` **Views/Optimization/OptimizationView.xaml** — DynamicResource 'ShadowHeavy' declared nowhere & not runtime-published
- `W7` **Views/Production/ProductionControlCenterView.xaml** — DynamicResource 'FontSize.TitleLarge' declared nowhere & not runtime-published
- `W7` **Views/ProformaInvoice/Step7PrintPreview/PrintPreviewView.xaml** — DynamicResource 'SurfaceBrush' declared nowhere & not runtime-published
- `W7` **Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml** — DynamicResource 'Chart.IndigoBrush.Alpha1A.5E' declared nowhere & not runtime-published
- `W7` **Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml** — DynamicResource 'Chart.LimeBrush.Alpha1A.30' declared nowhere & not runtime-published
- `W7` **Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml** — DynamicResource 'Chart.MintBrush.Alpha1A' declared nowhere & not runtime-published
- `W7` **Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml** — DynamicResource 'Chart.NavyBrush.Alpha1A.F6' declared nowhere & not runtime-published
- `W7` **Views/Reports/StatementGenerator/Preview/PreviewStep.xaml** — DynamicResource 'Radius.Top.Large' declared nowhere & not runtime-published
- `W7` **Views/Reports/StatementGenerator/Preview/PreviewStep.xaml** — DynamicResource 'Radius.Top.Large' declared nowhere & not runtime-published
- `W3` **Views/Settings/SettingsView.xaml** — duplicate x:Key 'SettingsCardMargin' — later declaration shadows the earlier one (latent bug)
- `W5` **Views/Settings/SettingsView.xaml** — StaticResource 'SettingsPageTemplateSelector' declared after first use in file
- `W7` **Views/Splash/SplashView.xaml** — DynamicResource 'Transparent' declared nowhere & not runtime-published
- `W7` **Views/Splash/SplashView.xaml** — DynamicResource 'Transparent' declared nowhere & not runtime-published
- `W7` **Views/Splash/SplashView.xaml** — DynamicResource 'Transparent' declared nowhere & not runtime-published

## NOTE (3)

- `W17` **Agent/UI/AgentChatView.xaml** — 3 hardcoded hex value(s) in view
- `W17` **Views/Personal/PersonalSheetStoreView.xaml** — 5 hardcoded hex value(s) in view
- `W17` **Views/Splash/SplashView.xaml** — 9 hardcoded hex value(s) in view
