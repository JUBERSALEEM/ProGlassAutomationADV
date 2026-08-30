using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Documents;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Documents
{
    public class DocumentListStatusTests
    {
        [Fact]
        public void Remaining_dn_does_not_double_count_ordered_qty()
        {
            var snap = SplitDeliverySnapshot();
            var jo = snap.ForJobOrder("JOB-2026-001");
            var pi = snap.ForProforma("PI-2026-001");

            Assert.Equal(20, jo.Ordered);
            Assert.Equal(10, jo.ConfirmedDelivered);
            Assert.Equal(20, pi.Ordered);
            Assert.Equal(10, pi.ConfirmedDelivered);
            Assert.Equal(DocumentListStatus.PartiallyDelivered,
                DocumentListStatus.ForJobOrder(jo.Ordered, jo.ConfirmedDelivered));
            Assert.Equal(DocumentListStatus.PartiallyDelivered,
                DocumentListStatus.ForProforma("Approved", true, pi).Status);
        }

        [Fact]
        public void Dispatching_qty_is_not_confirmed_delivery()
        {
            var snap = DocumentProgressSnapshot.From(
                Jobs(),
                new[] { Master(20, 0) },
                new[] { Dn("DN-1", ordered: 20, delivered: 10, status: "Dispatching") },
                invoices: null);

            var jo = snap.ForJobOrder("JOB-2026-001");
            Assert.Equal(20, jo.Ordered);
            Assert.Equal(0, jo.ConfirmedDelivered);
            Assert.Equal(0, jo.ConfirmedDnCount);
            Assert.Equal(DocumentListStatus.NotDelivered,
                DocumentListStatus.ForJobOrder(jo.Ordered, jo.ConfirmedDelivered));
        }

        [Fact]
        public void Draft_tax_invoice_does_not_mark_pi_fully_invoiced()
        {
            var snap = DocumentProgressSnapshot.From(
                Jobs(),
                new[] { Master(20, 20) },
                new[] { Dn("DN-1", ordered: 20, delivered: 20, status: "Dispatched") },
                new[] { Ti("Draft", 20, "DN-1") });

            var pi = snap.ForProforma("PI-2026-001");
            Assert.Equal(1, pi.DraftTiCount);
            Assert.Equal(0, pi.IssuedTiCount);
            Assert.Equal(0, pi.IssuedQty);
            Assert.Equal(DocumentListStatus.FullyDelivered,
                DocumentListStatus.ForProforma("Approved", true, pi).Status);
        }

        [Fact]
        public void Issued_invoices_covering_ordered_qty_are_fully_invoiced()
        {
            var snap = DocumentProgressSnapshot.From(
                Jobs(),
                new[] { Master(20, 20) },
                new[] { Dn("DN-1", ordered: 20, delivered: 20, status: "Delivered") },
                new[] { Ti("Issued", 20, "DN-1") });

            var pi = snap.ForProforma("PI-2026-001");
            var dn = DocumentListStatus.ForDeliveryFamily(snap.ForJobOrder("JOB-2026-001"));

            Assert.Equal(DocumentListStatus.FullyInvoiced,
                DocumentListStatus.ForProforma("Approved", true, pi).Status);
            Assert.Equal(DocumentListStatus.FullyInvoiced, dn.Status);
        }

        [Fact]
        public void Some_issued_qty_is_partially_invoiced_when_no_drafts_remain()
        {
            var snap = DocumentProgressSnapshot.From(
                Jobs(),
                new[] { Master(20, 20) },
                new[]
                {
                    Dn("DN-1", ordered: 20, delivered: 10, status: "Dispatched"),
                    Dn("DN-2", ordered: 20, delivered: 10, status: "Dispatched")
                },
                new[] { Ti("Issued", 10, "DN-1") });

            var pi = snap.ForProforma("PI-2026-001");
            var jo = snap.ForJobOrder("JOB-2026-001");

            Assert.Equal(10, pi.IssuedQty);
            Assert.Equal(1, jo.ConfirmedDnsCoveredByIssued);
            Assert.Equal(2, jo.ConfirmedDnCount);
            Assert.Equal(DocumentListStatus.PartiallyInvoiced,
                DocumentListStatus.ForProforma("Approved", true, pi).Status);
            Assert.Equal(DocumentListStatus.PartiallyInvoiced,
                DocumentListStatus.ForDeliveryFamily(jo).Status);
        }

        [Fact]
        public void Fully_delivered_job_is_present_in_snapshot()
        {
            var snap = DocumentProgressSnapshot.From(
                Jobs(),
                new[] { Master(8, 8) },
                new[] { Dn("DN-1", ordered: 8, delivered: 8, status: "Signed") },
                invoices: null);

            var jo = snap.ForJobOrder("JOB-2026-001");
            Assert.Equal(8, jo.Ordered);
            Assert.Equal(8, jo.ConfirmedDelivered);
            Assert.Equal(DocumentListStatus.FullyDelivered,
                DocumentListStatus.ForJobOrder(jo.Ordered, jo.ConfirmedDelivered));
        }

        [Fact]
        public void Dn_family_starts_as_dn_created()
        {
            var snap = SplitDeliverySnapshot();
            var label = DocumentListStatus.ForDeliveryFamily(snap.ForJobOrder("JOB-2026-001"));
            Assert.Equal(DocumentListStatus.DnCreated, label.Status);
        }

        [Fact]
        public void Cancelled_pi_stays_cancelled()
        {
            var snap = SplitDeliverySnapshot();
            Assert.Equal(DocumentListStatus.Cancelled,
                DocumentListStatus.ForProforma("Cancelled", false, snap.ForProforma("PI-2026-001")).Status);
        }

        [Fact]
        public void Lists_use_shared_progress_and_raise_on_ti_status()
        {
            var root = LocateProjectRoot();
            var piList = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.cs"));
            var joList = File.ReadAllText(Path.Combine(root,
                "ViewModels/JobOrder/SavedJobOrdersList/SavedJobOrdersListViewModel.cs"));
            var dnGroup = File.ReadAllText(Path.Combine(root,
                "ViewModels/DeliveryNote/DeliveryNoteList/JobOrderGroupRowViewModel.cs"));
            var tiRepo = File.ReadAllText(Path.Combine(root,
                "Services/TaxInvoice/Repository/TaxInvoiceRepository.cs"));

            Assert.Contains("DocumentProgressSnapshot.LoadAsync", piList);
            Assert.Contains("DocumentListStatus.ForProforma", piList);
            Assert.DoesNotContain("GetPendingQtyJobsAsync()", joList);
            Assert.Contains("DocumentProgressSnapshot.LoadAsync", joList);
            Assert.Contains("ForDeliveryFamily", dnGroup);
            Assert.DoesNotContain("StatusLabel = AllDelivered ? \"Completed\"", dnGroup);

            var statusBlock = ExtractionBetween(
                tiRepo,
                "public async Task UpdateStatusAsync(string invoiceNumber, string newStatus)",
                "// ── Attachments");
            Assert.Contains("DocumentLifecycle.Raise()", statusBlock);
        }

        private static DocumentProgressSnapshot SplitDeliverySnapshot() =>
            DocumentProgressSnapshot.From(
                Jobs(),
                new[] { Master(20, 10) },
                new[]
                {
                    Dn("DN-1", ordered: 20, delivered: 10, status: "Dispatched"),
                    Dn("DN-2", ordered: 20, delivered: 0, status: "Dispatching")
                },
                invoices: null);

        private static IEnumerable<(string JobOrderId, string SourceProformaId)> Jobs() =>
            new[] { ("JOB-2026-001", "PI-2026-001") };

        private static DeliveryNoteListItem Master(int ordered, int delivered) => new()
        {
            DeliveryNoteId = "MDN-2026-001",
            JobOrderId = "JOB-2026-001",
            ProformaId = "PI-2026-001",
            IsMasterDn = true,
            DeliveryType = "Dummy",
            TotalOrderedQty = ordered,
            TotalDeliveredQty = delivered,
            TotalBalanceQty = ordered - delivered
        };

        private static DeliveryNoteListItem Dn(
            string id, int ordered, int delivered, string status) => new()
        {
            DeliveryNoteId = id,
            JobOrderId = "JOB-2026-001",
            ProformaId = "PI-2026-001",
            Status = status,
            DeliveryType = "Original",
            TotalOrderedQty = ordered,
            TotalDeliveredQty = delivered,
            TotalBalanceQty = ordered - delivered
        };

        private static TaxInvoiceListItem Ti(
            string status, int qty, string dnRef) => new()
        {
            InvoiceNumber = "INV-2026-001",
            ProformaId = "PI-2026-001",
            JobOrderId = "JOB-2026-001",
            DeliveryNoteReference = dnRef,
            Status = status,
            TotalQty = qty
        };

        private static string ExtractionBetween(string source, string start, string end)
        {
            int a = source.IndexOf(start, System.StringComparison.Ordinal);
            Assert.True(a >= 0, "start marker missing");
            int b = source.IndexOf(end, a, System.StringComparison.Ordinal);
            Assert.True(b > a, "end marker missing");
            return source.Substring(a, b - a);
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
