// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/SnapshotRoundTripTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// SCOPE
// ─────
// Pins the JSON round-trip contract for the wizard's persisted
// snapshot. The snapshot POCO is the sole cross-version boundary
// between what the operator types and what lands in the SQLite
// FullSnapshotJson column — a silent change here would break every
// previously-saved invoice on the next release.
//
// The test suite is pure — no DB, no App.Config, no filesystem.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using System;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public class SnapshotRoundTripTests
    {
        // ── Basic serialize / deserialize ─────────────────────────

        [Fact]
        public void Empty_snapshot_serialises_and_deserialises_to_defaults()
        {
            var snap = new ProformaInvoiceSnapshot();

            string json = ProformaInvoiceMapper.SerializeSnapshot(snap);
            var back = ProformaInvoiceMapper.DeserializeSnapshot(
                new SavedProformaInvoice { FullSnapshotJson = json });

            Assert.NotNull(back);
            Assert.NotNull(back.Company);
            Assert.NotNull(back.Client);
            Assert.NotNull(back.Project);
            Assert.Equal(1, back.SchemaVersion);
        }

        [Fact]
        public void Snapshot_header_sync_uses_persisted_row_identity_status_and_dates()
        {
            var snapshot = new ProformaInvoiceSnapshot
            {
                Company = new CompanySnapshot
                {
                    ProformaId = "PI-detached",
                    InquiryId = 1,
                    ProformaStatus = "Draft",
                    IssueDate = new DateTime(2026, 1, 2),
                    ValidUntil = new DateTime(2026, 1, 9)
                },
                Client = new ClientSnapshot { CustomerName = "Snapshot customer" }
            };
            var entity = new SavedProformaInvoice
            {
                ProformaId = "PI-canonical",
                InquiryId = 42,
                Status = "Approved",
                IssueDate = new DateTime(2026, 2, 3),
                ValidUntil = new DateTime(2026, 3, 3),
                FullSnapshotJson = ProformaInvoiceMapper.SerializeSnapshot(snapshot)
            };

            Assert.True(ProformaInvoiceMapper.SynchronizeSnapshotHeader(entity));

            var canonical = ProformaInvoiceMapper.DeserializeSnapshot(entity);
            Assert.Equal("PI-canonical", canonical.Company.ProformaId);
            Assert.Equal(42, canonical.Company.InquiryId);
            Assert.Equal("Approved", canonical.Company.ProformaStatus);
            Assert.Equal(entity.IssueDate, canonical.Company.IssueDate);
            Assert.Equal(entity.ValidUntil, canonical.Company.ValidUntil);
            Assert.Equal("Snapshot customer", canonical.Client.CustomerName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("{}")]
        [InlineData("{ } ")]
        [InlineData("{\"Company\":{}}")]
        [InlineData("{ this is not valid json")]
        [InlineData("{\"Company\":null}")]
        public void Snapshot_header_sync_refuses_unreadable_or_blank_snapshot_without_replacing_it(
            string json)
        {
            var entity = new SavedProformaInvoice
            {
                ProformaId = "PI-2026-0042",
                Status = "Draft",
                FullSnapshotJson = json
            };

            Assert.False(ProformaInvoiceMapper.SynchronizeSnapshotHeader(entity));
            Assert.Equal(json, entity.FullSnapshotJson);
        }

        [Fact]
        public void All_step1_company_fields_roundtrip()
        {
            var snap = new ProformaInvoiceSnapshot
            {
                Company = new CompanySnapshot
                {
                    CompanyName = "ProGlass LLC",
                    CompanyTrn = "100000000000003",
                    CompanyLocation = "Ajman, UAE",
                    ProformaId = "PI-2026-0042",
                    InquiryId = 17,
                    IssueDate = new DateTime(2026, 8, 24),
                    ValidUntil = new DateTime(2026, 9, 23),
                    ProformaStatus = "Sent",
                    Representative = "S. Kumar",
                    RepresentativeMobile = "+971 50 111 2222",
                    CompanyEmail = "sales@proglass.ae",
                    AccountNumber = "AE07 0331 2345 6789 0123 456",
                    BankName = "Emirates NBD",
                    LogoPath = "logo.png",
                    CompanyRecordId = 42
                }
            };

            var back = RoundTrip(snap);

            Assert.Equal(snap.Company.CompanyName, back.Company.CompanyName);
            Assert.Equal(snap.Company.CompanyTrn, back.Company.CompanyTrn);
            Assert.Equal(snap.Company.CompanyLocation, back.Company.CompanyLocation);
            Assert.Equal(snap.Company.ProformaId, back.Company.ProformaId);
            Assert.Equal(snap.Company.InquiryId, back.Company.InquiryId);
            Assert.Equal(snap.Company.IssueDate, back.Company.IssueDate);
            Assert.Equal(snap.Company.ValidUntil, back.Company.ValidUntil);
            Assert.Equal(snap.Company.ProformaStatus, back.Company.ProformaStatus);
            Assert.Equal(snap.Company.Representative, back.Company.Representative);
            Assert.Equal(snap.Company.RepresentativeMobile, back.Company.RepresentativeMobile);
            Assert.Equal(snap.Company.CompanyEmail, back.Company.CompanyEmail);
            Assert.Equal(snap.Company.AccountNumber, back.Company.AccountNumber);
            Assert.Equal(snap.Company.BankName, back.Company.BankName);
            Assert.Equal(snap.Company.LogoPath, back.Company.LogoPath);
            Assert.Equal(snap.Company.CompanyRecordId, back.Company.CompanyRecordId);
        }

        [Fact]
        public void All_step2_client_fields_roundtrip()
        {
            var snap = new ProformaInvoiceSnapshot
            {
                Client = new ClientSnapshot
                {
                    CustomerName = "Al Habtoor Interiors",
                    CustomerTrn = "999999999999999",
                    CustomerReferenceCode = "REF-4210",
                    SalesRepresentative = "N. Ahmed",
                    DocumentPreparedBy = "M. Yousef",
                    BillingAddress = "PO Box 1234\nDubai, UAE",
                    AttentionExecutiveName = "Mr. Khan",
                    ClientContactPhone = "+971 4 555 0100"
                }
            };

            var back = RoundTrip(snap);

            Assert.Equal(snap.Client.CustomerName, back.Client.CustomerName);
            Assert.Equal(snap.Client.CustomerTrn, back.Client.CustomerTrn);
            Assert.Equal(snap.Client.CustomerReferenceCode, back.Client.CustomerReferenceCode);
            Assert.Equal(snap.Client.SalesRepresentative, back.Client.SalesRepresentative);
            Assert.Equal(snap.Client.DocumentPreparedBy, back.Client.DocumentPreparedBy);
            Assert.Equal(snap.Client.BillingAddress, back.Client.BillingAddress);
            Assert.Equal(snap.Client.AttentionExecutiveName, back.Client.AttentionExecutiveName);
            Assert.Equal(snap.Client.ClientContactPhone, back.Client.ClientContactPhone);
        }

        [Fact]
        public void All_step3_project_fields_roundtrip()
        {
            var snap = new ProformaInvoiceSnapshot
            {
                Project = new ProjectSnapshot
                {
                    ProjectIdentifierName = "IMAD HABIB",
                    ProjectReference = "HOUSE FOR IMAD HABIB",
                    ProjectContractNo = "428 IMAD HABIB",
                    SiteDeliveryLocation = "AJMAN",
                    LpoNumber = "2680"
                }
            };

            var back = RoundTrip(snap);

            Assert.Equal(snap.Project.ProjectIdentifierName, back.Project.ProjectIdentifierName);
            Assert.Equal(snap.Project.ProjectReference, back.Project.ProjectReference);
            Assert.Equal(snap.Project.ProjectContractNo, back.Project.ProjectContractNo);
            Assert.Equal(snap.Project.SiteDeliveryLocation, back.Project.SiteDeliveryLocation);
            Assert.Equal(snap.Project.LpoNumber, back.Project.LpoNumber);
        }

        [Fact]
        public void Wastage_live_quantity_and_amount_roundtrip()
        {
            var snap = new ProformaInvoiceSnapshot
            {
                OtherCharges = new OtherChargesSnapshot
                {
                    Wastages = new()
                    {
                        new WastageRowSnapshot
                        {
                            IsActive = true,
                            ColorName = "HD Grey",
                            Rate = 10.084033m,
                            TotalSqm = 10m,
                            ResultAmount = 100.84m
                        }
                    }
                }
            };

            var back = RoundTrip(snap);
            var wastage = Assert.Single(back.OtherCharges.Wastages);

            Assert.Equal<decimal?>(10m, wastage.TotalSqm);
            Assert.Equal<decimal?>(100.84m, wastage.ResultAmount);
        }

        // ── Legacy JSON compatibility (P&L integrity anti-regression) ──

        [Fact]
        public void Legacy_wastage_without_live_calculation_values_deserialises_to_null()
        {
            const string legacyJson =
                "{\"OtherCharges\":{\"Wastages\":[{\"IsActive\":true,\"Rate\":10}]}}";

            var back = ProformaInvoiceMapper.DeserializeSnapshot(
                new SavedProformaInvoice { FullSnapshotJson = legacyJson });
            var wastage = Assert.Single(back.OtherCharges.Wastages);

            Assert.Null(wastage.TotalSqm);
            Assert.Null(wastage.ResultAmount);
        }

        [Fact]
        public void Legacy_json_without_ProjectReference_deserialises_to_empty_string()
        {
            // CHAT 15 added ProjectReference. Any invoice saved BEFORE
            // CHAT 15 must still deserialise cleanly with the new
            // property defaulting to "".
            string legacyJson =
                "{" +
                "  \"SchemaVersion\": 1," +
                "  \"Company\": { \"CompanyName\": \"Legacy Co\" }," +
                "  \"Project\": { \"ProjectIdentifierName\": \"Old Site\", \"ProjectContractNo\": \"C-1\" }," +
                "  \"Client\":  { \"CustomerName\": \"Legacy Cust\" }" +
                "}";

            var back = ProformaInvoiceMapper.DeserializeSnapshot(
                new SavedProformaInvoice { FullSnapshotJson = legacyJson });

            Assert.Equal("Old Site", back.Project.ProjectIdentifierName);
            Assert.Equal("C-1", back.Project.ProjectContractNo);
            Assert.Equal(string.Empty, back.Project.ProjectReference);
            Assert.Equal(string.Empty, back.Project.SiteDeliveryLocation);
            Assert.Equal(string.Empty, back.Project.LpoNumber);
        }

        [Fact]
        public void Legacy_json_without_v2_company_fields_deserialises_to_empty_strings()
        {
            // v2 added Representative / RepresentativeMobile / CompanyEmail
            // etc. Older invoices must still load without exceptions or
            // null-ref surfaces.
            string legacyJson =
                "{" +
                "  \"SchemaVersion\": 1," +
                "  \"Company\": {" +
                "     \"CompanyName\": \"Legacy Co\"," +
                "     \"CompanyTrn\":  \"100000000000003\"," +
                "     \"ProformaId\":  \"OLD-001\"" +
                "  }," +
                "  \"Project\": {}," +
                "  \"Client\":  {}" +
                "}";

            var back = ProformaInvoiceMapper.DeserializeSnapshot(
                new SavedProformaInvoice { FullSnapshotJson = legacyJson });

            Assert.Equal("Legacy Co", back.Company.CompanyName);
            Assert.Equal("100000000000003", back.Company.CompanyTrn);
            Assert.Equal("OLD-001", back.Company.ProformaId);
            Assert.Equal(string.Empty, back.Company.Representative);
            Assert.Equal(string.Empty, back.Company.RepresentativeMobile);
            Assert.Equal(string.Empty, back.Company.CompanyEmail);
            Assert.Equal(string.Empty, back.Company.AccountNumber);
            Assert.Equal(string.Empty, back.Company.BankName);
            Assert.Equal(0, back.Company.CompanyRecordId);
        }

        // ── Error-tolerance surface ────────────────────────────────

        [Fact]
        public void Corrupt_json_returns_empty_snapshot_not_throws()
        {
            var back = ProformaInvoiceMapper.DeserializeSnapshot(
                new SavedProformaInvoice { FullSnapshotJson = "{ this is not valid json" });

            Assert.NotNull(back);
            Assert.Equal(string.Empty, back.Company.CompanyName);
        }

        [Fact]
        public void Null_entity_returns_empty_snapshot_not_throws()
        {
            var back = ProformaInvoiceMapper.DeserializeSnapshot(null!);

            Assert.NotNull(back);
            Assert.NotNull(back.Company);
        }

        [Fact]
        public void Null_snapshot_serialises_to_empty_snapshot_json()
        {
            string json = ProformaInvoiceMapper.SerializeSnapshot(null!);

            Assert.False(string.IsNullOrEmpty(json));
            Assert.Contains("\"SchemaVersion\"", json);
        }

        // ── Helper ─────────────────────────────────────────────────

        private static ProformaInvoiceSnapshot RoundTrip(ProformaInvoiceSnapshot snap)
        {
            string json = ProformaInvoiceMapper.SerializeSnapshot(snap);
            return ProformaInvoiceMapper.DeserializeSnapshot(
                new SavedProformaInvoice { FullSnapshotJson = json });
        }
    }
}
