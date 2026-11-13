# Pinned source reconciliation

**Compared:** `JUBERSALEEM/ProGlassAutomation@2ce9856` (`arena/f1e0ac70-proglassautomation`) against the imported `Theme/` and `step1.ThemeStudio/` trees in this repository.

**Result:** all 241 source files under those two imported paths are present locally. There are zero missing paths, one local-only file (`Theme/IMPORT_NOTES.md`), and six byte-different source files. This comparison was performed against the source archive for the pinned commit, not inferred from the local manifest.

## Byte-different files and adaptation rationale

| File | Adaptation |
|---|---|
| `Theme/step1.a.ThemeEngine/step1.a.2.ThemeService.cs` | Host-safe runtime integration: candidate load/validation before resource mutation, single package slot, appearance selection, rollback on slot replacement failure, package change notification, overlay notification, and active style/resource access. |
| `Theme/step1.d.tests/step1.d.1.RuntimeChecks.cs` | Reworked from the standalone demo's `MainWindow` smoke test into a host-ERP STA harness. It checks merged host resources, token/style publication, all 18 theme/mode activations, package-slot stability, custom plate role propagation, and every icon kind/style geometry. It still needs execution on Windows. |
| `Theme/step1.d.tests/step1.d.3.verify_sources.py` | Adapted the source contract audit to validate the imported component boundary in the ERP instead of asserting standalone-demo assumptions about the host's unrelated resources. |
| `Theme/step1.d.tests/step1.d.6.verify_foundation.py` | Adapted the foundation audit for preserved ERP startup, actual `ThemePipeline`, Settings-to-Studio route, host resource adapter, icon wiring, and data/test isolation. |
| `Theme/step1.g.Customization/step1.g.1.CustomizationModel.cs` | Whitespace-only end-of-file difference. |
| `Theme/step1.g.Customization/step1.g.8.IconService.cs` | Extended the imported icon catalog with action-oriented semantic marks consumed by shared ERP action templates; original icon styles and kinds remain. |

No theme-family source dictionary, family resource loader, Light/Dark palette, extras, or Studio source file was omitted or changed from the pinned source. The WPF build and the STA runtime harness remain required to validate compilation and actual resource/template behavior.

## Project inclusion boundary

- WPF SDK default item discovery includes production `.cs` and `.xaml` under `Theme/` and `step1.ThemeStudio/`.
- `Theme/step1.d.tests/**/*.cs` and `*.py` are explicitly excluded from the shipping application's compile/content items by `ProGlassAutomation.csproj`.
- The imported standalone `App.xaml`/`MainWindow.xaml` are not part of the imported paths and are not used to replace the ERP shell/startup.
- `LiveChartsCore.SkiaSharpView.WPF` is referenced for the imported Studio/analytics. Compatibility with the app's pinned SkiaSharp dependencies still needs Windows restore/build verification.
