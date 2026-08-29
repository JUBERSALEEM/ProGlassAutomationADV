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
//     pages are read-only surfaces — activation/export/import work
//     belongs to the dedicated licensing UI, which already uses
//     LicenseService directly.
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
        private int _trialTotalDays;
        private bool _isValid;
        private bool _isTrial;
        private bool _isExpired;
        private bool _isGracePeriod;
        private bool _isExpiringSoon;
        private bool _isNotActivated;
        private bool _isPlaceholder;
        private string _machineFingerprint = "—";
        private string _currentFingerprint = "Computing…";
        private string _featuresDisplay = "—";
        private string _licenseIdDisplay = "—";
        private string _notesDisplay = "";
        private string _errorDetail = "";
        private string _activationModeDisplay = "—";
        private string _licenseSourceDisplay = "—";
        private string _licenseWindowDisplay = "—";
        private string _trustedIssuerDisplay = "—";
        private string _featureSummaryDisplay = "—";

        private LicenseStatusViewModel()
        {
            // Subscribe on first construction. The
            // constructor runs exactly once (Lazy<>) so
            // the subscription is permanent for the
            // process lifetime.
            LicenseService.StatusChanged += OnLicenseChanged;
            Refresh();
            EnsureFingerprintAsync();
        }

        /// <summary>
        /// Computes the machine fingerprint exactly once, OFF the UI
        /// thread, and publishes it when ready. The UI shows
        /// "Computing…" until then. 2026-08-25: replaces the
        /// synchronous compute inside Refresh() that froze the About
        /// page on machines with many network adapters / slow drives.
        /// </summary>
        private void EnsureFingerprintAsync()
        {
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                string fp;
                try
                {
                    fp = ProGlassAutomation.Services.Licensing
                        .MachineFingerprint.ComputeCurrent();
                }
                catch
                {
                    // Defensive: the fingerprint code can throw on
                    // weird hardware; show "—" rather than crash.
                    fp = "—";
                }

                var disp = System.Windows.Application.Current?.Dispatcher;
                void Apply()
                {
                    _currentFingerprint = fp;
                    OnPropertyChanged(nameof(CurrentFingerprint));
                }

                try
                {
                    if (disp != null && !disp.CheckAccess())
                        disp.BeginInvoke(new Action(Apply));
                    else
                        Apply();
                }
                catch
                {
                    Apply();
                }
            });
        }

        /// <summary>
        /// Re-read the license state and raise change
        /// notifications for every bound property. Called
        /// from <see cref="OnLicenseChanged"/> and from
        /// tests.
        /// </summary>
        public void Refresh()
        {
            // 2026-08-25 senior hardening: this method runs from the
            // singleton CONSTRUCTOR (i.e. the moment the About page /
            // license cards are built) and on every license StatusChanged
            // event. If any license data shape throws here (extreme
            // sentinel dates, corrupt payloads), the exception kills the
            // singleton construction and takes the About page down with
            // it. The whole computation is therefore guarded: on failure
            // the previous values remain and the page still renders.
            try
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
                _featureSummaryDisplay = "No active feature set";
                _activationModeDisplay = "No active activation response";
                _licenseSourceDisplay = "Generate an activation request and import the vendor-signed offline response in Settings → License.";
                _licenseWindowDisplay = "—";
                _trustedIssuerDisplay = LicenseVerifier.EmbeddedPublicKeyFingerprint;
                _notesDisplay = "";
                _daysRemaining = 0;
                _trialTotalDays = 0;
            }
            else
            {
                _editionDisplay = FormatEdition(p.Edition, r.Status);
                _customerDisplay = string.IsNullOrEmpty(p.CustomerId)
                    ? "—" : p.CustomerId;
                _companyDisplay = string.IsNullOrEmpty(p.CompanyName)
                    ? "—" : p.CompanyName;
                _issuedDisplay = SafeLocalString(p.IssuedAt);
                _expiresDisplay = SafeLocalString(p.EffectiveExpiresAt);
                _licenseIdDisplay = string.IsNullOrEmpty(p.LicenseId)
                    ? "—" : p.LicenseId;
                _machineFingerprint = string.IsNullOrEmpty(p.MachineFingerprint)
                    ? "— (portable)" : p.MachineFingerprint;
                _featuresDisplay = (p.Features == null || p.Features.Count == 0)
                    ? "—"
                    : string.Join(", ", p.Features);
                _featureSummaryDisplay = BuildFeatureSummary(p.Features);
                _activationModeDisplay = BuildActivationMode(p);
                _licenseSourceDisplay = string.IsNullOrEmpty(p.TrialSource)
                    ? "Vendor-issued signed offline activation response."
                    : $"{p.TotalDays}-day vendor-issued offline trial response.";
                _licenseWindowDisplay = $"{_issuedDisplay} → {_expiresDisplay}";
                _trustedIssuerDisplay = LicenseVerifier.EmbeddedPublicKeyFingerprint;
                _notesDisplay = p.Notes ?? "";
                // Read the expiry from the payload's own authoritative
                // value (TrialExpiresAt for trials, ExpiresAt otherwise),
                // never from a hardcoded "7-day" / "30-day" string — that
                // is the fix for "shows 7 days when it's a 30-day trial".
                _daysRemaining = SafeDaysRemaining(p.EffectiveExpiresAt);
                _trialTotalDays = p.TotalDays;
            }

            // 2026-08-25 senior fix: the fingerprint was computed HERE,
            // synchronously, on every Refresh — and Refresh runs when the
            // About page opens and on every license StatusChanged event.
            // NIC enumeration + the volume probe can stall the UI thread
            // for seconds (the About page freeze). The fingerprint is now
            // computed once on a background thread at construction
            // (see EnsureFingerprintAsync) and cached by the service.
            }
            catch (Exception ex)
            {
                // Whatever the license data looks like, it must never
                // take the About page down. Previous values remain.
                ProGlassAutomation.Services.Logging.AppLogService.Warning(
                    "[LicenseStatusVM] Refresh failed: " + ex.Message);
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
            OnPropertyChanged(nameof(TrialTotalDays));
            OnPropertyChanged(nameof(TrialStatusText));
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
            OnPropertyChanged(nameof(FeatureSummaryDisplay));
            OnPropertyChanged(nameof(LicenseIdDisplay));
            OnPropertyChanged(nameof(ActivationModeDisplay));
            OnPropertyChanged(nameof(LicenseSourceDisplay));
            OnPropertyChanged(nameof(LicenseWindowDisplay));
            OnPropertyChanged(nameof(TrustedIssuerDisplay));
            OnPropertyChanged(nameof(NotesDisplay));
            OnPropertyChanged(nameof(HasNotes));
            OnPropertyChanged(nameof(HasPayloadDetails));
            OnPropertyChanged(nameof(SupportSummary));
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

        /// <summary>
        /// Formats a license date for display WITHOUT ever throwing.
        /// ToLocalTime() overflows on extreme sentinel dates (e.g.
        /// DateTime.MaxValue on machines east of UTC — the UAE is UTC+4),
        /// which would otherwise crash the singleton constructor.
        /// </summary>
        private static string SafeLocalString(DateTime dt)
        {
            try { return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"); }
            catch { return dt.ToString("yyyy-MM-dd HH:mm"); }
        }

        /// <summary>Days-until-expiry that can never throw.</summary>
        private static int SafeDaysRemaining(DateTime expiresAt)
        {
            try
            {
                // Use Ceiling so the final partial day counts as a full day:
                // a trial with 28.4 days shown as "28 days" would drop a day
                // the moment any time elapses. Ceiling keeps "29 days" until
                // the true 28-day boundary, matching how users read a countdown.
                return (int)Math.Max(0,
                    Math.Ceiling((expiresAt.ToUniversalTime() - DateTime.UtcNow).TotalDays));
            }
            catch
            {
                return 0;
            }
        }

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
        /// <summary>Total trial duration in days (read from the payload).</summary>
        public int TrialTotalDays => _trialTotalDays;
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
        public string FeatureSummaryDisplay => _featureSummaryDisplay;
        public string LicenseIdDisplay => _licenseIdDisplay;
        public string ActivationModeDisplay => _activationModeDisplay;
        public string LicenseSourceDisplay => _licenseSourceDisplay;
        public string LicenseWindowDisplay => _licenseWindowDisplay;
        public string TrustedIssuerDisplay => _trustedIssuerDisplay;
        public string NotesDisplay => _notesDisplay;
        public bool HasNotes => !string.IsNullOrWhiteSpace(_notesDisplay);
        public bool HasPayloadDetails => LicenseService.Current.Payload != null;
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
        /// Human string for a trial that states the REAL balance read from
        /// the payload, e.g. "29 of 30 days remaining". Empty when not a
        /// trial. Replaces the old hardcoded "7-day trial" text.
        /// </summary>
        public string TrialStatusText
        {
            get
            {
                if (!IsTrial) return "";
                int total = _trialTotalDays > 0
                    ? _trialTotalDays
                    : 30;
                if (_daysRemaining <= 0) return "trial ending today";
                return _daysRemaining + " of " + total + " days remaining";
            }
        }

        /// <summary>
        /// True when the user can run the app with full
        /// functionality (no read-only mode).
        /// </summary>
        public bool CanRunFullUi => IsValid || IsTrial;

        /// <summary>
        /// About/support surface helper. This summarizes who the license
        /// belongs to, what mode it runs in, and which issuer ring this build
        /// trusts.
        /// </summary>
        public string SupportSummary =>
            $"{CustomerDisplay} · {EditionDisplay} · {ActivationModeDisplay} · trusted key ring {TrustedIssuerDisplay}";

        /// <summary>
        /// True ONLY when the app is in a hard-broken licensing state
        /// that prevents productive work — no signed activation response,
        /// invalid signature/state, wrong machine binding, or expired paid
        /// license outside any signed read-only grace policy.
        ///
        /// EXPLICITLY excluded from this flag:
        ///   • <see cref="LicenseStatus.Trial"/>
        ///   • <see cref="LicenseStatus.TrialExpired"/> (read-only)
        ///   • <see cref="LicenseStatus.Valid"/> /
        ///     <see cref="LicenseStatus.ExpiringSoon"/> /
        ///     <see cref="LicenseStatus.GracePeriod"/>
        ///
        /// PRODUCT DECISION:
        /// The secure offline redesign removes client-side self-issued
        /// trial activation. A fresh install therefore shows the
        /// LicenseActivationView until the operator generates an activation
        /// request and imports the vendor-signed response. Read-only states
        /// still stay inside the shell so customer data remains accessible.
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
        /// True when the app is in read-only mode because a vendor-issued
        /// trial response expired. The read-only banner uses this to pick
        /// the trial wording vs. the paid grace-period wording.
        /// </summary>
        public bool IsTrialExpiredReadOnly =>
            LicenseService.Current.Status == LicenseStatus.TrialExpired;

        // ── Helpers ─────────────────────────────────────────────

        private static string BuildActivationMode(LicensePayload payload)
        {
            if (payload == null)
                return "No active license";

            if (!string.IsNullOrWhiteSpace(payload.MachineFingerprint))
                return "Machine-bound · only the licensed fingerprint may run this license";

            return payload.MaxActivations >= 9999
                ? "Portable · high-volume / effectively unlimited device allowance"
                : $"Portable · up to {payload.MaxActivations} activated device(s)";
        }

        private static string BuildFeatureSummary(IReadOnlyList<string>? features)
        {
            if (features == null || features.Count == 0)
                return "No active feature entries";
            if (features.Any(f => string.Equals(f, "*", StringComparison.Ordinal)))
                return "All modules and feature sets granted";
            return features.Count == 1
                ? "1 granted feature entry"
                : $"{features.Count} granted feature entries";
        }

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
