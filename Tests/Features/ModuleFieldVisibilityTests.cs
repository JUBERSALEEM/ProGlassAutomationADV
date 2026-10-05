// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Features/ModuleFieldVisibilityTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the guarantees the Job Order / Delivery Note / Tax Invoice editors
// depend on after the Aug-2026 module feature-flags pass:
//   • When App.Config is null, every ShowXxx defaults to true.
//   • Refresh() raises PropertyChanged for every ShowXxx property.
//
// Mirrors ProformaFieldVisibilityTests / InquiryFieldVisibilityTests —
// parity is a deliberate design goal.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Features;
using System.Collections.Generic;
using Xunit;

namespace ProGlassAutomation.Tests.Features
{
    public class JobOrderFieldVisibilityTests
    {
        [Fact]
        public void Instance_returns_the_same_singleton_every_time()
        {
            var a = JobOrderFieldVisibility.Instance;
            var b = JobOrderFieldVisibility.Instance;
            Assert.Same(a, b);
        }

        [Fact]
        public void Every_ShowXxx_defaults_to_true_when_config_is_null()
        {
            var vm = JobOrderFieldVisibility.Instance;
            Assert.True(vm.ShowAssignedTo);
            Assert.True(vm.ShowSiteDeliveryContact);
            Assert.True(vm.ShowSiteDeliveryPhone);
            Assert.True(vm.ShowSiteDeliveryAddress);
            Assert.True(vm.ShowDrawingReference);
            Assert.True(vm.ShowCuttingInstructions);
            Assert.True(vm.ShowInternalNotes);
        }

        [Fact]
        public void HasAnyHiddenField_is_false_when_every_flag_is_on()
        {
            Assert.False(JobOrderFieldVisibility.Instance.HasAnyHiddenField);
        }

        [Fact]
        public void Refresh_raises_PropertyChanged_for_every_ShowXxx()
        {
            var vm = JobOrderFieldVisibility.Instance;
            var raised = new List<string>();

            System.ComponentModel.PropertyChangedEventHandler handler =
                (_, e) => { if (e.PropertyName != null) raised.Add(e.PropertyName); };
            vm.PropertyChanged += handler;
            try
            {
                vm.Refresh();
                Assert.Contains(nameof(vm.ShowAssignedTo), raised);
                Assert.Contains(nameof(vm.ShowSiteDeliveryContact), raised);
                Assert.Contains(nameof(vm.ShowSiteDeliveryPhone), raised);
                Assert.Contains(nameof(vm.ShowSiteDeliveryAddress), raised);
                Assert.Contains(nameof(vm.ShowDrawingReference), raised);
                Assert.Contains(nameof(vm.ShowCuttingInstructions), raised);
                Assert.Contains(nameof(vm.ShowInternalNotes), raised);
                Assert.Contains(nameof(vm.HasAnyHiddenField), raised);
            }
            finally
            {
                vm.PropertyChanged -= handler;
            }
        }
    }

    public class DeliveryNoteFieldVisibilityTests
    {
        [Fact]
        public void Instance_returns_the_same_singleton_every_time()
        {
            var a = DeliveryNoteFieldVisibility.Instance;
            var b = DeliveryNoteFieldVisibility.Instance;
            Assert.Same(a, b);
        }

        [Fact]
        public void Every_ShowXxx_defaults_to_true_when_config_is_null()
        {
            var vm = DeliveryNoteFieldVisibility.Instance;
            Assert.True(vm.ShowDriverName);
            Assert.True(vm.ShowVehicleNumber);
            Assert.True(vm.ShowIsFoc);
            Assert.True(vm.ShowReceiverName);
            Assert.True(vm.ShowNotes);
        }

        [Fact]
        public void HasAnyHiddenField_is_false_when_every_flag_is_on()
        {
            Assert.False(DeliveryNoteFieldVisibility.Instance.HasAnyHiddenField);
        }

        [Fact]
        public void Refresh_raises_PropertyChanged_for_every_ShowXxx()
        {
            var vm = DeliveryNoteFieldVisibility.Instance;
            var raised = new List<string>();

            System.ComponentModel.PropertyChangedEventHandler handler =
                (_, e) => { if (e.PropertyName != null) raised.Add(e.PropertyName); };
            vm.PropertyChanged += handler;
            try
            {
                vm.Refresh();
                Assert.Contains(nameof(vm.ShowDriverName), raised);
                Assert.Contains(nameof(vm.ShowVehicleNumber), raised);
                Assert.Contains(nameof(vm.ShowIsFoc), raised);
                Assert.Contains(nameof(vm.ShowReceiverName), raised);
                Assert.Contains(nameof(vm.ShowNotes), raised);
                Assert.Contains(nameof(vm.HasAnyHiddenField), raised);
            }
            finally
            {
                vm.PropertyChanged -= handler;
            }
        }
    }

    public class TaxInvoiceFieldVisibilityTests
    {
        [Fact]
        public void Instance_returns_the_same_singleton_every_time()
        {
            var a = TaxInvoiceFieldVisibility.Instance;
            var b = TaxInvoiceFieldVisibility.Instance;
            Assert.Same(a, b);
        }

        [Fact]
        public void Every_ShowXxx_defaults_to_true_when_config_is_null()
        {
            var vm = TaxInvoiceFieldVisibility.Instance;
            Assert.True(vm.ShowDueDate);
            Assert.True(vm.ShowPaymentTerms);
            Assert.True(vm.ShowDiscountPercent);
            Assert.True(vm.ShowIban);
            Assert.True(vm.ShowSwiftCode);
        }

        [Fact]
        public void HasAnyHiddenField_is_false_when_every_flag_is_on()
        {
            Assert.False(TaxInvoiceFieldVisibility.Instance.HasAnyHiddenField);
        }

        [Fact]
        public void Refresh_raises_PropertyChanged_for_every_ShowXxx()
        {
            var vm = TaxInvoiceFieldVisibility.Instance;
            var raised = new List<string>();

            System.ComponentModel.PropertyChangedEventHandler handler =
                (_, e) => { if (e.PropertyName != null) raised.Add(e.PropertyName); };
            vm.PropertyChanged += handler;
            try
            {
                vm.Refresh();
                Assert.Contains(nameof(vm.ShowDueDate), raised);
                Assert.Contains(nameof(vm.ShowPaymentTerms), raised);
                Assert.Contains(nameof(vm.ShowDiscountPercent), raised);
                Assert.Contains(nameof(vm.ShowIban), raised);
                Assert.Contains(nameof(vm.ShowSwiftCode), raised);
                Assert.Contains(nameof(vm.HasAnyHiddenField), raised);
            }
            finally
            {
                vm.PropertyChanged -= handler;
            }
        }
    }
}
