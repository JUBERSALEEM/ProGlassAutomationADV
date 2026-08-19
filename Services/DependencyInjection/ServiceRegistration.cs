// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/DependencyInjection/ServiceRegistration.cs
// PROJECT: ProGlassAutomation
// MODULE: Infrastructure — Dependency Injection composition root
//
// PURPOSE:
//   Introduces Microsoft.Extensions.DependencyInjection at the true
//   composition root (App.xaml.cs). Previously the app had:
//     0 IServiceCollection / AddSingleton / ServiceProvider
//     311 static XService.Method(...) call sites
//     38  direct `new XService(...)`
//   This made the 2,000-line ViewModels untestable because their
//   dependencies could not be substituted.
//
//   This file registers all repositories, reporting and dashboard
//   services as singletons. ViewModels that already support constructor
//   injection (the new reporting ViewModels) receive IReportingDataService
//   via DI. Legacy ViewModels continue to work via App.* statics during
//   the migration, but new code must use constructor injection.
//
//   The container is built once in App.StartupCoreAsync and exposed as
//   App.Services (IServiceProvider). Tests can build their own provider
//   with fakes.
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

namespace ProGlassAutomation.Services.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static ServiceCollection AddProGlassServices(this ServiceCollection services)
        {
            // ── Repositories (each owns its own SQLite file) ──────────
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

            // ── ViewModels (transient — new instance per navigation) ──
            // New reporting hub and its modules are transient so each
            // navigation gets a fresh filter/pagination state while sharing
            // the singleton reporting service.
            services.AddTransient<ViewModels.Reports.SalesReportViewModel>();
            services.AddTransient<ViewModels.Reports.PiReport.PiReportViewModel>();
            services.AddTransient<ViewModels.Reports.JoReport.JoReportViewModel>();
            services.AddTransient<ViewModels.Reports.DeliveryReport.DeliveryReportViewModel>();
            services.AddTransient<ViewModels.Reports.TaxInvoiceReport.TaxInvoiceReportViewModel>();
            services.AddTransient<ViewModels.Reports.CompanyReport.CompanyReportViewModel>();
            services.AddTransient<ViewModels.Reports.SalesmanReport.SalesmanReportViewModel>();
            services.AddTransient<ViewModels.Reports.ProjectReport.ProjectReportViewModel>();
            services.AddTransient<ViewModels.Reports.GlassSpecReport.GlassSpecReportViewModel>();
            services.AddTransient<ViewModels.Reports.StatementReport.StatementReportViewModel>();
            services.AddTransient<ViewModels.Reports.ProfitabilityReport.ProfitabilityReportViewModel>();

            return services;
        }

        public static ServiceProvider BuildProGlassProvider()
        {
            var sc = new ServiceCollection();
            sc.AddProGlassServices();
            return sc.BuildServiceProvider();
        }
    }
}
