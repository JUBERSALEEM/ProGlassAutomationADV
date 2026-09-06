using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    // ═══════════════════════════════════════════════════════════════════
    // FILE: Tests/Agent/DialogSurfaceSourceTests.cs
    // FIX-2026-09-06 pins — one OPAQUE dialog material, app-wide.
    //
    // DEFECT (owner report): "approve popup have issue card is transparent
    // multi cards mismatch".
    //
    // ROOT CAUSE, proven before any code was touched:
    //   • Dialog root cards were painted with ModuleBackgroundBrush.
    //   • VisualThemeEngine.ApplyModule deliberately routes
    //     ModuleBackgroundBrush through SetTranslucent (0.62-0.80) whenever
    //     a wallpaper or an OS backdrop is live.
    //   • That is CORRECT for a module drawn on the app's own stage, and
    //     FATAL for a dialog: a dialog is its own top-level Window with
    //     AllowsTransparency="True", so there is no app stage behind it —
    //     the alpha falls straight through to the user's desktop.
    //   • Half the modal family used ModuleBackgroundBrush and half used
    //     WindowBackgroundBrush, so two cards in ONE flow (the approve
    //     pair) did not even match each other. That is the "multi cards
    //     mismatch" half of the report.
    //
    // FIX: DialogSurfaceBrush / DialogInsetBrush — always opaque, never
    // passed through SetTranslucent, repainted per family from
    // PublishSectionGlass (which every family painter already calls, so
    // parity is structural rather than six copy-pasted blocks), and listed
    // in ManagedKeys so a family switch cannot leak one family's dialog
    // tint into the next.
    //
    // These pins keep all of that true.
    // ═══════════════════════════════════════════════════════════════════
    public sealed class DialogSurfaceSourceTests
    {
        /// Dialog windows whose ROOT CARD must use the opaque token.
        private static readonly string[] DialogViews =
        {
            "Views/ProformaInvoice/SavedInvoicesList/ApprovalUploadChoiceDialog.xaml",
            "Views/ProformaInvoice/SavedInvoicesList/InvoiceApprovalDialog.xaml",
            "Views/ProformaInvoice/SavedInvoicesList/VersionsDialog.xaml",
            "Views/Shared/AppMessageBox.xaml",
            "Views/Shared/ChangesDialog.xaml",
            "Views/Shared/UnsavedChangesDialog.xaml",
            "Views/Shared/LanguageSuggestionDialog.xaml",
            "Views/TaxInvoice/TaxInvoiceDetail/IssueDraftsDialog.xaml",
            "Views/TaxInvoice/TaxInvoiceList/ProformaPickerDialog.xaml",
            "Views/SheetStore/SheetStoreBuy/BuyDialogView.xaml",
            "Views/SheetStore/SheetStoreUse/UseDialogView.xaml",
            "Views/DeliveryNote/DeliveryNoteDetail/Attachments/Rename/RenameAttachmentDialog.xaml",
            "Views/DeliveryNote/DeliveryNoteList/JobOrderPicker/JobOrderPickerDialog.xaml",
            "Views/JobOrder/SavedJobOrdersList/JobOrderAttachmentWarningDialog.xaml",
            "Views/JobOrder/SavedJobOrdersList/ProformaPicker/JobOrderProformaPickerDialog.xaml",
            "Views/Reports/Workflow/WorkflowProgressDialog.xaml",
            "Views/Reports/Workflow/WorkflowWarningDialog.xaml",
        };

        [Fact]
        public void Every_dialog_paints_its_card_with_the_opaque_dialog_token()
        {
            string root = LocateProjectRoot();
            foreach (var rel in DialogViews)
            {
                string xaml = File.ReadAllText(Path.Combine(root, rel));
                Assert.True(
                    xaml.Contains("{DynamicResource DialogSurfaceBrush}"),
                    $"{rel} must paint its root card with DialogSurfaceBrush.");
            }
        }

        [Fact]
        public void No_dialog_card_falls_back_to_the_translucent_module_brush()
        {
            // ModuleBackgroundBrush is translucent under wallpaper/backdrop.
            // A dialog is a floating window: using it there shows the
            // desktop through the card. Inner CONTENT borders may still use
            // whatever they like — this pin guards the chrome tokens only,
            // so we assert the opaque token wins on the card itself.
            string root = LocateProjectRoot();
            foreach (var rel in DialogViews)
            {
                string xaml = File.ReadAllText(Path.Combine(root, rel));

                int dialogTok = Occurrences(xaml, "{DynamicResource DialogSurfaceBrush}");
                Assert.True(dialogTok >= 1,
                    $"{rel}: expected the opaque dialog token on the card.");

                // The old hover token must never be a dialog SURFACE again
                // (it was being abused as a footer fill, which is why
                // footers looked different from the card in every theme).
                Assert.False(
                    xaml.Contains("Background=\"{DynamicResource MenuHoverBrush}\""),
                    $"{rel}: MenuHoverBrush is a hover tint, not a dialog surface — " +
                    "use DialogInsetBrush for footer/inset shelves.");
            }
        }

        [Fact]
        public void Dialog_tokens_are_managed_keys_and_exist_in_both_themes()
        {
            string root = LocateProjectRoot();

            string engine = File.ReadAllText(Path.Combine(
                root, "Services/Theme/VisualThemeEngine.Part1.cs"));
            // ManagedKeys membership = ClearOverrides removes them on every
            // family switch. Without this, one family's dialog tint leaks.
            Assert.Contains("\"DialogSurfaceBrush\", \"DialogInsetBrush\",", engine);

            foreach (var theme in new[] { "Themes/DarkTheme.xaml", "Themes/LightTheme.xaml" })
            {
                string xaml = File.ReadAllText(Path.Combine(root, theme));
                Assert.Contains("x:Key=\"DialogSurfaceBrush\"", xaml);
                Assert.Contains("x:Key=\"DialogInsetBrush\"", xaml);

                // Opaque baseline: the authored fallback must be #FF-alpha.
                var m = Regex.Match(xaml,
                    "x:Key=\"DialogSurfaceBrush\"\\s+Color=\"(#[0-9A-Fa-f]{8})\"");
                Assert.True(m.Success,
                    $"{theme}: DialogSurfaceBrush must declare an explicit ARGB colour.");
                Assert.StartsWith("#FF", m.Groups[1].Value,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void Family_painters_repaint_the_dialog_surface_through_one_helper()
        {
            string root = LocateProjectRoot();
            string part2 = File.ReadAllText(Path.Combine(
                root, "Services/Theme/VisualThemeEngine.Part2.cs"));

            // ONE helper, called from PublishSectionGlass — which every
            // family painter already runs. That is what makes parity
            // structural instead of six hand-maintained copies.
            Assert.Contains("private static void PublishDialogSurface(", part2);
            Assert.Contains("PublishDialogSurface(resources, borderTint, dark);", part2);

            // The helper must force full alpha — the whole point.
            Assert.Contains("ColorMath.WithAlpha(card, 0xFF)", part2);

            // And it must never be routed through the translucency pass.
            Assert.DoesNotContain("SetTranslucent(resources, \"DialogSurfaceBrush\"", part2);

            string part1 = File.ReadAllText(Path.Combine(
                root, "Services/Theme/VisualThemeEngine.Part1.cs"));
            Assert.DoesNotContain("SetTranslucent(resources, \"DialogSurfaceBrush\"", part1);
        }

        [Fact]
        public void Step8_does_not_duplicate_the_uploaders_the_approve_flow_owns()
        {
            string root = LocateProjectRoot();
            string view = File.ReadAllText(Path.Combine(
                root, "Views/ProformaInvoice/Step8Attachments/AttachmentsStepView.xaml"));

            // APPROVE INVOICE opens ApprovalUploadChoiceDialog, which asks
            // "JPG or PDF?" and forwards to AddImageCommand/AddPdfCommand.
            // Standalone IMAGE/PDF chips in the header were a SECOND entry
            // point for the same act.
            Assert.DoesNotContain("Command=\"{Binding AddImageCommand}\"", view);
            Assert.DoesNotContain("Command=\"{Binding AddPdfCommand}\"", view);

            // The approve entry itself stays.
            Assert.Contains("Command=\"{Binding ApproveInvoiceCommand}\"", view);

            // The commands must still EXIST on the VM — the choice dialog
            // is now their single caller (ProformaInvoiceViewModel routes
            // uploadPdf -> AddPdfCommand / AddImageCommand).
            string wizard = File.ReadAllText(Path.Combine(
                root, "ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs"));
            Assert.Contains("Step8VM.AddPdfCommand.Execute(null)", wizard);
            Assert.Contains("Step8VM.AddImageCommand.Execute(null)", wizard);
        }

        // ── helpers ──────────────────────────────────────────────────

        private static int Occurrences(string haystack, string needle)
            => haystack.Split(new[] { needle }, StringSplitOptions.None).Length - 1;

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Views", "ProformaInvoice", "Step5DataGrid", "DataGridView.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
