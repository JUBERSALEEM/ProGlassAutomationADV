// Pins Statement identity after forensic audit. Source-level — no SDK.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Statement
{
    public sealed class StatementIdentityHardeningTests
    {
        [Fact]
        public void InsertAsync_retries_unique_StatementNumber_and_syncs_snapshot()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Statement", "Repository", "StatementRepository.cs"));

            Assert.Contains("for (int attempt = 0; attempt < 5; attempt++)", src);
            Assert.Contains("SqliteErrorClassifier.IsUnique(ex)", src);
            Assert.Contains("ReallocateStatementNumber(entity)", src);
            Assert.Contains("snap.Header.StatementNumber = entity.StatementNumber", src);
        }

        [Fact]
        public void Existing_databases_get_unique_StatementNumber_index_via_migration_1003()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Database", "SchemaMigrations",
                "AppSchemaMigrations.cs"));

            Assert.Contains(
                "new(1003, \"Statement: unique StatementNumber index\"", src);
            Assert.Contains(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_Statements_StatementNumber ON Statements(StatementNumber);",
                src);
        }

        [Fact]
        public void Generate_retry_rewrites_snapshot_json_not_just_the_header_field()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "ViewModels", "Reports", "StatementGenerator",
                "StatementGeneratorViewModel.Generate.cs"));

            Assert.Contains("entity.FullSnapshotJson = JsonSerializer.Serialize(", src);
            Assert.Contains("BuildStatementNumber(filter)", src);
        }

        [Fact]
        public void Aggregator_orders_invoices_deterministically()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Statement", "Aggregation",
                "StatementAggregator.Part1.cs"));

            Assert.Contains("OrderBy(i => i.IssueDate)", src);
            Assert.Contains("ThenBy(i => i.InvoiceNumber", src);
            Assert.Contains("TaxInvoiceStatusValues.HasBeenIssued", src);
            Assert.Contains("TaxInvoiceStatusValues.IsPaid(status)", src);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Services", "Statement", "Repository",
                    "StatementRepository.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
