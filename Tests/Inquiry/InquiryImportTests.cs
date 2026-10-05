using ProGlassAutomation.Services.Inquiry;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.Inquiry
{
    /// <summary>
    /// Regression tests for the Inquiry CSV importer.
    ///
    /// S3 fix: duplicate inquiry numbers inside one file used to hit the
    /// UNIQUE index mid-import and abort the batch with a raw
    /// DbUpdateException. The parser now blanks duplicate numbers so the
    /// repository generates fresh sequential ones, and skips invalid rows.
    /// </summary>
    public class InquiryImportTests : System.IDisposable
    {
        private readonly string _tempDir;

        public InquiryImportTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "inq-tests-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        }

        private string WriteCsv(string content)
        {
            string path = Path.Combine(_tempDir, Path.GetRandomFileName() + ".csv");
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public async Task Parses_quoted_cells_and_normalizes_status()
        {
            string csv = "Inquiry,Reference,Customer,Location,Project,Description,Salesman,Prepared By,Received,Updated,Status,Attachments\n" +
                         "\"INQ-2026-0001\",\"Customer Inquiry\",\"\"\"Al\"\" Glass LLC\",\"Dubai\",\"Tower A glazing\",\"Curtain wall\",\"Ali\",,\"2026-08-01T10:00:00\",\"2026-08-01T11:00:00\",\"On Hold\",2\n";
            var result = await new InquiryImportExportService().ParseCsvAsync(WriteCsv(csv));

            var row = Assert.Single(result.Rows);
            Assert.Equal("INQ-2026-0001", row.InquiryNumber);
            Assert.Equal("\"Al\" Glass LLC", row.CustomerName);
            Assert.Equal("Tower A glazing", row.ProjectName);
            Assert.Equal("Hold", row.Status); // legacy spelling normalized
            Assert.Equal(0, result.SkippedInvalidRows);
        }

        [Fact]
        public async Task Duplicate_numbers_are_blanked_for_regeneration()
        {
            string line = "\"INQ-2026-0009\",\"Ref\",\"Cust\",\"\",\"Proj\",\"Desc\"\n";
            string csv = "Inquiry,Reference,Customer,Location,Project,Description\n" + line + line;
            var result = await new InquiryImportExportService().ParseCsvAsync(WriteCsv(csv));

            Assert.Equal(2, result.Rows.Count);
            Assert.Equal(1, result.DuplicateNumbersRegenerated);
            Assert.Equal("INQ-2026-0009", result.Rows[0].InquiryNumber);
            Assert.Equal("", result.Rows[1].InquiryNumber); // repository assigns a fresh number
        }

        [Fact]
        public async Task Rows_without_customer_or_project_are_skipped_and_counted()
        {
            string csv = "Inquiry,Reference,Customer,Location,Project,Description\n" +
                         "\"INQ-1\",\"Ref\",\"Cust\",\"\",\"Proj\",\"ok\"\n" +
                         "\"INQ-2\",\"Ref\",\"\",\"\",\"Proj\",\"no customer\"\n" +
                         "\"INQ-3\",\"Ref\",\"Cust\",\"\",\"\",\"no project\"\n";
            var result = await new InquiryImportExportService().ParseCsvAsync(WriteCsv(csv));

            var row = Assert.Single(result.Rows);
            Assert.Equal("INQ-1", row.InquiryNumber);
            Assert.Equal(2, result.SkippedInvalidRows);
        }

        [Fact]
        public async Task Missing_received_date_defaults_to_now_with_local_kind()
        {
            string csv = "Inquiry,Reference,Customer,Location,Project,Description\n" +
                         "\"\",\"Ref\",\"Cust\",\"\",\"Proj\",\"Desc\"\n";
            var result = await new InquiryImportExportService().ParseCsvAsync(WriteCsv(csv));

            var row = Assert.Single(result.Rows);
            Assert.NotEqual(default, row.ReceivedAt);
            Assert.Equal(System.DateTimeKind.Local, row.ReceivedAt.Kind);
            Assert.Equal("Received", row.Status);
        }
    }
}
