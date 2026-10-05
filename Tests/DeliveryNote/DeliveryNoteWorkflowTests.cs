using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Mapper;
using System;
using System.IO;
using System.Linq;
using Xunit;
using ProGlassAutomation.Tests.TestSupport;

namespace ProGlassAutomation.Tests.DeliveryNote
{
    public class DeliveryNoteWorkflowTests
    {
        [Theory]
        [InlineData("Dispatching", true)]
        [InlineData("Draft", true)]
        [InlineData("draft", true)]
        [InlineData("Dispatched", false)]
        [InlineData("Delivered", false)]
        [InlineData("Signed", false)]
        [InlineData("Received", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Open_for_dispatch_is_only_dispatching_or_draft(string? status, bool expected)
        {
            Assert.Equal(expected, DeliveryNoteWorkflow.IsOpenForDispatch(status));
        }

        [Theory]
        [InlineData("Dispatched", true)]
        [InlineData("Delivered", true)]
        [InlineData("Signed", true)]
        [InlineData("Received", true)]
        [InlineData("Dispatching", false)]
        [InlineData("Draft", false)]
        public void Confirmed_delivery_excludes_in_progress(string status, bool expected)
        {
            Assert.Equal(expected, DeliveryNoteWorkflow.IsConfirmedDelivery(status));
        }

        [Fact]
        public void FromMasterBalance_keeps_original_serial_and_drops_closed_lines()
        {
            var master = new SavedDeliveryNote
            {
                DeliveryNoteId = "MDN-2026-001",
                JobOrderId = "JOB-2026-001",
                ProformaId = "PI-2026-001",
                CustomerName = "Acme",
                ProjectName = "Tower",
                IsMasterDn = true,
                Items =
                {
                    Line(1, 1, "A", ordered: 10, prev: 10, balanceStored: 0),
                    Line(1, 7, "B", ordered: 8, prev: 3, balanceStored: 5),
                    Line(2, 4, "C", ordered: 4, prev: 0, balanceStored: 4)
                }
            };

            var dn = DeliveryNoteMapper.FromMasterBalance(master, "DN-2026-001-02", 2);

            Assert.Equal("Dispatching", dn.Status);
            Assert.False(dn.IsMasterDn);
            Assert.Equal(2, dn.Items.Count);
            Assert.Equal(new[] { 7, 4 }, dn.Items.Select(i => i.SerialNumber).ToArray());
            Assert.All(dn.Items, i => Assert.Equal(0, i.DeliveredQty));
            Assert.Equal(5, dn.Items.First(i => i.SerialNumber == 7).BalanceQty);
            Assert.Equal(3, dn.Items.First(i => i.SerialNumber == 7).PreviouslyDeliveredQty);
            Assert.DoesNotContain(dn.Items, i => i.SerialNumber == 1);
        }

        [Fact]
        public void FromMasterBalance_does_not_renumber_remaining_lines_from_one()
        {
            var master = new SavedDeliveryNote
            {
                JobOrderId = "JOB-1",
                Items =
                {
                    Line(1, 5, "X", ordered: 2, prev: 0, balanceStored: 2),
                    Line(1, 9, "Y", ordered: 2, prev: 0, balanceStored: 2)
                }
            };

            var dn = DeliveryNoteMapper.FromMasterBalance(master, "DN-1-01", 1);

            Assert.Equal(new[] { 5, 9 }, dn.Items.Select(i => i.SerialNumber).ToArray());
        }

        [Fact]
        public void Workspace_new_dn_never_opens_dispatched_and_preserves_serials()
        {
            var root = LocateProjectRoot();
            var create = File.ReadAllText(Path.Combine(root,
                "Services/DeliveryNote/DeliveryNoteCreateService.cs"));
            var mapper = File.ReadAllText(Path.Combine(root,
                "Services/DeliveryNote/Mapper/DeliveryNoteMapper.cs"));
            var repo = SourceFamily.Read(root,
                "Services/DeliveryNote/Repository/DeliveryNoteRepository.cs");
            var listXaml = File.ReadAllText(Path.Combine(root,
                "Views/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml"));
            var detailVm = SourceFamily.Read(root,
                "ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.cs");

            Assert.Contains("Content=\"NEW DN\"", listXaml);
            Assert.Contains("IsOpenForDispatch", create);
            Assert.Contains("FromMasterBalance", create);
            Assert.DoesNotContain("serialCounter++", create);
            Assert.Contains("SerialNumber = masterItem.SerialNumber", mapper);
            Assert.Contains("x.Status == \"Dispatched\"", repo);
            Assert.Contains("x.Status == \"Delivered\"", repo);
            Assert.Contains("ConfirmDispatchAsync()", detailVm);
            Assert.Contains("IsOpenForDispatch(Status)", detailVm);
        }

        [Fact]
        public void Previously_delivered_map_is_confirmed_only_and_spec_key()
        {
            var root = LocateProjectRoot();
            var repo = SourceFamily.Read(root,
                "Services/DeliveryNote/Repository/DeliveryNoteRepository.cs");
            var block = Extraction.Between(
                repo,
                "public async Task<Dictionary<string, int>> GetPreviouslyDeliveredMapAsync(",
                "private static Dictionary<string, int> BuildDeliveredLookup(");
            Assert.NotNull(block);
            Assert.Contains("x.Status == \"Dispatched\"", block!);
            Assert.Contains("x.Status == \"Delivered\"", block!);
            Assert.DoesNotContain("Glass-dimension fallback", block!);
        }

        [Fact]
        public void Repository_does_not_count_dispatching_qty_on_master()
        {
            var root = LocateProjectRoot();
            var repo = SourceFamily.Read(root,
                "Services/DeliveryNote/Repository/DeliveryNoteRepository.cs");
            var block = Extraction.Between(
                repo,
                "public async Task UpdateMasterDnAsync(",
                "public async Task<string> BuildMasterDnIdAsync");
            Assert.NotNull(block);
            Assert.Contains("x.Status == \"Dispatched\"", block!);
            Assert.DoesNotContain("Dispatching/Draft with saved qty", block!);
        }

        private static DeliveryNoteItem Line(
            int group, int serial, string glass, int ordered, int prev, int balanceStored)
        {
            return new DeliveryNoteItem
            {
                SpecGroupNumber = group,
                SerialNumber = serial,
                GlassRef = glass,
                OrderedQty = ordered,
                PreviouslyDeliveredQty = prev,
                DeliveredQty = 0,
                BalanceQty = balanceStored
            };
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName, "Views", "Settings",
                    "SettingsSectionStyles.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
