#!/usr/bin/env python3
"""
Bento Phase Tracker & Quality Gate Guard
Tracks, audits, and validates the full Indigo Studio Bento redesign across all 236 views.
"""

import os
import re
import sys
import glob
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))

ALREADY_BENTO_CORE = [
    'Views/Dashboard/IndigoDashboardResources.xaml',
    'Views/Dashboard/Overview/IndigoStudioResources.xaml',
    'Views/Dashboard/DashboardView.xaml',
    'Views/Dashboard/Overview/OverviewDashboardView.xaml',
    'Views/Dashboard/ProformaInvoice/PiDashboardView.xaml',
    'Views/Dashboard/JobOrder/JoDashboard/JoDashboardView.xaml',
    'Views/Dashboard/DeliveryNote/DnDashboard/DnDashboardView.xaml',
    'Views/Dashboard/TaxInvoice/TiDashboard/TiDashboardView.xaml',
    'Views/Dashboard/SheetStore/SheetStoreDashboardView.xaml',
    'Views/Dashboard/Statement/StDashboardView.xaml',
    'Views/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml',
    'Views/DeliveryNote/GodView/DeliveryNoteGodView.xaml',
    'Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml',
    'Views/JobOrder/GodView/JobOrderGodView.xaml',
    'Views/TaxInvoice/TaxInvoiceList/TaxInvoiceListView.xaml',
    'Views/TaxInvoice/GodView/TaxInvoiceGodView.xaml',
    'Views/Inquiry/InquiryListView.xaml',
    'Views/ProformaInvoice/SavedInvoicesList/SavedInvoicesListView.xaml',
]

PHASES = {
    1: {
        'name': 'Settings Foundation & Personalization Suite',
        'status': 'COMPLETED',
        'files': [
            'Views/Settings/SettingsView.xaml',
            'Views/Settings/SettingsCardStyles.xaml',
            'Views/Settings/SettingsPageTemplates.xaml',
            'Views/Settings/SettingsSectionStyles.xaml',
            'Views/Settings/Sections/SettingsAppearanceSection.xaml',
            'Views/Settings/Sections/SettingsThemesSection.xaml',
            'Views/Settings/Sections/SettingsVoiceSection.xaml',
            'Views/Settings/Sections/SettingsLicenseSection.xaml',
            'Views/Settings/Sections/SettingsAboutSection.xaml',
            'Views/Settings/Sections/SettingsSharingSection.xaml',
            'Views/Settings/Sections/Cards/AppearanceAccentFontCard.xaml',
            'Views/Settings/Sections/Cards/AppearanceCardEdgesTile.xaml',
            'Views/Settings/Sections/Cards/AppearanceCrystalTile.xaml',
            'Views/Settings/Sections/Cards/AppearanceDensityCard.xaml',
            'Views/Settings/Sections/Cards/AppearanceFontColorTile.xaml',
            'Views/Settings/Sections/Cards/AppearanceFrostTile.xaml',
            'Views/Settings/Sections/Cards/AppearanceGradientTile.xaml',
            'Views/Settings/Sections/Cards/AppearanceLanguageCard.xaml',
            'Views/Settings/Sections/Cards/AppearanceModeCard.xaml',
            'Views/Settings/Sections/Cards/AppearancePreviewTile.xaml',
            'Views/Settings/Sections/Cards/AppearanceRadiusTile.xaml',
            'Views/Settings/Sections/Cards/AppearanceSplashCard.xaml',
            'Views/Settings/Sections/Cards/AppearanceThemeFamilyTile.xaml',
            'Views/Settings/Sections/Cards/AppearanceThemeLayoutCard.xaml',
            'Views/Settings/Sections/Cards/AppearanceWallpapersCard.xaml',
            'Views/Settings/Sections/Cards/ThemesCrystalMaterialCard.xaml',
            'Views/Settings/Sections/Cards/VoiceAudioCard.xaml',
        ]
    },
    2: {
        'name': 'Settings Data, Storage, Features, AI & Demo Suite',
        'status': 'COMPLETED',
        'files': [
            'Views/Settings/Sections/SettingsDataSection.xaml',
            'Views/Settings/Sections/SettingsFeaturesSection.xaml',
            'Views/Settings/Sections/SettingsAiSection.xaml',
            'Views/Settings/Sections/SettingsAgentSection.xaml',
            'Views/Settings/Sections/SettingsDemoSection.xaml',
            'Views/Settings/Sections/SettingsUpdatesSection.xaml',
            'Views/Settings/Sections/Cards/DataBackupCard.xaml',
            'Views/Settings/Sections/Cards/DataHealthCard.xaml',
            'Views/Settings/Sections/Cards/DataRootCard.xaml',
            'Views/Settings/Sections/Cards/DataSampleCard.xaml',
            'Views/Settings/Sections/Cards/FeaturesDeliveryNoteCard.xaml',
            'Views/Settings/Sections/Cards/FeaturesInquiryCard.xaml',
            'Views/Settings/Sections/Cards/FeaturesJobOrderCard.xaml',
            'Views/Settings/Sections/Cards/FeaturesProformaCard.xaml',
            'Views/Settings/Sections/Cards/FeaturesSessionCard.xaml',
            'Views/Settings/Sections/Cards/FeaturesTaxInvoiceCard.xaml',
            'Views/Settings/Sections/Cards/AiAssistantCard.xaml',
            'Views/Settings/Sections/Cards/AiProvidersCard.xaml',
            'Views/Settings/Sections/Cards/AiSecurityCard.xaml',
            'Views/Settings/Sections/Cards/AgentCoreCard.xaml',
            'Views/Settings/Sections/Cards/AgentDataCard.xaml',
            'Views/Settings/Sections/Cards/DemoFfmpegCard.xaml',
            'Views/Settings/Sections/Cards/DemoHistoryCard.xaml',
            'Views/Settings/Sections/Cards/DemoIsolationCard.xaml',
            'Views/Settings/Sections/Cards/DemoOutputCard.xaml',
            'Views/Settings/Sections/Cards/DemoQualityCard.xaml',
            'Views/Settings/Sections/Cards/DemoRecordCard.xaml',
            'Views/Settings/Sections/Cards/DemoWorkflowCard.xaml',
        ]
    },
    3: {
        'name': 'System Centers, Diagnostics, About & System Hubs',
        'status': 'COMPLETED',
        'files': [
            'Views/CompanyCenter/CompanyCenterView.xaml',
            'Views/UserCenter/UserCenterView.xaml',
            'Views/SystemCenter/SystemCenterView.xaml',
            'Views/Diagnostics/DiagnosticsView.xaml',
            'Views/CrashReports/CrashReportDialog.xaml',
            'Views/CrashReports/CrashReportDialogStyles.xaml',
            'Views/AI/AiChatView.xaml',
            'Views/Specifications/SpecificationsCenterView.xaml',
            'Views/Sharing/ShareComposerWindow.xaml',
            'Views/About/AboutIos26Theme.xaml',
            'Views/About/AboutStyles.xaml',
            'Views/About/AboutView.xaml',
            'Views/About/Sections/AboutCrashReportsCard.xaml',
            'Views/About/Sections/AboutLicenseCard.xaml',
            'Views/About/Sections/AboutOverviewSection.xaml',
            'Views/About/Sections/DeveloperSection.xaml',
            'Views/About/Sections/DiagnosticsSection.xaml',
            'Views/About/Sections/ModulesSection.xaml',
            'Views/About/Sections/SystemSection.xaml',
        ]
    },
    4: {
        'name': 'Core ERP Document Details & Transaction Views (DN, JO, TI, Inquiry)',
        'status': 'COMPLETED',
        'files': [
            'Views/DeliveryNote/DeliveryNoteView.xaml',
            'Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml',
            'Views/DeliveryNote/DeliveryNoteDetail/Sections/DnOverviewSection.xaml',
            'Views/DeliveryNote/DeliveryNoteDetail/Sections/DnHistorySection.xaml',
            'Views/DeliveryNote/DeliveryNoteDetail/Sections/DnAttachmentsSection.xaml',
            'Views/DeliveryNote/DeliveryNoteDetail/Sections/DnSectionStyles.xaml',
            'Views/DeliveryNote/DeliveryNoteList/JobOrderPicker/JobOrderPickerDialog.xaml',
            'Views/DeliveryNote/DeliveryNoteDetail/Attachments/Rename/RenameAttachmentDialog.xaml',
            'Views/JobOrder/JobOrderView.xaml',
            'Views/JobOrder/JobOrderDetail/JobOrderDetailView.xaml',
            'Views/JobOrder/JobOrderDetail/Sections/JoOverviewSection.xaml',
            'Views/JobOrder/JobOrderDetail/Sections/JoProductionSection.xaml',
            'Views/JobOrder/JobOrderDetail/Sections/JoSectionStyles.xaml',
            'Views/JobOrder/SavedJobOrdersList/JobOrderAttachmentWarningDialog.xaml',
            'Views/JobOrder/SavedJobOrdersList/ProformaPicker/JobOrderProformaPickerDialog.xaml',
            'Views/TaxInvoice/TaxInvoiceView.xaml',
            'Views/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailView.xaml',
            'Views/TaxInvoice/TaxInvoiceDetail/Sections/TiOverviewSection.xaml',
            'Views/TaxInvoice/TaxInvoiceDetail/Sections/TiFamilySection.xaml',
            'Views/TaxInvoice/TaxInvoiceDetail/Sections/TiAttachmentsSection.xaml',
            'Views/TaxInvoice/TaxInvoiceDetail/Sections/TiSectionStyles.xaml',
            'Views/TaxInvoice/TaxInvoiceDetail/IssueDraftsDialog.xaml',
            'Views/TaxInvoice/TaxInvoiceList/ProformaPickerDialog.xaml',
            'Views/Inquiry/InquiryEditorWindow.xaml',
            'Views/Inquiry/InquiryAttachmentPreviewWindow.xaml',
        ]
    },
    5: {
        'name': 'Proforma Invoice 13-Step Wizard & Pricing Suite',
        'status': 'COMPLETED',
        'files': [
            'Views/ProformaInvoice/ProformaInvoiceView.xaml',
            'Views/ProformaInvoice/Step1CompanySetup/CompanySetupView.xaml',
            'Views/ProformaInvoice/Step2ClientProfile/ClientProfileView.xaml',
            'Views/ProformaInvoice/Step3ProjectSpecs/ProjectSpecsView.xaml',
            'Views/ProformaInvoice/Step4PricingEstimator/PricingEstimatorView.xaml',
            'Views/ProformaInvoice/Step4PricingEstimator/DguLamParameters/DguLamParametersView.xaml',
            'Views/ProformaInvoice/Step4PricingEstimator/DguParameters/DguParametersView.xaml',
            'Views/ProformaInvoice/Step4PricingEstimator/LamParameters/LamParametersView.xaml',
            'Views/ProformaInvoice/Step4PricingEstimator/SguParameters/SguParametersView.xaml',
            'Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml',
            'Views/ProformaInvoice/Step5DataGrid/GrandTotalSummary/GrandTotalSummaryView.xaml',
            'Views/ProformaInvoice/Step6OtherCharges/OtherChargesView.xaml',
            'Views/ProformaInvoice/Step6OtherCharges/CustomCharges/CustomChargesSectionView.xaml',
            'Views/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionView.xaml',
            'Views/ProformaInvoice/Step7PrintPreview/PrintPreviewView.xaml',
            'Views/ProformaInvoice/Step7PrintPreview/InvoiceMainSummary/InvoiceMainSummaryView.xaml',
            'Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceView.xaml',
            'Views/ProformaInvoice/Step7PrintPreview/TermsAndConditions/TermsAndConditionsView.xaml',
            'Views/ProformaInvoice/Step8Attachments/AttachmentsStepView.xaml',
            'Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml',
            'Views/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonView.xaml',
            'Views/ProformaInvoice/Step9ProfitLoss/ServiceProfitabilityView.xaml',
            'Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml',
            'Views/ProformaInvoice/SavedInvoicesList/InvoiceApprovalDialog.xaml',
            'Views/ProformaInvoice/SavedInvoicesList/VersionsDialog.xaml',
            'Views/ProformaInvoice/SavedInvoicesList/SavedInvoicesListDialog.xaml',
        ]
    },
    6: {
        'name': 'Factory Floor Production Work Centers & Controls',
        'status': 'COMPLETED',
        'files': [
            'Views/Production/CuttingView.xaml',
            'Views/Production/CuttingWorkCenterView.xaml',
            'Views/Production/DoublingLaminationView.xaml',
            'Views/Production/EdgeDeletionView.xaml',
            'Views/Production/HolesCutoutsView.xaml',
            'Views/Production/MitringView.xaml',
            'Views/Production/PairingView.xaml',
            'Views/Production/PolishView.xaml',
            'Views/Production/ProcessWorkCenterView.xaml',
            'Views/Production/ProductionControlCenterView.xaml',
            'Views/Production/TemperingView.xaml',
            'Views/Production/Controls/ProductionFilterBar.xaml',
            'Views/Production/Controls/ProductionKpiStrip.xaml',
            'Views/Production/Controls/ProductionPipelineBar.xaml',
            'Views/Production/Controls/ProductionReleaseBar.xaml',
            'Views/Production/Controls/ProductionStationBody.xaml',
            'Views/Production/Controls/ProductionUnitDetailControl.xaml',
            'Views/Production/Controls/ProductionUnitDetailTemplates.xaml',
        ]
    },
    7: {
        'name': 'Glass Optimization Engine & SheetStore Warehouse',
        'status': 'PENDING',
        'files': [
            'Views/Optimization/OptimizationView.xaml',
            'Views/Optimization/Tabs/CutListTabView.xaml',
            'Views/Optimization/Tabs/JobsTabView.xaml',
            'Views/Optimization/Tabs/ReportTabView.xaml',
            'Views/Optimization/Tabs/SettingsTabView.xaml',
            'Views/Optimization/Tabs/SizesTabView.xaml',
            'Views/Optimization/Tabs/StocksTabView.xaml',
            'Views/Optimization/Tabs/SummaryTabView.xaml',
            'Views/Optimization/Tabs/TwoDViewTabView.xaml',
            'Views/SheetStore/SheetStoreView.xaml',
            'Views/SheetStore/AddEditSheetView.xaml',
            'Views/SheetStore/SheetDetailWindow.xaml',
            'Views/SheetStore/SheetStoreBuy/BuyDialogView.xaml',
            'Views/SheetStore/SheetStoreCsv/DuplicateResolutionView.xaml',
            'Views/SheetStore/SheetStoreHistory/HistoryView.xaml',
            'Views/SheetStore/SheetStoreItems/ItemMovementView.xaml',
            'Views/SheetStore/SheetStoreUse/UseDialogView.xaml',
        ]
    },
    8: {
        'name': 'Reports Hub, Executive Analytics & Statement Generators',
        'status': 'PENDING',
        'files': [
            'Views/Reports/SalesReportView.xaml',
            'Views/Reports/CompanyReport/CompanyReportView.xaml',
            'Views/Reports/CustomerReport/CustomerReportView.xaml',
            'Views/Reports/DeliveryReport/DeliveryReportView.xaml',
            'Views/Reports/ExecutiveReport/ExecutiveReportView.xaml',
            'Views/Reports/GlassSpecReport/GlassSpecReportView.xaml',
            'Views/Reports/JoReport/JoReportView.xaml',
            'Views/Reports/PiReport/PiReportView.xaml',
            'Views/Reports/ProfitabilityReport/ProfitabilityReportView.xaml',
            'Views/Reports/ProjectReport/ProjectReportView.xaml',
            'Views/Reports/SalesmanReport/SalesmanReportView.xaml',
            'Views/Reports/StatementReport/StatementReportView.xaml',
            'Views/Reports/TaxInvoiceReport/TaxInvoiceReportView.xaml',
            'Views/Reports/VatSubmission/VatSubmissionView.xaml',
            'Views/Reports/OrderReport/OrderReportGeneratorView.xaml',
            'Views/Reports/OrderReport/OrderReportListView.xaml',
            'Views/Reports/OrderReport/OrderReportModuleView.xaml',
            'Views/Reports/OrderReport/Filter/FilterStep.xaml',
            'Views/Reports/OrderReport/Pipeline/ExecuteStep.xaml',
            'Views/Reports/OrderReport/Preview/PreviewStep.xaml',
            'Views/Reports/OrderReport/Review/ReviewStep.xaml',
            'Views/Reports/OrderReport/Specifications/SpecificationsStep.xaml',
            'Views/Reports/StatementGenerator/Shell/StatementGeneratorView.xaml',
            'Views/Reports/StatementList/StatementListView.xaml',
            'Views/Reports/StatementModule/StatementModuleView.xaml',
            'Views/Reports/StatementGenerator/Filter/FilterStep.xaml',
            'Views/Reports/StatementGenerator/Pipeline/ExecuteStep.xaml',
            'Views/Reports/StatementGenerator/Preview/PreviewStep.xaml',
            'Views/Reports/StatementGenerator/Review/ReviewStep.xaml',
            'Views/Reports/StatementGenerator/Styles/WizardStyles.xaml',
            'Views/Reports/Workflow/WorkflowProgressDialog.xaml',
            'Views/Reports/Workflow/WorkflowWarningDialog.xaml',
        ]
    },
    9: {
        'name': 'Job Profitability Analytics & Personal Glass Workspace',
        'status': 'PENDING',
        'files': [
            'Views/JobProfitability/JobDetailPanel.xaml',
            'Views/JobProfitability/JobFullDetailView.xaml',
            'Views/JobProfitability/JobProfitabilityView.xaml',
            'Views/JobProfitability/PortfolioGlassTypeComparisonView.xaml',
            'Views/JobProfitability/PortfolioServiceProfitabilityView.xaml',
            'Views/JobProfitability/PortfolioWastageAnalysisView.xaml',
            'Views/Personal/GlassShell.xaml',
            'Views/Personal/PersonalAiChatView.xaml',
            'Views/Personal/PersonalDashboardOverviewView.xaml',
            'Views/Personal/PersonalDashboardView.xaml',
            'Views/Personal/PersonalDeliveryNoteView.xaml',
            'Views/Personal/PersonalExecutiveReportView.xaml',
            'Views/Personal/PersonalJobOrderView.xaml',
            'Views/Personal/PersonalProductionControlCenterView.xaml',
            'Views/Personal/PersonalProformaInvoiceView.xaml',
            'Views/Personal/PersonalSalesReportView.xaml',
            'Views/Personal/PersonalSettingsView.xaml',
            'Views/Personal/PersonalSheetStoreView.xaml',
            'Views/Personal/PersonalTaxInvoiceDashboardView.xaml',
        ]
    },
    10: {
        'name': 'Authentication, Splash & Global Shared Dialogs',
        'status': 'PENDING',
        'files': [
            'Views/Login/AuthSurfaceStyles.xaml',
            'Views/Login/FirstRunSetupPanel.xaml',
            'Views/Login/ForcePasswordChangeWindow.xaml',
            'Views/Login/LoginPanel.xaml',
            'Views/Login/LoginWindow.xaml',
            'Views/Login/ResetPasswordWindow.xaml',
            'Views/Splash/SplashView.xaml',
            'Views/Shared/ActivityPanel.xaml',
            'Views/Shared/AuditReportDialog.xaml',
            'Views/Shared/ChangesDialog.xaml',
            'Views/Shared/ComingSoonView.xaml',
            'Views/Shared/CountryPhoneControl.xaml',
            'Views/Shared/LanguageSuggestionDialog.xaml',
            'Views/Shared/QuickSettingsPanel.xaml',
            'Views/Shared/RecordOverlayWindow.xaml',
            'Views/Shared/ScreenRecordPanel.xaml',
            'Views/Shared/ScreenshotPanel.xaml',
            'Views/Shared/SectionActivityView.xaml',
            'Views/Shared/StorageDetailsPanel.xaml',
            'Views/Shared/StorageSidebarCard.xaml',
            'Views/Shared/UnsavedChangesDialog.xaml',
            'Views/Shared/UserSwitchPanel.xaml',
            'Views/Shared/VoidReasonDialog.xaml',
            'Views/Shared/Lists/StudioListPopupResources.xaml',
            'Views/Shared/Lists/StudioListResources.xaml',
        ]
    }
}

def verify_manifest():
    all_disk_views = set(glob.glob(f"{ROOT}/Views/**/*.xaml", recursive=True))
    all_disk_rel = {os.path.relpath(p, ROOT) for p in all_disk_views}

    registered_files = []
    for p_id, p_info in PHASES.items():
        registered_files.extend(p_info['files'])

    already_bento_set = set(ALREADY_BENTO_CORE)
    registered_set = set(registered_files)

    print("=" * 80)
    print(" BENTO 236-VIEW REDESIGN AUDIT & MANIFEST VERIFICATION")
    print("=" * 80)
    print(f"Total XAML Views on disk:               {len(all_disk_rel)}")
    print(f"Already Bento Benchmark (Dashboard/etc): {len(already_bento_set)}")
    print(f"Applicable Views in 10-Phase Plan:      {len(registered_set)}")

    # Verify no overlap between core and registered
    overlap_core = already_bento_set.intersection(registered_set)
    if overlap_core:
        print(f"ERROR: Found {len(overlap_core)} files in both core and phases!")
        for f in overlap_core:
            print(f"  - {f}")
        return False

    # Verify all registered exist on disk
    missing_on_disk = registered_set - all_disk_rel
    if missing_on_disk:
        print(f"ERROR: {len(missing_on_disk)} registered files do NOT exist on disk:")
        for f in sorted(missing_on_disk):
            print(f"  - {f}")
        return False

    # Verify every view on disk is accounted for
    unaccounted = all_disk_rel - already_bento_set - registered_set
    if unaccounted:
        print(f"ERROR: {len(unaccounted)} views on disk are NOT accounted for in phases:")
        for f in sorted(unaccounted):
            print(f"  - {f}")
        return False

    # Check for duplicates across phases
    if len(registered_files) != len(registered_set):
        print(f"ERROR: Duplicate views detected across phases! Total count {len(registered_files)} vs unique {len(registered_set)}")
        seen = set()
        for f in registered_files:
            if f in seen:
                print(f"  Duplicate: {f}")
            seen.add(f)
        return False

    print("SUCCESS: 100% of all 236 applicable views are strictly accounted for (0 missing, 0 duplicates).\n")
    return True

def audit_xaml_integrity(phase_id=None):
    print("=" * 80)
    if phase_id:
        print(f" AUDITING PHASE {phase_id}: {PHASES[phase_id]['name']}")
        targets = PHASES[phase_id]['files']
    else:
        print(" AUDITING ALL 236 VIEWS ACROSS ALL 10 PHASES")
        targets = []
        for p in PHASES.values():
            targets.extend(p['files'])
    print("=" * 80)

    xml_errors = []
    concentric_card_issues = []

    # regex patterns
    card_pattern = re.compile(r'Style="\{StaticResource (SettingsCard|SettingsCompactCard|Ov\.Card|Ov\.CardCompact|Ov\.CardMicro)\}"')
    
    for rel_path in targets:
        full_path = os.path.join(ROOT, rel_path)
        try:
            with open(full_path, 'r', encoding='utf-8') as f:
                content = f.read()
            ET.fromstring(content)
        except Exception as e:
            xml_errors.append((rel_path, str(e)))
            continue

        # Check for concentric card nesting: card immediately inside card without content
        lines = content.splitlines()
        depth = 0
        card_stack = []
        for idx, line in enumerate(lines, 1):
            if '<Border' in line and card_pattern.search(line):
                card_stack.append(idx)
                if len(card_stack) > 1:
                    concentric_card_issues.append((rel_path, f"Concentric Card at line {idx} inside card at line {card_stack[0]}"))
            if '</Border>' in line and card_stack:
                card_stack.pop()

    print(f"Scanned {len(targets)} files.")
    if xml_errors:
        print(f"FAIL: {len(xml_errors)} XAML syntax errors found:")
        for p, err in xml_errors:
            print(f"  - {p}: {err}")
    else:
        print("PASS: All target views are valid, well-formed XAML.")

    if concentric_card_issues:
        print(f"WARNING: {len(concentric_card_issues)} concentric card nesting instances:")
        for p, issue in concentric_card_issues:
            print(f"  - {p}: {issue}")
    else:
        print("PASS: Zero concentric card nesting found in checked views.")

    return len(xml_errors) == 0

def print_summary_table():
    print("=" * 80)
    print(" PHASE EXECUTION DASHBOARD (10 PHASES / 236 APPLICABLE VIEWS)")
    print("=" * 80)
    print(f"{'Phase':<8}{'Status':<16}{'Views':<8}{'Phase Title'}")
    print("-" * 80)
    total_views = 0
    for p_id, p_info in sorted(PHASES.items()):
        total_views += len(p_info['files'])
        status_display = f"[{p_info['status']}]"
        print(f"Phase {p_id:<3}{status_display:<16}{len(p_info['files']):<8}{p_info['name']}")
    print("-" * 80)
    print(f"Total Views across 10 Phases: {total_views} views\n")

if __name__ == '__main__':
    if not verify_manifest():
        sys.exit(1)
    
    phase_arg = None
    if len(sys.argv) > 1:
        try:
            phase_arg = int(sys.argv[1])
        except:
            pass

    print_summary_table()
    ok = audit_xaml_integrity(phase_arg)
    if not ok:
        sys.exit(1)
    print("Phase Tracker verification completed successfully.")
