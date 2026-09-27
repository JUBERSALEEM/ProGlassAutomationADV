using Microsoft.Extensions.DependencyInjection;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Application.MasterData;
using ProGlass.Saas.Application.Pricing;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Application.Specifications;
using ProGlass.Saas.Application.JobOrders;
using ProGlass.Saas.Application.Delivery;
using ProGlass.Saas.Application.Stock;
using ProGlass.Saas.Application.Fiscal;
using ProGlass.Saas.Application.Subscriptions;

namespace ProGlass.Saas.Application;

/// <summary>
/// Registers the application layer. The Api composes this with the
/// Infrastructure registrations (AddSaasInfrastructure) which provide the
/// port implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSaasApplication(
        this IServiceCollection services, AuthOptions? options = null)
    {
        services.AddSingleton(options ?? new AuthOptions());
        services.AddScoped<AuthService>();
        services.AddScoped<MasterDataService>();
        services.AddScoped<PricingService>();
        services.AddScoped<ProformaService>();
        services.AddScoped<SpecificationsService>();
        services.AddScoped<StockService>();
        services.AddScoped<JobOrderService>();
        services.AddScoped<DeliveryService>();
        services.AddScoped<Production.ProductionService>();
        services.AddScoped<Production.ReservationService>();
        // Phase 26a — process execution, pairing/assembly, control,
        // queue/dashboard/progress, workstation master.
        services.AddScoped<Production.ProcessExecutionService>();
        services.AddScoped<Production.PairingAssemblyService>();
        services.AddScoped<Production.ProductionControlService>();
        services.AddScoped<Production.ProductionQueueService>();
        services.AddScoped<Production.WorkstationService>();
        // Phase 26b — cutting workflow, allocation ledger + actual
        // costs, replacements/FOC, integrity scanner, reports.
        services.AddScoped<Production.ReplacementService>();
        services.AddScoped<Production.AllocationCostService>();
        services.AddScoped<Production.CuttingService>();
        services.AddScoped<Production.ProductionIntegrityService>();
        services.AddScoped<Reporting.ProductionReportsService>();
        services.AddScoped<Optimization.OptimizationService>();
        services.AddScoped<Fiscal.FiscalService>();
        services.AddScoped<Fiscal.ReconciliationService>();
        services.AddScoped<Reporting.ReportingService>();
        services.AddScoped<Reporting.RowTraceabilityService>();
        services.AddScoped<Reporting.CncExportService>();
        services.AddScoped<Reporting.CuttingReportService>();
        services.AddSingleton<Reporting.ReportingCacheService>();
        services.AddScoped<Attachments.AttachmentService>();
        services.AddScoped<Subscriptions.SubscriptionService>();
        services.AddScoped<Subscriptions.PlanLimitGuard>();
        return services;
    }
}
