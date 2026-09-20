// Pins PI number allocation: placeholder PI-{year}-000 is not persistable,
// InsertAsync reallocates before write, missing-row UpdateAsync uses
// InsertAsync (unique-index retry). No live SQLite — SDK may be absent.

using ProGlassAutomation.Models.ProformaInvoice.Common;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public sealed class ProformaIdHardeningTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("PI-2026-000")]
        [InlineData("pi-2026-000")]
        [InlineData("PI-1999-000")]
        public void Unallocated_seed_and_blank_are_not_persistable(string? id)
        {
            Assert.True(ProformaInvoiceStatus.IsUnallocatedSeed(id));
        }

        [Theory]
        [InlineData("PI-2026-001")]
        [InlineData("PI-2026-0001")]
        [InlineData("PI-2026-00")]
        [InlineData("INV-42")]
        [InlineData("PI-2026-000-R1")]
        public void Real_or_custom_ids_are_not_treated_as_seed(string id)
        {
            Assert.False(ProformaInvoiceStatus.IsUnallocatedSeed(id));
        }

        [Fact]
        public void Canonical_All_includes_Cancelled_and_not_Void()
        {
            Assert.Contains(ProformaInvoiceStatus.Cancelled, ProformaInvoiceStatus.All);
            Assert.Contains(ProformaInvoiceStatus.Rejected, ProformaInvoiceStatus.All);
            Assert.DoesNotContain("Void", ProformaInvoiceStatus.All);
        }

        [Fact]
        public void InsertAsync_reallocates_unallocated_seed_before_write()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "ProformaInvoice", "ProformaInvoiceRepository",
                "ProformaInvoiceRepository.cs"));

            Assert.Contains("ProformaInvoiceStatus.IsUnallocatedSeed(invoice.ProformaId)", src);
            Assert.Contains("invoice.ProformaId = await SuggestNextIdAsync(", src);
            Assert.Contains("for (int attempt = 0; attempt < 5; attempt++)", src);
            Assert.Contains("when (IsUniqueViolation(ex) && attempt < 4)", src);
        }

        [Fact]
        public void UpdateAsync_missing_row_delegates_to_InsertAsync_after_dispose()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "ProformaInvoice", "ProformaInvoiceRepository",
                "ProformaInvoiceRepository.cs"));

            Assert.Contains("db.Dispose();", src);
            Assert.Contains("await InsertAsync(invoice).ConfigureAwait(false);", src);
            Assert.DoesNotContain("BUG-10: Insert inline instead of calling InsertAsync()", src);
        }

        [Fact]
        public void Step1_status_picker_uses_ProformaInvoiceStatus_All()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "ViewModels", "ProformaInvoice", "Step1CompanySetup",
                "CompanySetupViewModel.cs"));

            Assert.Contains("public IReadOnlyList<string> StatusOptions => ProformaInvoiceStatus.All;", src);
            Assert.DoesNotContain("SavedInvoicesListViewModel StatusFilterOptions", src);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "ViewModels", "ProformaInvoice", "ProformaInvoiceViewModel.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
