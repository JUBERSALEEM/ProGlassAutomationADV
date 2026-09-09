// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Sharing/ShareContextFactoryTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the one reviewed entity→context mapping per document type:
// identity, contacts, dates (PI validity wins over issue date),
// books currency (always AED), and title behavior for saved
// statements/reports versus composed type·number titles.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.OrderReport;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Sharing;
using System;
using Xunit;

namespace ProGlassAutomation.Tests.Sharing
{
    public class ShareContextFactoryTests
    {
        [Fact]
        public void FromProformaInvoice_maps_identity_contacts_and_validity()
        {
            var entity = new SavedProformaInvoice
            {
                ProformaId = "PI-2026-001",
                CompanyName = "ProGlass LLC",
                CustomerName = "Skyline Interiors LLC",
                IssueDate = new DateTime(2026, 8, 1),
                GrandTotalVatInclusive = 10500.00m
            };
            var snapshot = new ProformaInvoiceSnapshot
            {
                Company = new CompanySnapshot
                {
                    Representative = "Juber Saleem",
                    RepresentativeMobile = "052 111 2233",
                    CompanyEmail = "ops@proglass.example",
                    ValidUntil = new DateTime(2026, 8, 31)
                },
                Client = new ClientSnapshot
                {
                    AttentionExecutiveName = "Mr. Ahmed",
                    ClientContactPhone = "050 123 4567",
                    SalesRepresentative = "Ravi"
                },
                PreparedBy = new PreparedBySnapshot
                {
                    PreparedByName = "Juber",
                    PreparedByDept = "Estimation"
                }
            };

            ShareContext ctx =
                ShareContextFactory.FromProformaInvoice(entity, snapshot);

            Assert.Equal("PI", ctx.DocumentTypeKey);
            Assert.Equal("PI-2026-001", ctx.DocumentNumber);
            Assert.Equal("Mr. Ahmed", ctx.AttentionName);
            Assert.Equal("050 123 4567", ctx.CustomerPhone);
            Assert.Equal("Ravi", ctx.SalesmanName);
            Assert.Equal("Juber Saleem", ctx.CompanyRepresentative);
            Assert.Equal("ops@proglass.example", ctx.CompanyEmail);
            Assert.Equal(new DateTime(2026, 8, 31), ctx.DocumentDate);
            Assert.Equal(10500.00m, ctx.Amount);
            Assert.Equal("AED", ctx.Currency);
            Assert.Equal("Proforma Invoice · PI-2026-001", ctx.DisplayTitle);
        }

        [Fact]
        public void FromProformaInvoice_falls_back_to_issue_date_without_validity()
        {
            var entity = new SavedProformaInvoice
            {
                ProformaId = "PI-2026-002",
                IssueDate = new DateTime(2026, 8, 5)
            };

            ShareContext ctx = ShareContextFactory.FromProformaInvoice(
                entity, new ProformaInvoiceSnapshot());

            Assert.Equal(new DateTime(2026, 8, 5), ctx.DocumentDate);
        }

        [Fact]
        public void FromJobOrder_prefers_delivery_date()
        {
            var entity = new SavedJobOrder
            {
                JobOrderId = "JO-2026-010",
                CustomerName = "Skyline Interiors LLC",
                GrandTotalVatInclusive = 8000m,
                DeliveryDate = new DateTime(2026, 9, 20),
                CreatedAt = new DateTime(2026, 9, 1)
            };

            ShareContext ctx = ShareContextFactory.FromJobOrder(entity);

            Assert.Equal("JO", ctx.DocumentTypeKey);
            Assert.Equal("JO-2026-010", ctx.DocumentNumber);
            Assert.Equal(new DateTime(2026, 9, 20), ctx.DocumentDate);
            Assert.Equal(8000m, ctx.Amount);
            Assert.Equal("AED", ctx.Currency);
        }

        [Fact]
        public void FromDeliveryNote_maps_receiver_as_attention()
        {
            var entity = new SavedDeliveryNote
            {
                DeliveryNoteId = "DN-2026-003",
                CustomerName = "Skyline Interiors LLC",
                ReceiverName = "Site Storekeeper",
                DeliveryDate = new DateTime(2026, 9, 10)
            };

            ShareContext ctx = ShareContextFactory.FromDeliveryNote(entity);

            Assert.Equal("DN", ctx.DocumentTypeKey);
            Assert.Equal("Site Storekeeper", ctx.AttentionName);
            Assert.Equal(new DateTime(2026, 9, 10), ctx.DocumentDate);
        }

        [Fact]
        public void FromTaxInvoice_reads_nested_snapshot_channels()
        {
            var entity = new SavedTaxInvoice
            {
                InvoiceNumber = "INV-2026-001",
                CustomerName = "Skyline Interiors LLC",
                CompanyName = "ProGlass LLC",
                IssueDate = new DateTime(2026, 9, 2),
                GrandTotal = 11025.00m
            };
            var snapshot = new TaxInvoiceSnapshot
            {
                Customer = new InvCustomerSnapshot
                {
                    ContactPerson = "Ms. Sara",
                    CustomerPhone = "055 999 1122",
                    CustomerEmail = "sara@skyline.example"
                },
                Company = new InvCompanySnapshot
                {
                    CompanyEmail = "accounts@proglass.example"
                },
                Header = new InvHeaderSnapshot
                {
                    IssueDate = new DateTime(2026, 9, 3)
                },
                PreparedBy = new InvPreparedBySnapshot
                {
                    Name = "Juber",
                    Department = "Accounts"
                }
            };

            ShareContext ctx =
                ShareContextFactory.FromTaxInvoice(entity, snapshot);

            Assert.Equal("TI", ctx.DocumentTypeKey);
            Assert.Equal("INV-2026-001", ctx.DocumentNumber);
            Assert.Equal("Ms. Sara", ctx.AttentionName);
            Assert.Equal("055 999 1122", ctx.CustomerPhone);
            Assert.Equal("sara@skyline.example", ctx.CustomerEmail);
            Assert.Equal("accounts@proglass.example", ctx.CompanyEmail);
            Assert.Equal(new DateTime(2026, 9, 3), ctx.DocumentDate);
            Assert.Equal("AED", ctx.Currency);
        }

        [Fact]
        public void FromStatement_uses_saved_title_or_fallback()
        {
            var titled = new SavedStatement
            {
                StatementNumber = "ST-2026-001",
                Title = "August 2026 Statement",
                FilterCustomer = "Skyline Interiors LLC",
                FilterCompany = "ProGlass LLC",
                FilterSalesman = "Ravi",
                CreatedAt = new DateTime(2026, 9, 1)
            };
            var untitled = new SavedStatement { StatementNumber = "ST-2" };

            ShareContext a = ShareContextFactory.FromStatement(titled);
            ShareContext b = ShareContextFactory.FromStatement(untitled);

            Assert.Equal("August 2026 Statement", a.DisplayTitle);
            Assert.Equal("Skyline Interiors LLC", a.CustomerName);
            Assert.Equal(new DateTime(2026, 9, 1), a.DocumentDate);
            Assert.Equal("Statement ST-2", b.DisplayTitle);
        }

        [Fact]
        public void FromOrderReport_uses_saved_title_or_fallback()
        {
            var titled = new SavedOrderReport
            {
                ReportNumber = "OR-2026-001",
                Title = "September Order Review",
                FilterCustomer = "Skyline Interiors LLC",
                CreatedAt = new DateTime(2026, 9, 4)
            };
            var untitled = new SavedOrderReport { ReportNumber = "OR-2" };

            ShareContext a = ShareContextFactory.FromOrderReport(titled);
            ShareContext b = ShareContextFactory.FromOrderReport(untitled);

            Assert.Equal("September Order Review", a.DisplayTitle);
            Assert.Equal("Order Report OR-2", b.DisplayTitle);
        }

        [Fact]
        public void FromListReport_builds_title_led_context()
        {
            ShareContext ctx = ShareContextFactory.FromListReport(
                "Report", "Cut List · PI-2026-001", "ProformaInvoice",
                "Skyline Interiors LLC", "ProGlass LLC");

            Assert.Equal("Report", ctx.DocumentTypeKey);
            Assert.Equal("Cut List · PI-2026-001", ctx.DisplayTitle);
            Assert.Equal("Skyline Interiors LLC", ctx.CustomerName);
            Assert.Equal("ProGlass LLC", ctx.CompanyName);
        }
    }
}
