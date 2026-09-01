# Phase Balance — ProGlass Automation ADV

_Short status file. Last updated: 2026-09-02. Detailed audit: `INDUSTRIAL_CODE_AUDIT.md`._

## Phase status

| # | Scope | Status | Evidence |
|---|-------|--------|----------|
| 1 | Service/DI infrastructure | ✅ | earlier commits |
| 2.1–2.3 | `App.*` static purge | ✅ | `a5abdad` (60 files) |
| 2.4 | `IDatabaseServices` facade | ✅ | `c3af619` (17 sites) |
| 2.5 | Code-behind handler sweep | ✅ series (see census) | `cd6d3ff`…`e0a7038` |
| 2.6 | Window dialogs → `IDialogService` | ✅ (OS file dialogs + PrintDialog → Phase 5.2) | `084afcf` (16 sites) |
| 3.1 | SettingsViewModel 3007 → 318-line core + 11 partials | ✅ | `9024ee5` |
| 3.1a | `App.*` leftovers (`AiProviderChanged`, `App.Specs`) | ✅ | `1049d66` |
| 3.2 | ReportingDataService 2430 → 202 + 10 partials | ✅ | `4bcfaab`, `54a7901` |
| 3.3 | StatementGeneratorViewModel 2596 → 346 + 10 partials | ✅ | `2bce90e` |
| 3.4 | PnL 2260 → 124+9 · TaxInvoiceDetail 2322 → 223+19 | ✅ | `0401d46` |
| 3.5 | App.xaml.cs 1338 → 306 + 4 partials | ✅ | `8478841` |
| 3.6 | MainWindow.xaml → region UserControls | ⏸ deferred — 37 `x:Name` fields contract; needs build | — |
| 3.7 | Controller dialogs → VM-backed | 🔄 (see remaining list) | `96cf731`…`a8a8b93` |
| 3.8 | Next god-file wave (16 files > 1000 lines) | ⏳ can proceed statically | — |
| 5.1 | `AppStartupPipeline` service extraction | ⏸ deferred until build | — |
| 5.2 | OS dialogs / PrintDialog / DemoModeService facade | ⏳ | — |
| 6 | CommunityToolkit `[ObservableProperty]`/`[RelayCommand]` + `ProGlassAutomation.Core` split | ⏸ deferred until build | — |

## Census (XAML handler attributes)

Series used one consistent regex (Click/Checked/Unchecked/SelectionChanged/KeyDown/Closed/Loaded/MouseLeftButtonDown): **174 → 157 → 128 → 113 → 107 → 95 → 86 → 76 → 70 → 64 → 61 → 57** (27 files).

**2026-09-02 cross-check correction:** a wider 36-event scan shows the series under-counts events outside that family (`Opened`, `PasswordChanged`, `PreviewKeyDown`, `TextChanged`, `MouseMove`, `SizeChanged`, `Drop`, …). True current surface: **86 attrs / 38 files** — 78 attrs / 37 files in the app + **8 in `Tools/LicenseGenerator`** (separate tool project). Per-file targets below use the true counts.

## Remaining 3.7 targets (true counts)

| Attrs | File | Notes |
|------:|------|-------|
| 5 | `Views/Shared/RecordOverlayWindow.xaml` | record HUD: pause/stop/open-folder/close + drag |
| 4 | `Views/JobOrder/…/JobOrderProformaPickerDialog.xaml` | search, show-all toggle, confirm |
| 4 | `Views/Login/LoginPanel.xaml` | sign-in/forgot/exit + preview-keydown |
| 4 | `Views/Splash/SplashView.xaml` | terms accept/decline + continue |
| 4 | `Views/CrashReports/CrashReportDialog.xaml` | restart/copy/open-folder/close |
| 4 | `Controls/TrendChart/TrendChartControl.xaml` | chart internals (size/mouse/preview) |
| 3 | `MainWindow.xaml` | popup Opened/Closed — 3.6-deferred keeps |
| 3 | `Views/Inquiry/InquiryEditorWindow.xaml` | window file-drop controller |
| 3 | `Views/Reports/Workflow/WorkflowWarningDialog.xaml` | create/skip/abort |
| 3 | `Views/Settings/Sections/SettingsAiSection.xaml` | PasswordBox keeps (by design) |
| 3 | `Views/UserCenter/UserCenterView.xaml` | create/switch/change-password |
| 2 | AboutLicenseCard, JobOrderAttachmentWarningDialog, ApprovalUploadChoiceDialog, FirstRunSetupPanel, UserSwitchPanel, JobOrderPicker, DeliveryNoteListView, SavedJobOrdersListView, SystemCenterView, TaxInvoiceListView, TaxInvoice ProformaPickerDialog, InquiryAttachmentPreviewWindow | trim or keep |
| 1 | 11 files (charts, login windows, list views, sections, versions) | mostly by-design keeps |

## God files still to split (`.cs` > 1000 lines)

| Lines | File |
|------:|------|
| 2199 | `ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.cs` |
| 1979 | `Services/Theme/VisualThemeEngine.cs` |
| 1918 | `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.cs` |
| 1859 | `Services/Statement/Workflow/StatementWorkflowEngine.cs` |
| 1601 | `ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.cs` |
| 1350 | `ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.cs` |
| 1233 | `ViewModels/Inquiry/InquiryListViewModel.cs` |
| 1207 | `ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.cs` |
| 1178 | `ViewModels/DeliveryNote/DeliveryNoteList/DeliveryNoteListViewModel.cs` |
| 1149 | `Services/Licensing/LicenseService.cs` |
| 1142 | `ViewModels/MainViewModel.cs` |
| 1085 | `Services/DeliveryNote/Repository/DeliveryNoteRepository.cs` |
| 1076 | `Services/Export/Pdf/Renderers/PdfChrome.cs` |
| 1051 | `Controls/GlassShowcase/GlassAssemblyShowcase.cs` |
| 1004 | `ViewModels/SheetStore/SheetStoreViewModel.cs` |
| 1001 | `Services/ThemeManager.cs` |

Plus `MainWindow.xaml` (1175 lines, 3.6-deferred). Theme language dictionaries (2396 lines ×15) are generated resource files — not refactor targets.

## Cross-check results (2026-09-02)

- Wide handler scan: **0 missed handler wirings**; reverse scan (dead handler methods): **0**.
- Every census attr has a matching method; **0 broken wirings**.
- Duplicate-member scan across all 6 partial groups: **0 real duplicates** (hits were distinct nested classes `AccentOption`/`FontColorOption`/`DeliveryRowAgg`/`StatementLineProjection` and anonymous types).
- Partial declarations consistent (one style fix applied: `SettingsViewModel.AppearanceSection.cs`).
- Remaining `App.Config`/`App.ServiceProvider` refs = documented keeps (config + DI facades).

## Next steps

1. 3.7: `RecordOverlayWindow` (5) → `LoginPanel` (4) → `SplashView` (4) → `CrashReportDialog` (4) → `JobOrderProformaPickerDialog` (4) → tails.
2. 3.8 god-file wave: start `DeliveryNoteDetailViewModel` (2199) or `PrintableInvoiceViewModel` (1601) — same census-verified split recipe.
3. Deferred until a compile/run is available: 3.6, 5.1, 6 (and 2.6's OS-dialog carve-out in 5.2).
