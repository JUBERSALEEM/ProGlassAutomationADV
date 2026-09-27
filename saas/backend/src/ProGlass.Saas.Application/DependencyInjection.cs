using Microsoft.Extensions.DependencyInjection;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Application.MasterData;
using ProGlass.Saas.Application.Pricing;

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
        return services;
    }
}
