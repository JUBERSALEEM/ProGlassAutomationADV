using ProGlassAutomation.Services.Database.SchemaMigrations;
using ProGlassAutomation.Services.Logging;
using System;

namespace ProGlassAutomation.Services.Database
{
    /// <summary>
    /// Initializes the main application database before any module UI or
    /// repository is allowed to operate. Database startup is fail-closed:
    /// a failed schema migration or database initialization aborts startup
    /// instead of allowing the application to write against an unknown schema.
    /// </summary>
    public static class DatabaseInitializer
    {
        public static void Initialize()
        {
            try
            {
                using var db = new AppDbContext();

                // Creates DB file + tables if they don't exist.
                db.Database.EnsureCreated();

                // Bring an existing proglass.db forward if migrations are
                // registered. A failed migration is a startup blocker:
                // continuing could let modules write against an unknown schema.
                bool upgraded = SchemaMigrationRunner.Upgrade(
                    AppDbContext.DatabaseFilePath,
                    AppSchemaMigrations.ProGlass,
                    "Sheet Store");

                if (!upgraded)
                {
                    string detail = SchemaMigrationRunner.FailureSummary;
                    throw new InvalidOperationException(
                        "The main database schema migration failed. " +
                        "Startup has been stopped to protect business data. " +
                        detail);
                }

                AppLogService.Info(
                    $"Main database ready at {AppDbContext.DatabaseFilePath}",
                    new LogContext
                    {
                        Module = "Database",
                        Class = nameof(DatabaseInitializer),
                        Operation = "Initialize"
                    });
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "Main database initialization failed. Startup must stop; " +
                    "the application must not continue with an unknown schema.",
                    ex,
                    new LogContext
                    {
                        Module = "Database",
                        Class = nameof(DatabaseInitializer),
                        Operation = "Initialize"
                    });

                // App.Application_Startup owns the fatal-startup UX and
                // exits with a non-zero code. Preserve the original cause.
                throw new InvalidOperationException(
                    "ProGlass cannot safely start because the main database " +
                    "could not be initialized. No module UI should be opened.",
                    ex);
            }
        }
    }
}
