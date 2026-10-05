// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Architecture/XamlDuplicatePropertyRatchetTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Architecture — prevent duplicate property definitions (MC3024/XLS0501)
//
// WHY THIS EXISTS
// ───────────────
// In XAML specification (§4.1.2), a property can only be set once on an element.
// Setting a property via attribute syntax AND via child property-element syntax
// (for example: <Border Style="..."> followed by <Border.Style>) halts Visual
// Studio and the WPF markup compiler with:
//
//   MC3024: '[Type].[Property]' property has already been set and can be set only once.
//   XLS0501: The property '[Property]' is set more than once.
//   XDG0039: The property "[Property]" is set multiple times.
//
// This architecture test statically verifies all XAML files across the entire
// solution and fails if ANY property (especially Style, but all properties) is
// set multiple times on any element.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Architecture
{
    public sealed class XamlDuplicatePropertyRatchetTests
    {
        [Fact]
        public void No_xaml_element_sets_any_property_more_than_once()
        {
            var root = Root();
            var violations = new List<string>();

            foreach (var path in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ||
                    rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) ||
                    rel.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) ||
                    rel.StartsWith("node_modules/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var doc = XDocument.Load(path);
                    foreach (var elem in doc.Descendants())
                    {
                        var elemName = elem.Name.LocalName;
                        var childPropElements = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                        foreach (var child in elem.Elements())
                        {
                            var childName = child.Name.LocalName;
                            if (childName.Contains('.'))
                            {
                                var parts = childName.Split('.', 2);
                                var propName = parts[1];
                                childPropElements[propName] = childPropElements.GetValueOrDefault(propName, 0) + 1;
                                if (childPropElements[propName] > 1)
                                {
                                    violations.Add($"{rel}: Property element <{childName}> declared multiple times on <{elemName}> (MC3024 / XLS0501).");
                                }
                            }
                        }

                        foreach (var attr in elem.Attributes())
                        {
                            var attrName = attr.Name.LocalName;
                            var attrProp = attrName.Contains('.') ? attrName.Split('.', 2)[1] : attrName;
                            if (childPropElements.ContainsKey(attrProp))
                            {
                                violations.Add($"{rel}: Property '{attrProp}' is set more than once on <{elemName}>: as an XML attribute ({attrName}=\"...\") AND as a child property element <{elemName}.{attrProp}>. This triggers compiler errors MC3024, XLS0501, and XDG0039.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    violations.Add($"{rel}: Failed to parse XAML: {ex.Message}");
                }
            }

            Assert.True(
                violations.Count == 0,
                $"Found {violations.Count} duplicate property violation(s) in XAML files:\n" +
                string.Join("\n", violations));
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
