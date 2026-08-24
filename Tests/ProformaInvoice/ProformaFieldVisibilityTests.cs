// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/ProformaFieldVisibilityTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the two guarantees the wizard's XAML depends on:
//   1. When App.Config is null, every ShowXxx flag returns TRUE
//      (open-by-default fallback) — a test host / designer never
//      goes NRE on the singleton.
//   2. Refresh() raises PropertyChanged for every ShowXxx property
//      so the XAML Visibility bindings react live to a Settings
//      save.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Features;
using System.Collections.Generic;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public class ProformaFieldVisibilityTests
    {
        [Fact]
        public void Instance_returns_the_same_singleton_every_time()
        {
            var a = ProformaFieldVisibility.Instance;
            var b = ProformaFieldVisibility.Instance;

            Assert.Same(a, b);
        }

        [Fact]
        public void Every_ShowXxx_defaults_to_true_when_config_is_null()
        {
            // In a test host App.Config is null. The singleton must
            // fall back to defaults (all visible) or the wizard would
            // hide every field on a machine with no config yet.
            var vm = ProformaFieldVisibility.Instance;

            Assert.True(vm.ShowCompanyTrn);
            Assert.True(vm.ShowCompanyLocation);
            Assert.True(vm.ShowRepresentative);
            Assert.True(vm.ShowRepresentativeMobile);
            Assert.True(vm.ShowCompanyEmail);
            Assert.True(vm.ShowBankName);
            Assert.True(vm.ShowAccountNumber);
            Assert.True(vm.ShowIssueDate);
            Assert.True(vm.ShowValidUntil);
            Assert.True(vm.ShowProformaStatus);

            Assert.True(vm.ShowCustomerTrn);
            Assert.True(vm.ShowCustomerReferenceCode);
            Assert.True(vm.ShowSalesRepresentative);
            Assert.True(vm.ShowDocumentPreparedBy);
            Assert.True(vm.ShowAttentionExecutive);
            Assert.True(vm.ShowClientContactPhone);
            Assert.True(vm.ShowBillingAddress);

            Assert.True(vm.ShowProjectName);
            Assert.True(vm.ShowProjectReference);
            Assert.True(vm.ShowContractNo);
            Assert.True(vm.ShowLpoNumber);
            Assert.True(vm.ShowSiteDeliveryLocation);
        }

        [Fact]
        public void HasAnyHiddenField_is_false_when_every_flag_is_on()
        {
            var vm = ProformaFieldVisibility.Instance;

            // Defaults are all true, so HasAnyHiddenField must be false.
            Assert.False(vm.HasAnyHiddenField);
        }

        [Fact]
        public void Refresh_raises_PropertyChanged_for_every_ShowXxx()
        {
            var vm = ProformaFieldVisibility.Instance;
            var raised = new List<string>();

            // Capture the handler in a local so we can unhook it at
            // the end of the test — the singleton lives for the
            // whole process and would otherwise accumulate closures
            // across test runs (a real, if benign, memory leak in
            // the test host).
            System.ComponentModel.PropertyChangedEventHandler handler =
                (_, e) =>
                {
                    if (e.PropertyName != null) raised.Add(e.PropertyName);
                };
            vm.PropertyChanged += handler;

            try
            {
                vm.Refresh();

            // Every bindable ShowXxx must be re-notified so live
            // bindings never miss a Settings save.
            Assert.Contains(nameof(vm.ShowCompanyTrn), raised);
            Assert.Contains(nameof(vm.ShowCompanyLocation), raised);
            Assert.Contains(nameof(vm.ShowRepresentative), raised);
            Assert.Contains(nameof(vm.ShowRepresentativeMobile), raised);
            Assert.Contains(nameof(vm.ShowCompanyEmail), raised);
            Assert.Contains(nameof(vm.ShowBankName), raised);
            Assert.Contains(nameof(vm.ShowAccountNumber), raised);
            Assert.Contains(nameof(vm.ShowIssueDate), raised);
            Assert.Contains(nameof(vm.ShowValidUntil), raised);
            Assert.Contains(nameof(vm.ShowProformaStatus), raised);
            Assert.Contains(nameof(vm.ShowCustomerTrn), raised);
            Assert.Contains(nameof(vm.ShowCustomerReferenceCode), raised);
            Assert.Contains(nameof(vm.ShowSalesRepresentative), raised);
            Assert.Contains(nameof(vm.ShowDocumentPreparedBy), raised);
            Assert.Contains(nameof(vm.ShowAttentionExecutive), raised);
            Assert.Contains(nameof(vm.ShowClientContactPhone), raised);
            Assert.Contains(nameof(vm.ShowBillingAddress), raised);
            Assert.Contains(nameof(vm.ShowProjectName), raised);
            Assert.Contains(nameof(vm.ShowProjectReference), raised);
            Assert.Contains(nameof(vm.ShowContractNo), raised);
            Assert.Contains(nameof(vm.ShowLpoNumber), raised);
                Assert.Contains(nameof(vm.ShowSiteDeliveryLocation), raised);
                Assert.Contains(nameof(vm.HasAnyHiddenField), raised);
            }
            finally
            {
                vm.PropertyChanged -= handler;
            }
        }
    }
}
