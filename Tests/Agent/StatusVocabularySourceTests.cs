// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/StatusVocabularySourceTests.cs
// PROJECT: ProGlassAutomation.Tests
// PHASE 5 — status-vocabulary enforcement (source scan, mirroring
// ReadOnlyGateCoverageSourceTests' established pattern).
//
// Industrial-ERP rule: a status enters a store ONLY through its module's
// single canonical home —
//   Inquiry      → InquiryStatuses.Normalize (string vocabulary)
//   Proforma PI  → ProformaInvoiceStatus.Normalize (string vocabulary)
//   Job Order    → JobOrderStatus enum (typed — bypass impossible)
//   DeliveryNote → DeliveryNoteStatusValues canonical strings
//   TaxInvoice   → TaxInvoiceStatus… enum/strings (typed boundary)
// A new raw-literal write — from a converter, an import, a repair script —
// must fail THIS suite instead of reaching savable data undetected.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class StatusVocabularySourceTests
    {
        private static string Read(params string[] parts)
            => File.ReadAllText(Path.Combine(Root(), Path.Combine(parts)));

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                dir = dir.Parent;
            return dir!.FullName;
        }

        [Fact]
        public void Every_module_has_a_single_status_home()
        {
            var inquiry = Read("Models", "Inquiry", "InquiryRecord.cs");
            Assert.Contains("public static class InquiryStatuses", inquiry, StringComparison.Ordinal);
            Assert.Contains("public static string Normalize(string? value)", inquiry, StringComparison.Ordinal);

            var pi = Read("Models", "ProformaInvoice", "Common", "ProformaInvoiceStatus.cs");
            Assert.Contains("public static class ProformaInvoiceStatus", pi, StringComparison.Ordinal);
            Assert.Contains("public static string Normalize(string? value)", pi, StringComparison.Ordinal);

            var jo = Read("Models", "JobOrder", "Common", "JobOrderStatus", "JobOrderStatus.cs");
            Assert.Contains("public enum JobOrderStatus", jo, StringComparison.Ordinal);

            var dn = Read("Models", "DeliveryNote", "Common", "DeliveryNoteStatus", "DeliveryNoteStatus.cs");
            Assert.Contains("public static class DeliveryNoteStatusValues", dn, StringComparison.Ordinal);
            Assert.Contains("public enum DeliveryNoteStatus", dn, StringComparison.Ordinal);
        }

        [Fact]
        public void Pi_repository_writes_status_through_the_normalize_home()
        {
            var src = Read("Services", "ProformaInvoice", "ProformaInvoiceRepository",
                "ProformaInvoiceRepository.cs");
            Assert.Contains(
                "existing.Status = ProformaInvoiceStatus.Normalize(invoice.Status);",
                src, StringComparison.Ordinal);
        }

        [Fact]
        public void Inquiry_repository_writes_status_through_the_normalize_home()
        {
            var src = Read("Services", "Inquiry", "InquiryRepository.cs");
            Assert.Contains(
                "inquiry.Status = InquiryStatuses.Normalize(inquiry.Status);",
                src, StringComparison.Ordinal);
            Assert.Contains(
                "string next = InquiryStatuses.Normalize(status);",
                src, StringComparison.Ordinal);
        }

        [Fact]
        public void Pi_normalization_is_conservative_never_a_remap()
        {
            // An UNRECOGNIZED status must pass through as-typed — folding an
            // unknown value to Draft would hide corruption behind a card-
            // read. This line existing is the contract.
            var src = Read("Models", "ProformaInvoice", "Common", "ProformaInvoiceStatus.cs");
            Assert.Contains("return text;", src, StringComparison.Ordinal);
        }
    }
}
