using ProGlassAutomation.Services.Logging;
using System;

namespace ProGlassAutomation.Services.Database
{
    /// <summary>
    /// Handles database creation on first app run.
    /// Called from App.xaml.cs startup before UI loads.
    /// </summary>
    public static class DatabaseInitializer
    {
        public static void Initialize()
        {
            try
            {
                using var db = new AppDbContext();

                // Creates DB file + tables if they don't exist
                db.Database.EnsureCreated();

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
                // R-1 RECURRENCE: this previously reported only through
                // Debug.WriteLine, which is [Conditional("DEBUG")] and is
                // removed by the compiler in Release. In a shipped build a
                // failure here was completely invisible — the Sheet Store
                // would simply appear empty with no explanation, because
                // the database was never created.
                //
                // Still non-fatal by design (the app runs with degraded
                // features rather than refusing to start), but the reason
                // is now recoverable from the log.
                AppLogService.Error(
                    "Main database could not be initialized. Sheet Store "
                    + "and pricing features will be unavailable.",
                    ex,
                    new LogContext
                    {
                        Module = "Database",
                        Class = nameof(DatabaseInitializer),
                        Operation = "Initialize"
                    });
            }
        }
    }
}
