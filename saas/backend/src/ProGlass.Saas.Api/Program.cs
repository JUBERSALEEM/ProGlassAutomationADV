using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using ProGlass.Saas.Api.Authorization;
using ProGlass.Saas.Api.Middleware;
using ProGlass.Saas.Application;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Tenancy;
using ProGlass.Saas.Infrastructure;
using ProGlass.Saas.Infrastructure.Persistence;
using ProGlass.Saas.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────
// Runtime (RLS-restricted) connection: all tenant data access.
var connectionString =
    builder.Configuration.GetConnectionString("Saas")
    ?? Environment.GetEnvironmentVariable("SAAS_DB")
    ?? throw new InvalidOperationException(
        "Database connection is required: set ConnectionStrings:Saas " +
        "or the SAAS_DB environment variable. Never commit real secrets " +
        "(see saas/docs/SAAS_ARCHITECTURE §11).");

// Elevated connection: tenant provisioning only. The runtime role cannot
// INSERT INTO saas.tenants by design (ADR-011) — registration runs here.
var platformConnectionString =
    builder.Configuration.GetConnectionString("SaasPlatform")
    ?? Environment.GetEnvironmentVariable("SAAS_PLATFORM_DB")
    ?? throw new InvalidOperationException(
        "Platform database connection is required for tenant provisioning: " +
        "set ConnectionStrings:SaasPlatform or the SAAS_PLATFORM_DB " +
        "environment variable (elevated role, e.g. the migration owner).");

// JWT settings from config, with the signing key also accepted from the
// environment (SAAS_JWT_SIGNING_KEY) — secrets never live in appsettings.
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("Authentication:Jwt").Bind(jwtSettings);
jwtSettings.SigningKey =
    builder.Configuration["Authentication:Jwt:SigningKey"]
    ?? Environment.GetEnvironmentVariable("SAAS_JWT_SIGNING_KEY")
    ?? jwtSettings.SigningKey;

var migrateOnStartup =
    builder.Configuration.GetValue<bool?>("Saas:MigrateOnStartup")
    ?? string.Equals(Environment.GetEnvironmentVariable("SAAS_MIGRATE_ON_STARTUP"),
        "true", StringComparison.OrdinalIgnoreCase);

// ── Application + Infrastructure ──────────────────────────────────────
builder.Services.AddSaasApplication();
builder.Services.AddSaasInfrastructure(
    connectionString, platformConnectionString, jwtSettings);

// ── Tenancy pipeline (request-scoped) ─────────────────────────────────
// ONE AmbientTenantContext per request: TenantContextMiddleware writes it
// (as the concrete type — ITenantContext is read-only by design) and the
// DbContext/interceptor read the SAME instance through the interface.
// An unset context sees NO rows (EF query filters + RLS fail closed).
builder.Services.AddScoped<AmbientTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp =>
    sp.GetRequiredService<AmbientTenantContext>());

builder.Services.AddDbContext<SaasDbContext>((sp, options) =>
{
    var tenant = sp.GetRequiredService<ITenantContext>();
    options.UseNpgsql(connectionString);
    // Scoped interceptor instance: gets the same ITenantContext as the
    // DbContext in this scope.
    options.AddInterceptors(new TenantSessionInterceptor(tenant));
});

// ── Authentication: HS256 access tokens ───────────────────────────────
// Validated at startup (AddSaasInfrastructure throws when the key is
// missing or shorter than 32 bytes).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the claim names exactly as issued ("sub", "tid", "role",
        // "preferred_username") — the default inbound map would rewrite
        // them to long URI types and break the tenancy/permission lookups.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtSettings.CreateSigningKey(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// ── Authorization: permission policies from the WPF-verbatim catalog ──
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

builder.Services.AddControllers();

var app = builder.Build();

// ── Optional schema migration on startup ─────────────────────────────
if (migrateOnStartup)
{
    var migrationsDirectory = builder.Configuration["Saas:MigrationsDirectory"];
    var runner = new SqlMigrationRunner(connectionString, migrationsDirectory,
        app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger<SqlMigrationRunner>());
    var applied = await runner.ApplyAsync();
    if (applied.Count > 0)
        app.Logger.LogInformation("Applied {Count} migration(s): {Names}",
            applied.Count, string.Join(", ", applied));
}

// ── Pipeline ──────────────────────────────────────────────────────────
app.UseAuthentication();

// The ONLY writer of the request tenant context (claims → context); runs
// after authentication, before anything that touches tenant data.
app.UseMiddleware<TenantContextMiddleware>();

app.UseAuthorization();
app.MapControllers();

// ── Endpoints ─────────────────────────────────────────────────────────
app.MapGet("/health", async (SaasDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect
        ? Results.Ok(new { status = "healthy", database = "connected" })
        : Results.ServiceUnavailable(new { status = "degraded", database = "unreachable" });
});

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
