// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Database/SqliteResilienceCoverageTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Database — coverage guard for SqliteResilience adoption
//
// REGRESSION LOCK for the Aug-2026 industrial SQLite hardening pass.
//
// After the hardening, every EF Core SaveChangesAsync call in the
// Services/ + Agent/ folders must be routed through
// SqliteResilience.SaveWithRetryAsync so a transient SQLITE_BUSY under
// WAL contention is retried with exponential back-off instead of
// killing the user's action.
//
// This test walks the source tree and fails the build if any repository
// or service adds a raw `SaveChangesAsync` again. The message points
// the offending author at the fix.
//
// EXEMPTIONS (kept small on purpose):
//   • SqliteResilience.cs itself — where the retry is defined.
//   • Anything under Tests/ — test doubles / fakes.
//   • Migration + backup services that use raw Microsoft.Data.Sqlite
//     (no EF DbContext, so no SaveChangesAsync appears there anyway).
//
// The test is source-file only (no DB, no filesystem beyond reading the
// tree). It walks upward from AppContext.BaseDirectory the same way
// WorkflowNarrationDepthTests does, so it works whether the build
// runs from bin/Debug or bin/Release.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Database
{
    public class SqliteResilienceCoverageTests
    {
        // Directories inside the main project that must NOT contain a raw
        // `SaveChangesAsync` call. Every save in these trees is expected to
        // route through SqliteResilience.SaveWithRetryAsync.
        private static readonly string[] ScanRoots =
        {
            "Services",
            "Agent",
        };

        // Regex — matches a `SaveChangesAsync` invocation that is NOT
        // preceded by the SqliteResilience wrapper token on the same line.
        // Also matches on the next-line pattern where the arg spills over.
        //
        // We keep the pattern narrow: we look for the LITERAL invocation
        // `.SaveChangesAsync(` and then check whether the enclosing line
        // contains a marker that proves it's the wrapped call
        // (`SaveWithRetryAsync`) or the lambda body inside one (`=> ` on
        // the same line for `_ => ctx.SaveChangesAsync()` pattern).
        private static readonly Regex SaveCall =
            new(@"\.SaveChangesAsync\s*\(", RegexOptions.Compiled);

        [Fact]
        public void No_repository_calls_raw_SaveChangesAsync()
        {
            var root = LocateProjectRoot();
            var failures = new List<string>();

            foreach (var scanRoot in ScanRoots)
            {
                var folder = Path.Combine(root, scanRoot);
                if (!Directory.Exists(folder)) continue;

                foreach (var file in Directory.EnumerateFiles(
                    folder, "*.cs", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(root, file);
                    // Never scan the wrapper itself.
                    if (rel.EndsWith("SqliteResilience.cs",
                        StringComparison.OrdinalIgnoreCase)) continue;

                    var lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (!SaveCall.IsMatch(line)) continue;

                        // Wrapped call? Accepted shapes:
                        //   • One-line:  `await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync())`
                        //   • Two-line:  `await SqliteResilience.SaveWithRetryAsync(`
                        //                `    token => db.SaveChangesAsync(token), ct)`
                        // Accept the wrap if the marker appears on this
                        // line OR on either of the two preceding lines
                        // (long-form invocation with the delegate on the
                        // next line, or the arguments broken over three
                        // physical lines).
                        if (line.Contains("SaveWithRetryAsync",
                            StringComparison.Ordinal)) continue;
                        if (i >= 1 && lines[i - 1].Contains("SaveWithRetryAsync",
                            StringComparison.Ordinal)) continue;
                        if (i >= 2 && lines[i - 2].Contains("SaveWithRetryAsync",
                            StringComparison.Ordinal)) continue;

                        // Method-signature reference / interface / comment
                        // exclusions. We only care about ACTUAL calls, not
                        // documentation.
                        var trimmed = line.TrimStart();
                        if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                        if (trimmed.StartsWith("*", StringComparison.Ordinal)) continue;
                        if (trimmed.StartsWith("///", StringComparison.Ordinal)) continue;

                        failures.Add($"{rel}:{i + 1}  {line.Trim()}");
                    }
                }
            }

            Assert.True(failures.Count == 0,
                "Unwrapped SaveChangesAsync detected — every write must go " +
                "through SqliteResilience.SaveWithRetryAsync so transient " +
                "SQLITE_BUSY under WAL is retried. Fix by wrapping:" +
                Environment.NewLine +
                "    await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());" +
                Environment.NewLine + Environment.NewLine +
                "Offending locations:" + Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void SchemaMigrationRunner_and_AuditDbMigrations_apply_pragmas_manually()
        {
            // Both files own a `Open()` helper that creates a raw
            // SqliteConnection (bypassing the EF DbConnectionInterceptor).
            // They MUST manually run SqliteResilience.PragmaScript after
            // Open — otherwise their migration statements run under the
            // default rollback-journal / FK-off / no-busy-timeout mode.
            var root = LocateProjectRoot();
            foreach (var rel in new[]
            {
                "Services/Database/SchemaMigrations/SchemaMigrationRunner.cs",
                "Services/Audit/AuditDbMigrations.cs",
            })
            {
                var path = Path.Combine(root, rel);
                Assert.True(File.Exists(path), $"Missing: {rel}");
                var src = File.ReadAllText(path);

                Assert.Contains("SqliteResilience.PragmaScript", src);
                Assert.Contains("SqliteResilience", src);
            }
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Services")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "Agent")) &&
                    File.Exists(Path.Combine(dir.FullName, "Services",
                        "Database", "SqliteResilience.cs")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                "Could not locate project root (Services + Agent + SqliteResilience.cs) " +
                "above " + AppContext.BaseDirectory);
        }
    }
}
