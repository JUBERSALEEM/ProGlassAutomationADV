# Indigo Studio Bento Design System: Master Plan & Execution Roadmap (236 Views)

> **Document Status**: Active Standard  
> **Target Branch**: `arena/01a0ce09-proglassautomationadv`  
> **Automation Guard**: `python3 scripts/bento_phase_tracker.py`  
> **Scope**: 236 Applicable UI Views across 10 Execution Phases (Zero Misses, Zero Duplicates)

---

## 1. Executive Summary & Design System Foundations

The **Indigo Studio Bento Design System** provides a compact, high-density industrial user experience rooted in Japanese Bento Box design principles:
- **Maximum Information Density**: Zero dead space, high situational awareness.
- **The One-Sentence Rule**: *"If a card's content takes up less than 40% of its area, it needs more columns, not more padding."*
- **No Concentric Cards**: Eliminating nested cards within cards. A card is a self-contained unit of information; inner groups use dividers, tint variations, or borderless rows.
- **Rule 1**: Never use 2 equal columns for small form content.
- **Rule 2**: Column counts determined strictly by content density:
  - **5 Columns (`Ov.CardMicro`)**: Single KPI metrics (IconBox + Tabular Value in Viewbox + Label).
  - **4 Columns (`Ov.CardCompact`)**: Small form controls (Label + Toggle / Dropdown).
  - **3 Columns (`Ov.CardCompact`)**: Medium form controls (Label + Input pair + Action).
  - **2 Columns (`Ov.Card`)**: Large complex surfaces (DataGrids, Timelines, Charts, Feeds).
  - **1 Column (`Ov.Card`)**: Full-width tables, chronological audit logs, transaction ledgers.
- **Rule 3**: Visual hierarchy: Hero Number / Primary Title > Card Title > Description / Metadata.
- **Rule 4**: Stateful interaction: Tactile hover states, focused focus rings, and explicit feedback.
- **Rule 5**: Numbers always formatted in `Viewbox` with `Ov.Value` (tabular numerals) to prevent layout shifting.

---

## 2. Standardized Token & Primitive Universe

All views must standardize on the tokens defined in `Views/Dashboard/Overview/IndigoStudioResources.xaml`:

### Palette Brushes
- `Ov.Canvas`: `#F3F0FA` (Global page background)
- `Ov.Surface`: `#FFFEFC` (Card background surface)
- `Ov.Soft`: `#EEE8F8` (Pill / chip background tint)
- `Ov.Line`: `#E2DBEF` (Card and control border stroke)
- `Ov.Ink`: `#2C2440` (Primary readable text)
- `Ov.Muted`: `#6B6078` (Secondary label and hint text)
- `Ov.Accent`: `#6546B5` (Signature brand accent)
- `Ov.AccentHover`: `#533698` (Interactive hover fill)
- `Ov.Hero`: LinearGradient `#6744B9` $\rightarrow$ `#493080` (Hero cards & active headers)
- `Ov.Success` (`#107C41`), `Ov.Danger` (`#A80000`), `Ov.Warning` (`#797775`)

### Card Primitives
```xml
<!-- Full Card: Charts, DataGrids, Complex Panels (Radius: 18, Padding: 24) -->
<Border Style="{StaticResource Ov.Card}"> ... </Border>

<!-- Compact Card: Form Inputs, Multi-column Fieldsets (Radius: 14, Padding: 16,14) -->
<Border Style="{StaticResource Ov.CardCompact}"> ... </Border>

<!-- Micro Card: Single KPI metrics, Status Tiles (Radius: 12, Padding: 14,12) -->
<Border Style="{StaticResource Ov.CardMicro}"> ... </Border>
```

### Typography Scale
- `Ov.Title`: 32px SemiBold (Page and workspace headings)
- `Ov.CardTitle`: 18px SemiBold (Section and card titles)
- `Ov.Value`: 30px SemiBold with Tabular Numbers (KPI figures)
- `Ov.Text`: 14px Regular (Body text and input labels)
- `Ov.Meta`: 12px Regular (Subtitles, metadata, timestamps)
- `Ov.Glyph`: 18px Segoe MDL2 Assets (Iconography)

---

## 3. Protocol for Future Chats & Continuous Delivery

Any new agent session or developer picking up this repository must follow this lifecycle:

1. **Check Status**:
   ```bash
   python3 scripts/bento_phase_tracker.py
   git log -n 5 --oneline
   ```
2. **Identify Current Phase**:
   - Locate the phase marked `[IN_PROGRESS]` or `[READY_TO_START]` in this master plan.
3. **Safety Checklist Before Editing**:
   - Never break existing `Binding`, `Command`, `CommandParameter`, `Converter`, `ElementName`, or `RelativeSource`.
   - Never delete existing `x:Name` or `AutomationProperties.Name` used by view models or automated tests.
   - Never hardcode margin literals `> 4` in sections (use semantic styles or static margin keys).
   - Shell views must stay $\le 420$ lines; Sections $\le 400$ lines; Cards $\le 520$ lines.
4. **Automated Verification**:
   ```bash
   python3 scripts/bento_phase_tracker.py <PHASE_NUMBER>
   python3 scripts/verify-dashboard.py
   python3 scripts/test_settings_redesign.py
   ```
5. **Commit & Push**:
   ```bash
   git add <modified-files>
   git commit -m "feat(bento-phase-X): <concise summary of transformed views>"
   git push origin arena/01a0ce09-proglassautomationadv
   ```
6. **Update Tracking**:
   - Mark the completed phase `[COMPLETED]` and the next phase `[IN_PROGRESS]` in this file and in `scripts/bento_phase_tracker.py`.

---

## 4. Master 10-Phase Breakdown (236 Views)

```
Total Views in Repository:                254
Already Bento Reference (Dashboard/etc):   18
Views in Bento Transformation Plan:       236
```

### Phase Summary Dashboard

| Phase | Category | Target Modules | View Count | Status |
| :---: | :--- | :--- | :---: | :---: |
| **1** | Settings Foundation & Personalization | Settings Shell, Card Styles, Appearance, Themes, Voice, License, About, Sharing | 27 | **COMPLETED** |
| **2** | Settings Data, Storage, Features & AI | Data, Backup, Features, AI Assistant, Agent, Demo Hub, Updates | 28 | **COMPLETED** |
| **3** | Centers, Diagnostics & System Hubs | Company Center, User Center, System Center, Diagnostics, Crash, About, AI Chat | 19 | **COMPLETED** |
| **4** | Core ERP Document Details & Trans. | DeliveryNote, JobOrder, TaxInvoice Details & Inquiry Editor | 25 | **COMPLETED** |
| **5** | Proforma Invoice Wizard & Estimator | Proforma 13-Step Wizard (Steps 1–9), Pricing Parameters, Approval Dialogs | 26 | **COMPLETED** |
| **6** | Factory Production & Operations | Cutting, Tempering, Polish, Lamination Work Centers & Pipeline Bars | 18 | **COMPLETED** |
| **7** | Cutting Optimization & SheetStore | 2D Optimization Tabs (8 Tabs), SheetStore Warehouse & Inventory Dialogs | 17 | **COMPLETED** |
| **8** | Reports Hub & Statement Generators | Executive & Domain Reports (14), Order Report Pipeline, Statement Generator | 32 | **PENDING** |
| **9** | Job Profitability & Personal Glass | Job Profitability Analytics (6), Personal Glass Workspace (13 views) | 19 | **PENDING** |
| **10** | Auth, Splash & Shared System Overlays | Login Suite, Splash, Quick Settings, Record Panels, Audit Dialogs | 25 | **PENDING** |
| **ALL**| **Complete Application Suite** | **All 236 Views Accounted For (100% Coverage)** | **236** | **0 Misses** |

---

### Phase 1: Settings Foundation & Personalization Suite (27 Views)
**Status**: `COMPLETED`  
**Focus**: Structural foundation of Settings, un-nesting concentric cards, unifying typography into Bento tokens, compact 4-column card placement.

#### File Manifest:
1. `Views/Settings/SettingsView.xaml` (Shell navigation host)
2. `Views/Settings/SettingsCardStyles.xaml` (Card resource dictionary)
3. `Views/Settings/SettingsPageTemplates.xaml` (DataTemplate selector definitions)
4. `Views/Settings/SettingsSectionStyles.xaml` (Section spacing and behavior styles)
5. `Views/Settings/Sections/SettingsAppearanceSection.xaml`
6. `Views/Settings/Sections/SettingsThemesSection.xaml`
7. `Views/Settings/Sections/SettingsVoiceSection.xaml`
8. `Views/Settings/Sections/SettingsLicenseSection.xaml`
9. `Views/Settings/Sections/SettingsAboutSection.xaml`
10. `Views/Settings/Sections/SettingsSharingSection.xaml`
11. `Views/Settings/Sections/Cards/AppearanceAccentFontCard.xaml`
12. `Views/Settings/Sections/Cards/AppearanceCardEdgesTile.xaml`
13. `Views/Settings/Sections/Cards/AppearanceCrystalTile.xaml`
14. `Views/Settings/Sections/Cards/AppearanceDensityCard.xaml`
15. `Views/Settings/Sections/Cards/AppearanceFontColorTile.xaml`
16. `Views/Settings/Sections/Cards/AppearanceFrostTile.xaml`
17. `Views/Settings/Sections/Cards/AppearanceGradientTile.xaml`
18. `Views/Settings/Sections/Cards/AppearanceLanguageCard.xaml`
19. `Views/Settings/Sections/Cards/AppearanceModeCard.xaml`
20. `Views/Settings/Sections/Cards/AppearancePreviewTile.xaml`
21. `Views/Settings/Sections/Cards/AppearanceRadiusTile.xaml`
22. `Views/Settings/Sections/Cards/AppearanceSplashCard.xaml`
23. `Views/Settings/Sections/Cards/AppearanceThemeFamilyTile.xaml`
24. `Views/Settings/Sections/Cards/AppearanceThemeLayoutCard.xaml`
25. `Views/Settings/Sections/Cards/AppearanceWallpapersCard.xaml`
26. `Views/Settings/Sections/Cards/ThemesCrystalMaterialCard.xaml`
27. `Views/Settings/Sections/Cards/VoiceAudioCard.xaml`

---

### Phase 2: Settings Data, Storage, Features, AI & Demo Suite (28 Views)
**Status**: `COMPLETED`  
**Focus**: Data backup and health cards (fixing crowded concentric cards), multi-column feature toggles, AI/Agent provider cards, and Demo Hub recording controls. Concentric card nesting eliminated across all 28 views. Numbers wrapped in Viewbox for tabular responsive scaling. Single-action rows and clean Bento stack grids standardized.

#### File Manifest:
1. `Views/Settings/Sections/SettingsDataSection.xaml`
2. `Views/Settings/Sections/SettingsFeaturesSection.xaml`
3. `Views/Settings/Sections/SettingsAiSection.xaml`
4. `Views/Settings/Sections/SettingsAgentSection.xaml`
5. `Views/Settings/Sections/SettingsDemoSection.xaml`
6. `Views/Settings/Sections/SettingsUpdatesSection.xaml`
7. `Views/Settings/Sections/Cards/DataBackupCard.xaml`
8. `Views/Settings/Sections/Cards/DataHealthCard.xaml`
9. `Views/Settings/Sections/Cards/DataRootCard.xaml`
10. `Views/Settings/Sections/Cards/DataSampleCard.xaml`
11. `Views/Settings/Sections/Cards/FeaturesDeliveryNoteCard.xaml`
12. `Views/Settings/Sections/Cards/FeaturesInquiryCard.xaml`
13. `Views/Settings/Sections/Cards/FeaturesJobOrderCard.xaml`
14. `Views/Settings/Sections/Cards/FeaturesProformaCard.xaml`
15. `Views/Settings/Sections/Cards/FeaturesSessionCard.xaml`
16. `Views/Settings/Sections/Cards/FeaturesTaxInvoiceCard.xaml`
17. `Views/Settings/Sections/Cards/AiAssistantCard.xaml`
18. `Views/Settings/Sections/Cards/AiProvidersCard.xaml`
19. `Views/Settings/Sections/Cards/AiSecurityCard.xaml`
20. `Views/Settings/Sections/Cards/AgentCoreCard.xaml`
21. `Views/Settings/Sections/Cards/AgentDataCard.xaml`
22. `Views/Settings/Sections/Cards/DemoFfmpegCard.xaml`
23. `Views/Settings/Sections/Cards/DemoHistoryCard.xaml`
24. `Views/Settings/Sections/Cards/DemoIsolationCard.xaml`
25. `Views/Settings/Sections/Cards/DemoOutputCard.xaml`
26. `Views/Settings/Sections/Cards/DemoQualityCard.xaml`
27. `Views/Settings/Sections/Cards/DemoRecordCard.xaml`
28. `Views/Settings/Sections/Cards/DemoWorkflowCard.xaml`

---

### Phase 3: System Centers, Diagnostics, About & System Hubs (19 Views)
**Status**: `COMPLETED`  
**Focus**: Modular Bento decks across CompanyCenter and UserCenter; audit logs, live activity feeds, and diagnostics with responsive severity badges; light studio Bento architecture across About 6-screen suite; Tabular Viewbox number scaling in SpecificationsCenter. Zero concentric card nesting.

#### File Manifest:
1. `Views/CompanyCenter/CompanyCenterView.xaml`
2. `Views/UserCenter/UserCenterView.xaml`
3. `Views/SystemCenter/SystemCenterView.xaml`
4. `Views/Diagnostics/DiagnosticsView.xaml`
5. `Views/CrashReports/CrashReportDialog.xaml`
6. `Views/CrashReports/CrashReportDialogStyles.xaml`
7. `Views/AI/AiChatView.xaml`
8. `Views/Specifications/SpecificationsCenterView.xaml`
9. `Views/Sharing/ShareComposerWindow.xaml`
10. `Views/About/AboutIos26Theme.xaml`
11. `Views/About/AboutStyles.xaml`
12. `Views/About/AboutView.xaml`
13. `Views/About/Sections/AboutCrashReportsCard.xaml`
14. `Views/About/Sections/AboutLicenseCard.xaml`
15. `Views/About/Sections/AboutOverviewSection.xaml`
16. `Views/About/Sections/DeveloperSection.xaml`
17. `Views/About/Sections/DiagnosticsSection.xaml`
18. `Views/About/Sections/ModulesSection.xaml`
19. `Views/About/Sections/SystemSection.xaml`

---

### Phase 4: Core ERP Document Details & Transaction Views (25 Views)
**Status**: `COMPLETED`  
**Focus**: Converting DeliveryNote, JobOrder, and TaxInvoice detail screens from rigid forms into Bento structures with dedicated KPI strips and attachment sidebars. Applied Rule 5 Viewbox scaling to KPI metrics, financial totals, and quantity summaries across all 25 transaction views. Zero concentric nesting.

#### File Manifest:
1. `Views/DeliveryNote/DeliveryNoteView.xaml`
2. `Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml`
3. `Views/DeliveryNote/DeliveryNoteDetail/Sections/DnOverviewSection.xaml`
4. `Views/DeliveryNote/DeliveryNoteDetail/Sections/DnHistorySection.xaml`
5. `Views/DeliveryNote/DeliveryNoteDetail/Sections/DnAttachmentsSection.xaml`
6. `Views/DeliveryNote/DeliveryNoteDetail/Sections/DnSectionStyles.xaml`
7. `Views/DeliveryNote/DeliveryNoteList/JobOrderPicker/JobOrderPickerDialog.xaml`
8. `Views/DeliveryNote/DeliveryNoteDetail/Attachments/Rename/RenameAttachmentDialog.xaml`
9. `Views/JobOrder/JobOrderView.xaml`
10. `Views/JobOrder/JobOrderDetail/JobOrderDetailView.xaml`
11. `Views/JobOrder/JobOrderDetail/Sections/JoOverviewSection.xaml`
12. `Views/JobOrder/JobOrderDetail/Sections/JoProductionSection.xaml`
13. `Views/JobOrder/JobOrderDetail/Sections/JoSectionStyles.xaml`
14. `Views/JobOrder/SavedJobOrdersList/JobOrderAttachmentWarningDialog.xaml`
15. `Views/JobOrder/SavedJobOrdersList/ProformaPicker/JobOrderProformaPickerDialog.xaml`
16. `Views/TaxInvoice/TaxInvoiceView.xaml`
17. `Views/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailView.xaml`
18. `Views/TaxInvoice/TaxInvoiceDetail/Sections/TiOverviewSection.xaml`
19. `Views/TaxInvoice/TaxInvoiceDetail/Sections/TiFamilySection.xaml`
20. `Views/TaxInvoice/TaxInvoiceDetail/Sections/TiAttachmentsSection.xaml`
21. `Views/TaxInvoice/TaxInvoiceDetail/Sections/TiSectionStyles.xaml`
22. `Views/TaxInvoice/TaxInvoiceDetail/IssueDraftsDialog.xaml`
23. `Views/TaxInvoice/TaxInvoiceList/ProformaPickerDialog.xaml`
24. `Views/Inquiry/InquiryEditorWindow.xaml`
25. `Views/Inquiry/InquiryAttachmentPreviewWindow.xaml`

---

### Phase 5: Proforma Invoice 13-Step Wizard & Pricing Suite (26 Views)
**Status**: `COMPLETED`  
**Focus**: Redesigning estimation parameters (DGU, SGU, Laminated), DataGrid summary rows, and profit/loss calculation cards into high-density Bento card layouts. Standardized Rule 5 Viewbox scaling across Step 4 Pricing Estimator, Step 5 GrandTotalSummary, and Step 9 ProfitLoss cards. Zero concentric nesting.

#### File Manifest:
1. `Views/ProformaInvoice/ProformaInvoiceView.xaml`
2. `Views/ProformaInvoice/Step1CompanySetup/CompanySetupView.xaml`
3. `Views/ProformaInvoice/Step2ClientProfile/ClientProfileView.xaml`
4. `Views/ProformaInvoice/Step3ProjectSpecs/ProjectSpecsView.xaml`
5. `Views/ProformaInvoice/Step4PricingEstimator/PricingEstimatorView.xaml`
6. `Views/ProformaInvoice/Step4PricingEstimator/DguLamParameters/DguLamParametersView.xaml`
7. `Views/ProformaInvoice/Step4PricingEstimator/DguParameters/DguParametersView.xaml`
8. `Views/ProformaInvoice/Step4PricingEstimator/LamParameters/LamParametersView.xaml`
9. `Views/ProformaInvoice/Step4PricingEstimator/SguParameters/SguParametersView.xaml`
10. `Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml`
11. `Views/ProformaInvoice/Step5DataGrid/GrandTotalSummary/GrandTotalSummaryView.xaml`
12. `Views/ProformaInvoice/Step6OtherCharges/OtherChargesView.xaml`
13. `Views/ProformaInvoice/Step6OtherCharges/CustomCharges/CustomChargesSectionView.xaml`
14. `Views/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionView.xaml`
15. `Views/ProformaInvoice/Step7PrintPreview/PrintPreviewView.xaml`
16. `Views/ProformaInvoice/Step7PrintPreview/InvoiceMainSummary/InvoiceMainSummaryView.xaml`
17. `Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceView.xaml`
18. `Views/ProformaInvoice/Step7PrintPreview/TermsAndConditions/TermsAndConditionsView.xaml`
19. `Views/ProformaInvoice/Step8Attachments/AttachmentsStepView.xaml`
20. `Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml`
21. `Views/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonView.xaml`
22. `Views/ProformaInvoice/Step9ProfitLoss/ServiceProfitabilityView.xaml`
23. `Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml`
24. `Views/ProformaInvoice/SavedInvoicesList/InvoiceApprovalDialog.xaml`
25. `Views/ProformaInvoice/SavedInvoicesList/VersionsDialog.xaml`
26. `Views/ProformaInvoice/SavedInvoicesList/SavedInvoicesListDialog.xaml`

---

### Phase 6: Factory Floor Production Work Centers & Controls (18 Views)
**Status**: `COMPLETED`  
**Focus**: Industrial work center screens (Cutting, Tempering, Polish, Mitring, Doubling) converted into Bento live-status decks with `Ov.CardMicro` metrics, responsive Viewbox tabular KPIs across `ProductionKpiStrip`, `CuttingWorkCenterView`, `ProcessWorkCenterView`, `ProductionControlCenterView`, and `ProductionUnitDetailControl`. Zero concentric nesting.

#### File Manifest:
1. `Views/Production/CuttingView.xaml`
2. `Views/Production/CuttingWorkCenterView.xaml`
3. `Views/Production/DoublingLaminationView.xaml`
4. `Views/Production/EdgeDeletionView.xaml`
5. `Views/Production/HolesCutoutsView.xaml`
6. `Views/Production/MitringView.xaml`
7. `Views/Production/PairingView.xaml`
8. `Views/Production/PolishView.xaml`
9. `Views/Production/ProcessWorkCenterView.xaml`
10. `Views/Production/ProductionControlCenterView.xaml`
11. `Views/Production/TemperingView.xaml`
12. `Views/Production/Controls/ProductionFilterBar.xaml`
13. `Views/Production/Controls/ProductionKpiStrip.xaml`
14. `Views/Production/Controls/ProductionPipelineBar.xaml`
15. `Views/Production/Controls/ProductionReleaseBar.xaml`
16. `Views/Production/Controls/ProductionStationBody.xaml`
17. `Views/Production/Controls/ProductionUnitDetailControl.xaml`
18. `Views/Production/Controls/ProductionUnitDetailTemplates.xaml`

---

### Phase 7: Glass Optimization Engine & SheetStore Warehouse (17 Views)
**Status**: `COMPLETED`  
**Focus**: Modernizing 2D cutting layout tabs and SheetStore warehouse racks with unified Bento cards, responsive Viewbox scaling on KPI quad across OptimizationView, SummaryTabView, SheetStoreView, SheetDetailWindow, BuyDialogView, and UseDialogView. Zero concentric nesting.

#### File Manifest:
1. `Views/Optimization/OptimizationView.xaml`
2. `Views/Optimization/Tabs/CutListTabView.xaml`
3. `Views/Optimization/Tabs/JobsTabView.xaml`
4. `Views/Optimization/Tabs/ReportTabView.xaml`
5. `Views/Optimization/Tabs/SettingsTabView.xaml`
6. `Views/Optimization/Tabs/SizesTabView.xaml`
7. `Views/Optimization/Tabs/StocksTabView.xaml`
8. `Views/Optimization/Tabs/SummaryTabView.xaml`
9. `Views/Optimization/Tabs/TwoDViewTabView.xaml`
10. `Views/SheetStore/SheetStoreView.xaml`
11. `Views/SheetStore/AddEditSheetView.xaml`
12. `Views/SheetStore/SheetDetailWindow.xaml`
13. `Views/SheetStore/SheetStoreBuy/BuyDialogView.xaml`
14. `Views/SheetStore/SheetStoreCsv/DuplicateResolutionView.xaml`
15. `Views/SheetStore/SheetStoreHistory/HistoryView.xaml`
16. `Views/SheetStore/SheetStoreItems/ItemMovementView.xaml`
17. `Views/SheetStore/SheetStoreUse/UseDialogView.xaml`

---

### Phase 8: Reports Hub, Executive Analytics & Statement Generators (32 Views)
**Status**: `PENDING`  
**Focus**: Standardizing all remaining financial, tax, and order reports under `Ov.Card` and `Ov.CardMicro` KPI banners, plus statement generator wizard steps.

#### File Manifest:
1. `Views/Reports/SalesReportView.xaml`
2. `Views/Reports/CompanyReport/CompanyReportView.xaml`
3. `Views/Reports/CustomerReport/CustomerReportView.xaml`
4. `Views/Reports/DeliveryReport/DeliveryReportView.xaml`
5. `Views/Reports/ExecutiveReport/ExecutiveReportView.xaml`
6. `Views/Reports/GlassSpecReport/GlassSpecReportView.xaml`
7. `Views/Reports/JoReport/JoReportView.xaml`
8. `Views/Reports/PiReport/PiReportView.xaml`
9. `Views/Reports/ProfitabilityReport/ProfitabilityReportView.xaml`
10. `Views/Reports/ProjectReport/ProjectReportView.xaml`
11. `Views/Reports/SalesmanReport/SalesmanReportView.xaml`
12. `Views/Reports/StatementReport/StatementReportView.xaml`
13. `Views/Reports/TaxInvoiceReport/TaxInvoiceReportView.xaml`
14. `Views/Reports/VatSubmission/VatSubmissionView.xaml`
15. `Views/Reports/OrderReport/OrderReportGeneratorView.xaml`
16. `Views/Reports/OrderReport/OrderReportListView.xaml`
17. `Views/Reports/OrderReport/OrderReportModuleView.xaml`
18. `Views/Reports/OrderReport/Filter/FilterStep.xaml`
19. `Views/Reports/OrderReport/Pipeline/ExecuteStep.xaml`
20. `Views/Reports/OrderReport/Preview/PreviewStep.xaml`
21. `Views/Reports/OrderReport/Review/ReviewStep.xaml`
22. `Views/Reports/OrderReport/Specifications/SpecificationsStep.xaml`
23. `Views/Reports/StatementGenerator/Shell/StatementGeneratorView.xaml`
24. `Views/Reports/StatementList/StatementListView.xaml`
25. `Views/Reports/StatementModule/StatementModuleView.xaml`
26. `Views/Reports/StatementGenerator/Filter/FilterStep.xaml`
27. `Views/Reports/StatementGenerator/Pipeline/ExecuteStep.xaml`
28. `Views/Reports/StatementGenerator/Preview/PreviewStep.xaml`
29. `Views/Reports/StatementGenerator/Review/ReviewStep.xaml`
30. `Views/Reports/StatementGenerator/Styles/WizardStyles.xaml`
31. `Views/Reports/Workflow/WorkflowProgressDialog.xaml`
32. `Views/Reports/Workflow/WorkflowWarningDialog.xaml`

---

### Phase 9: Job Profitability Analytics & Personal Glass Workspace (19 Views)
**Status**: `PENDING`  
**Focus**: Re-skinning Job Full Detail & Wastage analytics, plus the complete suite of 13 Personal Glass workspace views into Bento layout standards.

#### File Manifest:
1. `Views/JobProfitability/JobDetailPanel.xaml`
2. `Views/JobProfitability/JobFullDetailView.xaml`
3. `Views/JobProfitability/JobProfitabilityView.xaml`
4. `Views/JobProfitability/PortfolioGlassTypeComparisonView.xaml`
5. `Views/JobProfitability/PortfolioServiceProfitabilityView.xaml`
6. `Views/JobProfitability/PortfolioWastageAnalysisView.xaml`
7. `Views/Personal/GlassShell.xaml`
8. `Views/Personal/PersonalAiChatView.xaml`
9. `Views/Personal/PersonalDashboardOverviewView.xaml`
10. `Views/Personal/PersonalDashboardView.xaml`
11. `Views/Personal/PersonalDeliveryNoteView.xaml`
12. `Views/Personal/PersonalExecutiveReportView.xaml`
13. `Views/Personal/PersonalJobOrderView.xaml`
14. `Views/Personal/PersonalProductionControlCenterView.xaml`
15. `Views/Personal/PersonalProformaInvoiceView.xaml`
16. `Views/Personal/PersonalSalesReportView.xaml`
17. `Views/Personal/PersonalSettingsView.xaml`
18. `Views/Personal/PersonalSheetStoreView.xaml`
19. `Views/Personal/PersonalTaxInvoiceDashboardView.xaml`

---

### Phase 10: Authentication, Splash & Global Shared Dialogs (25 Views)
**Status**: `PENDING`  
**Focus**: Polishing login surfaces, startup splash, quick settings, screen recording panels, and global modal confirmation dialogs into refined Bento card containers.

#### File Manifest:
1. `Views/Login/AuthSurfaceStyles.xaml`
2. `Views/Login/FirstRunSetupPanel.xaml`
3. `Views/Login/ForcePasswordChangeWindow.xaml`
4. `Views/Login/LoginPanel.xaml`
5. `Views/Login/LoginWindow.xaml`
6. `Views/Login/ResetPasswordWindow.xaml`
7. `Views/Splash/SplashView.xaml`
8. `Views/Shared/ActivityPanel.xaml`
9. `Views/Shared/AuditReportDialog.xaml`
10. `Views/Shared/ChangesDialog.xaml`
11. `Views/Shared/ComingSoonView.xaml`
12. `Views/Shared/CountryPhoneControl.xaml`
13. `Views/Shared/LanguageSuggestionDialog.xaml`
14. `Views/Shared/QuickSettingsPanel.xaml`
15. `Views/Shared/RecordOverlayWindow.xaml`
16. `Views/Shared/ScreenRecordPanel.xaml`
17. `Views/Shared/ScreenshotPanel.xaml`
18. `Views/Shared/SectionActivityView.xaml`
19. `Views/Shared/StorageDetailsPanel.xaml`
20. `Views/Shared/StorageSidebarCard.xaml`
21. `Views/Shared/UnsavedChangesDialog.xaml`
22. `Views/Shared/UserSwitchPanel.xaml`
23. `Views/Shared/VoidReasonDialog.xaml`
24. `Views/Shared/Lists/StudioListPopupResources.xaml`
25. `Views/Shared/Lists/StudioListResources.xaml`

---

## 5. Senior Engineering Quality Gates

Every phase must pass four automated verification gates prior to commit:

1. **Gate 1 - Manifest & Syntax**:
   ```bash
   python3 scripts/bento_phase_tracker.py <PHASE_NUMBER>
   ```
   Ensures valid XAML parsing, zero missing files, and zero concentric card nesting.

2. **Gate 2 - Binding & Command Preservation**:
   ```bash
   python3 scripts/binding-lint.py
   python3 scripts/settings-rewrite/verify_bindings.py
   ```
   Ensures zero lost bindings, zero orphaned commands, and full ViewModel compatibility.

3. **Gate 3 - Design System Token Discipline**:
   ```bash
   python3 scripts/design-system-guard.py
   python3 scripts/verify-dashboard.py
   ```
   Ensures all colors and typefaces resolve through canonical `Ov.*` resource tokens.

4. **Gate 4 - Layout & Rule Integrity**:
   - Tabular numerals are enclosed in `Viewbox` with `Ov.Value`.
   - Small controls use $\ge 3$ columns (no 2-column small forms).
   - Card content occupies $\ge 40\%$ of card area.
