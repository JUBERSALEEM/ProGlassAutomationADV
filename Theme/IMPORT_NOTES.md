# Imported nine-theme subsystem

Source: `JUBERSALEEM/ProGlassAutomation`, branch `arena/f1e0ac70-proglassautomation`, commit `2ce9856`.

`Theme/` and `step1.ThemeStudio/` contain the source branch's complete theme engine, nine theme packages, Light/Dark dictionaries, component patterns, customization/color-plate/font/icon services, analytics, Studio UI, and verification assets.

## Adaptation into the ERP application

The standalone source branch's `App.xaml`, `MainWindow.xaml`, and project file are deliberately not used. The host remains the existing ERP WPF app and its existing `ThemePipeline` is the user-facing orchestrator. `Services/Theme/ImportedThemeRuntime.cs` initializes the imported `ThemeService` against the existing application resources, keeps the imported package in its own validated/replaceable resource slot, maps all nine host family identities, translates supported imported token types into the ERP resource vocabulary, and synchronizes app theme selection and mode.

Settings → Themes opens the imported Theme Studio against that same runtime. Imported color plates/font/geometry customizations are composed into the active package; icon style preferences are applied to the imported icon service, used by Theme Studio marks and the ERP sidebar's `ImportedIconMark` controls. Customization storage remains versioned and bounded by the imported JSON store. The existing ERP app config remains the source of truth for host family/mode.

WPF SDK default item discovery includes the imported production C#/XAML. The standalone theme verification `.cs` files are explicitly excluded from the shipping app and run through their dedicated test project. Full Windows build and interactive runtime verification are still required before marking integration complete.
