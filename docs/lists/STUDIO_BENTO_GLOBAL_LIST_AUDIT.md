# Studio Bento — Global List Pages Implementation & Audit

**2026-09-22 · Architecture: Studio Bento Global List Design System**
**Baseline:** `1185a09f4e8b2baa386121483075ba94408fc068`
**Branch:** `arena/01a0c955-proglassautomationadv`

## 1. Executive Summary & Purpose

Following the implementation of the Studio Bento list design system in commit `1185a09f4e8b2baa386121483075ba94408fc068` for the five primary saved document lists (Proforma, Job Order, Delivery Note, Tax Invoice, Statement List), this implementation applies the identical design system across all remaining list views, overview God Views, Job Profitability analytics views, Report Hub modules, and entity registers in ProGlass Automation.

The implementation strictly preserves:
- 100% of data bindings, paths, formatting strings, and converters.
- 100% of command endpoints, command parameters, keyboard shortcuts, and double-click mouse bindings.
- 100% of ViewModel interactions, collection virtualization parameters, and code-behind event handlers.
- Zero mockups, PNG/JPG assets, or fabricated UI data added.

## 2. Global Census & Categorization of List Views

A complete audit of list surfaces across the application identified the target modules for Studio Bento styling:

| Module Category | View Path | Data Model & List Controls | Key Retained Contracts |
|---|---|---|---|
| **God Views** | `Views/JobOrder/GodView/JobOrderGodView.xaml` | `DataGrid` (ItemsSource: `Rows`), `ItemsControl` (`Kpis`) | Search text delay (200ms), Refresh command, Open command with double-click binding, 8 data columns, empty state |
| | `Views/DeliveryNote/GodView/DeliveryNoteGodView.xaml` | `DataGrid` (ItemsSource: `Rows`), `ItemsControl` (`Kpis`) | Search text, Refresh command, Open command with double-click binding, 10 data columns, empty state |
| | `Views/TaxInvoice/GodView/TaxInvoiceGodView.xaml` | `DataGrid` (ItemsSource: `Rows`), `ItemsControl` (`Kpis`) | Search text, Refresh command, Open command with double-click binding, 8 data columns, empty state |
| **Profitability** | `Views/JobProfitability/JobProfitabilityView.xaml` | `ItemsControl` (ItemsSource: `Rows`), 4 KPI cards, 5 summary chips | Full filtering (Company, Customer, Salesman, Date range), selection commands, double-click full detail, sidebar bridge |
| | `Views/JobProfitability/PortfolioServiceProfitabilityView.xaml` | `ItemsControl` (ItemsSource: `ServiceBreakdown`), 5 KPI cards | Aggregated service profitability across confirmed PIs, cost/revenue/margin breakdown, empty/loading states |
| | `Views/JobProfitability/PortfolioGlassTypeComparisonView.xaml` | `ItemsControl` (ItemsSource: `GlassTypeRows`), 5 KPI cards | DGU, SGU, LAM, DGU+LAM comparison, cost/profit/margin breakdown, best performer highlight |
| | `Views/JobProfitability/PortfolioWastageAnalysisView.xaml` | `ItemsControl` (ItemsSource: `WastageBreakdown`), 5 KPI cards | Actual wastage charges from Step 6, glass color grouping, high wastage triggers |
| **Report Hub Shell** | `Views/Reports/SalesReportView.xaml` | `ItemsControl` (ItemsSource: `NavItems`), Overview screen | Data-driven navigation rail with active pill indicator, toggle rail command, live KPI strip across all 5 modules |
| **Order Report** | `Views/Reports/OrderReport/OrderReportListView.xaml` | `ListView` (ItemsSource: `Rows`), 3 KPI cards, filter bar | Archetype 1 saved register, New Report command, PDF/Share/Open/Delete actions, selection binding |
| **Document Reports** | `Views/Reports/PiReport/PiReportView.xaml` | `ListBox` (ItemsSource: `Rows`), 4 KPI cards, filter card | Proforma quotation volume, confirmed/converted breakdown, date range, company/customer/project filters, CSV/Excel/PDF export |
| | `Views/Reports/JoReport/JoReportView.xaml` | `ListBox` (ItemsSource: `Rows`), 4 KPI cards, filter card | Factory job orders register, production status filters, date range, CSV/Excel/PDF export |
| | `Views/Reports/DeliveryReport/DeliveryReportView.xaml` | `ListBox` (ItemsSource: `Rows`), 4 KPI cards, filter card | Row-level delivery trace, ordered/delivered/balance SQM reconciliation, inline note expansion, PDF/Share export |
| | `Views/Reports/TaxInvoiceReport/TaxInvoiceReportView.xaml` | `ListBox` (ItemsSource: `Rows`), 4 KPI cards, filter card | Confirmed PIs awaiting invoicing, invoiced totals, payment tracking, CSV/Excel/PDF export |
| | `Views/Reports/StatementReport/StatementReportView.xaml` | `ListBox` (ItemsSource: `Rows`), 4 KPI cards, filter card | Financial statement ledger, outstanding balances, overdue breakdown, CSV/Excel/PDF export |
| | `Views/Reports/VatSubmission/VatSubmissionView.xaml` | `ListBox` (ItemsSource: `Rows`), 4 KPI cards, 4 view modes | 5% UAE FTA VAT breakdown, detail/salesman/company/project tabs, CSV/Excel/PDF export |
| **Entity Registers** | `Views/Reports/CustomerReport/CustomerReportView.xaml` | `ListBox` (ItemsSource: `Rows`), 4 KPI cards | Customer directory, receivables, proforma/invoiced/outstanding amounts |
| | `Views/Reports/CompanyReport/CompanyReportView.xaml` | `ListBox` (ItemsSource: `Rows`), 4 KPI cards | Issuing companies register, financial summary per legal entity |
| | `Views/Reports/SalesmanReport/SalesmanReportView.xaml` | `ListBox` (ItemsSource: `Rows`), 4 KPI cards | Salesman performance, quotation volume, conversion rate, confirmed revenue |
| | `Views/Reports/ProjectReport/ProjectReportView.xaml` | `ListBox` (ItemsSource: `Rows`), 4 KPI cards | Project register, contract/LPO/site details, fulfillment tracking |
| | `Views/Reports/GlassSpecReport/GlassSpecReportView.xaml` | `ListBox` (ItemsSource: `Rows`), 4 KPI cards | Glass row-level specifications, thickness/type/finish traceability |
| | `Views/Reports/ProfitabilityReport/ProfitabilityReportView.xaml` | `ListBox` (ItemsSource: `Rows`), 4 KPI cards | Per-proforma P&L from Step 9 costing, margin health indicators |
| | `Views/Reports/ExecutiveReport/ExecutiveReportView.xaml` | KPI Funnel + breakdown lists | C-suite conversion funnel (PI → JO → DN → TI → Paid), date filtering |
| **Operational Lists** | `Views/Inquiry/InquiryListView.xaml` | `ListBox` (ItemsSource: `Inquiries`), 4 KPI cards, 7 status tabs, 6 pipeline tabs | Commercial intake register, slide-in lifecycle rail, double-click open, convert to PI action |
| **Global Theme** | `Themes/Styles/ListPageStyles.xaml` | Shared chrome for `ModuleList*` and `Report*` styles | Upgraded to Studio Bento radius (18 DIP), tabular numbers, padding, borders, and input heights |

## 3. Architecture & Style Implementation

### 3.1 Studio Bento System Extensions (`Views/Shared/Lists/StudioListResources.xaml`)
Extended `StudioListResources.xaml` with semantic DataGrid styles:
- `Sl.DataGrid`: Themed DataGrid using `Ov.Surface` rows, `Ov.Soft` alternating rows, `Ov.Line` dividers, virtualization recycling, and pixel scroll units.
- `Sl.DataGridHeader`, `Sl.DataGridHeaderCenter`, `Sl.DataGridHeaderRight`: Semi-bold headers with device-pixel snapping and precise alignment matching cell contents.
- `Sl.CellText`, `Sl.CellTextCenter`, `Sl.CellTextRight`: Tabular numeral alignment (`Typography.NumeralAlignment="Tabular"`), standard 13px body text.

### 3.2 Global List Page Chrome (`Themes/Styles/ListPageStyles.xaml`)
Enhanced the globally registered list page styles:
- `ModuleListShellStyle`: Border radius updated to `CardCornerRadius` (18 DIP), hairline keyline border, clip-to-bounds.
- `ModuleListKpiStripStyle` & `ModuleListKpiIconStyle`: Standardized 40x40 icon tile with 10 DIP radius, 16,14 padding.
- `ModuleListKpiValueStyle`: Tabular numeral formatting enabled across all summary values.
- `ReportFilterCardStyle` & `ReportSearchBandStyle`: 14 DIP and 10 DIP corner radii, clean padding, and hairline borders.
- `ModuleListFilterInputStyle`, `ModuleListFilterComboStyle`, `ModuleListFilterDatePickerStyle`: Uniform 36px input height.

### 3.3 Visual & Ergonomic Improvements
- **Porcelain & Lavender Aesthetic:** Consistent background (`Ov.Canvas`), surface cards (`Ov.Surface`), borders (`Ov.Line`), and typography (`Ov.Ink` and `Ov.Muted`).
- **Hero & Supporting Bento Cards:** Prominent primary metrics styled in `Sl.Hero` (solid indigo fill, white tabular numbers) paired with secondary `Sl.Card` metrics.
- **Data Table Clarity:** Clear, high-contrast column labels, zebra-striped rows with subtle hover feedback, and clear badge indicators for statuses and alerts.

## 4. Verification & Testing Evidence

All automated verification test suites pass without regressions:

| Test Suite / Script | Target Scope | Result | Details |
|---|---|---|---|
| `scripts/verify-studio-lists.py` | 5 core Studio Bento lists | **PASS (27/27 tests)** | All 309 binding attributes, 7 object bindings, 36 shell bindings, 21 summary values preserved |
| `scripts/verify-indigo-modules.py` | 6 dashboard modules | **PASS (18/18 tests)** | All 25 KPIs, bindings, row templates, wheel bubbling preserved |
| `scripts/verify-indigo-overview.py` | Overview & Shell | **PASS (12/12 tests)** | All 17 KPIs, 5 stages, shell bindings, responsive properties verified |
| `scripts/verify-dashboard.py` | Repository-wide XAML | **PASS (341/341 files)** | 0 unresolved StaticResource refs, 0 unresolved DynamicResource refs, 0 binding head mismatches |
| `scripts/xaml-resource-lint.py` | XAML syntax & resource lint | **PASS (341 files)** | 0 fatal errors, 121 non-fatal legacy typed-resource warnings |
| `scripts/xaml-xref-audit.py` | Resource cross-reference audit | **PASS** | 0 forward reference errors, 0 duplicate keys reported |

## 5. Summary of Git Commits

1. `23d46b0f`: `feat(lists): apply Studio Bento redesign to Job Order, Delivery Note, and Tax Invoice God Views`
2. `6c281ea0`: `feat(profitability): apply Studio Bento redesign to Job Profitability and portfolio analytics views`
3. `3342c845`: `feat(reports): apply Studio Bento redesign to Report Hub shell and Order Report register`
4. `142000ee`: `feat(reports): apply Studio Bento design system to Report Hub document modules and global list page styles`
5. `d5584068`: `feat(lists): apply Studio Bento design system to Inquiry List and all Report Hub entity registers`
