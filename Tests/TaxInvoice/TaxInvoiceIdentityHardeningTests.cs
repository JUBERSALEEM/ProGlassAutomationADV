// Pins TI identity after forensic audit. Source-level — no SDK / SQLite.
// Does not touch PI, JO, or DN.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.TaxInvoice
{
    public sealed class TaxInvoiceIdentityHardeningTests
    {
        [Fact]
        public void InsertAsync_retries_unique_InvoiceNumber_and_syncs_snapshot()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "TaxInvoice", "Repository",
                "TaxInvoiceRepository.cs"));

            Assert.Contains("for (int attempt = 0; attempt < 5; attempt++)", src);
            Assert.Contains("SqliteErrorClassifier.IsUnique(ex)", src);
            Assert.Contains("ReallocateInvoiceNumberAsync(entity)", src);
            Assert.Contains("snap.Header.InvoiceNumber = entity.InvoiceNumber", src);
        }

        [Fact]
        public void Sequence_letter_does_not_wrap_Z_onto_A()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "TaxInvoice", "Repository",
                "TaxInvoiceRepository.cs"));

            Assert.Contains("No free sequence letter A–Z remains", src);
            Assert.DoesNotContain(
                "return last < 'Z' ? ((char)(last + 1)).ToString() : \"A\"", src);
        }

        [Fact]
        public void Existing_databases_get_unique_InvoiceNumber_index_via_migration_1003()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Database", "SchemaMigrations",
                "AppSchemaMigrations.cs"));

            Assert.Contains(
                "new(1003, \"Tax Invoice: unique InvoiceNumber index\"", src);
            Assert.Contains(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_TaxInvoices_InvoiceNumber ON TaxInvoices(InvoiceNumber);",
                src);
        }

        [Fact]
        public void Create_and_save_refuse_unconfirmed_or_already_invoiced_dns()
        {
            string init = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "ViewModels", "TaxInvoice", "TaxInvoiceDetail",
                "TaxInvoiceDetailViewModel.InitNew.cs"));
            string save = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "ViewModels", "TaxInvoice", "TaxInvoiceDetail",
                "TaxInvoiceDetailViewModel.Save.cs"));

            Assert.Contains("TaxInvoiceLineService.CanInvoiceDeliveryNote", init);
            Assert.Contains("EnsureReferencedDnsInvoiceableAsync", save);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Services", "TaxInvoice", "Repository",
                    "TaxInvoiceRepository.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
