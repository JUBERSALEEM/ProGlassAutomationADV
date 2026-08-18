# Dashboard Module — Phase 8.1

## Summary
Multi-page dashboard shell with underline tab navigation.
Each module gets its own dashboard page with KPI cards and charts.
All charts are pure WPF (no NuGet) using ChartBarModel + ScaleConverter.

## Folder Structure

    Models/Dashboard/
        KpiCardModel.cs                     shared KPI card POCO
        ChartBarModel.cs                    shared chart bar POCO

    ViewModels/Dashboard/
        DashboardPageBase.cs                base class: LoadAsync, IsLoading, HasError
        DashboardViewModel.cs               shell: Pages, ActivePage, SelectTabCommand
        Overview/
            OverviewDashboardViewModel.cs   invoices + sheets KPIs + monthly sales chart
        SheetStore/
            SheetStoreDashboardViewModel.cs sheet KPIs + category SQM chart
        ProformaInvoice/
            PiDashboardViewModel.cs         invoice KPIs + status bars + top customers

    Views/Dashboard/
        DashboardView.xaml / .cs            shell: underline tab strip + ContentPresenter
        Overview/
            OverviewDashboardView.xaml/.cs  business overview page
        SheetStore/
            SheetStoreDashboardView.xaml/.cs sheet store page
        ProformaInvoice/
            PiDashboardView.xaml/.cs        proforma invoice page
        README.md

    Converters/ (new — registered in App.xaml)
        ReferenceEqualityConverter.cs       active tab detection (MultiBinding)
        ScaleConverter.cs                   NormalizedHeight × param → px height
        InverseBoolToVisibilityConverter.cs true→Collapsed for empty states

## Tab Navigation
    Underline style tabs — accent color line + SemiBold text on active tab.
    ReferenceEqualityConverter: MultiBinding (ActivePage, item) → bool → DataTrigger.
    SelectTabCommand sets ActivePage → triggers LoadAsync on the new sub-page.

## Lazy Loading
    Sub-page LoadAsync fires only when its tab is selected.
    DashboardPageBase uses Interlocked reentrancy guard.
    OnViewLoaded() refreshes active page on every navigation to Dashboard.

## Charts (pure WPF)
    Shared kit:       Docs/DASHBOARD_2050_KIT.md
                      TrendChartControl + MiniBarChart + DonutChartControl
                      ChartSliceModel for donuts (Accent = Green|Blue|Amber|Red)
    Overview:         Monthly Sales bars + Delivery mix donut
    Sheet Store:      Available SQM per category — vertical bars
    Proforma Invoice: Status distribution bars + Top 5 customers bars

    ChartBarModel.NormalizedHeight (0.0–1.0) × ScaleConverter param = px height.
    Minimum bar height enforced at 4px.
    Highlight bar gets DropShadowEffect glow.

## Converters Registered in App.xaml
    BoolToVisibilityConverter           built-in WPF
    ColorStringConverter                hex string → SolidColorBrush
    DecimalIsPositiveConverter          existing
    ReferenceEqualityConverter          NEW Phase 8.1
    ScaleConverter                      NEW Phase 8.1
    InverseBoolToVisibilityConverter    NEW Phase 8.1

## Adding a New Module Dashboard
    1. Create ViewModels/Dashboard/YourModule/YourModuleDashboardViewModel.cs
       extends DashboardPageBase — override TabTitle, TabIcon, LoadCoreAsync()
    2. Create Views/Dashboard/YourModule/YourModuleDashboardView.xaml + .cs
    3. Add DataTemplate in DashboardView.xaml Resources
    4. Add instance to DashboardViewModel Pages list

## Low Stock Threshold
    Balance <= 5 in OverviewDashboardViewModel + SheetStoreDashboardViewModel.
    Change both to adjust.

## Files Deleted vs Replaced
    DELETED (old flat Phase 8.1):
        ViewModels/Dashboard/DashboardViewModel.cs  (old version with ProformaInvoiceVM dep)
        Views/Dashboard/DashboardView.xaml           (old flat 4-card layout)
        Views/Dashboard/DashboardView.xaml.cs        (old code-behind)

    REPLACED BY this multi-page architecture — all 21 files above.