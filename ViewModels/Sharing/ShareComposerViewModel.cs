// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Sharing/ShareComposerViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Sharing Center — share composer (Stage 2)
//
// ONE composer for every shareable document (PI, JO, DN, TI, statements,
// reports, exports). The calling module builds a ShareContext from data it
// already has and opens ShareComposerWindow; this VM resolves FROM/TO,
// renders the message from the template engine, and executes the share
// through DocumentSharingService — no per-module sharing logic anywhere.
//
// UX CONTRACT
// ──────────────
//   • FROM is shown for context (ProGlass identity). The WhatsApp account
//     that actually sends is the device's — the UI says so, honestly.
//   • TO defaults to the smartest resolved contact but is ALWAYS
//     changeable, including a manual fallback. Nothing ever sends without
//     the operator pressing Share.
//   • Providers enable/disable from live validation (no phone → WhatsApp
//     explains why instead of failing after the click).
//   • Outcomes are honest: handoffs report "opened", never "sent", with
//     follow-up guidance. The window stays open so sequential shares
//     (WhatsApp, then Email) work without rebuilding the context.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Sharing;
using ProGlassAutomation.Services.Sharing.Contacts;
using ProGlassAutomation.Services.Sharing.Providers;
using ProGlassAutomation.Services.Sharing.Templates;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Sharing
{
    /// <summary>One TO row in the recipient picker (localized kind label).</summary>
    public sealed class RecipientOption
    {
        public ShareRecipient Recipient { get; }
        public string KindLabel { get; }
        public bool IsManualEntry { get; }

        public RecipientOption(ShareRecipient recipient, string kindLabel,
            bool isManualEntry = false)
        {
            Recipient = recipient;
            KindLabel = kindLabel;
            IsManualEntry = isManualEntry;
        }

        public string DisplayLine =>
            IsManualEntry ? KindLabel : $"{KindLabel} — {Recipient.Name}";

        public string ContactLine =>
            IsManualEntry ? "" : Recipient.ContactLine;
    }

    /// <summary>One provider button (live validation state).</summary>
    public sealed class ShareProviderOption : ViewModelBase
    {
        public ISharingProvider Provider { get; }

        private bool _isAvailable = true;
        public bool IsAvailable
        {
            get => _isAvailable;
            set { _isAvailable = value; OnPropertyChanged(); }
        }

        private string _disabledReason = "";
        public string DisabledReason
        {
            get => _disabledReason;
            set { _disabledReason = value; OnPropertyChanged(); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public ShareProviderOption(ISharingProvider provider)
        {
            Provider = provider;
        }
    }

    public sealed class ShareComposerViewModel : ViewModelBase
    {
        private static string T(string key)
        {
            try { return LocalizationService.Instance.T(key); }
            catch { return key; }
        }

        private readonly ShareContext _context;
        private readonly IDocumentSharingService _sharing;
        private readonly IContactResolver _resolver;
        private CancellationTokenSource? _shareCts;
        private bool _messageEditedByUser;
        private bool _renderingMessage;
        private bool _initialized;

        /// <summary>Wired by the window to DialogResult + Close.</summary>
        public Action<bool?>? RequestClose { get; set; }

        public ShareComposerViewModel(
            ShareContext context,
            IDocumentSharingService? sharing = null,
            IContactResolver? resolver = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _sharing = sharing ?? new DocumentSharingService();
            _resolver = resolver ?? new ContactResolver();

            Artifacts = new ObservableCollection<ShareableArtifact>(
                _context.Artifacts.Where(a => a != null));

            ShareCommand = new RelayCommand(
                async _ => await ShareAsync(),
                _ => CanShare);
            CloseCommand = new RelayCommand(_ =>
            {
                // Closing mid-share must not orphan the provider task:
                // signal cancellation first (best-effort, never blocks).
                try { _shareCts?.Cancel(); }
                catch { /* closing always wins */ }
                RequestClose?.Invoke(HasSharedSuccessfully);
            });
            SelectProviderCommand = new RelayCommand(p =>
            {
                if (p is ShareProviderOption opt && opt.IsAvailable)
                    SelectedProvider = opt;
            });
        }

        // ── Header / document ──────────────────────────────────────

        public string DocumentTitle => _context.DisplayTitle;

        public string CustomerLine =>
            string.IsNullOrWhiteSpace(_context.CustomerName)
                ? "" : _context.CustomerName.Trim();

        public ObservableCollection<ShareableArtifact> Artifacts { get; }

        public bool HasArtifacts => Artifacts.Count > 0;

        // ── FROM ───────────────────────────────────────────────────

        private ShareSender _sender =
            new ShareSender("ProGlass");
        public ShareSender Sender
        {
            get => _sender;
            private set { _sender = value; OnPropertyChanged(); }
        }

        public string SenderLine => Sender.DisplayLine;

        public string SenderContactLine
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Sender.Mobile))
                    return Sender.Mobile;
                if (!string.IsNullOrWhiteSpace(Sender.Email))
                    return Sender.Email;
                return T("Sharing.FromNameAndRoleOnly");
            }
        }

        // ── TO ─────────────────────────────────────────────────────

        public ObservableCollection<RecipientOption> RecipientOptions { get; } =
            new();

        private RecipientOption? _selectedRecipient;
        public RecipientOption? SelectedRecipient
        {
            get => _selectedRecipient;
            set
            {
                _selectedRecipient = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsManualRecipient));
                OnPropertyChanged(nameof(CanShare));
                RefreshProviderAvailability();
                RenderMessageIfPristine();
            }
        }

        public bool IsManualRecipient =>
            SelectedRecipient != null && SelectedRecipient.IsManualEntry;

        // Manual fallback editor (explicit user choice).
        private string _manualName = "";
        public string ManualName
        {
            get => _manualName;
            set { _manualName = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanShare)); RefreshProviderAvailability(); }
        }

        private string _manualPhone = "";
        public string ManualPhone
        {
            get => _manualPhone;
            set { _manualPhone = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanShare)); RefreshProviderAvailability(); }
        }

        private string _manualEmail = "";
        public string ManualEmail
        {
            get => _manualEmail;
            set { _manualEmail = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanShare)); RefreshProviderAvailability(); }
        }

        /// <summary>Effective TO (resolved or manual) for execution.</summary>
        public ShareRecipient? EffectiveRecipient
        {
            get
            {
                if (SelectedRecipient == null) return null;
                if (!SelectedRecipient.IsManualEntry)
                    return SelectedRecipient.Recipient;
                if (string.IsNullOrWhiteSpace(ManualName)
                    && string.IsNullOrWhiteSpace(ManualPhone)
                    && string.IsNullOrWhiteSpace(ManualEmail))
                    return null;
                return new ShareRecipient(
                    ShareRecipientKind.Manual,
                    string.IsNullOrWhiteSpace(ManualName) ? T("Sharing.ManualContact") : ManualName.Trim(),
                    _context.CustomerName ?? "",
                    ManualPhone.Trim(), ManualEmail.Trim());
            }
        }

        // ── Message / subject ──────────────────────────────────────

        private string _message = "";
        public string Message
        {
            get => _message;
            set
            {
                _message = value ?? "";
                OnPropertyChanged();
                if (!_renderingMessage)
                    _messageEditedByUser = true;
            }
        }

        public bool AllowMessageEdit
        {
            get
            {
                try
                {
                    return App.Config?.Sharing
                        ?.RuleFor(_context.DocumentTypeKey)
                        ?.AllowMessageEdit ?? true;
                }
                catch { return true; }
            }
        }

        private string _subject = "";
        public string Subject
        {
            get => _subject;
            set { _subject = value ?? ""; OnPropertyChanged(); }
        }

        public bool SubjectVisible =>
            SelectedProvider?.Provider.Capabilities.SupportsSubject == true;

        // ── Providers ──────────────────────────────────────────────

        public ObservableCollection<ShareProviderOption> ProviderOptions { get; } =
            new();

        private ShareProviderOption? _selectedProvider;
        public ShareProviderOption? SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                _selectedProvider = value;
                foreach (var opt in ProviderOptions)
                    opt.IsSelected = opt == value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubjectVisible));
                OnPropertyChanged(nameof(CanShare));
                RenderMessageIfPristine();
            }
        }

        // ── Status / outcome ───────────────────────────────────────

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanShare)); OnPropertyChanged(nameof(IsNotBusy)); }
        }

        /// <summary>Inverse of IsBusy for control enablement (no converter needed).</summary>
        public bool IsNotBusy => !_isBusy;

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        private ShareOutcome? _lastOutcome;
        public ShareOutcome? LastOutcome
        {
            get => _lastOutcome;
            private set
            {
                _lastOutcome = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasOutcome));
                OnPropertyChanged(nameof(OutcomeIsSuccess));
            }
        }

        public bool HasOutcome => LastOutcome != null;

        public bool OutcomeIsSuccess => LastOutcome?.IsSuccess == true;

        public bool HasSharedSuccessfully { get; private set; }

        public bool CanShare =>
            !IsBusy && HasArtifacts && SelectedProvider != null
            && SelectedProvider.IsAvailable && EffectiveRecipientValidForProvider();

        private bool EffectiveRecipientValidForProvider()
        {
            var provider = SelectedProvider?.Provider;
            if (provider == null) return false;
            if (!provider.Capabilities.RequiresRecipient) return true;
            return EffectiveRecipient != null;
        }

        public ICommand ShareCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand SelectProviderCommand { get; }

        // ── Init ───────────────────────────────────────────────────

        /// <summary>
        /// Resolves sender/recipients, picks smart defaults, renders the
        /// message. Called once by the window on load.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized) return;
            _initialized = true;
            IsBusy = true;
            StatusMessage = T("Sharing.Preparing");

            try
            {
                Sender = _resolver.ResolveSender(_context);
                OnPropertyChanged(nameof(SenderLine));
                OnPropertyChanged(nameof(SenderContactLine));

                var recipients = await _resolver
                    .ResolveRecipientsAsync(_context)
                    .ConfigureAwait(true);

                RecipientOptions.Clear();
                foreach (var recipient in recipients)
                    RecipientOptions.Add(new RecipientOption(
                        recipient, KindLabel(recipient.Kind)));

                // Manual fallback is always offered, last.
                RecipientOptions.Add(new RecipientOption(
                    new ShareRecipient(ShareRecipientKind.Manual, ""),
                    T("Sharing.OtherContact"), isManualEntry: true);

                BuildProviderOptions();
                SelectSmartDefaults();
                RenderMessageIfPristine(force: true);

                Subject = _context.DisplayTitle ?? "";
                _sharing.LogComposerOpened(_context);
                StatusMessage = "";
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Sharing.Composer] init failed: {ex.Message}");
                StatusMessage = T("Sharing.PrepareFailed");
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(CanShare));
            }
        }

        private void BuildProviderOptions()
        {
            ProviderOptions.Clear();
            string preferred = DefaultProviderId();
            ShareProviderOption? first = null;
            ShareProviderOption? match = null;

            foreach (var provider in _sharing.EnabledProviders)
            {
                var opt = new ShareProviderOption(provider);
                ProviderOptions.Add(opt);
                first ??= opt;
                if (string.Equals(provider.Id, preferred,
                        StringComparison.OrdinalIgnoreCase))
                    match = opt;
            }

            SelectedProvider = match ?? first;
        }

        private string DefaultProviderId()
        {
            try
            {
                string rule = App.Config?.Sharing
                    ?.RuleFor(_context.DocumentTypeKey)
                    ?.DefaultProviderId ?? "";
                if (!string.IsNullOrWhiteSpace(rule)) return rule.Trim();
                return App.Config?.Sharing?.DefaultProviderId ?? "WHATSAPP";
            }
            catch { return "WHATSAPP"; }
        }

        private void SelectSmartDefaults()
        {
            if (RecipientOptions.Count == 0) return;

            // Document rule's preferred recipient kind first…
            string wantKind = "";
            try
            {
                wantKind = App.Config?.Sharing
                    ?.RuleFor(_context.DocumentTypeKey)
                    ?.DefaultRecipientKind ?? "";
            }
            catch { wantKind = ""; }

            RecipientOption? pick = null;
            if (Enum.TryParse<ShareRecipientKind>(wantKind, true, out var kind)
                && kind != ShareRecipientKind.Manual)
            {
                pick = RecipientOptions.FirstOrDefault(o =>
                    !o.IsManualEntry && o.Recipient.Kind == kind
                    && SatisfiesSelectedProvider(o.Recipient));
                pick ??= RecipientOptions.FirstOrDefault(o =>
                    !o.IsManualEntry && o.Recipient.Kind == kind);
            }

            // …then the first candidate that satisfies the provider…
            pick ??= RecipientOptions.FirstOrDefault(o =>
                !o.IsManualEntry && SatisfiesSelectedProvider(o.Recipient));

            // …then the first resolved candidate…
            pick ??= RecipientOptions.FirstOrDefault(o => !o.IsManualEntry);

            // …else manual (the ERP knows nobody for this document).
            SelectedRecipient = pick
                ?? RecipientOptions.FirstOrDefault(o => o.IsManualEntry);
        }

        private bool SatisfiesSelectedProvider(ShareRecipient recipient)
        {
            var caps = SelectedProvider?.Provider.Capabilities;
            if (caps == null || !caps.RequiresRecipient) return true;
            if (caps.RequiresPhone && !recipient.HasPhone) return false;
            if (caps.RequiresEmail && !recipient.HasEmail) return false;
            return true;
        }

        private void RenderMessageIfPristine(bool force = false)
        {
            if (!force && _messageEditedByUser) return;
            if (_context == null) return;

            try
            {
                string providerId = SelectedProvider?.Provider.Id ?? "";
                IDictionary<string, string>? overrides = null;
                try { overrides = App.Config?.Sharing?.TemplateOverrides; }
                catch { overrides = null; }

                string template = ShareTemplateService.ResolveTemplateText(
                    _context.DocumentTypeKey, providerId, overrides);
                var sender = new ShareSenderInfo(Sender.Name, Sender.Role);

                _renderingMessage = true;
                Message = ShareTemplateService.Render(
                    template, _context, EffectiveRecipient, sender);
                if (force) _messageEditedByUser = false;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Sharing.Composer] render failed: {ex.Message}");
            }
            finally
            {
                _renderingMessage = false;
            }
        }

        private void RefreshProviderAvailability()
        {
            if (ProviderOptions.Count == 0) return;

            foreach (var opt in ProviderOptions)
            {
                try
                {
                    var request = new ShareExecutionRequest(
                        _context, _context.Artifacts,
                        EffectiveRecipient, Message ?? "", Subject ?? "",
                        CancellationToken.None);
                    string reason = opt.Provider.Validate(request);
                    opt.IsAvailable = reason.Length == 0;
                    opt.DisabledReason = reason;
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[Sharing.Composer] validate failed: {ex.Message}");
                    opt.IsAvailable = false;
                    opt.DisabledReason = T("Sharing.NotAvailable");
                }
            }

            // Keep the selection on an available provider.
            if (SelectedProvider != null && !SelectedProvider.IsAvailable)
            {
                SelectedProvider =
                    ProviderOptions.FirstOrDefault(o => o.IsAvailable)
                    ?? SelectedProvider;
            }
            OnPropertyChanged(nameof(CanShare));
        }

        // ── Share ──────────────────────────────────────────────────

        private async Task ShareAsync()
        {
            var provider = SelectedProvider?.Provider;
            if (provider == null || IsBusy) return;

            // Final gate (buttons can be stale for a keystroke).
            var preflight = new ShareExecutionRequest(
                _context, _context.Artifacts, EffectiveRecipient,
                Message ?? "", Subject ?? "", CancellationToken.None);
            string invalid = "";
            try { invalid = provider.Validate(preflight); } catch { invalid = ""; }
            if (invalid.Length > 0)
            {
                StatusMessage = invalid;
                RefreshProviderAvailability();
                return;
            }

            IsBusy = true;
            StatusMessage = T("Sharing.Sending");
            LastOutcome = null;
            _shareCts?.Dispose();
            _shareCts = new CancellationTokenSource();

            try
            {
                var outcome = await _sharing.ShareAsync(
                    provider.Id, _context, _context.Artifacts,
                    EffectiveRecipient, Message ?? "", Subject ?? "",
                    _shareCts.Token);

                LastOutcome = outcome;
                if (outcome.IsSuccess)
                    HasSharedSuccessfully = true;
                StatusMessage = outcome.Status == ShareOutcomeStatus.Failed
                    ? outcome.ErrorMessage
                    : outcome.Message;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Sharing.Composer] share failed: {ex.Message}");
                StatusMessage = T("Sharing.ShareFailed");
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(CanShare));
            }
        }

        private static string KindLabel(ShareRecipientKind kind) => kind switch
        {
            ShareRecipientKind.CustomerAttention => T("Sharing.Recipient.Attention"),
            ShareRecipientKind.Customer => T("Sharing.Recipient.Customer"),
            ShareRecipientKind.CompanyRepresentative => T("Sharing.Recipient.CompanyRep"),
            ShareRecipientKind.Salesman => T("Sharing.Recipient.Salesman"),
            _ => T("Sharing.Recipient.Other")
        };
    }
}
