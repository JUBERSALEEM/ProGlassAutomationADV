# Central Reporting Module — DataGrid & UI/UX Industrial Specification & Mockup

This document outlines the **unified DataGrid architecture, layout mockups, and shared style specifications** for all 10 report modules in **ProGlass Automation**.

---

## 1. Unified Shared DataGrid Architecture

All reports use the factory-tested **industrial list and table layout system**:
- **Outer Shell Card**: `ModuleListShellStyle` (`CornerRadius="12"`, `BorderThickness="1"`, `Background="{DynamicResource ModuleBackgroundBrush}"`)
- **KPI Summary Strip**: `ModuleListKpiStripStyle` (`Padding="16,12"`, border-bottom separator)
- **Unified Filter Bar**: `ModuleListFilterBarStyle` (`Padding="16,10"`, input height 32 DIP, border-bottom separator)
- **Table Header**: `ModuleListTableHeaderStyle` (`Background="{DynamicResource MenuHoverBrush}"`, `Padding="16,10"`, uppercase 10pt bold text)
- **Data Rows**: `ModuleListRowStyle` (`Padding="16,11"`, hover highlight, stretch content)
- **Container Style**: `ModuleListBoxItemStyle` (strips WPF default margins, padding, and focus rings)
- **Pagination Footer**: Fixed 44 DIP bar (`Padding="12,8"`, page count on left, action buttons on right)

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ 1. KPI STRIP (4-Column Balanced Metrics with Icon Badges & Subtle Dividers)                           │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 2. FILTER & TOOLBAR (Search, Date Pickers, Dropdowns, Apply/Clear, CSV/Excel/PDF)                       │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 3. TABLE HEADER (Pale Header, Muted Bold Labels, Right-Aligned Numerics)                              │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 4. SCROLLABLE DATA ROWS (Virtualizing ListBox, Hover Glow, Exact Column Alignment, Cell Badges)       │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 5. DETAIL / TRACE DRAWER (Optional expandable drawer for Row Trace & Spec Items)                      │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 6. PAGINATION FOOTER (Page X of Y, Prev, Next, Refresh)                                                │
└────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Navigation Rail Architecture (Left Panel)

```
┌──────────────────────────────────────┐
│ [Icon] REPORTS HUB                   │
│        Intelligence Center           │
├──────────────────────────────────────┤
│ DOCUMENTS                            │
│  [◉] Overview           KPI dashboard│
│  [◉] Proforma Invoice   Quotes & conv│
│  [◉] Job Order          Production   │
│  [◉] Delivery           Dispatched   │
│  [◉] Tax Invoice        Billed & due │
├──────────────────────────────────────┤
│ MASTER DATA                          │
│  [◉] Company / Customer Lifecycle    │
│  [◉] Salesman           Revenue / rep│
│  [◉] Project            Drill-down   │
├──────────────────────────────────────┤
│ INTELLIGENCE                         │
│  [◉] Glass / Spec       Row-level    │
│  [◉] Statement          Customer stmt│
│  [◉] Profitability      Revenue/cost │
├──────────────────────────────────────┤
│ ● Central Data Layer (Active)        │
└──────────────────────────────────────┘
```

---

## 3. Detailed Layout Mockup per Report Module

### Module 1: Proforma Invoice Report (`PiReportView`)
- **KPIs**: TOTAL PI (`AED Total`) · CONFIRMED (`Approved/Issued`) · CONVERTED (`JO converted`) · QUANTITY (`Total SQM`)
- **Grid Column Widths**: `[ 130 | 95 | 150 | * | 115 | 105 | 80 | 75 ]`

```
┌──────────────┬──────────┬─────────────────┬────────────────────────┬──────────────┬─────────────┬───────┬───────┐
│ PI #         │ STATUS   │ CUSTOMER        │ PROJECT / COMPANY      │ GRAND TOTAL  │ QTY / SQM   │ TRACE │ DRILL │
├──────────────┼──────────┼─────────────────┼────────────────────────┼──────────────┼─────────────┼───────┼───────┤
│ PI-2026-0842 │ Approved │ Al Futtaim Auto │ Dubai Mall Expansion   │ AED 148,250  │ 420 pcs     │ ● ● ● │ [Rows]│
│ 18 Aug 2026  │ [Green]  │                 │ ProGlass Industries    │ (Right)      │ 680.5 SQM   │(Flags)│ [Btn] │
├──────────────┼──────────┼─────────────────┼────────────────────────┼──────────────┼─────────────┼───────┼───────┤
│ PI-2026-0841 │ Draft    │ Emaar Projects  │ Downtown Tower B       │ AED 89,400   │ 180 pcs     │ ○ ○ ○ │ [Rows]│
│ 17 Aug 2026  │ [Gray]   │                 │ Emirates Glass Co      │ (Right)      │ 320.0 SQM   │       │ [Btn] │
└──────────────┴──────────┴─────────────────┴────────────────────────┴──────────────┴─────────────┴───────┴───────┘
```

---

### Module 2: Job Order Report (`JoReportView`)
- **KPIs**: TOTAL JO (`Total AED`) · AWAITING JO (`Pending conversion`) · QUANTITY (`Total SQM`) · VALUE (`Total Quoted`)
- **Grid Column Widths**: `[ 130 | 130 | * | 110 | 120 | 100 | 110 ]`

```
┌──────────────┬──────────────┬────────────────────────┬──────────────┬──────────────┬─────────────┬─────────────┐
│ JO #         │ PI #         │ CUSTOMER / PROJECT     │ STATUS       │ GRAND TOTAL  │ QTY / SQM   │ DELIV / BAL │
├──────────────┼──────────────┼────────────────────────┼──────────────┼──────────────┼─────────────┼─────────────┤
│ JO-2026-0318 │ PI-2026-0842 │ Al Futtaim Automotive  │ In Production│ AED 148,250  │ 420 pcs     │ 300 / 120   │
│ 24 Aug 2026  │ (Accent Link)│ Dubai Mall Expansion   │ [Blue Badge] │ (Right)      │ 680.5 SQM   │(Green/Amber)│
├──────────────┼──────────────┼────────────────────────┼──────────────┼──────────────┼─────────────┼─────────────┤
│ JO-2026-0317 │ PI-2026-0839 │ Sobha Realty           │ Completed    │ AED 210,000  │ 540 pcs     │ 540 / 0     │
│ 22 Aug 2026  │ (Accent Link)│ Hartland Phase 2       │ [Green Badge]│ (Right)      │ 920.0 SQM   │(Green)      │
└──────────────┴──────────────┴────────────────────────┴──────────────┴──────────────┴─────────────┴─────────────┘
```

---

### Module 3: Delivery Report (`DeliveryReportView`)
- **KPIs**: DELIVERY NOTES (`% Delivered`) · PENDING (`Not dispatched`) · PARTIAL (`In progress`) · COMPLETED (`Fulfilled`)
- **Grid Column Widths**: `[ 130 | 130 | 150 | * | 100 | 120 | 90 ]`

```
┌──────────────┬──────────────┬─────────────────┬────────────────────────┬───────────┬─────────────┬───────────┐
│ DN #         │ JO # / PI #  │ CUSTOMER        │ PROJECT                │ STATUS    │ ORDER / DEL │ INVOICED  │
├──────────────┼──────────────┼─────────────────┼────────────────────────┼───────────┼─────────────┼───────────┤
│ DN-2026-0512 │ JO-2026-0318 │ Al Futtaim Auto │ Dubai Mall Expansion   │ Delivered │ 420 / 300   │ [Invoiced]│
│ 19 Aug 2026  │ PI-2026-0842 │                 │                        │ [Blue]    │ Bal: 120    │ [Green]   │
├──────────────┼──────────────┼─────────────────┼────────────────────────┼───────────┼─────────────┼───────────┤
│ DN-2026-0511 │ JO-2026-0316 │ Damac Properties│ Akoya Villas Phase 4   │ Dispatched│ 150 / 150   │ [Pending] │
│ 18 Aug 2026  │ PI-2026-0835 │                 │                        │ [Amber]   │ Bal: 0      │ [Muted]   │
└──────────────┴──────────────┴─────────────────┴────────────────────────┴───────────┴─────────────┴───────────┘
```

---

### Module 4: Tax Invoice Report (`TaxInvoiceReportView`)
- **KPIs**: TAX INVOICES (`Total AED`) · OUTSTANDING (`Issued amount`) · OVERDUE (`Needs collection`) · PENDING INVOICE (`Confirmed PI without TI`)
- **Grid Column Widths**: `[ 140 | 120 | * | 110 | 120 | 80 | 90 ]`

```
┌──────────────┬──────────────┬────────────────────────┬─────────────┬──────────────┬─────────┬───────────┐
│ INVOICE #    │ PI # / JO #  │ CUSTOMER               │ ISSUE DATE  │ GRAND TOTAL  │ QTY     │ STATUS    │
├──────────────┼──────────────┼────────────────────────┼─────────────┼──────────────┼─────────┼───────────┤
│ INV-2026-0199│ PI-2026-0842 │ Al Futtaim Automotive  │ 19 Aug 2026 │ AED 148,250  │ 300 pcs │ Issued    │
│              │ JO-2026-0318 │                        │             │ (Right)      │ (Right) │ [Amber]   │
├──────────────┼──────────────┼────────────────────────┼─────────────┼──────────────┼─────────┼───────────┤
│ INV-2026-0198│ PI-2026-0830 │ Arabtec Construction   │ 12 Aug 2026 │ AED 320,000  │ 650 pcs │ Paid      │
│              │ JO-2026-0302 │                        │             │ (Right)      │ (Right) │ [Green]   │
└──────────────┴──────────────┴────────────────────────┴─────────────┴──────────────┴─────────┴───────────┘
```

---

### Module 5: Company / Customer Report (`CompanyReportView`)
- **Mode Toggle**: `[ Customer View ]` vs `[ Company View ]`
- **Customer View Grid Widths**: `[ * | 130 | 70 | 70 | 70 | 120 | 120 ]`
- **Company View Grid Widths**: `[ * | 80 | 80 | 80 | 120 | 120 ]`

```
Customer View:
┌────────────────────────┬──────────────┬──────┬──────┬──────┬──────────────┬──────────────┐
│ CUSTOMER               │ SALESMAN     │ PIs  │ JOs  │ TIs  │ PI VALUE     │ OUTSTANDING  │
├────────────────────────┼──────────────┼──────┼──────┼──────┼──────────────┼──────────────┤
│ Al Futtaim Automotive  │ Juber Saleem │ 14   │ 12   │ 10   │ AED 840,000  │ AED 125,400  │
│ (Click to inspect lifecycle)          │(Ctr) │(Ctr) │(Ctr) │ (Right)      │ (Amber/Red)  │
└────────────────────────┴──────────────┴──────┴──────┴──────┴──────────────┴──────────────┘

Company View:
┌────────────────────────┬──────┬──────┬──────┬──────────────┬──────────────┐
│ COMPANY                │ PIs  │ JOs  │ TIs  │ PI VALUE     │ INVOICED     │
├────────────────────────┼──────┼──────┼──────┼──────────────┼──────────────┤
│ ProGlass Industries LLC│ 85   │ 72   │ 68   │ AED 4,250,000│ AED 3,890,000│
└────────────────────────┴──────┴──────┴──────┴──────────────┴──────────────┘
```

---

### Module 6: Glass / Specification Report (`GlassSpecReportView`)
- **Mode Toggle**: `[ Row Detail ]` vs `[ Aggregated by Spec ]`
- **Detail Grid Widths**: `[ 120 | 100 | * | 90 | 60 | 70 | 80 | 90 ]`
- **Aggregation Grid Widths**: `[ * | 80 | 70 | 80 | 80 | 90 | 80 ]`

```
Row Detail Mode:
┌──────────────┬──────────┬────────────────────────┬───────────┬─────┬───────┬──────────┬─────────┐
│ PI # / ROW ID│ GLASS REF│ SPECIFICATION          │ DIMENSIONS│ QTY │ SQM   │ DELIVERY │ TRACE   │
├──────────────┼──────────┼────────────────────────┼───────────┼─────┼───────┼──────────┼─────────┤
│ PI-0842-G1-S1│ GL-01    │ 6mm Clear Temp + 12A   │ 1200×2400 │ 45  │ 129.6 │ Complete │ [Trace] │
│              │          │ + 6mm Low-E Temp (DGU) │ (Center)  │(Rgt)│ (Rgt) │ [Green]  │ [Btn]   │
└──────────────┴──────────┴────────────────────────┴───────────┴─────┴───────┴──────────┴─────────┘

Aggregated Mode:
┌──────────────────────────────────────┬───────┬──────┬──────┬──────┬────────────┬───────────┐
│ SPECIFICATION (CANONICAL)            │ TYPE  │ ROWS │ QTY  │ SQM  │ VALUE      │ BALANCE   │
├──────────────────────────────────────┼───────┼──────┼──────┼──────┼────────────┼───────────┤
│ 6mm Clear + 12A + 6mm Sunergy Low-E  │ DGU   │ 18   │ 340  │ 980.4│ AED 285,000│ 45 pcs    │
└──────────────────────────────────────┴───────┴──────┴──────┴──────┴────────────┴───────────┘
```

---

### Module 7: Profitability / P&L Report (`ProfitabilityReportView`)
- **KPIs**: REVENUE (`Total Net Ex VAT`) · PROFIT (`Gross Profit`) · WITH COST (`Calculated count`) · MARGIN (`Average %`)
- **Grid Column Widths**: `[ 130 | * | 110 | 90 | 110 | 140 ]`

```
┌──────────────┬────────────────────────┬──────────────┬────────┬─────────────┬──────────────┐
│ DOCUMENT     │ REVENUE / COST         │ PROFIT       │ MARGIN │ QTY / SQM   │ COST SOURCE  │
├──────────────┼────────────────────────┼──────────────┼────────┼─────────────┼──────────────┤
│ PI-2026-0842 │ AED 141,190 / 92,400   │ AED 48,790   │ 34.6%  │ 420 pcs /   │ Selling price│
│              │ Glass 62K + Prod 30.4K │ (Success)    │ (Right)│ 680.5 SQM   │(Wastage 10%) │
├──────────────┼────────────────────────┼──────────────┼────────┼─────────────┼──────────────┤
│ PI-2026-0839 │ AED 200,000 / 140,000  │ AED 60,000   │ 30.0%  │ 540 pcs /   │ Purchase rate│
│              │ Glass 95K + Prod 45K   │ (Success)    │ (Right)│ 920.0 SQM   │(Wastage 8%)  │
└──────────────┴────────────────────────┴──────────────┴────────┴─────────────┴──────────────┘
```

---

### Module 8: Salesman Report (`SalesmanReportView`)
- **Grid Column Widths**: `[ * | 80 | 110 | 80 | 90 | 120 | 90 | 80 ]`

```
┌──────────────┬──────┬──────────────┬──────┬───────────┬──────────────┬──────────┬──────┐
│ SALESMAN     │ PIs  │ CONFIRMED    │ JOs  │ CUSTOMERS │ REVENUE      │ PROJECTS │ TIs  │
├──────────────┼──────┼──────────────┼──────┼───────────┼──────────────┼──────────┼──────┤
│ Juber Saleem │ 42   │ AED 1,850,000│ 38   │ 18        │ AED 1,620,000│ 12       │ 34   │
│              │(Ctr) │ (Right)      │(Ctr) │ (Ctr)     │ (Success)    │ (Ctr)    │(Ctr) │
└──────────────┴──────┴──────────────┴──────┴───────────┴──────────────┴──────────┴──────┘
```

---

### Module 9: Project Report (`ProjectReportView`)
- **Grid Column Widths**: `[ * | 140 | 120 | 70 | 120 | 120 ]`

```
┌────────────────────────┬─────────────────┬──────────────┬──────┬──────────────┬──────────────┐
│ PROJECT                │ CUSTOMER        │ SALESMAN     │ PIs  │ PI VALUE     │ INVOICED     │
├────────────────────────┼─────────────────┼──────────────┼──────┼──────────────┼──────────────┤
│ Dubai Mall Expansion   │ Al Futtaim Auto │ Juber Saleem │ 6    │ AED 640,000  │ AED 580,000  │
│ Contract: PRJ-2026-99  │                 │              │(Ctr) │ (Right)      │ (Success)    │
└────────────────────────┴─────────────────┴──────────────┴──────┴──────────────┴──────────────┘
```

---

### Module 10: Statement Report (`StatementReportView`)
- **KPIs**: STATEMENTS (`Total generated`) · INVOICED (`Across statements`) · OUTSTANDING (`Outstanding / Overdue`)
- **Grid Column Widths**: `[ 140 | * | 100 | 120 | 120 | 100 ]`

```
┌───────────────┬────────────────────────┬──────────┬──────────────┬──────────────┬─────────────┐
│ STATEMENT #   │ CUSTOMER / COMPANY     │ INVOICES │ INVOICED     │ OUTSTANDING  │ DATE        │
├───────────────┼────────────────────────┼──────────┼──────────────┼──────────────┼─────────────┤
│ STMT-2026-004 │ Al Futtaim Automotive  │ 8        │ AED 520,000  │ AED 112,500  │ 15 Aug 2026 │
│               │ ProGlass Industries    │ (Center) │ (Right)      │ (Amber/Red)  │ (Muted)     │
└───────────────┴────────────────────────┴──────────┴──────────────┴──────────────┴─────────────┘
```

---

## 4. Shared Style Token Mapping (`Themes/Styles/ReportDataGridStyles.xaml`)

```xml
<!-- Outer Table Card -->
<Style x:Key="ReportGridShellStyle" TargetType="Border">
    <Setter Property="Background" Value="{DynamicResource ModuleBackgroundBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource OutlineBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="12"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
</Style>

<!-- Pale Table Header -->
<Style x:Key="ReportGridHeaderStyle" TargetType="Border">
    <Setter Property="Background" Value="{DynamicResource MenuHoverBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource DividerBrush}"/>
    <Setter Property="BorderThickness" Value="0,0,0,1"/>
    <Setter Property="Padding" Value="16,10"/>
</Style>

<!-- Row Hover Surface -->
<Style x:Key="ReportGridRowStyle" TargetType="Border">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="{DynamicResource DividerBrush}"/>
    <Setter Property="BorderThickness" Value="0,0,0,1"/>
    <Setter Property="Padding" Value="16,11"/>
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="{DynamicResource MenuHoverBrush}"/>
        </Trigger>
    </Style.Triggers>
</Style>
```

---

## 5. Summary & Approval Readiness

| Item | Current State | Mockup Target |
|---|---|---|
| Left Nav Buttons | Previously showed "Report" fallback | **Fixed**: Explicit module titles, icons, and categories |
| Table Headers | Inconsistent column widths | **Fixed**: Standardized pixel + 1-star column definitions |
| Alignment | Varied padding across views | **Standardized**: `Padding="16,10"` header, `16,11` rows |
| Data Binding | Resolved getter crashes | **Fixed**: Explicit `Mode=OneWay` on all `<Run>` elements |
| Tooltips | Previously absent on controls | **Added**: On all search, date pickers, combos, exports, buttons |
| XAML Validation | 0 compiler errors | `xaml-guard` & `verify_all.sh` 100% clean |
