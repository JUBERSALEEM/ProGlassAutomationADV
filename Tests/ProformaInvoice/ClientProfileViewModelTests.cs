// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/ClientProfileViewModelTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins Step 2's validation surface (CustomerName required, Customer
// TRN 15-digit rule) and PropertyChanged plumbing. Same hermetic
// contract as CompanySetupViewModelTests: no DB, no filesystem.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.ProformaInvoice.Step2ClientProfile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public class ClientProfileViewModelTests
    {
        // ── Required-field message ─────────────────────────────────

        [Fact]
        public void CustomerName_blank_sets_error_message()
        {
            var vm = new ClientProfileViewModel { CustomerName = "" };

            Assert.True(vm.HasCustomerNameError);
            Assert.NotNull(vm.CustomerNameValidationMessage);
            Assert.Contains("required", vm.CustomerNameValidationMessage!,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CustomerName_filled_clears_error_message()
        {
            var vm = new ClientProfileViewModel { CustomerName = "Al Habtoor Interiors" };

            Assert.False(vm.HasCustomerNameError);
            Assert.Null(vm.CustomerNameValidationMessage);
        }

        [Fact]
        public void CustomerName_whitespace_only_is_treated_as_blank()
        {
            var vm = new ClientProfileViewModel { CustomerName = "\t \n" };

            Assert.True(vm.HasCustomerNameError);
        }

        // ── UAE TRN validation on client side ──────────────────────

        [Fact]
        public void CustomerTrn_empty_is_valid()
        {
            var vm = new ClientProfileViewModel { CustomerTrn = "" };

            Assert.True(vm.IsCustomerTrnValid);
            Assert.Null(vm.CustomerTrnValidationMessage);
        }

        [Fact]
        public void CustomerTrn_exact_15_digits_is_valid()
        {
            var vm = new ClientProfileViewModel { CustomerTrn = "100000000000003" };

            Assert.True(vm.IsCustomerTrnValid);
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("1000000000000")]
        [InlineData("10000000000000000")]
        public void CustomerTrn_wrong_length_is_rejected(string trn)
        {
            var vm = new ClientProfileViewModel { CustomerTrn = trn };

            Assert.False(vm.IsCustomerTrnValid);
            Assert.NotNull(vm.CustomerTrnValidationMessage);
        }

        [Theory]
        [InlineData("ABCDEFGHIJKLMNO")]
        [InlineData("100 000 000 000 003")]
        public void CustomerTrn_with_non_digits_is_rejected(string trn)
        {
            var vm = new ClientProfileViewModel { CustomerTrn = trn };

            Assert.False(vm.IsCustomerTrnValid);
        }

        // ── PropertyChanged plumbing ───────────────────────────────

        [Fact]
        public void Changing_CustomerName_raises_change_for_derived_properties()
        {
            var vm = new ClientProfileViewModel();
            var raised = CapturePropertyChanges(vm);

            vm.CustomerName = "Global Glass Co.";

            Assert.Contains(nameof(vm.CustomerName), raised);
            Assert.Contains(nameof(vm.CustomerNameValidationMessage), raised);
            Assert.Contains(nameof(vm.HasCustomerNameError), raised);
        }

        [Fact]
        public void Changing_CustomerTrn_raises_change_for_validation_pair()
        {
            var vm = new ClientProfileViewModel();
            var raised = CapturePropertyChanges(vm);

            vm.CustomerTrn = "10000";

            Assert.Contains(nameof(vm.CustomerTrn), raised);
            Assert.Contains(nameof(vm.CustomerTrnValidationMessage), raised);
        }

        [Fact]
        public void Setting_same_CustomerName_does_not_raise_change()
        {
            var vm = new ClientProfileViewModel { CustomerName = "Al Habtoor" };
            var raised = CapturePropertyChanges(vm);

            vm.CustomerName = "Al Habtoor";

            Assert.DoesNotContain(nameof(vm.CustomerName), raised);
        }

        [Fact]
        public void Free_text_fields_roundtrip_without_error()
        {
            var vm = new ClientProfileViewModel
            {
                CustomerName = "Acme Interiors",
                CustomerReferenceCode = "REF-4210",
                SalesRepresentative = "S. Kumar",
                DocumentPreparedBy = "N. Ahmed",
                AttentionExecutiveName = "Mr. Khan",
                BillingAddress = "PO Box 1234\nDubai, UAE",
                ClientContactPhone = "+971 4 555 0100"
            };

            Assert.Equal("Acme Interiors", vm.CustomerName);
            Assert.Equal("REF-4210", vm.CustomerReferenceCode);
            Assert.Equal("S. Kumar", vm.SalesRepresentative);
            Assert.Equal("N. Ahmed", vm.DocumentPreparedBy);
            Assert.Equal("Mr. Khan", vm.AttentionExecutiveName);
            Assert.Contains("Dubai", vm.BillingAddress);
            Assert.Equal("+971 4 555 0100", vm.ClientContactPhone);
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
