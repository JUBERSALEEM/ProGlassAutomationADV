using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Runs the test only when a live PostgreSQL is configured. Two connections:
///   SAAS_TEST_DB      — owner/superuser connection (applies migrations, seeds platform rows)
///   SAAS_TEST_APP_DB  — the UNPRIVILEGED runtime role (saas_test_app) the app uses;
///                       Row-Level-Security assertions require a non-superuser connection.
/// See saas/backend/README.md for how to provide both via pgserver.
/// </summary>
public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAAS_TEST_DB"))
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAAS_TEST_APP_DB")))
        {
            Skip = "Set SAAS_TEST_DB (owner) and SAAS_TEST_APP_DB (runtime role) " +
                   "PostgreSQL connection strings to run database tests. " +
                   "See saas/backend/README.md.";
        }
    }
}
