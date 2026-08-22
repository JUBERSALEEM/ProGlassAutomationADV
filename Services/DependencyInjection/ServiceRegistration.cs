// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/DependencyInjection/ServiceRegistration.cs
// PROJECT: ProGlassAutomation
// MODULE: Infrastructure — Dependency Injection composition root (Phase 5)
// VERSION: Phase 5 — 2026-08-21 — Full DI, MainViewModel via DI, testable
//
// PURPOSE (Phase 5: DI & Composition Root):
//   Previously:
//     0 IServiceCollection / AddSingleton / ServiceProvider
//     311 static XService.Method(...) call sites
//     38  direct `new XService(...)`
//     22  `new ViewModel()` in MainViewModel constructor
//
//   Now:
//     • Repositories as singletons (each owns SQLite file)
//     • Reporting/Dashboard as singletons (stateless)
//     • All module ViewModels as transient (fresh state per navigation)
//     • MainViewModel as singleton (shell lives whole session)
//     • Logging + Common services as singletons
//     • Agent services as singletons (lazy)
//     • ViewModels support constructor injection — testable with fakes
//
//   Pattern:
//     App.StartupCoreAsync builds provider once → App.ServiceProvider
//     MainWindow gets MainViewModel via provider, not XAML new()
//     New code must use constructor injection; legacy App.* statics remain
//     for migration but are now backed by provider.
//
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Services.Dashboard.Core;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.About;
using ProGlassAutomation.ViewModels.CompanyCenter;
using ProGlassAutomation.ViewModels.Dashboard;
using ProGlassAutomation.ViewModels.DeliveryNote;
using ProGlassAutomation.ViewModels.JobOrder;
using ProGlassAutomation.ViewModels.Inquiry;
using ProGlassAutomation.ViewModels.JobProfitability;
using ProGlassAutomation.ViewModels.Optimization;
using ProGlassAutomation.ViewModels.ProformaInvoice;
using ProGlassAutomation.ViewModels.Production;
using ProGlassAutomation.ViewModels.Reports;
using ProGlassAutomation.ViewModels.Settings;
using ProGlassAutomation.ViewModels.SheetStore;
using ProGlassAutomation.ViewModels.SystemCenter;
using ProGlassAutomation.ViewModels.TaxInvoice;
using ProGlassAutomation.ViewModels.UserCenter;
using ProGlassAutomation.ViewModels.Diagnostics;
using ProGlassAutomation.ViewModels.Shared;
using System;

namespace ProGlassAutomation.Services.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static ServiceCollection AddProGlassServices(this ServiceCollection services)
        {
            // ── Core — Logging & Common (Phase 2) ─────────────────────
            // AppLogService and CorrelationContext are STATIC facades and
            // are consumed directly (not through the container). Static
            // classes cannot be used as type arguments (CS0718), so they
            // are intentionally NOT registered here — see Phase 10 fix.

            // ── Repositories (each owns its own SQLite file) ──────────
            // Singleton — one per app lifetime, owns DB file + connection pool
            services.AddSingleton<IProformaInvoiceRepository, ProformaInvoiceRepository>();
            services.AddSingleton<IJobOrderRepository, JobOrderRepository>();
            services.AddSingleton<IDeliveryNoteRepository, DeliveryNoteRepository>();
            services.AddSingleton<ITaxInvoiceRepository, TaxInvoiceRepository>();
            services.AddSingleton<IStatementRepository, StatementRepository>();
            services.AddSingleton<IMasterDataRepository, MasterDataRepository>();
            services.AddSingleton<ISpecificationsRepository, SpecificationsRepository>();

            // ── Reporting / Dashboard (stateless, singleton-safe) ─────
            services.AddSingleton<IReportingDataService, ReportingDataService>();
            services.AddSingleton<IDashboardMetricsService, DashboardMetricsService>();
            services.AddSingleton<IDashboardFilterService, DashboardFilterService>();

            // ── Workflow — Multi-DB Coordinator (Phase 2) ─────────────
            // DocumentOperationCoordinator is a static facade (CS0718 —
            // not registrable). Consumed directly.
            services.AddSingleton<Services.DeliveryNote.DeliveryNoteLineService>();
            services.AddSingleton<Services.DeliveryNote.DeliveryNoteValidationService>();
            services.AddSingleton<Services.DeliveryNote.DeliveryNoteExportService>();
            services.AddSingleton<Services.DeliveryNote.DeliveryNoteHistoryService>();
            services.AddSingleton<Services.TaxInvoice.TaxInvoiceLineService>();
            services.AddSingleton<Services.TaxInvoice.TaxInvoiceValidationService>();
            services.AddSingleton<Services.TaxInvoice.TaxInvoiceFamilyService>();
            services.AddSingleton<Services.ProformaInvoice.SpecGroupService>();
            services.AddSingleton<Services.Settings.AppearanceService>();

            // ── Theme — Weak Event Manager (Phase 4) ──────────────────
            // ThemeWeakEventManager / VisualThemeEngine are static facades
            // (CS0718 — not registrable). Consumed directly.

            // ── Module ViewModels — Transient (fresh state per navigation) ──
            // Phase 5: Main shell now resolves these via DI, not `new`
            // Transient so each navigation gets fresh filter/pagination, but
            // shares singleton repositories.

            // Shell
            services.AddSingleton<MainViewModel>();

            // Dashboard + sub-dashboards
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<ViewModels.Dashboard.OverviewDashboardViewModel>();
            services.AddTransient<ViewModels.Dashboard.DeliveryNote.DnDashboard.DnDashboardViewModel>();
            services.AddTransient<ViewModels.Dashboard.JobOrder.JoDashboard.JoDashboardViewModel>();
            services.AddTransient<ViewModels.Dashboard.TaxInvoice.TiDashboard.TiDashboardViewModel>();
            services.AddTransient<ViewModels.Dashboard.Statement.StDashboardViewModel>();
            services.AddTransient<ViewModels.Dashboard.SheetStoreDashboardViewModel>();
            services.AddTransient<ViewModels.Dashboard.PiDashboardViewModel>();

            // Inquiry list shell + Proforma Invoice wizard
            services.AddTransient<InquiryListViewModel>();
            services.AddTransient<ProformaInvoiceViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step1CompanySetup.CompanySetupViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step2ClientProfile.ClientProfileViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step3ProjectSpecs.ProjectSpecsViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step4PricingEstimator.PricingEstimatorViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step5DataGrid.DataGridViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step6OtherCharges.OtherChargesViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step7PrintPreview.PrintPreviewViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step7PrintPreview.PrintableInvoice.PrintableInvoiceViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step8Attachments.AttachmentsStepViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step9ProfitLoss.ProfitLossStepViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.SavedInvoicesList.SavedInvoicesListViewModel>();

            // Job Order
            services.AddTransient<JobOrderViewModel>();
            services.AddTransient<ViewModels.JobOrder.JobOrderDetail.JobOrderDetailViewModel>();
            services.AddTransient<ViewModels.JobOrder.SavedJobOrdersList.SavedJobOrdersListViewModel>();
            services.AddTransient<ViewModels.JobOrder.GodView.JobOrderGodViewViewModel>();

            // Delivery Note
            services.AddTransient<DeliveryNoteViewModel>();
            services.AddTransient<ViewModels.DeliveryNote.DeliveryNoteDetail.DeliveryNoteDetailViewModel>();
            services.AddTransient<ViewModels.DeliveryNote.DeliveryNoteList.DeliveryNoteListViewModel>();
            services.AddTransient<ViewModels.DeliveryNote.GodView.DeliveryNoteGodViewViewModel>();

            // Tax Invoice
            services.AddTransient<TaxInvoiceViewModel>();
            services.AddTransient<ViewModels.TaxInvoice.TaxInvoiceDetail.TaxInvoiceDetailViewModel>();
            services.AddTransient<ViewModels.TaxInvoice.TaxInvoiceList.TaxInvoiceListViewModel>();
            services.AddTransient<ViewModels.TaxInvoice.GodView.TaxInvoiceGodViewViewModel>();

            // Other modules
            services.AddTransient<SheetStoreViewModel>();
            services.AddTransient<OptimizationViewModel>();
            services.AddTransient<CuttingViewModel>();
            services.AddTransient<HolesCutoutsViewModel>();
            services.AddTransient<PolishViewModel>();
            services.AddTransient<EdgeDeletionViewModel>();
            services.AddTransient<MitringViewModel>();
            services.AddTransient<TemperingViewModel>();
            services.AddTransient<PairingViewModel>();
            services.AddTransient<DoublingLaminationViewModel>();
            services.AddTransient<SalesReportViewModel>();
            services.AddTransient<JobProfitabilityViewModel>();
            services.AddTransient<CompanyCenterViewModel>();
            services.AddTransient<UserCenterViewModel>();
            services.AddTransient<DiagnosticsViewModel>();
            services.AddTransient<AppLogPanelViewModel>();
            services.AddTransient<SectionActivityViewModel>();
            services.AddTransient<GlobalSearchViewModel>();
            services.AddTransient<AboutViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ScreenRecordPanelViewModel>();

            // ── Reporting Hub — Transient (new instance per navigation) ──
            services.AddTransient<ViewModels.Reports.SalesReportViewModel>();
            services.AddTransient<ViewModels.Reports.StatementModuleViewModel>();
            services.AddTransient<ViewModels.Reports.PiReport.PiReportViewModel>();
            services.AddTransient<ViewModels.Reports.JoReport.JoReportViewModel>();
            services.AddTransient<ViewModels.Reports.DeliveryReport.DeliveryReportViewModel>();
            services.AddTransient<ViewModels.Reports.TaxInvoiceReport.TaxInvoiceReportViewModel>();
            services.AddTransient<ViewModels.Reports.StatementReport.StatementReportViewModel>();

            // ── Agent (Phase 5: lazy, singleton) ──────────────────────
            // AgentHost is a static facade (CS0718 — not registrable).
            // Consumed directly.
            services.AddTransient<Agent.UI.AgentChatViewModel>();
            services.AddTransient<Agent.UI.AgentSettingsViewModel>();
            services.AddTransient<ViewModels.AI.AiChatViewModel>();

            return services;
        }

        public static ServiceProvider BuildProGlassProvider()
        {
            var sc = new ServiceCollection();
            sc.AddProGlassServices();
            return sc.BuildServiceProvider();
        }

        /// <summary>
        /// Builds a provider with test fakes — for unit tests of big ViewModels.
        /// Example:
        ///   var provider = ServiceRegistration.BuildTestProvider(
        ///       services => services.AddSingleton<IProformaInvoiceRepository>(new FakeProformaRepo()));
        ///   var vm = provider.GetRequiredService<ProformaInvoiceViewModel>();
        /// </summary>
        public static ServiceProvider BuildTestProvider(Action<ServiceCollection>? configure = null)
        {
            var sc = new ServiceCollection();
            sc.AddProGlassServices();
            configure?.Invoke(sc);
            return sc.BuildServiceProvider();
        }
    }
}
