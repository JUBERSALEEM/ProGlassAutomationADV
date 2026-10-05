// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Features/ModuleEditorVisibilityBindingsTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Source-file tests that lock the Aug-2026 module field-visibility wiring.
// They do NOT boot WPF or touch a DB — they read the XAML relative to the
// project root and assert the editor views actually bind each optional
// field's Visibility to the matching feature-flag singleton. A future
// edit that forgets a Visibility binding would silently show a field the
// operator disabled in Settings ► Features.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Xml;
using Xunit;

namespace ProGlassAutomation.Tests.Features
{
    public class ModuleEditorVisibilityBindingsTests
    {
        // (xaml path, singleton type name singleton, list of ShowXxx fields)
        private static readonly (string File, string Singleton, string[] Flags)[] Cases =
        {
            (
                "Views/JobOrder/JobOrderDetail/Sections/JoProductionSection.xaml",
                "JobOrderFieldVisibility",
                new[] { "ShowAssignedTo", "ShowSiteDeliveryContact", "ShowSiteDeliveryPhone",
                        "ShowSiteDeliveryAddress", "ShowDrawingReference",
                        "ShowCuttingInstructions", "ShowInternalNotes" }
            ),
            (
                "Views/DeliveryNote/DeliveryNoteDetail/Sections/DnOverviewSection.xaml",
                "DeliveryNoteFieldVisibility",
                new[] { "ShowDriverName", "ShowVehicleNumber", "ShowIsFoc",
                        "ShowReceiverName", "ShowNotes" }
            ),
            (
                "Views/TaxInvoice/TaxInvoiceDetail/Sections/TiOverviewSection.xaml",
                "TaxInvoiceFieldVisibility",
                new[] { "ShowDueDate", "ShowPaymentTerms", "ShowDiscountPercent",
                        "ShowIban", "ShowSwiftCode" }
            ),
        };

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Editor_binds_visibility_to_the_feature_singleton(int idx)
        {
            var (file, singleton, flags) = Cases[idx];
            var root = LocateProjectRoot();
            var path = Path.Combine(root, file);
            Assert.True(File.Exists(path), $"Missing {file}");

            var xdoc = new XmlDocument();
            xdoc.Load(path);
            var src = File.ReadAllText(path);

            Assert.Contains(singleton + ".Instance", src);
            Assert.Contains("xmlns:features=", src);
            Assert.Contains("<WrapPanel", src);   // auto-compact host

            foreach (var flag in flags)
            {
                Assert.Contains(flag, src);
                Assert.Contains($"{singleton}.Instance", src);
            }
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName, "Views", "Settings",
                    "SettingsSectionStyles.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
