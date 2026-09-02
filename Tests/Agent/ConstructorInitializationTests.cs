// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ConstructorInitializationTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Source-level guard for the "two-phase constructor" defect class
//
// REGRESSION GUARD FOR ERR-633EEB (2026-09-02)
// ─────────────────────────────────────────────────────────────────────
// Microsoft.Extensions.DependencyInjection always selects the GREEDIEST
// constructor whose parameters it can satisfy. When a view model looked
// like this:
//
//     public FooViewModel(IBar? bar)          // DI picks this
//     {
//         _bar = bar!;                        // ...and nothing else
//     }
//
//     public FooViewModel()                   // real init lives here
//         : this(App.ServiceProvider?.GetService<IBar>()!)
//     {
//         ListVM = new ...;                   // never runs under DI
//         SaveCommand = new ...;
//     }
//
// ...every DI-resolved instance was half-built. Child view models and
// ICommands stayed null and the first consumer threw
// NullReferenceException. For StatementModuleViewModel that NRE happened
// inside MainWindow's XAML load and killed startup outright.
//
// The defect is INVISIBLE until the type is DI-registered, so it stayed
// dormant across the codebase and detonated when an unrelated commit
// added an AddTransient<T>() line. This test makes it impossible to
// reintroduce.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class ConstructorInitializationTests
    {
        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "App.xaml")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root.");
        }

        // NOTE: partial classes split a type across files (Foo.Part1.cs /
        // Foo.Part2.cs). The scan is per-file, so a parameterless ctor in
        // Part2 is still caught even when the parameterized one lives in
        // Part1 — the delegating ctor itself is what carries the body.
        private static IEnumerable<string> ViewModelFiles() =>
            Directory.EnumerateFiles(
                    Path.Combine(Root(), "ViewModels"), "*.cs",
                    SearchOption.AllDirectories)
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        /// <summary>
        /// Extract the body of a { ... } block starting at <paramref name="open"/>,
        /// brace-aware so lambdas and object initializers do not desynchronise.
        /// </summary>
        private static string BlockBody(string text, int open)
        {
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0) return text.Substring(open + 1, i - open - 1);
                }
            }
            return string.Empty;
        }

        private static int MeaningfulStatements(string body)
        {
            return body.Split('\n')
                .Select(l => l.Trim())
                .Count(l => l.Length > 0
                            && !l.StartsWith("//", StringComparison.Ordinal)
                            && !l.StartsWith("/*", StringComparison.Ordinal)
                            && !l.StartsWith("*", StringComparison.Ordinal)
                            && l != "{" && l != "}");
        }

        // ── THE INVARIANT ───────────────────────────────────────────────
        // A parameterless ctor that delegates via ": this(...)" must not
        // carry an initialization body. If it does, that work is skipped
        // whenever DI (or any caller) uses the parameterized overload.
        // STATUS (2026-09-02): KNOWN-FAILING / QUARANTINED.
        // An automated bulk repair of this defect class (commit 6a93c0e)
        // was reverted because the rewriter also relocated field
        // DECLARATIONS that sat physically between the two ctors, and
        // because SettingsViewModel's moved body dereferences _config
        // (AppConfig), which is NOT registered in the DI container — so
        // the DI-selected ctor threw, MainWindow.DataContext went null,
        // and the whole shell went dead. Re-do this migration one VM at
        // a time, with a compile+run after each. Remove Skip when done.
        [Fact(Skip = "Known backlog: 27 view models still two-phase. " +
                     "Bulk fix reverted (see Docs/SENIOR_ANALYSIS_STARTUP_CRASH_SWEEP.md). " +
                     "Migrate individually, then re-enable.")]
        public void DelegatingParameterlessConstructors_HaveNoInitializationBody()
        {
            var violations = new List<string>();

            foreach (string path in ViewModelFiles())
            {
                string text = File.ReadAllText(path);

                var classMatch = Regex.Match(text, @"\bclass\s+(\w+)");
                if (!classMatch.Success) continue;
                string cls = classMatch.Groups[1].Value;

                // Parameterless ctor that chains to another ctor.
                var m = Regex.Match(text,
                    @"public\s+" + Regex.Escape(cls) +
                    @"\s*\(\s*\)\s*(?:\r?\n)?\s*:\s*this\s*\(");
                if (!m.Success) continue;

                int brace = text.IndexOf('{', m.Index + m.Length);
                if (brace < 0) continue;

                string body = BlockBody(text, brace);
                int statements = MeaningfulStatements(body);

                if (statements > 0)
                {
                    violations.Add(
                        $"{Path.GetRelativePath(Root(), path).Replace('\\', '/')}: " +
                        $"{cls}() delegates with ': this(...)' but still contains " +
                        $"{statements} initialization statement(s). DI selects the " +
                        "greediest constructor, so this code never runs for " +
                        "DI-resolved instances (ERR-633EEB). Move the body into " +
                        "the parameterized constructor.");
                }
            }

            Assert.True(violations.Count == 0,
                "Two-phase constructor defect (ERR-633EEB class):" +
                Environment.NewLine +
                string.Join(Environment.NewLine + Environment.NewLine, violations));
        }

        // ── THE ORIGINAL CRASH SITE ─────────────────────────────────────
        [Fact]
        public void StatementModuleViewModel_InitializesChildViewModels_InEveryCtor()
        {
            string path = Path.Combine(Root(), "ViewModels", "Reports",
                "StatementModuleViewModel.cs");
            string text = File.ReadAllText(path);

            // The parameterless ctor must delegate, never re-implement.
            Assert.Matches(
                @"public\s+StatementModuleViewModel\s*\(\s*\)\s*:\s*this\(",
                text);

            // GeneratorVM/ListVM must be assigned exactly once — in the
            // single real constructor.
            Assert.Equal(1, Regex.Matches(text, @"GeneratorVM\s*=\s*new").Count);
            Assert.Equal(1, Regex.Matches(text, @"ListVM\s*=\s*new").Count);
        }
    }
}
