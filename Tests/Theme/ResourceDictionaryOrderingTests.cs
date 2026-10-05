// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/ResourceDictionaryOrderingTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Resource dictionary declaration order
//
// WHY THIS EXISTS
// ───────────────
// 2026-09-26 crash report ERR-A537B3: opening the Optimization module killed
// the view with
//
//   XamlParseException: Set property 'FrameworkElement.Style' threw an exception
//     InvalidCastException: Unable to cast object of type 'MS.Internal.NamedObject'
//                           to type 'System.Windows.Style'
//     at System.Windows.Controls.ItemsControl.OnItemContainerStyleChanged(...)
//
// Cause: OptHeroComboStyle was declared near the top of
// Themes/Styles/OptimizationStyles.xaml and set
// ItemContainerStyle="{StaticResource OptComboItem}", but OptComboItem is
// declared several hundred lines further down the SAME file. A
// StaticResource inside a ResourceDictionary is resolved while that
// dictionary loads, so it has to point BACKWARD. Pointing forward resolves
// to the MS.Internal.NamedObject sentinel, and the cast to Style blows up
// the first time the template is applied — at runtime, not at build time,
// so the compiler never sees it and the solution builds clean.
//
// This lock walks every theme dictionary and fails when a StaticResource
// points at a key declared later in the same file.
//
// DynamicResource is deliberately excluded: it resolves by name when the
// element loads, by which time the dictionary is complete, so its position
// does not matter and flagging it would only produce false failures.
//
// XML comments are stripped before scanning so that prose (including the
// comment that documents this very bug) can never fail the build.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public sealed class ResourceDictionaryOrderingTests
    {
        private const string OptimizationStyles = "Themes/Styles/OptimizationStyles.xaml";

        /// <summary>
        /// No theme dictionary may consume a resource it declares later.
        /// This is the general form of ERR-A537B3.
        /// </summary>
        [Fact]
        public void ThemeDictionaries_NeverReferenceAResourceDeclaredLaterInTheSameFile()
        {
            var root = LocateProjectRoot();
            var offenders = new List<string>();

            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(root, "Themes"), "*.xaml", SearchOption.AllDirectories))
            {
                var body = StripComments(File.ReadAllText(file));
                var declaredAt = DeclarationOffsets(body);

                foreach (Match m in Regex.Matches(body, @"\{StaticResource ([A-Za-z0-9_]+)\}"))
                {
                    var key = m.Groups[1].Value;
                    if (declaredAt.TryGetValue(key, out var at) && at > m.Index)
                    {
                        var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                        offenders.Add($"  {rel}: '{key}' is used at offset {m.Index} but declared at offset {at}");
                    }
                }
            }

            Assert.True(
                offenders.Count == 0,
                "Forward StaticResource reference(s) in a resource dictionary (ERR-A537B3 regression). " +
                "A StaticResource must point at a key declared EARLIER in the same file — otherwise it " +
                "resolves to the MS.Internal.NamedObject sentinel and throws InvalidCastException when the " +
                "template is applied at runtime. Move the consuming style below the resource it uses:" +
                Environment.NewLine + string.Join(Environment.NewLine, offenders));
        }

        /// <summary>
        /// The specific pair that shipped the crash: the hero combo's item
        /// container style must be declared before the combo style that
        /// consumes it. Kept as an explicit lock so the intent survives even
        /// if the general scan above is ever narrowed.
        /// </summary>
        [Fact]
        public void OptimizationStyles_DeclaresOptComboItemBeforeItsConsumers()
        {
            var body = StripComments(File.ReadAllText(Path.Combine(LocateProjectRoot(), OptimizationStyles)));

            var comboItem = IndexOfKey(body, "OptComboItem");
            var heroCombo = IndexOfKey(body, "OptHeroComboStyle");
            var combo = IndexOfKey(body, "OptComboStyle");

            Assert.True(comboItem >= 0, OptimizationStyles + " must declare OptComboItem.");
            Assert.True(heroCombo >= 0, OptimizationStyles + " must declare OptHeroComboStyle.");
            Assert.True(combo >= 0, OptimizationStyles + " must declare OptComboStyle.");

            Assert.True(
                comboItem < heroCombo,
                "OptComboItem must be declared before OptHeroComboStyle — it is set as that style's " +
                "ItemContainerStyle, and a forward StaticResource here is exactly what produced " +
                "ERR-A537B3 (InvalidCastException from ItemsControl.OnItemContainerStyleChanged).");

            Assert.True(
                comboItem < combo,
                "OptComboItem must be declared before OptComboStyle — it is set as that style's ItemContainerStyle.");
        }

        // ── helpers ──────────────────────────────────────────────────

        private static string StripComments(string xaml) =>
            Regex.Replace(xaml, "<!--.*?-->", string.Empty, RegexOptions.Singleline);

        private static Dictionary<string, int> DeclarationOffsets(string xaml)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(xaml, "x:Key=\"([A-Za-z0-9_]+)\""))
            {
                if (!map.ContainsKey(m.Groups[1].Value))
                {
                    map.Add(m.Groups[1].Value, m.Index);
                }
            }

            return map;
        }

        private static int IndexOfKey(string xaml, string key)
        {
            var m = Regex.Match(xaml, "x:Key=\"" + Regex.Escape(key) + "\"");
            return m.Success ? m.Index : -1;
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName, "ProGlassAutomation.csproj");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
