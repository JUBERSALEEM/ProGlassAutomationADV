// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ResourceDictionaryOrderTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Parse-time resolvability of App.xaml's merged dictionaries
//
// REGRESSION GUARD FOR ERR-2FFFF1 (2026-09-02)
// ─────────────────────────────────────────────────────────────────────
// {StaticResource} is resolved at PARSE time against what is already
// defined. Merged dictionaries are parsed BEFORE the inline resource body
// of the dictionary that merges them. So a style dictionary merged in
// App.xaml can NEVER see a resource declared inline in App.xaml.
//
// When that rule was violated (converters declared inline, referenced by
// ShellStyles.xaml), the style dictionary aborted mid-parse. Every style
// it contained — including SidebarNavItem — silently ceased to exist, and
// the app died at the first consumer with a XamlParseException whose
// reported line number pointed at MainWindow.xaml, nowhere near the cause.
//
// These tests fail fast, at build time, with the ACTUAL offending file and
// key — instead of a fatal, misleading crash at customer startup.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class ResourceDictionaryOrderTests
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

            throw new DirectoryNotFoundException(
                "Could not locate repository root.");
        }

        private static string AppXaml =>
            File.ReadAllText(Path.Combine(Root(), "App.xaml"));

        /// <summary>Merged dictionary sources, in declaration order.</summary>
        private static List<string> MergedSources()
        {
            string app = AppXaml;
            int end = app.IndexOf("</ResourceDictionary.MergedDictionaries>",
                StringComparison.Ordinal);
            Assert.True(end > 0, "App.xaml has no MergedDictionaries block.");

            return Regex.Matches(app.Substring(0, end),
                    "<ResourceDictionary\\s+Source=\"([^\"]+)\"")
                .Select(m => m.Groups[1].Value)
                .ToList();
        }

        private static string ToRelativePath(string source) =>
            source.Replace("pack://application:,,,/", string.Empty)
                  .TrimStart('/')
                  .Replace('\\', '/');

        private static ISet<string> KeysIn(string xaml) =>
            new HashSet<string>(Regex.Matches(xaml, "x:Key=\"([^\"]+)\"")
                .Select(m => m.Groups[1].Value), StringComparer.Ordinal);

        private static ISet<string> StaticRefsIn(string xaml) =>
            new HashSet<string>(
                Regex.Matches(xaml, "\\{StaticResource\\s+([A-Za-z0-9_.]+)\\}")
                    .Select(m => m.Groups[1].Value), StringComparer.Ordinal);

        /// <summary>Keys declared INLINE in App.xaml (after the merge block).
        /// These are invisible to every merged dictionary.</summary>
        private static ISet<string> InlineAppKeys()
        {
            string app = AppXaml;
            int end = app.IndexOf("</ResourceDictionary.MergedDictionaries>",
                StringComparison.Ordinal);
            return KeysIn(app.Substring(end));
        }

        // ── 1. THE ERR-2FFFF1 INVARIANT ─────────────────────────────────
        // No merged dictionary may {StaticResource} a key that is only
        // declared inline in App.xaml.
        [Fact]
        public void MergedDictionaries_DoNotForwardReference_AppXamlInlineKeys()
        {
            var inline = InlineAppKeys();
            string root = Root();
            var violations = new List<string>();

            foreach (string source in MergedSources())
            {
                string path = Path.Combine(root, ToRelativePath(source));
                if (!File.Exists(path)) continue;

                string xaml = File.ReadAllText(path);
                var own = KeysIn(xaml);

                foreach (string key in StaticRefsIn(xaml))
                {
                    if (own.Contains(key)) continue;
                    if (!inline.Contains(key)) continue;

                    violations.Add(
                        $"{ToRelativePath(source)} references " +
                        $"{{StaticResource {key}}}, but '{key}' is declared " +
                        "INLINE in App.xaml and cannot be resolved at parse " +
                        "time. Move it into a dictionary merged BEFORE this " +
                        "one (e.g. Themes/Styles/ConverterResources.xaml).");
                }
            }

            Assert.True(violations.Count == 0,
                "Parse-time resource ordering violation (ERR-2FFFF1 class):" +
                Environment.NewLine +
                string.Join(Environment.NewLine, violations));
        }

        // ── 2. EVERY MERGED DICTIONARY RESOLVES IN ORDER ────────────────
        // A dictionary may only StaticResource keys it defines itself or
        // that an EARLIER-merged dictionary defined.
        [Fact]
        public void MergedDictionaries_ResolveAllStaticResources_InMergeOrder()
        {
            string root = Root();
            var available = new HashSet<string>(StringComparer.Ordinal);
            var violations = new List<string>();

            foreach (string source in MergedSources())
            {
                string path = Path.Combine(root, ToRelativePath(source));
                if (!File.Exists(path))
                {
                    violations.Add(
                        $"{ToRelativePath(source)} is merged by App.xaml but " +
                        "does not exist on disk.");
                    continue;
                }

                string xaml = File.ReadAllText(path);
                var own = KeysIn(xaml);

                foreach (string key in StaticRefsIn(xaml))
                {
                    if (own.Contains(key)) continue;
                    if (available.Contains(key)) continue;

                    violations.Add(
                        $"{ToRelativePath(source)} references " +
                        $"{{StaticResource {key}}} before any merged " +
                        "dictionary defines it.");
                }

                available.UnionWith(own);
            }

            Assert.True(violations.Count == 0,
                "Merged dictionary cannot resolve at parse time:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, violations));
        }

        // ── 3. CONVERTERS STAY OUT OF App.xaml's INLINE BODY ────────────
        // The structural fix. Converters are stateless and theme-independent,
        // so they belong in the first-merged dictionary where everything can
        // see them.
        [Fact]
        public void Converters_AreDeclaredInConverterResources_NotInlineInAppXaml()
        {
            string app = AppXaml;
            int end = app.IndexOf("</ResourceDictionary.MergedDictionaries>",
                StringComparison.Ordinal);

            Assert.False(app.Substring(end).Contains("<conv:", StringComparison.Ordinal),
                "A converter is declared inline in App.xaml. Declare it in " +
                "Themes/Styles/ConverterResources.xaml instead — inline " +
                "converters are invisible to merged style dictionaries and " +
                "cause ERR-2FFFF1-class startup crashes.");
        }

        [Fact]
        public void ConverterResources_IsMergedFirst()
        {
            string first = MergedSources().FirstOrDefault() ?? string.Empty;

            Assert.Contains("ConverterResources.xaml", first,
                StringComparison.Ordinal);
        }

        // ── 4. THE ORIGINAL CRASH SITE ──────────────────────────────────
        [Fact]
        public void SidebarNavItem_IsDefined_AndResolvable()
        {
            string shell = File.ReadAllText(Path.Combine(
                Root(), "Themes", "Styles", "ShellStyles.xaml"));

            Assert.Contains("x:Key=\"SidebarNavItem\"", shell,
                StringComparison.Ordinal);

            // ShellStyles must be merged, and after the converters it uses.
            var sources = MergedSources().Select(ToRelativePath).ToList();
            int converters = sources.FindIndex(
                s => s.EndsWith("ConverterResources.xaml", StringComparison.Ordinal));
            int shellIndex = sources.FindIndex(
                s => s.EndsWith("ShellStyles.xaml", StringComparison.Ordinal));

            Assert.True(shellIndex >= 0, "ShellStyles.xaml is not merged in App.xaml.");
            Assert.True(converters >= 0, "ConverterResources.xaml is not merged.");
            Assert.True(converters < shellIndex,
                "ConverterResources.xaml must be merged BEFORE ShellStyles.xaml.");
        }
    }
}
