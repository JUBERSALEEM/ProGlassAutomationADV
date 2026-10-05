// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/ProjectSpecsViewModelTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins Step 3's client-filter behaviour and free-text property
// round-trip. Every project field is optional; there are no
// required-field validators to test.
//
// The tests avoid LoadCardsAsync() because it touches App.MasterData;
// the VM guards on `App.MasterData == null` so the fire-and-forget
// launched inside SetClientFilter() is a safe no-op in a test host.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.ProformaInvoice.Step3ProjectSpecs;
using System.Collections.Generic;
using System.ComponentModel;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public class ProjectSpecsViewModelTests
    {
        // ── Defaults ───────────────────────────────────────────────

        [Fact]
        public void Constructor_starts_with_no_client_filter()
        {
            var vm = new ProjectSpecsViewModel();

            Assert.False(vm.IsClientFilterActive);
            Assert.Equal("All saved projects", vm.FilterBannerText);
        }

        [Fact]
        public void All_project_fields_default_to_empty()
        {
            var vm = new ProjectSpecsViewModel();

            Assert.Equal(string.Empty, vm.ProjectIdentifierName);
            Assert.Equal(string.Empty, vm.ProjectReference);
            Assert.Equal(string.Empty, vm.ProjectContractNo);
            Assert.Equal(string.Empty, vm.LpoNumber);
            Assert.Equal(string.Empty, vm.SiteDeliveryLocation);
        }

        // ── Field round-trip ───────────────────────────────────────

        [Fact]
        public void Project_fields_roundtrip_without_side_effects()
        {
            var vm = new ProjectSpecsViewModel
            {
                ProjectIdentifierName = "IMAD HABIB",
                ProjectReference = "HOUSE FOR IMAD HABIB",
                ProjectContractNo = "428 IMAD HABIB",
                LpoNumber = "2680",
                SiteDeliveryLocation = "AJMAN"
            };

            Assert.Equal("IMAD HABIB", vm.ProjectIdentifierName);
            Assert.Equal("HOUSE FOR IMAD HABIB", vm.ProjectReference);
            Assert.Equal("428 IMAD HABIB", vm.ProjectContractNo);
            Assert.Equal("2680", vm.LpoNumber);
            Assert.Equal("AJMAN", vm.SiteDeliveryLocation);
        }

        // ── Client-filter behaviour ────────────────────────────────

        [Fact]
        public void SetClientFilter_activates_filter_and_banner()
        {
            var vm = new ProjectSpecsViewModel();

            vm.SetClientFilter("Al Habtoor Interiors");

            Assert.True(vm.IsClientFilterActive);
            Assert.Equal("Al Habtoor Interiors", vm.FilteredClientName);
            Assert.Contains("Al Habtoor Interiors", vm.FilterBannerText);
        }

        [Fact]
        public void SetClientFilter_with_empty_clears_the_filter()
        {
            var vm = new ProjectSpecsViewModel();
            vm.SetClientFilter("Global Glass");
            Assert.True(vm.IsClientFilterActive);

            vm.SetClientFilter("");

            Assert.False(vm.IsClientFilterActive);
            Assert.Equal("All saved projects", vm.FilterBannerText);
        }

        [Fact]
        public void SetClientFilter_with_null_is_treated_as_empty()
        {
            var vm = new ProjectSpecsViewModel();

            vm.SetClientFilter(null!);

            Assert.False(vm.IsClientFilterActive);
        }

        [Fact]
        public void ClearClientFilterCommand_clears_the_filter()
        {
            var vm = new ProjectSpecsViewModel();
            vm.SetClientFilter("Global Glass");
            Assert.True(vm.IsClientFilterActive);

            vm.ClearClientFilterCommand.Execute(null);

            Assert.False(vm.IsClientFilterActive);
        }

        // ── PropertyChanged plumbing ───────────────────────────────

        [Fact]
        public void SetClientFilter_raises_PropertyChanged_for_derived_properties()
        {
            var vm = new ProjectSpecsViewModel();
            var raised = CapturePropertyChanges(vm);

            vm.SetClientFilter("Acme");

            Assert.Contains(nameof(vm.FilteredClientName), raised);
            Assert.Contains(nameof(vm.IsClientFilterActive), raised);
            Assert.Contains(nameof(vm.FilterBannerText), raised);
        }

        [Fact]
        public void Setting_same_project_name_does_not_raise_change()
        {
            var vm = new ProjectSpecsViewModel { ProjectIdentifierName = "SITE A" };
            var raised = CapturePropertyChanges(vm);

            vm.ProjectIdentifierName = "SITE A";

            Assert.DoesNotContain(nameof(vm.ProjectIdentifierName), raised);
        }

        // ── Helpers ────────────────────────────────────────────────

        private static List<string> CapturePropertyChanges(INotifyPropertyChanged vm)
        {
            var list = new List<string>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != null) list.Add(e.PropertyName);
            };
            return list;
        }
    }
}
