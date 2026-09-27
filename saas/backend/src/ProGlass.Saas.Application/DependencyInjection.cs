using Microsoft.Extensions.DependencyInjection;
using ProGlass.Saas.Application.Auth;

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
        return services;
    }
}
