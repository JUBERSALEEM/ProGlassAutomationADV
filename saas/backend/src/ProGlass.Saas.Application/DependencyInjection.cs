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
        services.AddScoped<Optimization.OptimizationService>();
        services.AddScoped<Fiscal.FiscalService>();
        services.AddScoped<Subscriptions.SubscriptionService>();
        services.AddScoped<Subscriptions.PlanLimitGuard>();
        return services;
    }
}
