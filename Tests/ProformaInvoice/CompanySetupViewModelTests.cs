// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/CompanySetupViewModelTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// SCOPE
// ─────
// Pins the pure validation and property-change behaviour of Step 1's
// CompanySetupViewModel. The tests intentionally exercise only the
// paths that do NOT touch App.MasterData or App.Invoices (both
// guarded by null checks inside the VM), so they run hermetically —
// no database, no filesystem, no App.Config.
//
// COVERAGE
//   1. CompanyName required-field message flips when text is blank.
//   2. ProformaId required-field message flips when text is blank.
//   3. TRN validator accepts blank, rejects wrong length / letters,
//      accepts exactly 15 digits.
//   4. PropertyChanged fires for CompanyName, TRN, ProformaId and
//      the derived error / validation properties.
//   5. Defaults on construction (ProformaId placeholder, IssueDate
//      today, ValidUntil +30 days, ProformaStatus = "Draft").
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.Common;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public class CompanySetupViewModelTests
    {
        // ── Defaults ──────────────────────────────────────────────

        [Fact]
        public void Constructor_seeds_status_and_dates_to_sensible_defaults()
        {
            var vm = new CompanySetupViewModel();

            Assert.False(string.IsNullOrEmpty(vm.ProformaId),
                "ProformaId should be seeded with a placeholder for BUG-33.");
            Assert.Equal("Draft", vm.ProformaStatus);
            Assert.Equal(DateTime.Now.Date, vm.IssueDate.Date);
            Assert.Equal(DateTime.Now.Date.AddDays(30), vm.ValidUntil.Date);
        }

        [Fact]
        public void StatusOptions_is_the_canonical_ProformaInvoiceStatus_All_including_Cancelled()
        {
            var vm = new CompanySetupViewModel();

            Assert.Equal(ProformaInvoiceStatus.All, vm.StatusOptions);
            Assert.Contains(ProformaInvoiceStatus.Cancelled, vm.StatusOptions);
            Assert.Contains(ProformaInvoiceStatus.Rejected, vm.StatusOptions);
            Assert.DoesNotContain("Void", vm.StatusOptions,
                StringComparer.OrdinalIgnoreCase);
        }

        // ── Required-field messages ────────────────────────────────

        [Fact]
        public void CompanyName_blank_sets_error_message()
        {
            var vm = new CompanySetupViewModel { CompanyName = "" };

            Assert.True(vm.HasCompanyNameError);
            Assert.NotNull(vm.CompanyNameValidationMessage);
            Assert.Contains("required", vm.CompanyNameValidationMessage!,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CompanyName_filled_clears_error_message()
        {
            var vm = new CompanySetupViewModel { CompanyName = "ProGlass LLC" };

            Assert.False(vm.HasCompanyNameError);
            Assert.Null(vm.CompanyNameValidationMessage);
        }

        [Fact]
        public void CompanyName_whitespace_only_is_treated_as_blank()
        {
            var vm = new CompanySetupViewModel { CompanyName = "   " };

            Assert.True(vm.HasCompanyNameError);
        }

        [Fact]
        public void ProformaId_blank_sets_error_message()
        {
            var vm = new CompanySetupViewModel { ProformaId = "" };

            Assert.True(vm.HasProformaIdError);
            Assert.NotNull(vm.ProformaIdValidationMessage);
        }

        [Fact]
        public void ProformaId_filled_clears_error_message()
        {
            var vm = new CompanySetupViewModel { ProformaId = "PI-2026-042" };

            Assert.False(vm.HasProformaIdError);
        }

        // ── UAE TRN validation ─────────────────────────────────────

        [Fact]
        public void Trn_empty_is_valid_no_message()
        {
            var vm = new CompanySetupViewModel { CompanyTrn = "" };

            Assert.True(vm.IsTrnValid);
            Assert.Null(vm.TrnValidationMessage);
        }

        [Theory]
        [InlineData("100000000000003")]  // exactly 15 digits
        [InlineData("999999999999999")]
        public void Trn_exactly_15_digits_is_valid(string trn)
        {
            var vm = new CompanySetupViewModel { CompanyTrn = trn };

            Assert.True(vm.IsTrnValid);
            Assert.Null(vm.TrnValidationMessage);
        }

        [Theory]
        [InlineData("100")]                     // too short
        [InlineData("10000000000000")]          // 14 digits
        [InlineData("1000000000000000")]        // 16 digits
        public void Trn_wrong_length_is_rejected(string trn)
        {
            var vm = new CompanySetupViewModel { CompanyTrn = trn };

            Assert.False(vm.IsTrnValid);
            Assert.NotNull(vm.TrnValidationMessage);
            Assert.Contains("15 digits", vm.TrnValidationMessage!,
                StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("100000000000ABC")]  // letters
        [InlineData("100 000 000 000 003")]  // spaces
        [InlineData("100-000-000-000-003")]  // hyphens
        public void Trn_with_non_digits_is_rejected(string trn)
        {
            var vm = new CompanySetupViewModel { CompanyTrn = trn };

            Assert.False(vm.IsTrnValid);
            Assert.NotNull(vm.TrnValidationMessage);
        }

        // ── PropertyChanged plumbing ───────────────────────────────

        [Fact]
        public void Changing_CompanyName_raises_PropertyChanged_for_derived_properties()
        {
            var vm = new CompanySetupViewModel();
            var raised = CapturePropertyChanges(vm);

            vm.CompanyName = "Acme";

            Assert.Contains(nameof(vm.CompanyName), raised);
            Assert.Contains(nameof(vm.CompanyNameValidationMessage), raised);
            Assert.Contains(nameof(vm.HasCompanyNameError), raised);
        }

        [Fact]
        public void Changing_CompanyTrn_raises_PropertyChanged_for_validation_pair()
        {
            var vm = new CompanySetupViewModel();
            var raised = CapturePropertyChanges(vm);

            vm.CompanyTrn = "123";

            Assert.Contains(nameof(vm.CompanyTrn), raised);
            Assert.Contains(nameof(vm.TrnValidationMessage), raised);
            Assert.Contains(nameof(vm.IsTrnValid), raised);
        }

        [Fact]
        public void Setting_same_value_does_not_raise_PropertyChanged()
        {
            var vm = new CompanySetupViewModel { CompanyName = "Acme" };
            var raised = CapturePropertyChanges(vm);

            vm.CompanyName = "Acme";  // same value

            Assert.DoesNotContain(nameof(vm.CompanyName), raised);
        }

        [Fact]
        public void HasLoadedProfile_is_false_by_default()
        {
            var vm = new CompanySetupViewModel();

            Assert.False(vm.HasLoadedProfile);
            Assert.True(string.IsNullOrEmpty(vm.LoadedProfileName));
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
