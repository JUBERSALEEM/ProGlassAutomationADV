// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ExportFileNamerTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins industrial export filenames:
//   {ID}_{CUSTOMER}_{PROJECT}_{dd-MM-yyyy}.{ext}
// Empty customer/project segments are skipped. Segments are UPPERCASE,
// spaces become hyphens, invalid characters are stripped, length is capped.
// ═══════════════════════════════════════════════════════════════════════

using System;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.FileSystem;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ExportFileNamerTests
    {
        [Fact]
        public void Build_joins_id_customer_project_and_date()
        {
            string name = ExportFileNamer.Build(
                "PI-2026-001",
                "Skyline Interiors LLC",
                "Showroom Partition Works",
                new DateTime(2026, 8, 30),
                ExportFormat.Pdf);

            Assert.Equal(
                "PI-2026-001_SKYLINE-INTERIORS-LLC_SHOWROOM-PARTITION-WORKS_30-08-2026.pdf",
                name);
        }

        [Theory]
        [InlineData(ExportFormat.Pdf, "pdf")]
        [InlineData(ExportFormat.Excel, "xlsx")]
        [InlineData(ExportFormat.Csv, "csv")]
        public void Build_uses_format_extension(ExportFormat format, string ext)
        {
            string name = ExportFileNamer.Build(
                "JOB-2026-001", "Acme", "Tower A",
                new DateTime(2026, 1, 5), format);

            Assert.EndsWith("." + ext, name);
            Assert.StartsWith("JOB-2026-001_ACME_TOWER-A_05-01-2026.", name);
        }

        [Fact]
        public void Build_skips_empty_customer_and_project()
        {
            string name = ExportFileNamer.Build(
                "DN-2026-001", "  ", null,
                new DateTime(2026, 8, 30),
                ExportFormat.Csv);

            Assert.Equal("DN-2026-001_30-08-2026.csv", name);
        }

        [Fact]
        public void Build_skips_only_the_blank_middle_segment()
        {
            string name = ExportFileNamer.Build(
                "INV-2026-001",
                "Emaar Properties",
                "   ",
                new DateTime(2026, 7, 22),
                ExportFormat.Pdf);

            Assert.Equal("INV-2026-001_EMAAR-PROPERTIES_22-07-2026.pdf", name);
        }

        [Fact]
        public void Build_falls_back_to_DOCUMENT_when_id_is_blank()
        {
            string name = ExportFileNamer.Build(
                " ", "Acme", "Site",
                new DateTime(2026, 8, 1),
                ExportFormat.Excel);

            Assert.Equal("DOCUMENT_ACME_SITE_01-08-2026.xlsx", name);
        }

        [Fact]
        public void Build_from_layout_uses_document_identity()
        {
            var layout = new StubLayout
            {
                DocumentId = "PI-2026-009",
                CompanyName = "Gulf Glass LLC",
                Reference = "Mall Facade",
                DocumentDate = new DateTime(2026, 3, 9)
            };

            Assert.Equal(
                "PI-2026-009_GULF-GLASS-LLC_MALL-FACADE_09-03-2026.pdf",
                ExportFileNamer.Build(layout, ExportFormat.Pdf));
        }

        [Fact]
        public void Sanitize_strips_invalid_characters_and_caps_length()
        {
            string longName = new string('a', 80) + " / extra";
            string sanitized = ExportFileNamer.Sanitize(longName);

            Assert.Equal(48, sanitized.Length);
            Assert.DoesNotContain("/", sanitized);
            Assert.DoesNotContain(" ", sanitized);
            Assert.Equal(sanitized.ToUpperInvariant(), sanitized);
        }

        [Fact]
        public void FirstFilled_returns_first_non_blank()
        {
            Assert.Equal(
                "SHOWROOM",
                ExportFileNamer.FirstFilled("  ", null, "SHOWROOM", "OTHER"));
            Assert.Equal(string.Empty, ExportFileNamer.FirstFilled(null, "  "));
            Assert.Equal(string.Empty, ExportFileNamer.FirstFilled());
        }

        private sealed class StubLayout : IDocumentLayout
        {
            public string DocumentPrefix { get; set; } = "PI";
            public string DocumentId { get; set; } = "";
            public string DocumentTitle { get; set; } = "Proforma Invoice";
            public string CompanyName { get; set; } = "";
            public string Reference { get; set; } = "";
            public DateTime DocumentDate { get; set; }
        }
    }
}
