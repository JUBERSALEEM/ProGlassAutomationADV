# step1.c.4 — Recorded cleanup inventory (before deletion)

Date: 2026-10-20. User explicitly authorized legacy cleanup and reference-page improvements, but no commit/push. Recorded before deleting any implementation.

Inspected root `/home/user/ProGlassAutomation`, remote `https://github.com/JUBERSALEEM/ProGlassAutomation.git`, branch `arena/bd154b4b-proglassautomation`, clean working tree, HEAD `57725f3`. Baseline `2d89b34` contains the prior implementation; the only subsequent commit contains the approved V3.0 work. Phase 00/01 cannot be separated further by Git history, so all approved documents are preserved.

All legacy file contents, imports, XAML namespaces, DI/navigation and dependencies were read. The current explicitly included root/Theme/Studio application has no references to this graph. SheetStore registers SQLite/Dapper/CSV/Toolkit services internally; old ShellStyles contains dashboard/sidebar navigation; old converters support only legacy views (ColorName directly uses SheetStore models). The empty old MainViewModel has no V3 consumer. No legacy-exclusive test project exists.

## Exact proposed deletions

| Path | Classification | Evidence / responsibility | SHA-256 before removal |
| --- | --- | --- | --- |
| `Converters/BoolToVisibilityConverter.cs` | Remove | Converter referenced only by legacy views, or unused; no V3 references/inclusion. | `ba38c9024ff6c68a74c9819f72ecbd9d6005998f6b7c19b71a81d4018db9e02b` |
| `Converters/ColorNameToBrushConverter.cs` | Remove | Converter referenced only by legacy views, or unused; no V3 references/inclusion. | `db97f44b4412ab20f9c5e1e1e149353b773517b67ddbab2230364fa36a5ad2e8` |
| `Converters/DateFormatConverter.cs` | Remove | Converter referenced only by legacy views, or unused; no V3 references/inclusion. | `1d025fec64ec582756c54c469270a7d465f822b1863506ad2e7abfce62503cd7` |
| `Converters/NullToBoolConverter.cs` | Remove | Converter referenced only by legacy views, or unused; no V3 references/inclusion. | `73d8f805f0e16aded829f7c76b672aa4da4e9aba2c6b07159e8987605b99fb4b` |
| `Shared/Styles/ShellStyles.xaml` | Remove | Old hard-coded dashboard/sidebar ShellStyles and its empty companion; no V3 dictionary merge. | `159d02ea9e53237388c9de0f5cbdd9466f7ef754ee2fabb16e6fc333825886ba` |
| `Shared/Styles/ShellStyles.xaml.cs` | Remove | Old hard-coded dashboard/sidebar ShellStyles and its empty companion; no V3 dictionary merge. | `6ee99e11584df8a3ffd9e1700b0221784c2a3c62ea02f42f36492503d5371abb` |
| `SheetStore/Common/Messages.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `9eeebf8c20ce8b64ede77cd2df24548348e9b5f954f3be9182ae1284b735913a` |
| `SheetStore/DI/SheetStoreModule.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `7d42602b32640d26f59afe0fcf315453275cf151f14950c2ca59625a9ef78cc6` |
| `SheetStore/Models/DTOs/ImportResult.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `9a9991143f8fa256e3ff8bc96d2a70bd81b2df1efdabd9b46f190e5d313c20cb` |
| `SheetStore/Models/DTOs/SheetFilter.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `c4f75caa058a356621c6f17b92d3ccc43cdb3a9ccbeb4cdb3fb14c217e966945` |
| `SheetStore/Models/DTOs/SheetStats.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `ed8ebe7819fa1cff84041aa293a058ae4fdbb276152b5ad73503eea314e43802` |
| `SheetStore/Models/Sheet.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `6ff3da909ca6d3d4c694cc9c80af7d4765d2b146d582ad3685d4091a07288fb1` |
| `SheetStore/Models/SheetPurchase.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `376fbaae60b4ad0d80b67ac084c7bbb63b675fa63def20800b2e88faceb450d6` |
| `SheetStore/Models/SheetUsage.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `9f85f31aa8ed78ad06d9df36fe0b402c5ef9c556adf420ab3bacaae8c5b18645` |
| `SheetStore/Models/Supplier.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `33f3c4cdd59c9120922e985ca6bf7436073260601dad2846e7133cf6b2553989` |
| `SheetStore/Navigation/INavigationService.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `4770d8e3aacf2962d9fd0c00b1a7dbfbe31cda456c3a93c013d52f63f9ed2bc9` |
| `SheetStore/Navigation/NavigationService.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `173ecaa73828fcbed6fe0a8c6560a494b329f4df0213b259bf1a325f33760939` |
| `SheetStore/Services/IMediator.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `0548f3e0572ba7c0ae9ad688cfd11be823e0243b9656c1b60737cbfff8853e3b` |
| `SheetStore/Services/ISheetStoreCache.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `196d3dff4b9d6398e1be745868e74457e22327e3a13b0cb64ad952a3de513745` |
| `SheetStore/Services/ISheetStoreExport.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `d926d294d171f3daa66d55d5abe228c589432a7b7553824108b3f7a8e32dd494` |
| `SheetStore/Services/ISheetStoreRepository.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `5f509087e2605584d6b351f851f67efd9baf3ab7b8576b5871e3661841b90a01` |
| `SheetStore/Services/ISheetStoreService.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `ba7cfa5d33f638d85579877231c8110e77da1ed91c845870d167943a2847b8ef` |
| `SheetStore/Services/SheetStoreService.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `b8b9a2c709e4217416addc95131fa288941fb7ba0b5ac599325e31e1d94bd5b6` |
| `SheetStore/Services/SqlSchema.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `e3f1790e8bcf6681fa47f6bd23c320200d8c7937091e21427f05268c4a532586` |
| `SheetStore/Services/SqliteRepository.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `bf498ff984c0f5d1d02db094fdcf40811511c18cf70c79f8992f39014f2bc2cb` |
| `SheetStore/ViewModels/AddSupplierDialogViewModel.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `41304f508171b0d5656dfbca7f83787fa9497090923a909cf159ef9dd253535c` |
| `SheetStore/ViewModels/BuySheetDialogViewModel.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `8c4b6e87b5682e756d405537df2b0399c88cd6598c11bb7563314a8b541eb98e` |
| `SheetStore/ViewModels/PriceHistoryDialogViewModel.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `51de972df8d232ce4f175528c5ef6152b45a402f6dd8aa1a872d35df3c264115` |
| `SheetStore/ViewModels/PurchasePriceDialogViewModel.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `68a1fd8353e282da2d81de2b4c5b6621908e3fe92c36efc3adfcd42afe98bb6c` |
| `SheetStore/ViewModels/SheetDialogViewModel.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `9102729e54dfdfda550030aa93b82b8a10ea6ffa45f83da7e65584012ef62a91` |
| `SheetStore/ViewModels/SheetStoreViewModel.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `3ecfa5bfabfea8ce1fe7c3fdb7fc4bea545b3cb98a096be0facde20db3f48c6d` |
| `SheetStore/ViewModels/UseSheetsDialogViewModel.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `f4474ba9ce1b3307dda2ae8863fd822c63d29b35b9670e7d9bde10e11700b374` |
| `SheetStore/Views/AddSupplierDialog.xaml` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `f19c6a79a8ac47c43c9480f1d0aa7973181e37c518d486806cf01c390395919a` |
| `SheetStore/Views/AddSupplierDialog.xaml.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `fcac42f7bcf8e7fa1ae642f1170c23a9334a1b240bf5932eb8d4719041a7e0cc` |
| `SheetStore/Views/BuySheetDialog.xaml` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `2123b60f13f8ac4b449ce60f80803c72e80cf6070ecebb739c0ad0c685c3c617` |
| `SheetStore/Views/BuySheetDialog.xaml.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `ded682be34d7692c174e7688542fbc37afd6aca60d7a85cd84d4c4ced93d5e6e` |
| `SheetStore/Views/PriceHistoryDialog.xaml` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `abcc705837eafce3d1eba02b6beb9d5201d2de6746e9855516ca24c63876bea6` |
| `SheetStore/Views/PriceHistoryDialog.xaml.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `449b6885eb82f460321b4b288773c6cad7b2fdfd8424a072150d603bdb2e4c15` |
| `SheetStore/Views/PurchasePriceDialog.xaml` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `01d3eb9fe7f78f868fef3a3933d96281092cde973d6c0838cd0a6da81c95dc6e` |
| `SheetStore/Views/PurchasePriceDialog.xaml.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `2088ee1e20efe0389032d8f1a6049522d3878b76cf7727546d1d1fa5c988a38b` |
| `SheetStore/Views/SheetDialog.xaml` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `b3fe2cc947fab5ada73b24093d3a328f5379c14864a3ec53bdf082ff60b19991` |
| `SheetStore/Views/SheetDialog.xaml.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `4ea98ec322a1de285684e100d9fd9c0a3a694c29f9e49fc035fb8dac7446a87d` |
| `SheetStore/Views/SheetStoreView.xaml` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `3097cc0d6ddb621a451d0e6e3837f264bd0ea1d0e4628b92f6f9f4d098fea6a1` |
| `SheetStore/Views/SheetStoreView.xaml.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `c68d0a3ce2c7e3d61853fe00e6a22e3086546063a1fd04d705c1383b5d2d8a4d` |
| `SheetStore/Views/UseSheetsDialog.xaml` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `4a61acdc359332c66a232e8eadb90850dc6d52859422e5d55a259a54025bd407` |
| `SheetStore/Views/UseSheetsDialog.xaml.cs` | Remove | Legacy domain, DTOs, UI, navigation or service graph; excluded from V3 Compile/Page and unreferenced by V3. | `afdd4eff6cf2dbcbd0918f726ab65e82d245e7944d314dbbb318f7113cd81eb4` |
| `ViewModels/MainViewModel.cs` | Remove | Empty old root MainViewModel; V3 uses ThemeStudioViewModel instead. | `a1a896cd101bb3790e3ca3900ede99b76eb305bd9d88e5ca005873ba01fb2f4d` |

**47 exact file deletions proposed.** No database data files found; no history rewrite or remote change.

## Preserve / review resolution — remaining tracked files

| Path | Classification | Origin / decision |
| --- | --- | --- |
| `.gitattributes` | Preserve | Pre-existing standard repository/WPF file; inspected and retained. |
| `.gitignore` | Preserve | Pre-existing standard repository/WPF file; inspected and retained. |
| `AGENTS.md` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `App.xaml` | Preserve | Review → Preserve: root integration or README already rewritten for V3.0; retain/refine, never delete. |
| `App.xaml.cs` | Preserve | Review → Preserve: root integration or README already rewritten for V3.0; retain/refine, never delete. |
| `AssemblyInfo.cs` | Preserve | Pre-existing standard repository/WPF file; inspected and retained. |
| `MainWindow.xaml` | Preserve | Review → Preserve: root integration or README already rewritten for V3.0; retain/refine, never delete. |
| `MainWindow.xaml.cs` | Preserve | Review → Preserve: root integration or README already rewritten for V3.0; retain/refine, never delete. |
| `ProGlassAutomation.csproj` | Preserve | Review → Preserve: root integration or README already rewritten for V3.0; retain/refine, never delete. |
| `ProGlassAutomation.slnx` | Preserve | Review → Preserve: root integration or README already rewritten for V3.0; retain/refine, never delete. |
| `README.md` | Preserve | Review → Preserve: root integration or README already rewritten for V3.0; retain/refine, never delete. |
| `Theme/ThemeBento/ThemeBento.Dark.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/ThemeBento/ThemeBento.Light.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/ThemeBento/ThemeBento.Readme.md` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/ThemeBento/step1.e.7.BentoResources.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/ThemeBento/step1.e.Components/step1.e.1.Components.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/ThemeBento/step1.e.Components/step1.e.2.Foundation.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/ThemeBento/step1.e.Components/step1.e.3.Buttons.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/ThemeBento/step1.e.Components/step1.e.4.Inputs.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/ThemeBento/step1.e.Components/step1.e.5.Selections.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/ThemeBento/step1.e.Components/step1.e.6.PopupsAndScroll.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/step1.a.ThemeEngine/step1.a.1.ThemeContract.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/step1.a.ThemeEngine/step1.a.2.ThemeService.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/step1.a.ThemeEngine/step1.a.3.ResourceValidation.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/step1.a.ThemeEngine/step1.a.5.ThemeContract-README.md` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/step1.a.ThemeEngine/step1.a.6.ThemeCatalog.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/step1.d.tests/step1.d.1.RuntimeChecks.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/step1.d.tests/step1.d.2.ContractChecks.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/step1.d.tests/step1.d.3.verify_sources.py` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/step1.d.tests/step1.d.4.CheckerRegression.py` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/step1.d.tests/step1.d.5.verify_isolation.py` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `Theme/step1.d.tests/step1.d.ThemeTests.csproj` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `app.manifest` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `docs/00.Project-Rules.md` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `docs/01.Architecture-Principles.md` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `docs/02.UI-UX-Design-System.md` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `docs/03.Development-and-Testing.md` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `docs/04.Project-Roadmap.md` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `docs/05.Decision-Log.md` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `docs/06.Feature-Registry.md` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `docs/ci/windows-wpf.yml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.1.ThemeStudioView.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.1.ThemeStudioView.xaml.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.10.SurfacesPanel.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.10.SurfacesPanel.xaml.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.11.TokensPanel.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.11.TokensPanel.xaml.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.12.PreviewWindow.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.12.PreviewWindow.xaml.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.3.ThemeStudioViewModel.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.4.DelegateCommand.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.5.TokenRow.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.6.OverviewPanel.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.6.OverviewPanel.xaml.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.7.AppearancePanel.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.7.AppearancePanel.xaml.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.8.ControlsPanel.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.8.ControlsPanel.xaml.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.9.TypeAndStatusPanel.xaml` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.b.ThemeStudioScreen/step1.b.9.TypeAndStatusPanel.xaml.cs` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.c.docs/step1.c.1.ThemeStudio-README.md` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.c.docs/step1.c.2.Phase01-Verification.md` | Preserve | Introduced by approved V3.0 commit; preserve. |
| `step1.ThemeStudio/step1.c.docs/step1.c.3.Theme-Isolation-and-CI.md` | Preserve | Introduced by approved V3.0 commit; preserve. |

No unresolved mixed-ownership files remain. Root App/MainWindow/project retain only V3 responsibilities. Historical reports remain evidence, not current acceptance claims. The workflow will move from inactive docs/ci to .github/workflows per the latest instruction; its contents are preserved.

## After-cleanup expectation

All listed Remove files absent; every Preserve path retained except the explicitly documented workflow relocation. No legacy namespaces, SQLite/DI packages, navigation commands or resources in active application sources. An executable foundation check will enforce this.
