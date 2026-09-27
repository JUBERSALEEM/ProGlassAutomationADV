using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Application.MasterData;
using ProGlass.Saas.Application.Security;
using ProGlass.Saas.Infrastructure.Persistence;
using ProGlass.Saas.Infrastructure.Security;

namespace ProGlass.Saas.Infrastructure;

/// <summary>
/// Registers the Infrastructure layer: the port implementations the
/// Application layer (AddSaasApplication) consumes. The API is the only
/// composition root — tests use fakes (Application.Tests) or build the
/// same graph with their own connection strings (IntegrationTests).
/// </summary>
public static class DependencyInjection
{
    /// <param name="connectionString">Runtime (RLS-restricted saas_app)
    /// connection — all tenant data access.</param>
    /// <param name="platformConnectionString">Elevated connection
    /// (SAAS_PLATFORM_DB) — tenant provisioning only; it must be able to
    /// INSERT INTO saas.tenants, which the runtime role cannot.</param>
    /// <param name="jwtSettings">Access-token settings; the signing key is
    /// validated HERE so a missing/short key fails at startup, not at the
    /// first token issue.</param>
    public static IServiceCollection AddSaasInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string platformConnectionString,
        JwtSettings jwtSettings)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException(
                "Runtime connection string is required.", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(platformConnectionString))
            throw new ArgumentException(
                "Platform connection string (SAAS_PLATFORM_DB) is required — " +
                "tenant provisioning cannot run as the RLS-restricted runtime role.",
                nameof(platformConnectionString));
        ArgumentNullException.ThrowIfNull(jwtSettings);

        // Fail fast: throws with an actionable message when the signing key
        // is missing or shorter than 32 bytes (HS256).
        jwtSettings.CreateSigningKey();

        services.AddSingleton<ISaasDbContextFactory>(
            new SaasDbContextFactory(connectionString));
        services.AddSingleton<ITenantProvisioner>(sp => new TenantProvisioner(
            platformConnectionString,
            sp.GetRequiredService<ILogger<TenantProvisioner>>()));

        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton(jwtSettings);
        services.AddSingleton<ITokenService>(sp => new JwtTokenService(
            sp.GetRequiredService<JwtSettings>(),
            sp.GetRequiredService<AuthOptions>()));

        // Stores are stateless (they build a context per operation) but are
        // registered scoped to mirror AuthService's lifetime and keep the
        // container graph obvious.
        services.AddScoped<IUserStore, UserStore>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IMasterDataStore, MasterDataStore>();

        return services;
    }
}
