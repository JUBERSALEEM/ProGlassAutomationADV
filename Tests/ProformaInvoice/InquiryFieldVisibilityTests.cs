// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/InquiryFieldVisibilityTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the two guarantees the Inquiry editor's XAML depends on:
//   • When App.Config is null, every ShowXxx defaults to true.
//   • Refresh() raises PropertyChanged for every ShowXxx property.
//
// Mirrors ProformaFieldVisibilityTests exactly — parity is a
// deliberate design goal so a future refactor that unifies the two
// singletons behind a base class has a clean template.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Features;
using System.Collections.Generic;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public class InquiryFieldVisibilityTests
    {
        [Fact]
        public void Instance_returns_the_same_singleton_every_time()
        {
            var a = InquiryFieldVisibility.Instance;
            var b = InquiryFieldVisibility.Instance;

            Assert.Same(a, b);
        }

        [Fact]
        public void Every_ShowXxx_defaults_to_true_when_config_is_null()
        {
            var vm = InquiryFieldVisibility.Instance;

            Assert.True(vm.ShowInquiryReference);
            Assert.True(vm.ShowCustomerLocation);
            Assert.True(vm.ShowProjectDescription);
            Assert.True(vm.ShowSalesman);
            Assert.True(vm.ShowPreparedBy);
            Assert.True(vm.ShowPriority);
            Assert.True(vm.ShowInquiryValue);
            Assert.True(vm.ShowNotes);
        }

        [Fact]
        public void HasAnyHiddenField_is_false_when_every_flag_is_on()
        {
            var vm = InquiryFieldVisibility.Instance;
            Assert.False(vm.HasAnyHiddenField);
        }

        [Fact]
        public void Refresh_raises_PropertyChanged_for_every_ShowXxx()
        {
            var vm = InquiryFieldVisibility.Instance;
            var raised = new List<string>();

            System.ComponentModel.PropertyChangedEventHandler handler =
                (_, e) => { if (e.PropertyName != null) raised.Add(e.PropertyName); };
            vm.PropertyChanged += handler;
            try
            {
                vm.Refresh();

                Assert.Contains(nameof(vm.ShowInquiryReference), raised);
                Assert.Contains(nameof(vm.ShowCustomerLocation), raised);
                Assert.Contains(nameof(vm.ShowProjectDescription), raised);
                Assert.Contains(nameof(vm.ShowSalesman), raised);
                Assert.Contains(nameof(vm.ShowPreparedBy), raised);
                Assert.Contains(nameof(vm.ShowPriority), raised);
                Assert.Contains(nameof(vm.ShowInquiryValue), raised);
                Assert.Contains(nameof(vm.ShowNotes), raised);
                Assert.Contains(nameof(vm.HasAnyHiddenField), raised);
            }
            finally
            {
                vm.PropertyChanged -= handler;
            }
        }
    }
}
