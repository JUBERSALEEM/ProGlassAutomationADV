using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using System.Text.Json;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class TaxInvoiceMapperStatusTests
    {
        [Fact]
        public void SetHeaderStatus_rewrites_snapshot_so_load_does_not_revive_draft()
        {
            var snap = new TaxInvoiceSnapshot
            {
                Header = new InvHeaderSnapshot
                {
                    InvoiceNumber = "INV-2026-001-A",
                    Status = "Draft"
                }
            };
            var entity = new SavedTaxInvoice
            {
                InvoiceNumber = "INV-2026-001-A",
                Status = "Draft",
                FullSnapshotJson = JsonSerializer.Serialize(snap)
            };

            TaxInvoiceMapper.SetHeaderStatus(entity, "Issued");

            Assert.Equal("Issued", entity.Status);
            var loaded = TaxInvoiceMapper.DeserializeSnapshot(entity);
            Assert.NotNull(loaded);
            Assert.Equal("Issued", loaded!.Header.Status);
        }
    }
}
