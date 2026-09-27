using ProGlass.Saas.Domain.Tenancy;
using ProGlass.Saas.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────
var connectionString =
    builder.Configuration.GetConnectionString("Saas")
    ?? Environment.GetEnvironmentVariable("SAAS_DB")
    ?? throw new InvalidOperationException(
        "Database connection is required: set ConnectionStrings:Saas " +
        "or the SAAS_DB environment variable. Never commit real secrets " +
        "(see saas/docs/SAAS_ARCHITECTURE.md §11).");

var migrateOnStartup =
    builder.Configuration.GetValue<bool?>("Saas:MigrateOnStartup")
    ?? string.Equals(Environment.GetEnvironmentVariable("SAAS_MIGRATE_ON_STARTUP"),
        "true", StringComparison.OrdinalIgnoreCase);

// ── DI ────────────────────────────────────────────────────────────────
// ITenantContext is scoped: the Phase 4 auth middleware populates it once
// per request from the validated JWT. Today it exists so the tenancy
// pipeline (query filters + RLS interceptor) is wired and testable end to
// end; an unset context sees NO rows by design.
builder.Services.AddScoped<ITenantContext, AmbientTenantContext>();

builder.Services.AddDbContext<SaasDbContext>((sp, options) =>
{
    var tenant = sp.GetRequiredService<ITenantContext>();
    options.UseNpgsql(connectionString);
    // Scoped interceptor instance: gets the same ITenantContext as the
    // DbContext in this scope.
    options.AddInterceptors(new TenantSessionInterceptor(tenant));
});

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

// ── Endpoints ─────────────────────────────────────────────────────────
app.MapGet("/health", async (SaasDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect
        ? Results.Ok(new { status = "healthy", database = "connected" })
        : Results.ServiceUnavailable(new { status = "degraded", database = "unreachable" });
});

app.Run();
