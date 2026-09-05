// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/TaxInvoice/LpoReuseWarningTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// AUDIT FINDING (2026-09-05, P1 — LPO is a text field, not a guard):
// Entering the same customer's LPO/PO reference on a second, UNRELATED
// invoice family is the classic double-billing path at the printer.
// The minimum-viable fix (P1 "fields + validation first, module later"):
// non-blocking cross-family warning at save time. These facts pin it.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using Xunit;

namespace ProGlassAutomation.Tests.TaxInvoice
{
    public sealed class LpoReuseWarningTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(LocateProjectRoot(), relative));

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new FileNotFoundException("Project root not found");
        }

        // ── 1. REPOSITORY SURFACE: the PO-lookup query exists ───────────
        [Fact]
        public void Repository_exposes_purchase_order_lookup()
        {
            var method = typeof(ITaxInvoiceRepository).GetMethod(
                "GetByPurchaseOrderAsync");
            Assert.NotNull(method);
            Assert.Equal(typeof(Task<>).Name, method!.ReturnType.Name);

            var impl = typeof(TaxInvoiceRepository).GetMethod(
                "GetByPurchaseOrderAsync");
            Assert.NotNull(impl);
        }

        // ── 2. QUERY SHAPE: same customer + same PO, list items only ────
        [Fact]
        public void Query_filters_on_po_and_customer_and_projects_list_items()
        {
            string src = Read(
                "Services/TaxInvoice/Repository/TaxInvoiceRepository.cs");
            Assert.Contains(
                "x.PurchaseOrderNumber == poNumber", src,
                StringComparison.Ordinal);
            Assert.Contains(
                "x.CustomerName == customerName", src, StringComparison.Ordinal);
        }

        // ── 3. SAVE PATH: warning is cross-family only and non-final ────
        [Fact]
        public void Save_warns_on_cross_family_po_reuse_only()
        {
            string src = Read(
                "ViewModels/TaxInvoice/TaxInvoiceDetail/" +
                "TaxInvoiceDetailViewModel.Save.cs");

            // Cross-family filter (same-family continuation is legitimate)
            Assert.Contains("c.ProformaId ?? \"\", ProformaId ?? \"\"",
                src, StringComparison.Ordinal);
            Assert.Contains("StringComparison.OrdinalIgnoreCase",
                src, StringComparison.Ordinal);

            // Advisory only: failure of the check READ logs and continues
            Assert.Contains(
                "PO-reuse check skipped", src, StringComparison.Ordinal);

            // Operator can still save when the reuse is intentional
            Assert.Contains(
                "Save anyway?", src, StringComparison.Ordinal);
        }

        // ── 4. NO SCHEMA TOUCH: the fix is fields + validation only ──────
        [Fact]
        public void Fix_introduces_no_new_entity_or_column()
        {
            // P1 scope discipline: LPO minimum = existing fields + warning.
            // A dedicated LPO entity is deliberately NOT added here (that
            // would be the P1 module work, gated behind customer scope).
            string root = LocateProjectRoot();
            Assert.False(Directory.EnumerateFiles(
                    Path.Combine(root, "Models"), "*Lpo*.cs",
                    SearchOption.AllDirectories).Any(),
                "Lpo* model files must not sneak in via the warning fix");
        }
    }
}
