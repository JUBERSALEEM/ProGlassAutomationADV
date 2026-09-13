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
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.Dashboard.Core;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.OrderReport.Repository;
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

            // ── Configuration ─────────────────────────────────────────
            // AppConfig was previously NOT registered, so any view model
            // taking it as a ctor parameter received null from DI while
            // the parameterless path fell back to App.Config. That split
            // meant DI-resolved instances null-referenced on _config.*.
            // Register the SAME instance App.Config exposes so both
            // construction routes observe identical state.
            services.AddSingleton<Services.AI.AIConfig.AppConfig>(_ =>
                App.Config ?? new Services.AI.AIConfig.AppConfig());

            // ── Repositories (each owns its own SQLite file) ──────────
            // Singleton — one per app lifetime, owns DB file + connection pool
            services.AddSingleton<IInquiryRepository, InquiryRepository>();
            services.AddSingleton<Services.Workflow.IStatusService, Services.Workflow.StatusService>();
            services.AddSingleton<Services.Dialogs.IDialogService, Services.Dialogs.DialogService>();
            services.AddSingleton<Services.Database.IDatabaseServices, Services.Database.DatabaseServices>();
            services.AddSingleton<IInquiryWorkflowService, InquiryWorkflowService>();
            services.AddSingleton<InquiryAttachmentService>();
            services.AddSingleton<InquiryImportExportService>();
            // Full commercial-pipeline hydration for the inquiry register
            // (reads PI / JO / DN / TI / STMT databases in parallel and
            // caches the result for 60 seconds). Singleton — the cache
            // is shared across all navigation instances of the register.
            services.AddSingleton<IInquiryLifecycleService, InquiryLifecycleService>();
            services.AddSingleton<IProformaInvoiceRepository, ProformaInvoiceRepository>();
            services.AddSingleton<IJobOrderRepository, JobOrderRepository>();
            services.AddSingleton<IDeliveryNoteRepository, DeliveryNoteRepository>();
            services.AddSingleton<ITaxInvoiceRepository, TaxInvoiceRepository>();
            services.AddSingleton<Services.CreditNote.Repository.ICreditNoteRepository,
                Services.CreditNote.Repository.CreditNoteRepository>();
            services.AddSingleton<IStatementRepository, StatementRepository>();
            services.AddSingleton<IOrderReportRepository, OrderReportRepository>();
            services.AddSingleton<IMasterDataRepository, MasterDataRepository>();
            services.AddSingleton<ISpecificationsRepository, SpecificationsRepository>();
            services.AddSingleton<Services.SheetStore.IInventoryItemRepository,
                                  Services.SheetStore.InventoryItemRepository>();
            services.AddSingleton<Services.AI.IAiProviderEvents, Services.AI.AiProviderEvents>();

            // ── Reporting / Dashboard (stateless, singleton-safe) ─────
            services.AddSingleton<IReportingDataService, ReportingDataService>();
            services.AddSingleton<IDashboardMetricsService, DashboardMetricsService>();
            services.AddSingleton<IDashboardFilterService, DashboardFilterService>();
            // TiDashboardViewModel (registered below) needs this; without
            // the registration MS.DI throws InvalidOperationException when
            // the dashboard is resolved.
            services.AddSingleton<Services.Dashboard.TaxInvoiceMetrics.ITaxInvoiceMetricsService,
                Services.Dashboard.TaxInvoiceMetrics.TaxInvoiceMetricsService>();

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
            services.AddTransient<InquiryEditorViewModel>();
            services.AddTransient<ProformaInvoiceViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step1CompanySetup.CompanySetupViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step2ClientProfile.ClientProfileViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step3ProjectSpecs.ProjectSpecsViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step4PricingEstimator.PricingEstimatorViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step5DataGrid.DataGridViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step6OtherCharges.OtherChargesViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step7PrintPreview.PrintPreviewViewModel>();
            // PrintableInvoiceViewModel takes these two and throws
            // ArgumentNullException on null, so they MUST be registered
            // or resolving the print preview fails outright. Both have
            // ctors whose own dependencies are already registered above.
            services.AddTransient<ViewModels.ProformaInvoice.Step7PrintPreview.InvoiceMainSummary.InvoiceMainSummaryViewModel>();
            services.AddTransient<ViewModels.ProformaInvoice.Step7PrintPreview.TermsAndConditions.TermsAndConditionsViewModel>();
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

            // Specifications Center. Its greedy ctor takes IDialogService +
            // ISpecificationsRepository, both registered above, so MS.DI
            // picks it rather than silently downgrading to the
            // parameterless fallback.
            services.AddTransient<
                ProGlassAutomation.ViewModels.Specifications.SpecificationsCenterViewModel>();
            services.AddTransient<OptimizationViewModel>();
            services.AddTransient<CuttingViewModel>();
            services.AddTransient<CuttingWorkCenterViewModel>();
            services.AddTransient<HolesCutoutsViewModel>();
            services.AddTransient<PolishViewModel>();
            services.AddTransient<EdgeDeletionViewModel>();
            services.AddTransient<MitringViewModel>();
            services.AddTransient<TemperingViewModel>();
            services.AddTransient<PairingViewModel>();
            services.AddTransient<DoublingLaminationViewModel>();
            services.AddTransient<SalesReportViewModel>();
            services.AddTransient<JobProfitabilityViewModel>();
            // Company Center: explicit factory so the greedy (DI-resolved)
            // ctor — which is the one that wires every command and loads the
            // card list — can never be bypassed by an overload-selection
            // change. (Its parameterless ctor is design-time only.)
            services.AddTransient<CompanyCenterViewModel>(sp =>
                new CompanyCenterViewModel(
                    sp.GetService<Services.Dialogs.IDialogService>(),
                    sp.GetService<IMasterDataRepository>()));
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
            // ERR-633EEB: build via an explicit factory so the container can never
            // pick a partially-initializing constructor overload.
            services.AddTransient(sp => new ViewModels.Reports.StatementModuleViewModel(
                sp.GetService<Services.Dialogs.IDialogService>(),
                sp.GetService<Services.Statement.Repository.IStatementRepository>(),
                sp.GetService<Services.MasterData.Repository.IMasterDataRepository>(),
                sp.GetService<Services.Database.IDatabaseServices>()));
            services.AddTransient(sp => new ViewModels.Reports.OrderReport.OrderReportModuleViewModel(
                sp.GetService<Services.Dialogs.IDialogService>(),
                sp.GetService<Services.OrderReport.Repository.IOrderReportRepository>(),
                sp.GetService<Services.MasterData.Repository.IMasterDataRepository>(),
                sp.GetService<Services.ProformaInvoice.ProformaInvoiceRepository.IProformaInvoiceRepository>(),
                sp.GetService<Services.Database.IDatabaseServices>()));
            services.AddTransient<ViewModels.Reports.PiReport.PiReportViewModel>();
            services.AddTransient<ViewModels.Reports.JoReport.JoReportViewModel>();
            services.AddTransient<ViewModels.Reports.DeliveryReport.DeliveryReportViewModel>();
            services.AddTransient<ViewModels.Reports.TaxInvoiceReport.TaxInvoiceReportViewModel>();
            services.AddTransient<ViewModels.Reports.StatementReport.StatementReportViewModel>();
            services.AddTransient<ViewModels.Reports.VatSubmission.VatSubmissionViewModel>();

            // ── Agent (Phase 5: lazy, singleton) ──────────────────────
            // AgentHost is a static facade (CS0718 — not registrable).
            // Consumed directly.
            //
            // The AI / voice services are App.Ai / App.SpeechToText /
            // App.TextToSpeech — runtime-initialised statics, NOT normally
            // DI services. But AiChatViewModel (registered below) has a
            // constructor that takes these interfaces, so MS.DI throws
            // InvalidOperationException ("Unable to resolve service for
            // IAiService...") unless they are registered. Register them as
            // singletons whose factory reads the App static at resolve time
            // (App sets them before MainViewModel/App chat is constructed in
            // StartupCoreAsync, so they are non-null by then). They may be
            // null in edge cases (no AI configured / no STT/TTS); the
            // factory returns null and AiChatViewModel tolerates it.
            services.AddSingleton<IAiService>(_ => global::ProGlassAutomation.App.Ai);
            services.AddSingleton<ISpeechToTextService>(_ => global::ProGlassAutomation.App.SpeechToText);
            services.AddSingleton<ITextToSpeechService>(_ => global::ProGlassAutomation.App.TextToSpeech);

            // SystemCenterViewModel takes UserPresenceService. It was
            // unregistered, so the DI path got null while the
            // parameterless path fell back to App.Presence -- meaning
            // peer presence silently stopped working for the real
            // instance. Every use is null-guarded so it never crashed,
            // it just did nothing. Resolve to the same App.Presence
            // singleton both paths expect.
            services.AddSingleton<Services.Network.UserPresenceService>(
                _ => global::ProGlassAutomation.App.Presence);

            // AiChatViewModel also takes IConnectivityService. It was
            // unregistered, so MS.DI could not build the greediest ctor
            // and the AI chat panel silently failed to resolve.
            services.AddSingleton<Services.AI.AIConnectivity.IConnectivityService,
                                  Services.AI.AIConnectivity.ConnectivityService>();

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
