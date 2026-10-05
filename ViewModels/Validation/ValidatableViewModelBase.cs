// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Validation/ValidatableViewModelBase.cs
// MODULE: Inventory (Sheet Store) — blocking validation foundation
// ADDED: 2026-09-06
//
// WHY THIS EXISTS
//
// Owner report: "if mistakenly enter 0 total buy it still saving when an
// industry erp will not let it happen".
//
// Root cause found in BuyDialogViewModel: it DID declare the right guard
//
//     public bool CanConfirm => Quantity > 0;
//
// ...and never wired it. The Use dialog passes `_ => CanConfirm` as the
// command's CanExecute; the Buy dialog does not. So the button stayed
// live at qty 0. BuyAsync then re-checked `vm.Quantity > 0` and quietly
// did nothing — dialog closed, no purchase, NO MESSAGE. The operator
// believes stock was added. Silent failure is worse than no validation.
//
// A second, subtler fault made the guard unreachable anyway. The XAML
// binds an INT property with UpdateSourceTrigger=PropertyChanged:
//
//     Text="{Binding Quantity, UpdateSourceTrigger=PropertyChanged}"
//
// Clearing the box sends "", int conversion FAILS, and WPF therefore
// never calls the setter. The box reads empty while the backing field
// still holds its old value. No amount of clamping in that setter can
// see the empty state, because the setter does not run.
//
// THE SHAPE THAT FIXES BOTH
//
// Bind the RAW STRING and parse it explicitly. The VM then sees exactly
// what the operator sees — including empty — and can say so. This is
// what every serious ERP entry grid does; the numeric type is an OUTPUT
// of validation, never the binding surface.
//
// This base supplies INotifyDataErrorInfo so errors attach to the FIELD
// (red adorner + message under the offending box) instead of one shared
// error line at the bottom of the form, which is what the module had.
//
// DESIGN RULES ENFORCED HERE
//   • Never silently correct input. The old code did `if (value < 1)
//     value = 1`, rewriting a deliberate 0 into 1 behind the operator's
//     back. An ERP must refuse and explain, not guess.
//   • Blocking, not advisory. Commands gate on IsValid, so the primary
//     action is visibly dead until the form is right.
//   • Explain the bound. "Enter a quantity" / "0 is not a valid
//     quantity" / "Only 12 sheet(s) available" — never a bare "invalid".
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using ViewModelBase = ProGlassAutomation.ViewModels.ViewModelBase;

namespace ProGlassAutomation.ViewModels.Validation
{
    /// <summary>
    /// ViewModelBase + per-field validation (INotifyDataErrorInfo).
    /// Inherits the existing ViewModelBase so nothing about the app's
    /// change-notification contract shifts.
    /// </summary>
    public abstract class ValidatableViewModelBase : ViewModelBase, INotifyDataErrorInfo
    {
        private readonly Dictionary<string, List<string>> _errors = new();

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public bool HasErrors => _errors.Count > 0;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return _errors.SelectMany(kv => kv.Value).ToList();

            return _errors.TryGetValue(propertyName!, out var list)
                ? list
                : Array.Empty<string>();
        }

        /// <summary>
        /// True when the whole form may be committed. Commands gate on
        /// this, which is the difference between "advisory" validation
        /// (message appears, save still works) and "blocking" validation.
        /// </summary>
        public virtual bool IsValid => !HasErrors;

        /// <summary>
        /// First error for a field, or empty. Bound by the inline error
        /// TextBlock under each input.
        /// </summary>
        public string ErrorFor(string propertyName) =>
            _errors.TryGetValue(propertyName, out var list) && list.Count > 0
                ? list[0]
                : string.Empty;

        /// <summary>
        /// A single summary line for the dialog footer — the FIRST
        /// outstanding problem, so the operator is given one thing to fix
        /// rather than a wall of red.
        /// </summary>
        public string ValidationSummary =>
            _errors.Count == 0
                ? string.Empty
                : _errors.SelectMany(kv => kv.Value).FirstOrDefault() ?? string.Empty;

        protected void SetError(string propertyName, string? error)
        {
            bool had = _errors.ContainsKey(propertyName);

            if (string.IsNullOrEmpty(error))
            {
                if (!had) return;              // no change — stay quiet
                _errors.Remove(propertyName);
            }
            else
            {
                if (had && _errors[propertyName].Count == 1 &&
                    _errors[propertyName][0] == error)
                    return;                     // identical — stay quiet

                _errors[propertyName] = new List<string> { error! };
            }

            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(HasValidationSummary));
        }

        public bool HasValidationSummary => !string.IsNullOrEmpty(ValidationSummary);

        protected void ClearErrors()
        {
            if (_errors.Count == 0) return;
            var keys = _errors.Keys.ToList();
            _errors.Clear();
            foreach (var k in keys)
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(k));

            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(HasValidationSummary));
        }

        // ═══════════════════════════════════════════════════════════════
        //  PARSE HELPERS
        //
        //  These return the parsed value AND record the field error, so a
        //  setter reads as one line. They deliberately distinguish EMPTY
        //  from ZERO from OUT-OF-RANGE, because those are three different
        //  operator mistakes deserving three different sentences.
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Parses a required whole-number quantity.
        /// </summary>
        /// <param name="max">Upper bound, or null for unbounded.</param>
        /// <param name="maxMessage">
        /// Message when over <paramref name="max"/> — should name the real
        /// limit, e.g. "Only 12 sheet(s) available".
        /// </param>
        protected int? ParseQuantity(
            string? raw,
            string propertyName,
            string fieldLabel,
            int? max = null,
            string? maxMessage = null)
        {
            raw = raw?.Trim();

            if (string.IsNullOrEmpty(raw))
            {
                SetError(propertyName, $"Enter a {fieldLabel}.");
                return null;
            }

            if (!int.TryParse(raw, out int value))
            {
                // AcceptIntegersOnly blocks typed letters, but paste,
                // IME and programmatic sets can still land here.
                SetError(propertyName, $"{fieldLabel} must be a whole number.");
                return null;
            }

            if (value == 0)
            {
                // Called out separately: "0 is not acceptable" is the
                // owner's own wording, and it is the single most common
                // real mistake.
                SetError(propertyName, $"0 is not a valid {fieldLabel}. Enter 1 or more.");
                return null;
            }

            if (value < 0)
            {
                SetError(propertyName, $"{fieldLabel} cannot be negative.");
                return null;
            }

            if (max.HasValue && value > max.Value)
            {
                SetError(propertyName,
                    maxMessage ?? $"{fieldLabel} cannot exceed {max.Value}.");
                return null;
            }

            SetError(propertyName, null);
            return value;
        }

        /// <summary>
        /// Parses a required decimal (money, SQM, linear metres). Zero is
        /// rejected only when <paramref name="allowZero"/> is false — a
        /// price of 0 is sometimes legitimate (free sample, promo), a
        /// quantity of 0 never is.
        /// </summary>
        protected decimal? ParseDecimal(
            string? raw,
            string propertyName,
            string fieldLabel,
            bool allowZero = true,
            decimal? max = null)
        {
            raw = raw?.Trim();

            if (string.IsNullOrEmpty(raw))
            {
                SetError(propertyName, $"Enter a {fieldLabel}.");
                return null;
            }

            if (!decimal.TryParse(raw, out decimal value))
            {
                SetError(propertyName, $"{fieldLabel} must be a number.");
                return null;
            }

            if (value < 0)
            {
                SetError(propertyName, $"{fieldLabel} cannot be negative.");
                return null;
            }

            if (!allowZero && value == 0)
            {
                SetError(propertyName, $"0 is not a valid {fieldLabel}.");
                return null;
            }

            if (max.HasValue && value > max.Value)
            {
                SetError(propertyName, $"{fieldLabel} cannot exceed {max.Value:N2}.");
                return null;
            }

            SetError(propertyName, null);
            return value;
        }

        /// <summary>Required free-text field (Category, Supplier, ...).</summary>
        protected string? ParseRequiredText(
            string? raw, string propertyName, string fieldLabel, int maxLength = 0)
        {
            raw = raw?.Trim();

            if (string.IsNullOrWhiteSpace(raw))
            {
                SetError(propertyName, $"{fieldLabel} is required.");
                return null;
            }

            if (maxLength > 0 && raw!.Length > maxLength)
            {
                SetError(propertyName,
                    $"{fieldLabel} cannot be longer than {maxLength} characters.");
                return null;
            }

            SetError(propertyName, null);
            return raw;
        }

        protected void Raise([CallerMemberName] string? name = null) => OnPropertyChanged(name!);
    }
}
