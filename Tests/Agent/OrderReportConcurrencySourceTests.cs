using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class OrderReportConcurrencySourceTests
    {
        [Fact]
        public void Order_report_full_and_special_writes_require_a_row_version()
        {
            string contract = Read("Services/OrderReport/Repository/IOrderReportRepository.cs");
            string repository = Read("Services/OrderReport/Repository/OrderReportRepository.cs");
            string entity = Read("Models/OrderReport/SavedOrderReport.cs");
            string context = Read("Services/OrderReport/Database/OrderReportDbContext.cs");

            Assert.Contains("Task UpdateAsync(SavedOrderReport entity, int expectedRowVersion);", contract);
            Assert.Contains("Task DeleteAsync(int id, int expectedRowVersion);", contract);
            Assert.Contains("int reportId, string pdfPath, int expectedRowVersion", contract);
            Assert.DoesNotContain("Task UpdateAsync(SavedOrderReport entity);", contract);
            Assert.DoesNotContain("Task DeleteAsync(int id);", contract);
            Assert.DoesNotContain("Task UpdatePdfPathAsync(int reportId, string pdfPath);", contract);
            Assert.Contains("public int RowVersion { get; set; }", entity);
            Assert.Contains("entity.Property(e => e.RowVersion)", context);
            Assert.Contains("IsConcurrencyToken()", context);
            Assert.Contains("expectedRowVersion", repository);
            Assert.Contains("ConcurrencyConflictException", repository);
        }

        [Fact]
        public void Order_report_migration_verifies_versions_indexes_and_critical_data()
        {
            string migrations = Read("Services/Database/SchemaMigrations/AppSchemaMigrations.cs");
            string repository = Read("Services/OrderReport/Repository/OrderReportRepository.cs");
            string health = Read("Services/Database/SchemaHealthService.cs");

            Assert.Contains("OrderReports", migrations);
            Assert.Contains("AddRowVersionColumn(connection, \"OrderReports\")", migrations);
            Assert.Contains("RequireNoDuplicateKeyValues(", migrations);
            Assert.Contains("RequireNoEmptyValues(connection, \"OrderReports\", \"ReportNumber\")", migrations);
            Assert.Contains("RequireIndex(connection, \"IX_OrderReports_ReportNumber\"", migrations);
            Assert.Contains("SchemaMigrationRunner.Upgrade(", repository);
            Assert.Contains("AppSchemaMigrations.OrderReports", repository);
            Assert.Contains("OrderReportDbContext", health);
        }

        [Fact]
        public void Stale_order_report_delete_does_not_remove_its_pdf_first()
        {
            string source = Read("ViewModels/Reports/OrderReport/OrderReportListViewModel.cs");
            int delete = source.IndexOf("_repo.DeleteAsync(row.Id, row.RowVersion)",
                StringComparison.Ordinal);
            int deletePdf = source.IndexOf("OrderReportPdfExporter.DeletePdfs(row.ReportNumber, row.PdfPath)",
                StringComparison.Ordinal);

            Assert.True(delete >= 0 && deletePdf > delete);
        }

        private static string Read(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ProGlassAutomation.csproj")))
                    return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate repository root from " + AppContext.BaseDirectory);
        }
    }
}
