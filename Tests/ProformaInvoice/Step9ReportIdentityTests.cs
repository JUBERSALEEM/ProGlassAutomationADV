// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/Step9ReportIdentityTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// PINS STEP 9 PI-ORIENTED EXPORT IDENTITY
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;
using System;
using System.Collections.Generic;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public sealed class Step9ReportIdentityTests
    {
        [Theory]
        [InlineData(Step9ReportExport.ReportCodeOverview, "PI-2026-001-STEP9-OVERVIEW")]
        [InlineData(Step9ReportExport.ReportCodeCostLedger, "PI-2026-001-STEP9-COST-LEDGER")]
        [InlineData(Step9ReportExport.ReportCodePriceDecision, "PI-2026-001-STEP9-PRICE-DECISION")]
        [InlineData(Step9ReportExport.ReportCodeServices, "PI-2026-001-STEP9-SERVICES")]
        [InlineData(Step9ReportExport.ReportCodeSheetPricing, "PI-2026-001-STEP9-SHEET-PRICING")]
        [InlineData(Step9ReportExport.ReportCodeSpecStrategy, "PI-2026-001-STEP9-SPEC-STRATEGY")]
        public void Every_step9_report_code_builds_a_safe_pi_oriented_filename(
            string reportCode,
            string expectedDocumentId)
        {
            var context = Step9ReportExport.BuildDocumentContext(
                reportCode,
                CreateIdentity(),
                new DateTime(2026, 9, 8, 14, 30, 0));

            Assert.Equal(expectedDocumentId, context.DocumentId.Replace(" ", "-"));
            Assert.Equal(
                expectedDocumentId +
                "_EMAAR-PROPERTIES_SHOWROOM-PARTITION-WORKS_08-09-2026.pdf",
                Step9ReportExport.BuildSuggestedFileName(ExportFormat.Pdf, context));
        }

        [Fact]
        public void Step9_document_context_keeps_all_visible_pi_identity_fields()
        {
            var context = Step9ReportExport.BuildDocumentContext(
                Step9ReportExport.ReportCodeOverview,
                CreateIdentity(),
                new DateTime(2026, 9, 8, 14, 30, 0));

            Assert.Equal("PI", context.DocumentPrefix);
            Assert.Equal("PI-2026-001", context.ProformaId);
            Assert.Equal("Skyline Interiors LLC", context.CompanyName);
            Assert.Equal("Emaar Properties", context.CustomerName);
            Assert.Equal("Showroom Partition Works", context.ProjectName);
            Assert.Equal("Draft", context.Status);
            Assert.Equal("Emaar Properties", context.FilenameCustomer);
            Assert.Equal("Showroom Partition Works", context.FilenameReference);
            Assert.Equal("08 Sep 2026", context.IssueDateDisplay);
            Assert.Equal("08 Sep 2026 14:30", context.GeneratedAtDisplay);
            Assert.Contains("Proforma No. PI-2026-001", context.PrimaryMetadataLine);
            Assert.Contains("Customer Emaar Properties", context.PrimaryMetadataLine);
            Assert.Contains("Company Skyline Interiors LLC", context.SecondaryMetadataLine);
            Assert.Contains("Project Showroom Partition Works", context.SecondaryMetadataLine);
            Assert.Contains("Report / Invoice Date 08 Sep 2026", context.TertiaryMetadataLine);
            Assert.Contains("Status Draft", context.TertiaryMetadataLine);

            Assert.Collection(context.MetadataFields,
                field => Assert.Equal(new KeyValuePair<string, string>("Proforma No.", "PI-2026-001"), field),
                field => Assert.Equal(new KeyValuePair<string, string>("Company", "Skyline Interiors LLC"), field),
                field => Assert.Equal(new KeyValuePair<string, string>("Customer", "Emaar Properties"), field),
                field => Assert.Equal(new KeyValuePair<string, string>("Project", "Showroom Partition Works"), field),
                field => Assert.Equal(new KeyValuePair<string, string>("Report / Invoice Date", "08 Sep 2026"), field),
                field => Assert.Equal(new KeyValuePair<string, string>("Status", "Draft"), field),
                field => Assert.Equal(new KeyValuePair<string, string>("Generated", "08 Sep 2026 14:30"), field));
        }

        [Fact]
        public void Draft_identity_keeps_a_truthful_placeholder_and_never_invents_a_pi_number()
        {
            var context = Step9ReportExport.BuildDocumentContext(
                Step9ReportExport.ReportCodeOverview,
                new ReportDocumentContext { CustomerName = "Customer A" },
                new DateTime(2026, 9, 8, 14, 30, 0));

            Assert.Equal("PI-DRAFT STEP9 OVERVIEW", context.DocumentId);
            Assert.Equal("—", context.MetadataFields[0].Value);
            Assert.Equal(
                "PI-DRAFT-STEP9-OVERVIEW_CUSTOMER-A_08-09-2026.csv",
                Step9ReportExport.BuildSuggestedFileName(ExportFormat.Csv, context));
        }

        private static ReportDocumentContext CreateIdentity() => new()
        {
            ProformaId = "PI-2026-001",
            CompanyName = "Skyline Interiors LLC",
            CustomerName = "Emaar Properties",
            ProjectName = "Showroom Partition Works",
            IssueDate = new DateTime(2026, 9, 8),
            Status = "Draft"
        };
    }
}
