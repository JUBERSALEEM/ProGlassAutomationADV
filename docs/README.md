# Documentation Index

Everything written about ProGlass Automation, in one place.

**This file is generated** by `scripts/gen-docs-index.py` from the `docs/`
tree. Do not hand-edit the tables — add or rename a document and re-run the
script, otherwise the index drifts away from the folder again (it previously
listed 9 of 138 documents).

```
python3 scripts/gen-docs-index.py          # rewrite this file
python3 scripts/gen-docs-index.py --check  # fail if it is out of date
```

## Reading order

| If you are… | Read |
|---|---|
| installing the app | `INSTALL.md` → `FIRST_RUN.md` |
| new to the codebase | [`ARCHITECTURE.md`](ARCHITECTURE.md) → [`WORKFLOW.md`](WORKFLOW.md) → [`DESIGN_SYSTEM.md`](DESIGN_SYSTEM.md) |
| touching a money path | [`MONEY_PATH_ERROR_POLICY.md`](MONEY_PATH_ERROR_POLICY.md) first, always |
| working on one module | the *Business workflow* section below |
| cutting a release | [`RELEASE_READINESS_V2_STATUS.md`](RELEASE_READINESS_V2_STATUS.md) → [`CI.md`](CI.md) → [`E2E_WALKTHROUGH.md`](E2E_WALKTHROUGH.md) |

> [`WORKFLOW.md`](WORKFLOW.md) is the end-to-end business chain — Inquiry →
> Proforma → Job Order → Delivery → Tax Invoice → Statement — with the
> actual code path for each handoff. Start there if you want to understand
> what this application *does* rather than how a folder is arranged.


## Start here — customer & operator

| Document | What it covers |
|---|---|
| [`BACKUP.md`](BACKUP.md) | ProGlass Automation — Backup and Restore |
| [`FIRST_RUN.md`](FIRST_RUN.md) | ProGlass Automation — First Run |
| [`INSTALL.md`](INSTALL.md) | ProGlass Automation — Installation |
| [`LICENSE.md`](LICENSE.md) | ProGlass Automation — Licensing |
| [`OTA.md`](OTA.md) | 🔄 ProGlass Automation — OTA Update System |
| [`RUNBOOK.md`](RUNBOOK.md) | 🧯 ProGlass Automation — Runbook & Incident Response |
| [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) | ProGlass Automation — Troubleshooting |
| [`UPDATE.md`](UPDATE.md) | ProGlass Automation — Updates |

## Architecture & the money contract

| Document | What it covers |
|---|---|
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | ProGlass Automation — Architecture (v2.0) |
| [`DATABASE_MIGRATION_FIX.md`](DATABASE_MIGRATION_FIX.md) | Database Migration Fix: WeightPerPiece Column |
| [`DESIGN_SYSTEM.md`](DESIGN_SYSTEM.md) | ProGlass Automation — UI Design System (v2) |
| [`ERP_UPGRADE_ROADMAP.md`](ERP_UPGRADE_ROADMAP.md) | PROGLASS AUTOMATION — ROADMAP TO 10/10 INDUSTRIAL ERP WORKFLOW |
| [`LAN_SHARED_DATABASE_CONNECTIVITY_UPGRADE_2026-09-07.md`](LAN_SHARED_DATABASE_CONNECTIVITY_UPGRADE_2026-09-07.md) | LAN / Wi-Fi Shared Database Connectivity Upgrade — 2026-09-07 |
| [`MONEY_PATH_ERROR_POLICY.md`](MONEY_PATH_ERROR_POLICY.md) | MONEY-PATH ERROR & SILENT-CATCH POLICY |
| [`PRODUCTION_HARDENING_REPORT_2026-08.md`](PRODUCTION_HARDENING_REPORT_2026-08.md) | PROGLASS AUTOMATION — PRODUCTION HARDENING REPORT |
| [`PROFESSIONAL_AUDIT_TRAIL_UPGRADE_2026-09-07.md`](PROFESSIONAL_AUDIT_TRAIL_UPGRADE_2026-09-07.md) | Professional Audit Trail Upgrade — 2026-09-07 |
| [`ROADMAP.md`](ROADMAP.md) | 🗺️ ProGlass Automation — Phased Roadmap to a Professional WPF Platform |
| [`SQLITE_HARDENING_2026-08.md`](SQLITE_HARDENING_2026-08.md) | Industrial SQLite Hardening · August 2026 |
| [`SQLITE_HARDENING_CROSS_CHECK_2026-08.md`](SQLITE_HARDENING_CROSS_CHECK_2026-08.md) | SQLite Hardening — Full Cross-Check · August 2026 |

## Business workflow — module by module

| Document | What it covers |
|---|---|
| [`DELIVERY_NOTE_DEEP_AUDIT_2026-08.md`](DELIVERY_NOTE_DEEP_AUDIT_2026-08.md) | Delivery Note Module — Deep Senior Audit + PI→JO→DN Workflow (Aug 2026) |
| [`DELIVERY_NOTE_USER_GUIDE.md`](DELIVERY_NOTE_USER_GUIDE.md) | Delivery Note Workflow - User Guide |
| [`DELIVERY_NOTE_VISUAL_GUIDE.md`](DELIVERY_NOTE_VISUAL_GUIDE.md) | Delivery Note Visual Guide - Understanding What You See |
| [`DELIVERY_NOTE_WORKFLOW_ANALYSIS.md`](DELIVERY_NOTE_WORKFLOW_ANALYSIS.md) | Delivery Note Workflow Industrial Analysis & Redesign |
| [`INQUIRY_PROFORMA_INDUSTRIAL_HARDENING_2026-09-07.md`](INQUIRY_PROFORMA_INDUSTRIAL_HARDENING_2026-09-07.md) | Inquiry + Proforma Invoice Industrial Hardening Audit — 2026-09-07 |
| [`INQUIRY_REBUILD_DISCOVERY.md`](INQUIRY_REBUILD_DISCOVERY.md) | Inquiry Module Rebuild — Phase 0/1 Discovery |
| [`INQUIRY_SENIOR_ANALYSIS.md`](INQUIRY_SENIOR_ANALYSIS.md) | Senior Developer Analysis — Inquiry Module Rebuild (2026 SaaS Redesign) |
| [`JOB_ORDER_COMPACT_REDESIGN_2026-08.md`](JOB_ORDER_COMPACT_REDESIGN_2026-08.md) | Job Order — Compact Unified Detail Redesign (Aug 2026) |
| [`JOB_ORDER_DEEP_AUDIT_2026-08.md`](JOB_ORDER_DEEP_AUDIT_2026-08.md) | Job Order Module — Deep Senior Audit (Aug 2026) |
| [`JOB_ORDER_TABS_AND_SETTINGS_BUTTONS_2026-08.md`](JOB_ORDER_TABS_AND_SETTINGS_BUTTONS_2026-08.md) | Job Order Tab Switch Restore + Settings Button Full-Width (Aug 2026) |
| [`JOB_PROFITABILITY_FULL_DETAIL_2026-09-06.md`](JOB_PROFITABILITY_FULL_DETAIL_2026-09-06.md) | Job Profitability — Single-Click Sidebar + Double-Click Full Detail |
| [`JOB_PROFITABILITY_IMPLEMENTATION.md`](JOB_PROFITABILITY_IMPLEMENTATION.md) | Job Profitability Portfolio Analysis - Implementation Summary |
| [`PROFORMA_INQUIRY_INTEGRATION_2026-08.md`](PROFORMA_INQUIRY_INTEGRATION_2026-08.md) | Inquiry ↔ Proforma Invoice integration — senior audit (August 2026) |
| [`PROFORMA_INVOICE_ARCHITECTURE_REVIEW.md`](PROFORMA_INVOICE_ARCHITECTURE_REVIEW.md) | Proforma Invoice System — Senior Architectural Review |
| [`PROFORMA_INVOICE_AUDIT_2026-08.md`](PROFORMA_INVOICE_AUDIT_2026-08.md) | Proforma Invoice module — senior-dev audit (August 2026) |
| [`PROFORMA_INVOICE_RATING_2026-08.md`](PROFORMA_INVOICE_RATING_2026-08.md) | Proforma Invoice module — 10 / 10 delivery |
| [`REPORTS_DATAGRID_MOCKUP.md`](REPORTS_DATAGRID_MOCKUP.md) | Central Reporting Module — DataGrid & UI/UX Industrial Specification & Mockup |
| [`REPORTS_FILTER_UPGRADE_2026-09-10.md`](REPORTS_FILTER_UPGRADE_2026-09-10.md) | Reports Hub — Inventory-Grade Filter Panels (2026-09-10) |
| [`REPORTS_HUB_AUDIT_2026-09-10.md`](REPORTS_HUB_AUDIT_2026-09-10.md) | Reports Hub — Full Module Audit (2026-09-10) |
| [`SENIOR_ANALYSIS_ADVANCED_REPORTING.md`](SENIOR_ANALYSIS_ADVANCED_REPORTING.md) | Senior Analysis — Advanced Reporting |
| [`SHEET_DETAIL_FINAL_REPORT_2026-09-06.md`](SHEET_DETAIL_FINAL_REPORT_2026-09-06.md) | Sheet Detail — Step 9 Corrective Rework: Final Report (2026-09-06) |
| [`SHEET_DETAIL_STEP6_INTEGRATION_2026-09-06.md`](SHEET_DETAIL_STEP6_INTEGRATION_2026-09-06.md) | Sheet Detail — Step 6 Wastage Integration (2026-09-06) |
| [`STEP9_CATEGORIZED_REPORT_UPGRADE_2026-09-08.md`](STEP9_CATEGORIZED_REPORT_UPGRADE_2026-09-08.md) | Step 9 Categorized ERP Report Upgrade — 2026-09-08 |
| [`STEP9_DETAILED_REPORT_EXPORT_AUDIT_2026-09-07.md`](STEP9_DETAILED_REPORT_EXPORT_AUDIT_2026-09-07.md) | Step 9 Detailed Export / Report Generator Audit — 2026-09-07 |
| [`STEP9_PRICE_DECISION_REPORT_COMPLETENESS_2026-09-08.md`](STEP9_PRICE_DECISION_REPORT_COMPLETENESS_2026-09-08.md) | Step 9 Price-Decision Report Completeness — 2026-09-08 |
| [`STEP9_PROFIT_LOSS_MAINTENANCE_AUDIT_2026-09-07.md`](STEP9_PROFIT_LOSS_MAINTENANCE_AUDIT_2026-09-07.md) | Step 9 Profit & Loss Maintenance Audit — 2026-09-07 |
| [`STEP9_SIMPLIFIED_PROFESSIONAL_RESTRUCTURE_2026-09-07.md`](STEP9_SIMPLIFIED_PROFESSIONAL_RESTRUCTURE_2026-09-07.md) | Step 9 Professional Simplification / Restructure — 2026-09-07 |
| [`STEP9_SPACER_EXPORT_UPGRADE_2026-09-08.md`](STEP9_SPACER_EXPORT_UPGRADE_2026-09-08.md) | Step 9 Spacer / Production Service Export Upgrade — 2026-09-08 |
| [`TAX_INVOICE_AUDIT_2026-08-26.md`](TAX_INVOICE_AUDIT_2026-08-26.md) | Tax Invoice Module — Senior Deep Audit |

## Dashboards & data presentation

| Document | What it covers |
|---|---|
| [`DASHBOARD_2050_KIT.md`](DASHBOARD_2050_KIT.md) | Dashboard 2050 kit — shared across 7 dashboards |
| [`DASHBOARD_INDUSTRIAL_UI_AUDIT_2026-09-08.md`](DASHBOARD_INDUSTRIAL_UI_AUDIT_2026-09-08.md) | Dashboard Industrial UI Audit & Upgrade — 2026-09-08 |
| [`DASHBOARD_LAYOUT_REVISION_2026-08-25.md`](DASHBOARD_LAYOUT_REVISION_2026-08-25.md) | Dashboard layout revision — senior analysis (2026-08-25) |
| [`DASHBOARD_MODERNIZATION_2026-08.md`](DASHBOARD_MODERNIZATION_2026-08.md) | Dashboard Modernization — blue-gradient "modern app" polish (Aug 2026) |
| [`DATAGRID_INDUSTRIAL_SPEC.md`](DATAGRID_INDUSTRIAL_SPEC.md) | Line-item DataGrid — analysis + proposed shared chrome |
| [`DashboardAuditAndPlan.md`](DashboardAuditAndPlan.md) | Dashboard Audit & Implementation Plan |
| [`LIST_PAGE_INDUSTRIAL_SPEC.md`](LIST_PAGE_INDUSTRIAL_SPEC.md) | Document-list chrome — industrial target (2026-08-18) |
| [`OVERVIEW_BOTTOM_SECTION_AND_STATEMENT_STYLE_2026-08.md`](OVERVIEW_BOTTOM_SECTION_AND_STATEMENT_STYLE_2026-08.md) | Overview Bottom Section + Statement Style Match (Aug 2026) |

## Design system, themes & UI/UX

| Document | What it covers |
|---|---|
| [`APPEARANCE_IOS_GLASS_AND_TYPESCALE_2026-08.md`](APPEARANCE_IOS_GLASS_AND_TYPESCALE_2026-08.md) | Appearance / Theme Senior Audit — Glass, Monet, Settings & Global Typography |
| [`BUTTON_MIGRATION_REPORT.md`](BUTTON_MIGRATION_REPORT.md) | Button Standardization Migration Report (master prompt v1.0) |
| [`DEMO_HUB_AUDIT_2026-08.md`](DEMO_HUB_AUDIT_2026-08.md) | Demo Hub audit + recording UX hardening — August 2026 |
| [`DEMO_HUB_NARRATION_SYNC_2026-08.md`](DEMO_HUB_NARRATION_SYNC_2026-08.md) | Demo Hub — Narration Re-Sync Pass · August 2026 |
| [`FEATURES_MODULE_SWITCH_AND_SETTINGS_ALIGN_2026-08.md`](FEATURES_MODULE_SWITCH_AND_SETTINGS_ALIGN_2026-08.md) | Features Module Switch + Settings Button Alignment (Aug 2026) |
| [`IOS_LIQUID_GLASS_THEME_REWRITE_2026-08-29.md`](IOS_LIQUID_GLASS_THEME_REWRITE_2026-08-29.md) | iOS Liquid Glass — Complete Theme-System Rewrite (2026-08-29) |
| [`LIGHT_DARK_THEME_PROFESSIONAL_UPGRADE_2026-09-07.md`](LIGHT_DARK_THEME_PROFESSIONAL_UPGRADE_2026-09-07.md) | Light/Dark Professional Industrial Theme Upgrade |
| [`LIQUID_GLASS_DESIGN_SYSTEM_REWRITE_2026-08-29.md`](LIQUID_GLASS_DESIGN_SYSTEM_REWRITE_2026-08-29.md) | Liquid Glass Design System — Global Theme Rewrite (2026-08-29) |
| [`LOCALIZATION_2026-09-01.md`](LOCALIZATION_2026-09-01.md) | System-Wide Language & Currency — Implementation & Audit (2026-09-01) |
| [`MODULE_FEATURE_TOGGLES_2026-08.md`](MODULE_FEATURE_TOGGLES_2026-08.md) | Module Feature Toggles — Job Order / Delivery Note / Tax Invoice (Aug 2026) |
| [`SCREEN_RECORDING_DURATION_FIX_2026-09-10.md`](SCREEN_RECORDING_DURATION_FIX_2026-09-10.md) | Screen Recording — deep analysis, duration fix & 4K UHD support |
| [`SCREEN_RECORDING_REDESIGN.md`](SCREEN_RECORDING_REDESIGN.md) | Screen Recording — Redesign, Lag & Quality Fixes |
| [`SENIOR_ANALYSIS_BUILD_ERRORS_AND_MOTION_SYSTEM.md`](SENIOR_ANALYSIS_BUILD_ERRORS_AND_MOTION_SYSTEM.md) | Senior Analysis — Build Errors (MC3000 / CS0200 / CS0191 / CS0103) and the Global Motio… |
| [`SENIOR_ANALYSIS_HARDCODED_CARDS_AUDIT.md`](SENIOR_ANALYSIS_HARDCODED_CARDS_AUDIT.md) | Senior Analysis — App-Wide Hardcoded Card Audit |
| [`SENIOR_ANALYSIS_SCREEN_RECORDING.md`](SENIOR_ANALYSIS_SCREEN_RECORDING.md) | Senior Analysis — Screen Recording |
| [`SENIOR_ANALYSIS_STEP4_STEP5_STEP9_AND_DIALOG_THEME.md`](SENIOR_ANALYSIS_STEP4_STEP5_STEP9_AND_DIALOG_THEME.md) | Senior Analysis — Step 4→5→9 Data Flow and Dialog Theming |
| [`SETTINGS_FULL_REDESIGN_AND_JOBORDER_2026-08.md`](SETTINGS_FULL_REDESIGN_AND_JOBORDER_2026-08.md) | Settings Full Redesign + Job Order Production Panel — Aug 2026 |
| [`SETTINGS_FULL_WIDTH_FIX_2026-08.md`](SETTINGS_FULL_WIDTH_FIX_2026-08.md) | Settings Full-Width Fix — Root Cause + Consistent Alignment (Aug 2026) |
| [`SETTINGS_MODULE_AUDIT.md`](SETTINGS_MODULE_AUDIT.md) | SETTINGS MODULE — DEEP AUDIT & UPGRADE PHASES |
| [`SETTINGS_NATURAL_HEIGHT_FIX_2026-08.md`](SETTINGS_NATURAL_HEIGHT_FIX_2026-08.md) | Settings Natural-Height Fix — Global Wallpaper empty space (Aug 2026) |
| [`SETTINGS_REDESIGN.md`](SETTINGS_REDESIGN.md) | Settings Module v2 — Redesign Report |
| [`SETTINGS_REDESIGN_2026-08.md`](SETTINGS_REDESIGN_2026-08.md) | Settings Redesign · Tile-Grid Design System · August 2026 |
| [`THEME_NORMAL_INDUSTRIAL_RESTORATION_2026-09-07.md`](THEME_NORMAL_INDUSTRIAL_RESTORATION_2026-09-07.md) | Theme — "Normal (Industrial)" restoration + card edge lines (2026-09-07) |
| [`THEME_REDUCTION_2026-09-06.md`](THEME_REDUCTION_2026-09-06.md) | Theme Reduction — 2026-09-06 |
| [`THEME_RICHNESS_ENHANCEMENT_2026-09-01.md`](THEME_RICHNESS_ENHANCEMENT_2026-09-01.md) | Theme System — Senior Richness & Contrast Pass (2026-09-01) |
| [`THEME_SECTION_ADOPTION_UHD_2026-09-01.md`](THEME_SECTION_ADOPTION_UHD_2026-09-01.md) | Theme System — Section Adoption + Cream/SaaS + UHD Pass (2026-09-01) |
| [`THEME_STUDIO_REPORT_2026-09-02.md`](THEME_STUDIO_REPORT_2026-09-02.md) | THEME STUDIO — Final Report (13-Point) |
| [`UI_AUDIT_AND_REDESIGN_PLAN.md`](UI_AUDIT_AND_REDESIGN_PLAN.md) | ProGlass Automation — UI Audit & Industrial-Grade Redesign Plan (V2.0) |
| [`UI_CONSISTENCY_AUDIT_MAINWINDOW.md`](UI_CONSISTENCY_AUDIT_MAINWINDOW.md) | MainWindow Shell — UI Style / Pattern Consistency Audit |
| [`UI_REDESIGN_SPLASH_LOGIN_ABOUT.md`](UI_REDESIGN_SPLASH_LOGIN_ABOUT.md) | Splash · Login · About — Industrial Redesign (v5) |
| [`UI_UX_DEEP_AUDIT_VIEWS.md`](UI_UX_DEEP_AUDIT_VIEWS.md) | Global UI/UX Deep Audit — all Views (133 XAML files) |
| [`UI_UX_DESIGN_SYSTEM_AUDIT_2026-09-07.md`](UI_UX_DESIGN_SYSTEM_AUDIT_2026-09-07.md) | ProGlassAutomation — Industrial ERP UI/UX + WPF/XAML Design-System Audit |
| [`UiUxSkillAudit.md`](UiUxSkillAudit.md) | ui-ux-pro-max — install and audit findings |
| [`WATER_ON_CARDS_ANALYSIS_2026-09-06.md`](WATER_ON_CARDS_ANALYSIS_2026-09-06.md) | Why cards don't get the Ios-17pm "water fill" globally — root-cause analysis |
| [`WHITE_WATER_GLASS_THEME_FIX_2026-08-29.md`](WHITE_WATER_GLASS_THEME_FIX_2026-08-29.md) | White Water Glass Theme — Cross-Analysis & Fix (2026-08-29) |

## Licensing & security

| Document | What it covers |
|---|---|
| [`LICENSE_GENERATOR_SENIOR_ANALYSIS_2026-08-29.md`](LICENSE_GENERATOR_SENIOR_ANALYSIS_2026-08-29.md) | License Generator — Senior Analysis: Startup Failure "Cannot find resource named …" |
| [`LICENSE_GENERATOR_SIMPLE_FLOW_REWRITE_2026-08-29.md`](LICENSE_GENERATOR_SIMPLE_FLOW_REWRITE_2026-08-29.md) | License Generator — Complete Rewrite as a Single-Purpose Key Signer (2026-08-29) |
| [`LICENSING_AND_LOGIN_UX_2026-08.md`](LICENSING_AND_LOGIN_UX_2026-08.md) | Licensing & Login UX — August 2026 rework |
| [`LICENSING_HARDENING_2026-08.md`](LICENSING_HARDENING_2026-08.md) | Licensing hardening pass — August 2026 |
| [`LICENSING_REFACTOR.md`](LICENSING_REFACTOR.md) | Licensing Refactor — Summary |
| [`LICENSING_SECURITY_CROSS_CHECK_2026-08-29.md`](LICENSING_SECURITY_CROSS_CHECK_2026-08-29.md) | Licensing Security Cross-Check — Findings & Hardening (2026-08-29) |
| [`LICENSING_WALLPAPER_FULLWIDTH_2026-08.md`](LICENSING_WALLPAPER_FULLWIDTH_2026-08.md) | Licensing + Appearance Wallpaper Full-Width Fix (Aug 2026) |
| [`LICENSING_WORKFLOW_SIMPLIFICATION_2026-08-29.md`](LICENSING_WORKFLOW_SIMPLIFICATION_2026-08-29.md) | Licensing Workflow Simplification — Design & Implementation Record (2026-08-29) |
| [`OFFLINE_MAX_SECURITY_KEY_WORKFLOW_2026-08-29.md`](OFFLINE_MAX_SECURITY_KEY_WORKFLOW_2026-08-29.md) | ProGlassAutomation Offline Licensing — Key Management Workflow (2026-08-29) |
| [`OFFLINE_MAX_SECURITY_LEGACY_MIGRATION_2026-08-29.md`](OFFLINE_MAX_SECURITY_LEGACY_MIGRATION_2026-08-29.md) | ProGlassAutomation Offline Licensing — Legacy Migration Procedure (2026-08-29) |
| [`OFFLINE_MAX_SECURITY_LICENSING_ARCHITECTURE_2026-08-29.md`](OFFLINE_MAX_SECURITY_LICENSING_ARCHITECTURE_2026-08-29.md) | ProGlassAutomation Offline Max-Security Licensing Architecture (2026-08-29) |
| [`OFFLINE_MAX_SECURITY_LICENSING_FINAL_AUDIT_2026-08-29.md`](OFFLINE_MAX_SECURITY_LICENSING_FINAL_AUDIT_2026-08-29.md) | ProGlassAutomation Offline Max-Security Licensing — Final Audit Snapshot (2026-08-29) |
| [`OFFLINE_MAX_SECURITY_LICENSING_PHASE0_AUDIT_2026-08-29.md`](OFFLINE_MAX_SECURITY_LICENSING_PHASE0_AUDIT_2026-08-29.md) | ProGlassAutomation Offline Max-Security Licensing — Phase 0 Audit (2026-08-29) |
| [`PLAN_BASED_LICENSING_AND_NEVER_BLOCKING_STARTUP_2026-08-29.md`](PLAN_BASED_LICENSING_AND_NEVER_BLOCKING_STARTUP_2026-08-29.md) | Plan-Based Licensing & Never-Blocking Startup (2026-08-29) |
| [`SecurityAudit-Fixes.md`](SecurityAudit-Fixes.md) | Security audit — fixes applied |
| [`SecurityAudit.md`](SecurityAudit.md) | ProGlassAutomation — Pre-Publication Security, Privacy, Memory & Reliability Audit |

## Print & export

| Document | What it covers |
|---|---|
| [`PDF_LOGO_LETTERHEAD_AUDIT_2026-09-07.md`](PDF_LOGO_LETTERHEAD_AUDIT_2026-09-07.md) | PDF Logo / Letterhead Senior Audit — 2026-09-07 |
| [`PRINT_LAYOUT_UNIFICATION_2026-09-08.md`](PRINT_LAYOUT_UNIFICATION_2026-09-08.md) | Print Layout Unification — Senior Analysis & Upgrade |

## Release, CI & readiness

| Document | What it covers |
|---|---|
| [`BALANCE_TO_10_2026-08.md`](BALANCE_TO_10_2026-08.md) | Balance-to-10 pass — August 2026 |
| [`CI.md`](CI.md) | ProGlass Automation — Continuous Integration |
| [`CODE_QUALITY_IMPROVEMENTS_2026-08-19.md`](CODE_QUALITY_IMPROVEMENTS_2026-08-19.md) | Code Quality Improvements — 19 Aug 2026 |
| [`CODE_QUALITY_RATING_2026-08-19.md`](CODE_QUALITY_RATING_2026-08-19.md) | Code quality rating — ProGlassAutomation v2.0.0 |
| [`E2E_WALKTHROUGH.md`](E2E_WALKTHROUGH.md) | ProGlass Automation — End-to-End Walk-Through |
| [`INSTALLER.md`](INSTALLER.md) | ProGlass Automation — Installer Pipeline |
| [`PHASE_PLAN_STABILITY_AUDIT.md`](PHASE_PLAN_STABILITY_AUDIT.md) | Deep Audit & Phase Plan — Stability, Responsiveness, Quality |
| [`ProductionReadinessReport.md`](ProductionReadinessReport.md) | PROGLASSAUTOMATION PRE-PUBLICATION REPORT |
| [`RATINGS_AND_CLEANUP_2026-08.md`](RATINGS_AND_CLEANUP_2026-08.md) | Ratings snapshot + follow-up cleanup — August 2026 |
| [`RATING_PROGRESS.md`](RATING_PROGRESS.md) | Rating Progress — Phases to 10/10 |
| [`RELEASE_AUDIT.md`](RELEASE_AUDIT.md) | ProGlassAutomation — Commercial Release Audit |
| [`RELEASE_GATE.md`](RELEASE_GATE.md) | ProGlass Automation — Release Gate |
| [`RELEASE_READINESS_2026-09-01.md`](RELEASE_READINESS_2026-09-01.md) | ProGlass Automation — Production Release Readiness (Deep Audit) |
| [`RELEASE_READINESS_V2_STATUS.md`](RELEASE_READINESS_V2_STATUS.md) | PROGLASS AUTOMATION — V2.0 RELEASE-READINESS STATUS |
| [`UPGRADE_NOTES.md`](UPGRADE_NOTES.md) | ProGlass Automation — Upgrade Notes (Audit • God Views • Step 8 Attachments • Dynamic T… |

## Audits & senior analyses (dated)

| Document | What it covers |
|---|---|
| [`AUDIT_2026-08-19_SENIOR_REVIEW.md`](AUDIT_2026-08-19_SENIOR_REVIEW.md) | Senior engineering audit — 19 Aug 2026 |
| [`AUDIT_FINDINGS.md`](AUDIT_FINDINGS.md) | 🔬 Deep Audit — Full Project Analysis |
| [`AUDIT_REPORT_2026-08.md`](AUDIT_REPORT_2026-08.md) | PROGLASS AUTOMATION — COMPLETE REPOSITORY DEEP AUDIT |
| [`CURRENT_PRODUCT_AUDIT_2026-09-05.md`](CURRENT_PRODUCT_AUDIT_2026-09-05.md) | PROGLASSAUTOMATION — CURRENT PRODUCT AUDIT |
| [`EMPTY_CATCH_SWEEP_2026-08-25.md`](EMPTY_CATCH_SWEEP_2026-08-25.md) | Empty-catch sweep — 2026-08-25 |
| [`FILE_ANALYSIS_2026-08.md`](FILE_ANALYSIS_2026-08.md) | Full-tree analysis — 2026-08-18 |
| [`INDUSTRIALIZATION_AUDIT_2026-09-06.md`](INDUSTRIALIZATION_AUDIT_2026-09-06.md) | ProGlass Automation — Industrialization Audit & Safe Refactor |
| [`INDUSTRIAL_CODE_AUDIT.md`](INDUSTRIAL_CODE_AUDIT.md) | Industrial Code & Workflow Senior Audit — ProGlass Automation |
| [`INDUSTRIAL_ERP_ANALYSIS_2026-09-07.md`](INDUSTRIAL_ERP_ANALYSIS_2026-09-07.md) | Senior Industrial-ERP Analysis — Appearance, Branding & Theme Round |
| [`PHASE10_COMPLETE.md`](PHASE10_COMPLETE.md) | Phase 10 Complete — Production Hardening |
| [`PHASE1_COMPLETE.md`](PHASE1_COMPLETE.md) | Phase 1 Complete — CI/CD & Quality Gates |
| [`PHASE2_COMPLETE.md`](PHASE2_COMPLETE.md) | Phase 2 Complete — Error Handling Hardening v2 |
| [`PHASE3_COMPLETE.md`](PHASE3_COMPLETE.md) | Phase 3 Complete — Async & Threading Cleanup |
| [`PHASE4_COMPLETE.md`](PHASE4_COMPLETE.md) | Phase 4 Complete — Event Lifetime & Memory |
| [`PHASE5_COMPLETE.md`](PHASE5_COMPLETE.md) | Phase 5 Complete — DI & Composition Root |
| [`PHASE6B_COMPLETE.md`](PHASE6B_COMPLETE.md) | Phase 6b Complete — God ViewModel Decomposition Part 2 (DI Wiring) |
| [`PHASE6_COMPLETE.md`](PHASE6_COMPLETE.md) | Phase 6 Complete — God ViewModel Decomposition (Part 1) |
| [`PHASE7_COMPLETE.md`](PHASE7_COMPLETE.md) | Phase 7 Complete — Test Coverage Expansion |
| [`PHASE8_COMPLETE.md`](PHASE8_COMPLETE.md) | Phase 8 Complete — Security Hardening v2 |
| [`PHASE9_COMPLETE.md`](PHASE9_COMPLETE.md) | Phase 9 Complete — Performance & Virtualization |
| [`SENIOR_ANALYSIS.md`](SENIOR_ANALYSIS.md) | Senior Analysis — ProGlass Automation ADV |
| [`SENIOR_ANALYSIS_AND_IMPROVEMENTS.md`](SENIOR_ANALYSIS_AND_IMPROVEMENTS.md) | Senior Analysis: P&L and Profitability System |
| [`SENIOR_ANALYSIS_ERR-633EEB_AND_THEME_BOUNCE.md`](SENIOR_ANALYSIS_ERR-633EEB_AND_THEME_BOUNCE.md) | Senior Analysis — ERR-633EEB startup crash + splash colour bounce |
| [`SENIOR_ANALYSIS_LOCALIZATION.md`](SENIOR_ANALYSIS_LOCALIZATION.md) | Senior Deep Analysis — Localization & Globalization vs. Master Prompt |
| [`SENIOR_ANALYSIS_STARTUP_CRASH_SWEEP.md`](SENIOR_ANALYSIS_STARTUP_CRASH_SWEEP.md) | Senior Analysis — Startup Crash Sweep (ERR-918BAB and the whole defect family) |
| [`SENIOR_DEEP_AUDIT.md`](SENIOR_DEEP_AUDIT.md) | Senior Industrial Developer Deep Code Audit |
| [`SENIOR_ENGINEERING_AUDIT.md`](SENIOR_ENGINEERING_AUDIT.md) | Senior Engineering Audit Report |
| [`SENIOR_ERP_RUNTIME_FIX_NOTES_2026-09-07.md`](SENIOR_ERP_RUNTIME_FIX_NOTES_2026-09-07.md) | Senior ERP Runtime Fix Notes — 2026-09-07 |
| [`SENIOR_GLOBAL_AUDIT_2026-08-21.md`](SENIOR_GLOBAL_AUDIT_2026-08-21.md) | Senior Global Code Audit — ProGlassAutomationADV |

## Other documents

| Document | What it covers |
|---|---|
| [`BUILD_VERIFICATION_GUIDE.md`](BUILD_VERIFICATION_GUIDE.md) | Build Verification Guide |
| [`CODE_RATING_REPORT.md`](CODE_RATING_REPORT.md) | ProGlass Automation — Senior Engineering Rating |
| [`CORRECTED_MARGIN_CALCULATION.md`](CORRECTED_MARGIN_CALCULATION.md) | ✅ Corrected Margin Calculation - Example |
| [`DEEP_ANALYSIS_2026-09-09.md`](DEEP_ANALYSIS_2026-09-09.md) | ProGlass Automation — Deep Code Analysis, 2026-09-09 |
| [`DELIVERY-REPORT.md`](DELIVERY-REPORT.md) | ProGlass Automation — Localization & Globalization Delivery Report |
| [`DIALOG_SYSTEM_AUDIT.md`](DIALOG_SYSTEM_AUDIT.md) | Dialog & Popup System — Senior Audit and Unified Architecture |
| [`ERP_LIFECYCLE_HARDENING_ANALYSIS_2026-09-09.md`](ERP_LIFECYCLE_HARDENING_ANALYSIS_2026-09-09.md) | ProGlass Automation — Document-Lifecycle Hardening Analysis |
| [`EXECUTIVE_FUNNEL_FIX.md`](EXECUTIVE_FUNNEL_FIX.md) | Executive Report Hub — Order-to-Cash Funnel Fix (2026-09-10) |
| [`FULL_WORKFLOW_AUDIT.md`](FULL_WORKFLOW_AUDIT.md) | PROGLASS AUTOMATION — DEEP WORKFLOW AUDIT & UPGRADE |
| [`IMPLEMENTATION_COMPLETE.md`](IMPLEMENTATION_COMPLETE.md) | ✅ Implementation Complete: Three Analysis Views with Tab Control |
| [`IMPLEMENTATION_SUMMARY.md`](IMPLEMENTATION_SUMMARY.md) | ProGlassAutomationADV — Implementation Summary |
| [`KPI_LIVE_AND_DISPLAY_FIXES_2026-09-10.md`](KPI_LIVE_AND_DISPLAY_FIXES_2026-09-10.md) | KPI Live Updates + Overview Delivery Count + Per-SQM + Profitability Layout |
| [`MONEY_ROUNDING_POLICY.md`](MONEY_ROUNDING_POLICY.md) | Money Rounding Policy |
| [`OPTIMIZATION_CALIBRATION_BENCHMARK_2026-09-12.md`](OPTIMIZATION_CALIBRATION_BENCHMARK_2026-09-12.md) | Optimization — Calibration Benchmark Report (2026-09-12) |
| [`OPTIMIZATION_MODULE_DESIGN_2026-09-12.md`](OPTIMIZATION_MODULE_DESIGN_2026-09-12.md) | Glass Cutting Optimization Module — Deep Analysis & Implementation Plan |
| [`PHASE_BALANCE.md`](PHASE_BALANCE.md) | Phase Balance — ProGlass Automation ADV |
| [`PI_PDF_WYSIWYG_REDESIGN_2026-09-11.md`](PI_PDF_WYSIWYG_REDESIGN_2026-09-11.md) | PI Step 7 / PDF Export — WYSIWYG Redesign (Senior Analysis) |
| [`PI_SYSTEM_STATUS_REPORT.md`](PI_SYSTEM_STATUS_REPORT.md) | Proforma Invoice System - Complete Status Report |
| [`PL_COST_BASED_MARKUP_EXAMPLE.md`](PL_COST_BASED_MARKUP_EXAMPLE.md) | P&L Cost-Based Markup Implementation - Example |
| [`PRODUCTION_DGU_OVERLAP_PIPELINE_EXAMPLE_2026-09-11.md`](PRODUCTION_DGU_OVERLAP_PIPELINE_EXAMPLE_2026-09-11.md) | DGU Overlap Job — Full Production Pipeline Example to Delivery-Note Dispatch |
| [`PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md`](PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md) | Production Distribution — Analysis & Implementation Plan |
| [`PRODUCTION_MODULE_EXAMPLES_2026-09-11.md`](PRODUCTION_MODULE_EXAMPLES_2026-09-11.md) | Production Module Examples — one job through all eight screens |
| [`PRODUCTION_SCREENS_DESIGN_2026-09-11.md`](PRODUCTION_SCREENS_DESIGN_2026-09-11.md) | Production Module Screens — List & Detail Design (with worked samples) |
| [`PRODUCTION_STEP4_MAPPING_ANALYSIS_2026-09-11.md`](PRODUCTION_STEP4_MAPPING_ANALYSIS_2026-09-11.md) | Production — Complete Step 4 / Step 5 Mapping Analysis & Module Upgrade |
| [`REMEDIATION_PLAN.md`](REMEDIATION_PLAN.md) | Remediation Plan |
| [`REPORT_GENERATOR_SENIOR_ANALYSIS.md`](REPORT_GENERATOR_SENIOR_ANALYSIS.md) | Report Generator — Senior Analysis & Implementation Plan |
| [`SERVICE_CATEGORIZATION_FIX.md`](SERVICE_CATEGORIZATION_FIX.md) | Service Profitability - Business Logic Clarification |
| [`SPEC_CHARGE_EXAMPLES.md`](SPEC_CHARGE_EXAMPLES.md) | Complete Spec-to-Charge Examples (4 Build Types) |
| [`STEP4_STEP6_SHEET_STORE_LINK_2026-09-10.md`](STEP4_STEP6_SHEET_STORE_LINK_2026-09-10.md) | Step 4/5/6 ↔ Sheet Store linkage (2026-09-10) |
| [`STEP8_APPROVE_UNIFICATION.md`](STEP8_APPROVE_UNIFICATION.md) | Step 8 Approve — Unifying the Split JPG/PDF Flow (Senior Analysis) |
| [`USER_REPORT_FIXES_2026-09-10_B.md`](USER_REPORT_FIXES_2026-09-10_B.md) | User-Reported Fixes — Round 2 (2026-09-10) |
| [`WASTAGE_COLOUR_SYNC_FIX.md`](WASTAGE_COLOUR_SYNC_FIX.md) | Step 6 Wastage Charges — Colour Import & Spec-Side Sync Fix |
| [`WORKFLOW.md`](WORKFLOW.md) | Application Workflow |

## Sub-folders

| Folder | Contents |
|---|---|
| `CI/` | Pipeline definitions (`build-test.yml`). (2 files) |
| `Chat5/` | Working notes and data from an earlier session. (6 files) |
| `Chat5-9/` | Working notes, prompts and data from an earlier session. (7 files) |
| `ci/` | Pipeline definitions — duplicate casing of `CI/`; see `CI.md`. (2 files) |
| `phases/` | Supporting artefacts. (18 files) |
| `ui-redesign/` | UI redesign working artefacts. (1 files) |


## Folder READMEs

Every significant folder in the repository carries its own `README.md`
describing what lives there. `31` of them:

| Folder | What lives there |
|---|---|
| [`Behaviors/`](../Behaviors/README.md) | Behaviors |
| [`Controls/`](../Controls/README.md) | Controls/ — Reusable Custom UserControls |
| [`Converters/`](../Converters/README.md) | Converters |
| [`Services/AI/`](../Services/AI/README.md) | 🤖 AI Domain — ProGlass Automation |
| [`Services/Database/`](../Services/Database/README.md) | 🗄️ Database Layer — ProGlass Automation |
| [`Services/Database/DatabaseBackup/`](../Services/Database/DatabaseBackup/README.md) | Database Auto-Backup |
| [`Services/Export/`](../Services/Export/README.md) | Shared Export Engine |
| [`Services/JobOrder/Export/`](../Services/JobOrder/Export/README.md) | Job Order Export |
| [`Services/ProformaInvoice/Export/`](../Services/ProformaInvoice/Export/README.md) | ProformaInvoice Export — Phase 9.5b |
| [`Services/ProformaInvoice/ProformaInvoiceRepository/`](../Services/ProformaInvoice/ProformaInvoiceRepository/README.md) | Proforma Invoice Repository |
| [`Services/ProformaInvoice/SpecificationsRepository/`](../Services/ProformaInvoice/SpecificationsRepository/README.md) | Specifications Repository |
| [`Services/SheetStore/`](../Services/SheetStore/README.md) | 📦 Sheet Store Module |
| [`Services/SheetStore/SheetStoreCsv/`](../Services/SheetStore/SheetStoreCsv/README.md) | Sheet Store CSV Import / Export |
| [`Services/SheetStore/SheetStoreExcel/`](../Services/SheetStore/SheetStoreExcel/README.md) | Sheet Store Excel Export |
| [`Services/SheetStore/SheetStoreHistory/`](../Services/SheetStore/SheetStoreHistory/README.md) | Sheet Store History |
| [`Services/SheetStore/SheetStorePdf/`](../Services/SheetStore/SheetStorePdf/README.md) | Sheet Store PDF Report |
| [`Tests/`](../Tests/README.md) | ProGlassAutomation.Tests |
| [`Themes/Styles/`](../Themes/Styles/README.md) | Themes/Styles/ — Shared Style Library |
| [`Tools/LicenseGenerator/`](../Tools/LicenseGenerator/README.md) | ProGlassAutomation.LicenseGenerator |
| [`Views/Dashboard/`](../Views/Dashboard/README.md) | Dashboard Module |
| [`Views/Production/`](../Views/Production/README.md) | Production Modules — Stage Queues |
| [`Views/ProformaInvoice/`](../Views/ProformaInvoice/README.md) | Proforma Invoice Module |
| [`Views/ProformaInvoice/SavedInvoicesList/`](../Views/ProformaInvoice/SavedInvoicesList/README.md) | Saved Invoices List — Phase 7.16 |
| [`Views/ProformaInvoice/Step5DataGrid/GrandTotalSummary/`](../Views/ProformaInvoice/Step5DataGrid/GrandTotalSummary/README.md) | Grand Total Summary |
| [`Views/ProformaInvoice/Step6OtherCharges/`](../Views/ProformaInvoice/Step6OtherCharges/README.md) | Step 6 - Other Charges |
| [`Views/ProformaInvoice/Step7PrintPreview/`](../Views/ProformaInvoice/Step7PrintPreview/README.md) | Step 7 - Summary (Print Preview & Review) |
| [`Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/`](../Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/README.md) | Printable Invoice — Phase 7.13 |
| [`Views/ProformaInvoice/Step7PrintPreview/TermsAndConditions/`](../Views/ProformaInvoice/Step7PrintPreview/TermsAndConditions/README.md) | Terms & Conditions — Phase 7.14 |
| [`Views/Reports/`](../Views/Reports/README.md) | ProGlass Automation — Sales Report & Account Statement Module |
| [`Views/TaxInvoice/`](../Views/TaxInvoice/README.md) | ProGlass Automation - Tax Invoice Module |
| [`scripts/layout-verify/`](../scripts/layout-verify/README.md) | Layout verification harness |


## Undated vs dated

Documents whose name carries a date (`*_2026-08-19.md`) are point-in-time
audits and analyses. They are history: accurate for the code as it stood
that day, and **not** a description of the current build. The undated
documents — `ARCHITECTURE.md`, `WORKFLOW.md`, `DESIGN_SYSTEM.md`,
`MONEY_PATH_ERROR_POLICY.md`, the customer guides — are the ones kept
current. When the two disagree, the undated document wins.

`scripts/verify-readmes.py` checks that every link in every README resolves
and that this index still matches the `docs/` tree.
