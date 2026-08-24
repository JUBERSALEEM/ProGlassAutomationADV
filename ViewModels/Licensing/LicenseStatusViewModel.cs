// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Licensing/LicenseStatusViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Licensing — view model for the About / Settings pages
//
// The About page and the Settings → Data → Database Health
// card both need to show a license summary (status, edition,
// customer, expiry, days remaining, fingerprint). This VM is
// the single source of truth for that surface.
//
// LIFECYCLE
//   • Singleton — LicenseService is already a static, so this
//     VM is too. Anyone who needs a license badge / summary
//     reads LicenseStatusViewModel.Instance.
//   • Subscribes to LicenseService.StatusChanged on first
//     access, refreshes its properties, and unsubscribes on
//     dispose (called by the splash view on app shutdown).
//
// WHAT IT DOES NOT DO
//   • Does not call any other service. The About / Settings
//     pages are read-only surfaces — clicking "Activate" or
//     "Start Trial" is the welcome view's job, which already
//     uses LicenseService directly.
//   • Does not format dates for the user's locale. The
//     Windows-DateFormat is what the OS would show anyway,
//     and it matches every other VM in the codebase.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.ViewModels.Licensing;
using ProGlassAutomation.Views.Licensing;       // LicenseUiText (StatusBadge / StatusDescription)

namespace ProGlassAutomation.ViewModels.Licensing
{
    /// <summary>
    /// View model that surfaces the current license
    /// status as WPF-bindable properties. Use
    /// <see cref="Instance"/> from any view.
    /// </summary>
    public sealed class LicenseStatusViewModel : ViewModelBase
    {
        private static readonly Lazy<LicenseStatusViewModel> _instance =
            new(() => new LicenseStatusViewModel());

        public static LicenseStatusViewModel Instance => _instance.Value;

        private string _statusBadge = "—";
        private string _statusDescription = "Loading…";
        private string _editionDisplay = "—";
        private string _customerDisplay = "—";
        private string _companyDisplay = "—";
        private string _issuedDisplay = "—";
        private string _expiresDisplay = "—";
        private int _daysRemaining;
        private bool _isValid;
        private bool _isTrial;
        private bool _isExpired;
        private bool _isGracePeriod;
        private bool _isExpiringSoon;
        private bool _isNotActivated;
        private bool _isPlaceholder;
        private string _machineFingerprint = "—";
        private string _currentFingerprint = "—";
        private string _featuresDisplay = "—";
        private string _licenseIdDisplay = "—";
        private string _notesDisplay = "";
        private string _errorDetail = "";

        private LicenseStatusViewModel()
        {
            // Subscribe on first construction. The
            // constructor runs exactly once (Lazy<>) so
            // the subscription is permanent for the
            // process lifetime.
            LicenseService.StatusChanged += OnLicenseChanged;
            Refresh();
        }

        /// <summary>
        /// Re-read the license state and raise change
        /// notifications for every bound property. Called
        /// from <see cref="OnLicenseChanged"/> and from
        /// tests.
        /// </summary>
        public void Refresh()
        {
            var r = LicenseService.Current;
            _statusBadge = LicenseUiText.StatusBadge(r.Status);
            _statusDescription = string.IsNullOrEmpty(r.ErrorDetail)
                ? LicenseUiText.StatusDescription(r.Status)
                : r.ErrorDetail;
            _errorDetail = r.ErrorDetail ?? "";

            var p = r.Payload;
            _isValid = r.Status == LicenseStatus.Valid
                || r.Status == LicenseStatus.ExpiringSoon;
            _isTrial = r.Status == LicenseStatus.Trial;
            _isExpired = r.Status == LicenseStatus.Expired;
            _isGracePeriod = r.Status == LicenseStatus.GracePeriod;
            _isExpiringSoon = r.Status == LicenseStatus.ExpiringSoon;
            _isNotActivated = r.Status == LicenseStatus.NotActivated;
            _isPlaceholder = LicensePublicKey.IsPlaceholder;

            if (p == null)
            {
                _editionDisplay = "—";
                _customerDisplay = "—";
                _companyDisplay = "—";
                _issuedDisplay = "—";
                _expiresDisplay = "—";
                _licenseIdDisplay = "—";
                _machineFingerprint = "—";
                _featuresDisplay = "—";
                _notesDisplay = "";
                _daysRemaining = 0;
            }
            else
            {
                _editionDisplay = FormatEdition(p.Edition, r.Status);
                _customerDisplay = string.IsNullOrEmpty(p.CustomerId)
                    ? "—" : p.CustomerId;
                _companyDisplay = string.IsNullOrEmpty(p.CompanyName)
                    ? "—" : p.CompanyName;
                _issuedDisplay = p.IssuedAt.ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm");
                _expiresDisplay = p.ExpiresAt.ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm");
                _licenseIdDisplay = string.IsNullOrEmpty(p.LicenseId)
                    ? "—" : p.LicenseId;
                _machineFingerprint = string.IsNullOrEmpty(p.MachineFingerprint)
                    ? "— (portable)" : p.MachineFingerprint;
                _featuresDisplay = (p.Features == null || p.Features.Count == 0)
                    ? "—"
                    : string.Join(", ", p.Features);
                _notesDisplay = p.Notes ?? "";
                _daysRemaining = (int)Math.Max(0,
                    (p.ExpiresAt.ToUniversalTime() - DateTime.UtcNow)
                        .TotalDays);
            }

            try
            {
                // Fully-qualified because this class has a
                // property named MachineFingerprint (the
                // license-side fingerprint, of type string).
                // A bare MachineFingerprint would resolve to
                // the property, not to the static class.
                _currentFingerprint = ProGlassAutomation
                    .Services.Licensing.MachineFingerprint
                    .ComputeCurrent();
            }
            catch
            {
                // Defensive: the fingerprint code can throw on
                // weird hardware; show "—" rather than crash.
                _currentFingerprint = "—";
            }

            // Whichever properties are bound by callers —
            // raise change notifications for all of them.
            OnPropertyChanged(nameof(StatusBadge));
            OnPropertyChanged(nameof(StatusDescription));
            OnPropertyChanged(nameof(EditionDisplay));
            OnPropertyChanged(nameof(CustomerDisplay));
            OnPropertyChanged(nameof(CompanyDisplay));
            OnPropertyChanged(nameof(IssuedDisplay));
            OnPropertyChanged(nameof(ExpiresDisplay));
            OnPropertyChanged(nameof(DaysRemaining));
            OnPropertyChanged(nameof(DaysRemainingDisplay));
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(IsTrial));
            OnPropertyChanged(nameof(IsExpired));
            OnPropertyChanged(nameof(IsGracePeriod));
            OnPropertyChanged(nameof(IsExpiringSoon));
            OnPropertyChanged(nameof(IsNotActivated));
            OnPropertyChanged(nameof(IsPlaceholder));
            OnPropertyChanged(nameof(MachineFingerprint));
            OnPropertyChanged(nameof(CurrentFingerprint));
            OnPropertyChanged(nameof(FeaturesDisplay));
            OnPropertyChanged(nameof(LicenseIdDisplay));
            OnPropertyChanged(nameof(NotesDisplay));
            OnPropertyChanged(nameof(ErrorDetail));
            // Commit 4 of the licensing refactor: the main
            // window's overlay trigger binds to this. Raise
            // it explicitly so the overlay shows / hides on
            // every status change without needing the host
            // to wire anything.
            OnPropertyChanged(nameof(IsLicenseRequired));
            OnPropertyChanged(nameof(IsReadOnly));
            OnPropertyChanged(nameof(IsTrialExpiredReadOnly));
        }

        private void OnLicenseChanged(object? sender, LicenseVerificationResult e)
            => Refresh();

        // ── Properties ──────────────────────────────────────────
        //
        // These are read-only auto-properties whose values
        // come from the matching private backing fields.
        // The private setter is intentionally a no-op
        // assignment to a discarded value so the compiler
        // accepts the syntax; Refresh() is the only
        // writer, and it raises PropertyChanged for every
        // bound property explicitly.

        public string StatusBadge => _statusBadge;
        public string StatusDescription => _statusDescription;
        public string EditionDisplay => _editionDisplay;
        public string CustomerDisplay => _customerDisplay;
        public string CompanyDisplay => _companyDisplay;
        public string IssuedDisplay => _issuedDisplay;
        public string ExpiresDisplay => _expiresDisplay;
        public int DaysRemaining => _daysRemaining;
        public bool IsValid => _isValid;
        public bool IsTrial => _isTrial;
        public bool IsExpired => _isExpired;
        public bool IsGracePeriod => _isGracePeriod;
        public bool IsExpiringSoon => _isExpiringSoon;
        public bool IsNotActivated => _isNotActivated;
        public bool IsPlaceholder => _isPlaceholder;
        public string MachineFingerprint => _machineFingerprint;
        public string CurrentFingerprint => _currentFingerprint;
        public string FeaturesDisplay => _featuresDisplay;
        public string LicenseIdDisplay => _licenseIdDisplay;
        public string NotesDisplay => _notesDisplay;
        public string ErrorDetail => _errorDetail;

        /// <summary>
        /// "37 days" / "expired" — pre-formatted for the UI.
        /// </summary>
        public string DaysRemainingDisplay
        {
            get
            {
                if (IsExpired) return "expired";
                if (IsGracePeriod) return "grace period";
                if (DaysRemaining <= 0) return "today";
                if (DaysRemaining == 1) return "1 day";
                return DaysRemaining + " days";
            }
        }

        /// <summary>
        /// True when the user can run the app with full
        /// functionality (no read-only mode).
        /// </summary>
        public bool CanRunFullUi => IsValid || IsTrial;

        /// <summary>
        /// True ONLY when the app is in a hard-broken
        /// licensing state that the user cannot navigate
        /// past — invalid signature, machine-mismatch,
        /// exceeded activations, an expired paid license,
        /// or a fresh install where the auto-trial could
        /// not be issued (placeholder key pair). In every
        /// one of those cases the app boots into the
        /// LicenseActivationView because no productive
        /// work is possible.
        ///
        /// EXPLICITLY excluded from this flag:
        ///   • <see cref="LicenseStatus.Trial"/>
        ///   • <see cref="LicenseStatus.TrialExpired"/> (read-only)
        ///   • <see cref="LicenseStatus.Valid"/> /
        ///     <see cref="LicenseStatus.ExpiringSoon"/> /
        ///     <see cref="LicenseStatus.GracePeriod"/>
        ///
        /// PRODUCT DECISION:
        /// The old behaviour showed the LicenseActivationView
        /// as the default main screen on every fresh launch
        /// until a licence was accepted. That defeats the
        /// point of an out-of-the-box trial — nobody wants
        /// their industrial app to greet them with a
        /// licensing form. The 30-day auto-trial (see
        /// <see cref="LicenseService.AutoTrialDays"/>) covers
        /// the fresh-install case; TrialExpired flips the app
        /// to read-only and shows a slim banner, not a full
        /// overlay. The Settings → License page is where the
        /// user manages activation on their own schedule.
        /// </summary>
        public bool IsLicenseRequired
        {
            get
            {
                var status = LicenseService.Current.Status;
                switch (status)
                {
                    // Runnable — never force the overlay.
                    case LicenseStatus.Valid:
                    case LicenseStatus.ExpiringSoon:
                    case LicenseStatus.Trial:
                    case LicenseStatus.GracePeriod:
                    case LicenseStatus.TrialExpired:
                        return false;
                    // Hard broken — no productive work is
                    // possible until the user acts.
                    default:
                        return true;
                }
            }
        }

        /// <summary>
        /// True when the app is running but is not permitted
        /// to make new writes / exports / prints. The main
        /// window shows a slim, dismissable "read-only —
        /// activate to continue editing" banner while this is
        /// true. See <see cref="LicenseVerificationResult.ReadOnly"/>.
        /// </summary>
        public bool IsReadOnly => LicenseService.Current.ReadOnly;

        /// <summary>
        /// True when the app is in read-only mode BECAUSE the
        /// auto-trial ended. The read-only banner uses this to
        /// pick the right copy ("Your trial ended — activate to
        /// keep editing") vs. the paid grace-period copy
        /// ("Your licence has expired — renew to keep editing").
        /// </summary>
        public bool IsTrialExpiredReadOnly =>
            LicenseService.Current.Status == LicenseStatus.TrialExpired;

        // ── Helpers ─────────────────────────────────────────────

        private static string FormatEdition(LicenseEdition edition, LicenseStatus status)
        {
            // Show the user the edition + a status hint
            // when relevant. Trial is always shown as
            // "Trial" with a "(X days remaining)" hint.
            string name = edition switch
            {
                LicenseEdition.Trial => "Trial",
                LicenseEdition.Basic => "Basic",
                LicenseEdition.Business => "Business",
                LicenseEdition.Factory => "Factory",
                LicenseEdition.Enterprise => "Enterprise",
                _ => edition.ToString()
            };
            if (status == LicenseStatus.GracePeriod)
                return name + " (grace period)";
            if (status == LicenseStatus.ExpiringSoon)
                return name + " (expiring soon)";
            return name;
        }
    }
}
