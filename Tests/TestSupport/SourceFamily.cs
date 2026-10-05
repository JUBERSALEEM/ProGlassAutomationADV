using System;
using System.IO;
using System.Linq;

namespace ProGlassAutomation.Tests.TestSupport
{
    /// <summary>
    /// Source-reading helper for source-level assertion tests.
    ///
    /// Phase 3.8 split large files into partial families (X.cs +
    /// X.PartN.cs). A test that asserts on source text must read the
    /// WHOLE family — the member body being asserted may live in a
    /// part. This helper concatenates X.cs with every X.PartN.cs
    /// sibling in the same directory, in ordinal order.
    ///
    /// The App family uses a second convention: App.xaml.cs +
    /// App.StartupCore.cs / App.AuthHandlers.cs / ... — those parts
    /// carry the startup theme chain, so "App.xaml.cs" reads include
    /// every App.*.cs sibling.
    /// </summary>
    public static class SourceFamily
    {
        public static string Read(string root, string relative)
        {
            var full = Path.Combine(root, relative);
            var sb = new System.Text.StringBuilder(File.ReadAllText(full));
            string dir = Path.GetDirectoryName(full)!;
            string name = Path.GetFileNameWithoutExtension(relative);
            foreach (var part in Directory.EnumerateFiles(dir, name + ".Part*.cs")
                         .OrderBy(f => f, StringComparer.Ordinal))
                sb.Append(File.ReadAllText(part));

            string stem = name.Contains('.')
                ? name.Substring(0, name.IndexOf('.'))
                : name;
            if (stem == "App")
            {
                foreach (var part in Directory.EnumerateFiles(dir, "App.*.cs")
                             .Where(p => !p.EndsWith("App.xaml.cs", StringComparison.Ordinal))
                             .OrderBy(f => f, StringComparer.Ordinal))
                    sb.Append(File.ReadAllText(part));
            }
            return sb.ToString();
        }
    }
}
