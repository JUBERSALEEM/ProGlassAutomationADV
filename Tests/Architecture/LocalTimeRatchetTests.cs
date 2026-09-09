// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Architecture/LocalTimeRatchetTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Architecture — stop the local-time surface growing
//
// WHY THIS EXISTS
// ───────────────
// The codebase mixes DateTime.Now and DateTime.UtcNow. Measured 2026-09-09,
// across production .cs files with comment lines excluded:
//
//     DateTime.Now      224 occurrences in 111 files
//     DateTime.UtcNow   152 occurrences
//
// Most of the 224 are NOT defects. Classified by what the value is used for:
//
//     89  assigned to a persisted field      (CreatedAt, ReceivedAt, ...)
//     60  passed as an argument
//     59  formatted for display or a filename
//     14  compared or derived — the timezone-sensitive ones
//      7  other
//
// The 59 display uses are correct as they are. A filename stamped
// `20260909_141502` and a log line reading "Started at 14:15:02" should show
// the operator's own clock; converting those to UTC would be a regression, not
// a fix.
//
// The 89 persisted ones are the real migration, and they cannot be converted
// one file at a time. Existing rows hold local time. Change the write side
// without migrating the stored rows and the timeline splits in half — a report
// filtered on "this month" silently gains or loses a day's worth of records at
// the boundary, with no error anywhere.
//
// So this test does not attempt the migration. It holds the line: the count
// cannot grow. Phase 6 is a ratchet plus a plan, not a find-and-replace.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Architecture
{
    public sealed class LocalTimeRatchetTests
    {
        /// <summary>
        /// Production <c>DateTime.Now</c> occurrences, measured 2026-09-09 after
        /// the five in-memory timing sites were converted to UTC. Lower this as
        /// sites are migrated; the stale-baseline assertion below fails until
        /// you do.
        /// </summary>
        private const int Baseline = 224;

        [Fact]
        public void The_local_time_surface_does_not_grow()
        {
            int count = CountLocalTime(out var files);

            Assert.True(
                count <= Baseline,
                $"DateTime.Now appears {count} times across {files} production " +
                $"files, up from a baseline of {Baseline}. Not every use is wrong — " +
                "a timestamp shown to a person or written into a filename should " +
                "be local — but a new one should be a decision, not a default. " +
                "Use DateTime.UtcNow for anything persisted, compared, or " +
                "measuring an interval, and if this genuinely must be local, " +
                "raise the baseline here and say why.");
        }

        [Fact]
        public void The_baseline_is_not_stale()
        {
            int count = CountLocalTime(out _);

            Assert.True(
                count >= Baseline,
                $"DateTime.Now is down to {count} from a baseline of {Baseline}. " +
                "Lower the baseline so the ratchet protects the work that was " +
                "done instead of allowing it to come back.");
        }

        [Fact]
        public void Elapsed_time_measurements_use_utc()
        {
            // The two interval measurements that existed were converted in
            // Phase 6: an AI context cache expiry and a statement-generation
            // stopwatch. Both compare a DateTime against another DateTime that
            // never leaves the process, so local time made them wrong by a full
            // hour across a DST transition.
            //
            // Pinned by name so a new in-memory stopwatch written with
            // DateTime.Now is caught here rather than in production, where the
            // symptom is a cache that serves stale data for an hour.
            var ai = Read("Services/AI/AIContext/AiContextBuilder.cs");
            Assert.DoesNotContain("DateTime.Now", ai, StringComparison.Ordinal);
            Assert.Contains("DateTime.UtcNow < _cacheExpiry", ai, StringComparison.Ordinal);

            var gen = Read(
                "ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.Generate.cs");
            Assert.Contains("var generationStart = DateTime.UtcNow;", gen, StringComparison.Ordinal);
            Assert.Contains("(DateTime.UtcNow - generationStart)", gen, StringComparison.Ordinal);

            // This one is deliberately still local, and the assertion is here so
            // nobody "fixes" it. It is a log line a person reads.
            Assert.Contains("Started at {DateTime.Now:HH:mm:ss}", gen, StringComparison.Ordinal);
        }

        private static int CountLocalTime(out int filesWithAny)
        {
            var root = Root();
            int total = 0;
            int files = 0;

            foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (rel.StartsWith("Tests/", StringComparison.Ordinal)) continue;
                if (rel.Contains("/obj/", StringComparison.Ordinal)) continue;

                int inFile = 0;
                foreach (var raw in File.ReadLines(path))
                {
                    var line = raw.TrimStart();
                    if (line.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (line.StartsWith("*", StringComparison.Ordinal)) continue;
                    if (line.StartsWith("/*", StringComparison.Ordinal)) continue;

                    inFile += CountOccurrences(line, "DateTime.Now");
                }

                if (inFile > 0) { files++; total += inFile; }
            }

            filesWithAny = files;
            return total;
        }

        /// <summary>
        /// Counts non-overlapping occurrences. Written out rather than using
        /// Regex so the rule this test enforces is legible in the test.
        /// </summary>
        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, at = 0;
            while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }
            return count;
        }

        private static string Read(string relative)
        {
            var path = Path.Combine(
                Root(), relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"expected source file not found: {relative}");
            return File.ReadAllText(path);
        }

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }
}
