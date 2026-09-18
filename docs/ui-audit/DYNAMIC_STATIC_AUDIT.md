# Dynamic/Static Conversion Audit (MERIDIAN Phases 0–7)

**Generated:** 2026-09-18

Every rewritten view was diffed against its pre-phase baseline. The table below lists every OLD DynamicResource token that the new view no longer consumes dynamically, grouped by owner. Each row is classified:

- **PARITY** — the new view consumes the same owner through a dynamic Pg* token.
- **MIXED** — some consumers dynamic, some static.
- **STATIC-ONLY (policy violation)** — owner was dynamic before and is now static: density/theme changes no longer affect it. Requires root-cause fix or explicit documented waiver.
- **INTENTIONAL-STATIC** — structural constant, documented in the phase commit.

## Per-view verdicts

### `MainWindow.xaml`

| Owner | old dyn→new dynamic | old dyn→now static | old dyn→gone | Verdict |
|---|---|---|---|---|
| typography | 3 | 0 | 11 | DYNAMIC-PARITY |
| radii | 5 | 0 | 3 | DYNAMIC-PARITY |
| borders | 0 | 0 | 11 | GONE: check literals |
| colors | 27 | 0 | 22 | DYNAMIC-PARITY |
| effects | 1 | 0 | 0 | DYNAMIC-PARITY |

### `DashboardView.xaml`

| Owner | old dyn→new dynamic | old dyn→now static | old dyn→gone | Verdict |
|---|---|---|---|---|
| typography | 4 | 0 | 6 | DYNAMIC-PARITY |
| radii | 0 | 0 | 5 | GONE: check literals |
| borders | 0 | 0 | 2 | GONE: check literals |
| colors | 12 | 0 | 2 | DYNAMIC-PARITY |

### `OverviewDashboardView.xaml`

| Owner | old dyn→new dynamic | old dyn→now static | old dyn→gone | Verdict |
|---|---|---|---|---|
| typography | 0 | 0 | 3 | GONE: check literals |
| margins | 1 | 0 | 0 | DYNAMIC-PARITY |
| colors | 15 | 0 | 1 | DYNAMIC-PARITY |

### `SheetStoreDashboardView.xaml`

| Owner | old dyn→new dynamic | old dyn→now static | old dyn→gone | Verdict |
|---|---|---|---|---|
| typography | 0 | 0 | 2 | GONE: check literals |
| margins | 1 | 0 | 0 | DYNAMIC-PARITY |
| radii | 0 | 0 | 3 | GONE: check literals |
| colors | 7 | 0 | 3 | DYNAMIC-PARITY |

### `PiDashboardView.xaml`

| Owner | old dyn→new dynamic | old dyn→now static | old dyn→gone | Verdict |
|---|---|---|---|---|
| typography | 0 | 0 | 2 | GONE: check literals |
| margins | 1 | 0 | 0 | DYNAMIC-PARITY |
| radii | 0 | 0 | 3 | GONE: check literals |
| colors | 11 | 0 | 3 | DYNAMIC-PARITY |

### `JoDashboardView.xaml`

| Owner | old dyn→new dynamic | old dyn→now static | old dyn→gone | Verdict |
|---|---|---|---|---|
| typography | 0 | 0 | 2 | GONE: check literals |
| margins | 1 | 0 | 0 | DYNAMIC-PARITY |
| radii | 0 | 0 | 3 | GONE: check literals |
| colors | 11 | 0 | 3 | DYNAMIC-PARITY |

### `DnDashboardView.xaml`

| Owner | old dyn→new dynamic | old dyn→now static | old dyn→gone | Verdict |
|---|---|---|---|---|
| typography | 0 | 0 | 2 | GONE: check literals |
| margins | 1 | 0 | 0 | DYNAMIC-PARITY |
| radii | 0 | 0 | 3 | GONE: check literals |
| colors | 11 | 0 | 3 | DYNAMIC-PARITY |

### `TiDashboardView.xaml`

| Owner | old dyn→new dynamic | old dyn→now static | old dyn→gone | Verdict |
|---|---|---|---|---|
| typography | 8 | 0 | 15 | DYNAMIC-PARITY |
| margins | 2 | 0 | -1 | DYNAMIC-PARITY |
| radii | 2 | 1 | 11 | MIXED (dynamic+static) |
| borders | 0 | 0 | 1 | GONE: check literals |
| colors | 22 | 8 | 8 | MIXED (dynamic+static) |

### `StDashboardView.xaml`

| Owner | old dyn→new dynamic | old dyn→now static | old dyn→gone | Verdict |
|---|---|---|---|---|
| typography | 11 | 0 | 8 | DYNAMIC-PARITY |
| margins | 2 | 0 | -1 | DYNAMIC-PARITY |
| radii | 1 | 1 | 5 | MIXED (dynamic+static) |
| borders | 0 | 0 | 1 | GONE: check literals |
| colors | 23 | 0 | 2 | DYNAMIC-PARITY |

## Key-level: old Dynamic tokens no longer dynamic (top moves)

Old dynamic keys and their fate in the new file (D=dynamic, S=static, G=gone):

### `MainWindow.xaml`

| Old Dynamic key | old count | new state |
|---|---|---|
| AccentBrush | 5 | D (Pg-era: now 2) |
| AccentColor | 1 | G (replaced by Pg* token/literal) |
| AccentLightColor | 1 | G (replaced by Pg* token/literal) |
| AppBackgroundBrush | 1 | D (Pg-era: now 1) |
| AppFontFamily | 1 | D (Pg-era: now 1) |
| AppFontStyle | 1 | D (Pg-era: now 1) |
| AppFontWeight | 1 | D (Pg-era: now 1) |
| AvatarGradient | 2 | G (replaced by Pg* token/literal) |
| Border.BottomHairlineThickness | 1 | G (replaced by Pg* token/literal) |
| Border.HairlineThickness | 5 | D (Pg-era: now 1) |
| Border.NoneThickness | 3 | D (Pg-era: now 2) |
| Border.StrongThickness | 1 | D (Pg-era: now 1) |
| BorderBrush | 3 | G (replaced by Pg* token/literal) |
| CardBorderThickness | 1 | D (Pg-era: now 1) |
| CardCornerRadius | 2 | D (Pg-era: now 2) |
| CardCornerRadiusLarge | 3 | D (Pg-era: now 2) |
| ControlCornerRadius | 1 | G (replaced by Pg* token/literal) |
| DangerBrush | 4 | G (replaced by Pg* token/literal) |
| DateTimeIconGradient | 1 | G (replaced by Pg* token/literal) |
| DividerBrush | 1 | D (Pg-era: now 1) |
| FontSize.Body | 6 | D (Pg-era: now 1) |
| FontSize.BodySmall | 1 | G (replaced by Pg* token/literal) |
| FontSize.Caption | 2 | G (replaced by Pg* token/literal) |
| FontSize.H2 | 1 | D (Pg-era: now 1) |
| FontSize.H3 | 1 | G (replaced by Pg* token/literal) |
| FontSize.Title | 3 | D (Pg-era: now 1) |
| GlassBorderGradientCrystal | 1 | G (replaced by Pg* token/literal) |
| GlassBorderGradientSoft | 1 | G (replaced by Pg* token/literal) |
| GlassBorderSoft | 1 | D (Pg-era: now 1) |
| GlassSpecularHighlight | 1 | G (replaced by Pg* token/literal) |
| LiquidOverlayBrush | 1 | D (Pg-era: now 1) |
| MenuHoverBrush | 2 | G (replaced by Pg* token/literal) |
| ModuleBackgroundBrush | 3 | D (Pg-era: now 3) |
| ModuleSkinBlurEffect | 1 | D (Pg-era: now 1) |
| ModuleWallpaperBrush | 1 | D (Pg-era: now 1) |
| MutedForegroundBrush | 3 | G (replaced by Pg* token/literal) |
| Nav.CompanyCenter | 1 | D (Pg-era: now 1) |
| Nav.Cutting | 1 | D (Pg-era: now 1) |
| Nav.Dashboard | 1 | D (Pg-era: now 1) |
| Nav.Delivery | 1 | D (Pg-era: now 1) |
| Nav.DeliveryNote | 1 | D (Pg-era: now 1) |
| Nav.Dispatch | 1 | D (Pg-era: now 1) |
| Nav.DoublingLamination | 1 | D (Pg-era: now 1) |
| Nav.EdgeDeletion | 1 | D (Pg-era: now 1) |
| Nav.FinanceAccounting | 1 | D (Pg-era: now 1) |
| Nav.GroupDispatch | 1 | D (Pg-era: now 1) |
| Nav.GroupFinance | 1 | D (Pg-era: now 1) |
| Nav.GroupInventory | 1 | D (Pg-era: now 1) |
| Nav.GroupMain | 1 | D (Pg-era: now 1) |
| Nav.GroupMasterData | 1 | D (Pg-era: now 1) |
| Nav.GroupProduction | 1 | D (Pg-era: now 1) |
| Nav.GroupReports | 1 | D (Pg-era: now 1) |
| Nav.GroupSales | 1 | D (Pg-era: now 1) |
| Nav.GroupSystem | 1 | D (Pg-era: now 1) |
| Nav.HolesCutouts | 1 | D (Pg-era: now 1) |
| Nav.Inquiry | 1 | D (Pg-era: now 1) |
| Nav.JobOrder | 2 | D (Pg-era: now 2) |
| Nav.JobProfitability | 1 | D (Pg-era: now 1) |
| Nav.LogsActivity | 1 | D (Pg-era: now 1) |
| Nav.MasterDataItems | 1 | D (Pg-era: now 1) |
| Nav.Mitring | 1 | D (Pg-era: now 1) |
| Nav.Optimization | 1 | D (Pg-era: now 1) |
| Nav.Overview | 1 | D (Pg-era: now 1) |
| Nav.Pairing | 1 | D (Pg-era: now 1) |
| Nav.Polish | 1 | D (Pg-era: now 1) |
| Nav.ProductionItems | 1 | D (Pg-era: now 1) |
| Nav.ProformaInvoice | 2 | D (Pg-era: now 2) |
| Nav.Reports | 1 | D (Pg-era: now 1) |
| Nav.SalesOrders | 1 | D (Pg-era: now 1) |
| Nav.SheetStore | 1 | D (Pg-era: now 1) |
| Nav.Specifications | 1 | D (Pg-era: now 1) |
| Nav.Statement | 1 | D (Pg-era: now 1) |
| Nav.StatementGenerator | 1 | D (Pg-era: now 1) |
| Nav.System | 1 | D (Pg-era: now 1) |
| Nav.SystemCenter | 1 | D (Pg-era: now 1) |
| Nav.TaxInvoice | 2 | D (Pg-era: now 2) |
| Nav.Tempering | 1 | D (Pg-era: now 1) |
| Nav.UserCenter | 1 | D (Pg-era: now 1) |
| Nav.VatSubmission | 1 | D (Pg-era: now 1) |
| OnAccentForegroundBrush | 7 | D (Pg-era: now 1) |
| PrimaryForegroundBrush | 5 | D (Pg-era: now 3) |
| Radius.None | 1 | D (Pg-era: now 1) |
| Radius.XSmall | 1 | G (replaced by Pg* token/literal) |
| SecondaryForegroundBrush | 1 | G (replaced by Pg* token/literal) |
| Shell.ActivateLicense | 1 | D (Pg-era: now 1) |
| Shell.AppBrand | 1 | D (Pg-era: now 1) |
| Shell.AppSuiteTagline | 2 | D (Pg-era: now 2) |
| Shell.AppTitle | 1 | D (Pg-era: now 1) |
| Shell.DrawerClose | 1 | D (Pg-era: now 1) |
| Shell.Menu | 1 | D (Pg-era: now 1) |
| Shell.ReadOnly | 1 | D (Pg-era: now 1) |
| ShellLiquidBrush | 1 | G (replaced by Pg* token/literal) |
| ShellSheenBrush | 1 | G (replaced by Pg* token/literal) |
| SuccessBrush | 1 | G (replaced by Pg* token/literal) |
| Surface.Primary.BackgroundBrush | 2 | G (replaced by Pg* token/literal) |
| TintAmberBrush | 1 | D (Pg-era: now 1) |
| TintBlueBrush | 1 | G (replaced by Pg* token/literal) |
| ToolTip.About | 1 | D (Pg-era: now 1) |
| ToolTip.Agent | 1 | D (Pg-era: now 1) |
| ToolTip.AiChat | 1 | D (Pg-era: now 1) |
| ToolTip.DateTime | 1 | D (Pg-era: now 1) |
| ToolTip.NetworkState | 1 | D (Pg-era: now 1) |
| ToolTip.RecentActivity | 1 | D (Pg-era: now 1) |
| ToolTip.ScreenRecording | 1 | D (Pg-era: now 1) |
| ToolTip.Screenshot | 1 | D (Pg-era: now 1) |
| ToolTip.Settings | 1 | D (Pg-era: now 1) |
| ToolTip.SwitchUser | 1 | D (Pg-era: now 1) |
| ToolTip.ToggleMenu | 1 | D (Pg-era: now 1) |
| ToolTip.ToggleTheme | 1 | D (Pg-era: now 1) |
| WarningBrush | 2 | D (Pg-era: now 2) |
| WindowBackgroundBrush | 1 | D (Pg-era: now 1) |

### `DashboardView.xaml`

| Old Dynamic key | old count | new state |
|---|---|---|
| AccentBrush | 2 | G (replaced by Pg* token/literal) |
| AppFontFamily | 1 | D (Pg-era: now 1) |
| Border.NoneThickness | 1 | D (Pg-era: now 1) |
| BorderBrush | 1 | G (replaced by Pg* token/literal) |
| CardBorderThickness | 1 | G (replaced by Pg* token/literal) |
| Dashboard.LastRefreshActiveModule | 1 | D (Pg-era: now 1) |
| FontFamily.Icon | 2 | G (replaced by Pg* token/literal) |
| FontSize.Body | 4 | G (replaced by Pg* token/literal) |
| FontSize.BodySmall | 2 | G (replaced by Pg* token/literal) |
| OnAccentForegroundBrush | 2 | G (replaced by Pg* token/literal) |
| SecondaryForegroundBrush | 2 | G (replaced by Pg* token/literal) |
| Surface.Primary.CornerRadius | 5 | G (replaced by Pg* token/literal) |
| Surface.Secondary.BackgroundBrush | 1 | G (replaced by Pg* token/literal) |
| Surface.Secondary.BorderBrush | 1 | G (replaced by Pg* token/literal) |
| SurfaceTertiaryBrush | 1 | G (replaced by Pg* token/literal) |
| TextPrimaryBrush | 2 | G (replaced by Pg* token/literal) |
| TextTertiaryBrush | 2 | G (replaced by Pg* token/literal) |
| Weight.Bold | 1 | G (replaced by Pg* token/literal) |
| Weight.SemiBold | 3 | G (replaced by Pg* token/literal) |

### `OverviewDashboardView.xaml`

| Old Dynamic key | old count | new state |
|---|---|---|
| AccentBrush | 2 | G (replaced by Pg* token/literal) |
| AccentDarkBrush | 2 | G (replaced by Pg* token/literal) |
| AppFontFamily | 1 | D (Pg-era: now 1) |
| Common.Live | 1 | D (Pg-era: now 1) |
| Common.Overdue | 1 | D (Pg-era: now 1) |
| DangerBrush | 3 | G (replaced by Pg* token/literal) |
| Dashboard.CreateNewTaxInvoice | 1 | D (Pg-era: now 1) |
| Dashboard.Deliveries | 1 | D (Pg-era: now 1) |
| Dashboard.DeliveryMix | 1 | D (Pg-era: now 1) |
| Dashboard.FactoryPulse | 2 | D (Pg-era: now 2) |
| Dashboard.Jobs | 1 | D (Pg-era: now 1) |
| Dashboard.NewInvoice | 1 | D (Pg-era: now 1) |
| Dashboard.NotesRemainingPcs | 1 | D (Pg-era: now 1) |
| Dashboard.OperationalAnalyticsInvoicesJobsDelivery | 1 | D (Pg-era: now 1) |
| Dashboard.PendingPCS | 1 | D (Pg-era: now 1) |
| Dashboard.PipelineVolumeConfirmation | 1 | D (Pg-era: now 1) |
| Dashboard.Production | 1 | D (Pg-era: now 1) |
| Dashboard.RecentDocuments | 1 | D (Pg-era: now 1) |
| Dashboard.RevenueTrend | 1 | D (Pg-era: now 1) |
| Dashboard.ViewDeliveryNotes | 1 | D (Pg-era: now 1) |
| Dashboard.ViewJobOrders | 1 | D (Pg-era: now 1) |
| DbPageMargin | 1 | G (replaced by Pg* token/literal) |
| DividerBrush | 1 | G (replaced by Pg* token/literal) |
| FontSize.Body | 1 | G (replaced by Pg* token/literal) |
| FontSize.BodySmall | 1 | G (replaced by Pg* token/literal) |
| InfoBrush | 2 | G (replaced by Pg* token/literal) |
| Module.Production | 1 | D (Pg-era: now 1) |
| Module.SheetStore | 1 | D (Pg-era: now 1) |
| Nav.Delivery | 1 | D (Pg-era: now 1) |
| Nav.GroupSales | 1 | D (Pg-era: now 1) |
| SuccessBrush | 2 | G (replaced by Pg* token/literal) |
| TextTertiaryBrush | 1 | G (replaced by Pg* token/literal) |
| TintBlueBrush | 1 | G (replaced by Pg* token/literal) |
| TintRedBrush | 1 | G (replaced by Pg* token/literal) |
| WarningBrush | 1 | G (replaced by Pg* token/literal) |
| Weight.SemiBold | 1 | G (replaced by Pg* token/literal) |

### `SheetStoreDashboardView.xaml`

| Old Dynamic key | old count | new state |
|---|---|---|
| AccentBrush | 1 | G (replaced by Pg* token/literal) |
| AccentDarkBrush | 1 | G (replaced by Pg* token/literal) |
| AppFontFamily | 1 | D (Pg-era: now 1) |
| Common.Live | 1 | D (Pg-era: now 1) |
| DangerBrush | 1 | G (replaced by Pg* token/literal) |
| Dashboard.AvailableSQMCategory | 1 | D (Pg-era: now 1) |
| Dashboard.AvailableSQMGlassCategory | 1 | D (Pg-era: now 1) |
| Dashboard.BalanceSheets | 1 | D (Pg-era: now 1) |
| Dashboard.CategoryMix | 1 | D (Pg-era: now 1) |
| Dashboard.LiveSnapshotGlassSheetStockValueCoverage | 1 | D (Pg-era: now 1) |
| Dashboard.NoSheetStoreDataYet | 1 | D (Pg-era: now 1) |
| Dashboard.StockPulse | 1 | D (Pg-era: now 1) |
| Dashboard.TypesValueCoverageAlerts | 1 | D (Pg-era: now 1) |
| DbPageMargin | 1 | G (replaced by Pg* token/literal) |
| DividerBrush | 1 | G (replaced by Pg* token/literal) |
| FontSize.BodySmall | 1 | G (replaced by Pg* token/literal) |
| InfoBrush | 1 | G (replaced by Pg* token/literal) |
| Module.SheetStore | 1 | D (Pg-era: now 1) |
| SuccessBrush | 2 | G (replaced by Pg* token/literal) |
| Surface.Primary.CornerRadius | 3 | G (replaced by Pg* token/literal) |
| TextTertiaryBrush | 1 | G (replaced by Pg* token/literal) |
| TintBlueBrush | 1 | G (replaced by Pg* token/literal) |
| TintRedBrush | 1 | G (replaced by Pg* token/literal) |
| Weight.SemiBold | 1 | G (replaced by Pg* token/literal) |

### `PiDashboardView.xaml`

| Old Dynamic key | old count | new state |
|---|---|---|
| AccentBrush | 2 | G (replaced by Pg* token/literal) |
| AccentDarkBrush | 2 | G (replaced by Pg* token/literal) |
| AppFontFamily | 1 | D (Pg-era: now 1) |
| Common.Live | 1 | D (Pg-era: now 1) |
| DangerBrush | 1 | G (replaced by Pg* token/literal) |
| Dashboard.CountQuotes | 1 | D (Pg-era: now 1) |
| Dashboard.InvoicesGroupedCurrentStatus | 1 | D (Pg-era: now 1) |
| Dashboard.NoCustomerDataYet | 1 | D (Pg-era: now 1) |
| Dashboard.NoInvoicesYet | 1 | D (Pg-era: now 1) |
| Dashboard.PipelineVolumeConfirmation | 1 | D (Pg-era: now 1) |
| Dashboard.QuotePipelineStatusDistributionTopCustomers | 1 | D (Pg-era: now 1) |
| Dashboard.QuotePulse | 1 | D (Pg-era: now 1) |
| Dashboard.RankedTotalInvoiceValueVATInclusive | 1 | D (Pg-era: now 1) |
| Dashboard.Status | 1 | D (Pg-era: now 1) |
| Dashboard.StatusDistribution | 1 | D (Pg-era: now 1) |
| Dashboard.StatusMix | 1 | D (Pg-era: now 1) |
| Dashboard.TopCustomers | 1 | D (Pg-era: now 1) |
| DbPageMargin | 1 | G (replaced by Pg* token/literal) |
| DividerBrush | 2 | G (replaced by Pg* token/literal) |
| FontSize.BodySmall | 1 | G (replaced by Pg* token/literal) |
| InfoBrush | 1 | G (replaced by Pg* token/literal) |
| Module.ProformaInvoice | 1 | D (Pg-era: now 1) |
| SuccessBrush | 2 | G (replaced by Pg* token/literal) |
| Surface.Primary.CornerRadius | 3 | G (replaced by Pg* token/literal) |
| TextTertiaryBrush | 2 | G (replaced by Pg* token/literal) |
| TintBlueBrush | 1 | G (replaced by Pg* token/literal) |
| TintRedBrush | 1 | G (replaced by Pg* token/literal) |
| Weight.SemiBold | 1 | G (replaced by Pg* token/literal) |

### `JoDashboardView.xaml`

| Old Dynamic key | old count | new state |
|---|---|---|
| AccentBrush | 1 | G (replaced by Pg* token/literal) |
| AccentDarkBrush | 2 | G (replaced by Pg* token/literal) |
| AppFontFamily | 1 | D (Pg-era: now 1) |
| Common.Live | 1 | D (Pg-era: now 1) |
| Common.Priority | 1 | D (Pg-era: now 1) |
| DangerBrush | 1 | G (replaced by Pg* token/literal) |
| Dashboard.HighMediumLow | 1 | D (Pg-era: now 1) |
| Dashboard.JobOrderCount | 1 | D (Pg-era: now 1) |
| Dashboard.JobOrders | 1 | D (Pg-era: now 1) |
| Dashboard.Jobs | 1 | D (Pg-era: now 1) |
| Dashboard.JobsGroupedCurrentStatus | 1 | D (Pg-era: now 1) |
| Dashboard.LatestActivity | 1 | D (Pg-era: now 1) |
| Dashboard.LiveJobCountsFloor | 1 | D (Pg-era: now 1) |
| Dashboard.NoJobOrdersYet | 3 | D (Pg-era: now 3) |
| Dashboard.ProductionPulse | 1 | D (Pg-era: now 1) |
| Dashboard.ProductionWorkloadPriorityMixRecentJob | 1 | D (Pg-era: now 1) |
| Dashboard.RecentJobOrders | 1 | D (Pg-era: now 1) |
| Dashboard.StatusMix | 1 | D (Pg-era: now 1) |
| Dashboard.WorkloadStatus | 1 | D (Pg-era: now 1) |
| DbPageMargin | 1 | G (replaced by Pg* token/literal) |
| DividerBrush | 2 | G (replaced by Pg* token/literal) |
| FontSize.BodySmall | 1 | G (replaced by Pg* token/literal) |
| InfoBrush | 1 | G (replaced by Pg* token/literal) |
| SuccessBrush | 2 | G (replaced by Pg* token/literal) |
| Surface.Primary.CornerRadius | 3 | G (replaced by Pg* token/literal) |
| TextTertiaryBrush | 2 | G (replaced by Pg* token/literal) |
| TintBlueBrush | 1 | G (replaced by Pg* token/literal) |
| TintRedBrush | 1 | G (replaced by Pg* token/literal) |
| WarningBrush | 1 | G (replaced by Pg* token/literal) |
| Weight.SemiBold | 1 | G (replaced by Pg* token/literal) |

### `DnDashboardView.xaml`

| Old Dynamic key | old count | new state |
|---|---|---|
| AccentBrush | 1 | G (replaced by Pg* token/literal) |
| AccentDarkBrush | 2 | G (replaced by Pg* token/literal) |
| AppFontFamily | 1 | D (Pg-era: now 1) |
| Common.Delivered | 1 | D (Pg-era: now 1) |
| Common.Live | 1 | D (Pg-era: now 1) |
| DangerBrush | 1 | G (replaced by Pg* token/literal) |
| Dashboard.Deliveries | 1 | D (Pg-era: now 1) |
| Dashboard.DeliveryMix | 1 | D (Pg-era: now 1) |
| Dashboard.DeliveryNoteCount | 1 | D (Pg-era: now 1) |
| Dashboard.DispatchFlowDeliveryBalanceRecentMovements | 1 | D (Pg-era: now 1) |
| Dashboard.DispatchPulse | 1 | D (Pg-era: now 1) |
| Dashboard.DummySplitMaster | 1 | D (Pg-era: now 1) |
| Dashboard.LatestActivity | 1 | D (Pg-era: now 1) |
| Dashboard.NoDeliveryNotesYet | 3 | D (Pg-era: now 3) |
| Dashboard.NotesGroupedCurrentStatus | 1 | D (Pg-era: now 1) |
| Dashboard.NotesPendingPcsReturns | 1 | D (Pg-era: now 1) |
| Dashboard.RecentDeliveries | 1 | D (Pg-era: now 1) |
| Dashboard.StatusDistribution | 1 | D (Pg-era: now 1) |
| Dashboard.Type | 1 | D (Pg-era: now 1) |
| DbPageMargin | 1 | G (replaced by Pg* token/literal) |
| DividerBrush | 2 | G (replaced by Pg* token/literal) |
| FontSize.BodySmall | 1 | G (replaced by Pg* token/literal) |
| InfoBrush | 2 | G (replaced by Pg* token/literal) |
| SuccessBrush | 2 | G (replaced by Pg* token/literal) |
| Surface.Primary.CornerRadius | 3 | G (replaced by Pg* token/literal) |
| TextTertiaryBrush | 2 | G (replaced by Pg* token/literal) |
| TintBlueBrush | 1 | G (replaced by Pg* token/literal) |
| TintRedBrush | 1 | G (replaced by Pg* token/literal) |
| Weight.SemiBold | 1 | G (replaced by Pg* token/literal) |

### `TiDashboardView.xaml`

| Old Dynamic key | old count | new state |
|---|---|---|
| AccentBrush | 1 | G (replaced by Pg* token/literal) |
| AccentDarkBrush | 1 | G (replaced by Pg* token/literal) |
| AppFontFamily | 1 | D (Pg-era: now 1) |
| Border.HairlineThickness | 1 | G (replaced by Pg* token/literal) |
| BorderBrush | 2 | G (replaced by Pg* token/literal) |
| BorderSubtleBrush | 1 | G (replaced by Pg* token/literal) |
| Common.Live | 1 | D (Pg-era: now 1) |
| Common.Overdue | 1 | D (Pg-era: now 1) |
| DangerBrush | 4 | G (replaced by Pg* token/literal) |
| Dashboard.AgeingBrackets | 1 | D (Pg-era: now 1) |
| Dashboard.AgeingMix | 1 | D (Pg-era: now 1) |
| Dashboard.CollectionsPulse | 1 | D (Pg-era: now 1) |
| Dashboard.InvoicedAmountLast12Months | 1 | D (Pg-era: now 1) |
| Dashboard.InvoicedVATPaidPending | 1 | D (Pg-era: now 1) |
| Dashboard.InvoicesPastTerms | 1 | D (Pg-era: now 1) |
| Dashboard.LatestActivity | 1 | D (Pg-era: now 1) |
| Dashboard.NewInvoice | 2 | D (Pg-era: now 2) |
| Dashboard.NoCustomerData | 1 | D (Pg-era: now 1) |
| Dashboard.NoInvoicesYet | 1 | D (Pg-era: now 1) |
| Dashboard.NoOverdueInvoices | 1 | D (Pg-era: now 1) |
| Dashboard.NoRevenueDataYet | 1 | D (Pg-era: now 1) |
| Dashboard.OpenInvoice | 1 | D (Pg-era: now 1) |
| Dashboard.OverdueAED | 1 | D (Pg-era: now 1) |
| Dashboard.OverdueAmountsBracket | 1 | D (Pg-era: now 1) |
| Dashboard.OverdueAmountsGroupedAge | 1 | D (Pg-era: now 1) |
| Dashboard.RecentInvoices | 1 | D (Pg-era: now 1) |
| Dashboard.RevenueAgeingCollectionHealth | 1 | D (Pg-era: now 1) |
| Dashboard.RevenueTrend | 1 | D (Pg-era: now 1) |
| Dashboard.TaxInvoices | 1 | D (Pg-era: now 1) |
| Dashboard.TopCustomers | 1 | D (Pg-era: now 1) |
| Dashboard.View | 2 | D (Pg-era: now 2) |
| DbPageMargin | 1 | G (replaced by Pg* token/literal) |
| DividerBrush | 1 | G (replaced by Pg* token/literal) |
| FontSize.Body | 9 | G (replaced by Pg* token/literal) |
| FontSize.BodySmall | 4 | G (replaced by Pg* token/literal) |
| FontSize.H3 | 1 | G (replaced by Pg* token/literal) |
| InfoBrush | 1 | G (replaced by Pg* token/literal) |
| OnAccentForegroundBrush | 2 | G (replaced by Pg* token/literal) |
| Radius.XSmall | 4 | G (replaced by Pg* token/literal) |
| SecondaryForegroundBrush | 3 | G (replaced by Pg* token/literal) |
| SuccessBrush | 3 | G (replaced by Pg* token/literal) |
| Surface.Primary.BackgroundBrush | 1 | G (replaced by Pg* token/literal) |
| Surface.Primary.CornerRadius | 9 | G (replaced by Pg* token/literal) |
| TextPrimaryBrush | 5 | G (replaced by Pg* token/literal) |
| TextTertiaryBrush | 1 | G (replaced by Pg* token/literal) |
| TintBlueBrush | 1 | G (replaced by Pg* token/literal) |
| TintRedBrush | 2 | G (replaced by Pg* token/literal) |
| WarningBrush | 1 | G (replaced by Pg* token/literal) |
| Weight.Bold | 8 | G (replaced by Pg* token/literal) |
| Weight.SemiBold | 1 | G (replaced by Pg* token/literal) |

### `StDashboardView.xaml`

| Old Dynamic key | old count | new state |
|---|---|---|
| AccentBrush | 2 | G (replaced by Pg* token/literal) |
| AppFontFamily | 1 | D (Pg-era: now 1) |
| CardBorderThickness | 1 | G (replaced by Pg* token/literal) |
| Common.Invoiced | 1 | D (Pg-era: now 1) |
| Common.Live | 1 | D (Pg-era: now 1) |
| Common.Outstanding | 1 | D (Pg-era: now 1) |
| Common.Overdue | 2 | D (Pg-era: now 2) |
| DangerBrush | 3 | G (replaced by Pg* token/literal) |
| Dashboard.Attachments | 1 | D (Pg-era: now 1) |
| Dashboard.CollectedVsOutstandingVsOverdue | 1 | D (Pg-era: now 1) |
| Dashboard.CollectionMix | 1 | D (Pg-era: now 1) |
| Dashboard.CustomerBalancesOutstandingValueCollection | 1 | D (Pg-era: now 1) |
| Dashboard.GenerateNewCustomerStatement | 2 | D (Pg-era: now 2) |
| Dashboard.GenerateStatementSeeCustomerBalancesHere | 1 | D (Pg-era: now 1) |
| Dashboard.InvoiceS | 1 | D (Pg-era: now 1) |
| Dashboard.NewStatement | 2 | D (Pg-era: now 2) |
| Dashboard.NoStatementsYet | 1 | D (Pg-era: now 1) |
| Dashboard.OpenStatementPDF | 1 | D (Pg-era: now 1) |
| Dashboard.PastPaymentTerms | 1 | D (Pg-era: now 1) |
| Dashboard.RecentStatements | 1 | D (Pg-era: now 1) |
| Dashboard.StatementPulse | 1 | D (Pg-era: now 1) |
| Dashboard.Statements | 1 | D (Pg-era: now 1) |
| Dashboard.StatementsInvoicedOutstandingOverdue | 1 | D (Pg-era: now 1) |
| Dashboard.View | 1 | D (Pg-era: now 1) |
| Dashboard.ViewGeneratedStatements | 1 | D (Pg-era: now 1) |
| DbPageMargin | 1 | G (replaced by Pg* token/literal) |
| FontFamily.Icon | 2 | G (replaced by Pg* token/literal) |
| FontSize.Body | 8 | G (replaced by Pg* token/literal) |
| FontSize.BodySmall | 3 | G (replaced by Pg* token/literal) |
| FontSize.Title | 1 | G (replaced by Pg* token/literal) |
| InfoBrush | 2 | G (replaced by Pg* token/literal) |
| OnAccentForegroundBrush | 2 | G (replaced by Pg* token/literal) |
| SecondaryForegroundBrush | 3 | G (replaced by Pg* token/literal) |
| Sharing.Share | 1 | D (Pg-era: now 1) |
| SuccessBrush | 2 | G (replaced by Pg* token/literal) |
| Surface.Primary.BackgroundBrush | 1 | G (replaced by Pg* token/literal) |
| Surface.Primary.BorderBrush | 1 | G (replaced by Pg* token/literal) |
| Surface.Primary.CornerRadius | 6 | G (replaced by Pg* token/literal) |
| TextPrimaryBrush | 2 | G (replaced by Pg* token/literal) |
| TextTertiaryBrush | 4 | G (replaced by Pg* token/literal) |
| TintBlueBrush | 1 | G (replaced by Pg* token/literal) |
| TintRedBrush | 1 | G (replaced by Pg* token/literal) |
| WarningBrush | 1 | G (replaced by Pg* token/literal) |
| Weight.Bold | 5 | G (replaced by Pg* token/literal) |
| Weight.SemiBold | 2 | G (replaced by Pg* token/literal) |

## Findings

1. **Typography (all 7 dashboards + module bar):** old views read `{DynamicResource FontSize.*}` / `Typography*` (scaled live by `ThemeManager.ApplyDisplayDensity`). New views read `{StaticResource PgFont*}` tokens that are never republished → dashboard type no longer follows the Density setting. **Verdict: STATIC-ONLY (policy violation) — root-cause fix required** (make PgFont* DynamicResource consumers and republish them in ApplyDisplayDensity).

2. **Corner radius (all 7 dashboards):** old cards read `{DynamicResource Surface.Primary.CornerRadius}` (driven by the Appearance radius slider). New cards use `{StaticResource PgCardRadius/PgTileRadius/PgCtrlRadius}` (authored 12/9/8). **Verdict: STATIC-ONLY (policy violation)** — the user-configurable radius no longer affects dashboards. Fix: republish the Pg radius scale from the radius authority, proportionally.

3. **Page margin / gutter / min-heights:** old `{DynamicResource DbPageMargin}` etc. → new `{DynamicResource PgPageMargin}` (fixed in Phase 7) + static `PgGutter`/`PgKpiTileMinHeight` inside shared styles (styles resolve at parse time; density ladder already republishes PgGutter/PgKpiTileMinHeight/PgChartMinHeight). **Verdict: page-margin PARITY; in-style gutter/min-heights MIXED — convert style consumers to DynamicResource for full live reflow.**

4. **Colours/brushes:** 100% of old DynamicResource brush tokens map to DynamicResource Pg* brush tokens (both themes). **Verdict: PARITY.**

5. **Hairline borders:** old `{DynamicResource Border.HairlineThickness}` (runtime density) → new literal `0,1,0,0` docrow hairlines. **Verdict: INTENTIONAL-STATIC** — 1px structural hairline, not theme-dependent; documented here as the waiver.

6. **Shadows/effects:** old card effects came from `SurfacePrimaryStyle` (`Surface.Primary.Effect`, runtime material engine). New `PgCardStyle` carries the Meridian card treatment. **Verdict: check — confirm PgCardStyle effect source (Dynamic token vs baked-in effect).**
