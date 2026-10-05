// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/OrderReport/OrderReportPdfNamingTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// PINS PI-ORIENTED SAVED ORDER-REPORT PDF NAMES
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using ProGlassAutomation.Models.OrderReport;
using ProGlassAutomation.Services.OrderReport.Pdf;
using Xunit;

namespace ProGlassAutomation.Tests.OrderReport
{
    public sealed class OrderReportPdfNamingTests
    {
        [Fact]
        public void Single_pi_pdf_uses_the_proforma_export_naming_style_and_keeps_the_report_number()
        {
            var bundle = new OrderReportBundle
            {
                ReportNumber = "REP-2026-042",
                GeneratedAt = new DateTime(2026, 9, 9, 10, 15, 0),
                Sections = new List<OrderReportSnapshot>
                {
                    new()
                    {
                        Header = new OrderReportHeader
                        {
                            ProformaId = "PI-2026-001",
                            CompanyName = "Skyline Interiors LLC",
                            CustomerName = "Emaar Properties",
                            ProjectName = "Showroom Partition Works",
                            IssueDate = new DateTime(2026, 9, 8)
                        }
                    }
                }
            };

            Assert.Equal(
                "PI-2026-001-STEP9-ORDER-REPORT_EMAAR-PROPERTIES_" +
                "REP-2026-042-SHOWROOM-PARTITION-WORKS_08-09-2026.pdf",
                OrderReportPdfExporter.BuildOutputFileName(bundle));
        }

        [Fact]
        public void Single_pi_without_an_issue_date_uses_the_saved_generated_date_not_a_minimum_date()
        {
            var bundle = new OrderReportBundle
            {
                ReportNumber = "REP-2026-043",
                GeneratedAt = new DateTime(2026, 9, 9, 10, 15, 0),
                Sections = new List<OrderReportSnapshot>
                {
                    new()
                    {
                        Header = new OrderReportHeader
                        {
                            ProformaId = "PI-2026-001",
                            CustomerName = "Emaar Properties"
                        }
                    }
                }
            };

            Assert.EndsWith("_09-09-2026.pdf", OrderReportPdfExporter.BuildOutputFileName(bundle));
        }

        [Fact]
        public void Batch_pdf_uses_a_truthful_report_identity_and_the_saved_report_number()
        {
            var bundle = new OrderReportBundle
            {
                ReportNumber = "REP-2026-043",
                GeneratedAt = new DateTime(2026, 9, 9, 10, 15, 0),
                Sections = new List<OrderReportSnapshot>
                {
                    new() { Header = new OrderReportHeader { ProformaId = "PI-2026-001" } },
                    new() { Header = new OrderReportHeader { ProformaId = "PI-2026-002" } }
                }
            };

            Assert.Equal(
                "STEP9-ORDER-REPORT_REP-2026-043_09-09-2026.pdf",
                OrderReportPdfExporter.BuildOutputFileName(bundle));
        }
    }
}
