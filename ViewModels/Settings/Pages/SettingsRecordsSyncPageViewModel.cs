using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.RecordsSync;
using ProGlassAutomation.Services.Security;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Settings.Pages
{
    /// <summary>
    /// Settings state for the existing PI-only Records Sync connection.
    /// This page can save credentials and perform an explicit read-only test;
    /// it never publishes records or changes the sync scope.
    /// </summary>
    public sealed class SettingsRecordsSyncPageViewModel : ViewModelBase
    {
        private readonly RecordsSyncService? _service;
        private RecordsSyncSettings _settings;
        private RecordsSyncStatusSnapshot _syncStatus;
        private string _supabaseUrl = string.Empty;
        private string _authEmail = string.Empty;
        private string _apiKeyInput = string.Empty;
        private string _passwordInput = string.Empty;
        private string _connectionMessage =
            "Saved credentials are never displayed. Blank fields reuse saved values only for their bound project/account; if that binding is missing or changed, enter both secrets again.";
        private bool _isBusy;
        private bool _hasSavedApiKey;
        private bool _hasSavedPassword;

        public SettingsRecordsSyncPageViewModel(RecordsSyncService? service)
        {
            _service = service;
            _settings = service?.GetSettingsSnapshot() ?? new RecordsSyncSettings();
            _syncStatus = service?.Status ?? new RecordsSyncStatusSnapshot();
            _supabaseUrl = string.IsNullOrWhiteSpace(_settings.SupabaseUrl)
                ? RecordsSyncService.ExpectedProjectUrl
                : _settings.SupabaseUrl;
            _authEmail = _settings.AuthEmail ?? string.Empty;

            SaveConfigurationCommand = new RelayCommand(
                async _ => await SaveConfigurationAsync(), _ => CanSaveConfiguration);
            TestConnectionCommand = new RelayCommand(
                async _ => await TestConnectionAsync(), _ => CanTestConnection);

            RefreshSavedCredentialState();
            if (_service != null)
                _service.StatusChanged += OnSyncStatusChanged;
        }

        public string SupabaseUrl
        {
            get => _supabaseUrl;
            set
            {
                if (_supabaseUrl == value) return;
                _supabaseUrl = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SavedCredentialSummary));
            }
        }

        public string AuthEmail
        {
            get => _authEmail;
            set
            {
                if (_authEmail == value) return;
                _authEmail = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SavedCredentialSummary));
            }
        }

        /// <summary>New key text is transient. Existing key values are never
        /// loaded back into the editor.</summary>
        public string ApiKeyInput
        {
            get => _apiKeyInput;
            set
            {
                if (_apiKeyInput == value) return;
                _apiKeyInput = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        /// <summary>Transient mirror of the PasswordBox input. It is cleared
        /// after a successful save and never persisted in app settings JSON.</summary>
        public string PasswordInput
        {
            get => _passwordInput;
            set
            {
                if (_passwordInput == value) return;
                _passwordInput = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public bool HasSavedApiKey
        {
            get => _hasSavedApiKey;
            private set
            {
                if (_hasSavedApiKey == value) return;
                _hasSavedApiKey = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SavedCredentialSummary));
            }
        }

        public bool HasSavedPassword
        {
            get => _hasSavedPassword;
            private set
            {
                if (_hasSavedPassword == value) return;
                _hasSavedPassword = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SavedCredentialSummary));
            }
        }

        public string SavedCredentialSummary
        {
            get
            {
                var matchesCurrentScope = _service != null &&
                    RecordsSyncService.CredentialBindingMatches(
                        _service.GetSettingsSnapshot(), SupabaseUrl, AuthEmail);

                if ((HasSavedApiKey || HasSavedPassword) && !matchesCurrentScope)
                    return "Saved credential(s) have no matching project/account binding. Re-enter both here before testing or saving; values from another app are separate.";
                if (HasSavedApiKey && HasSavedPassword)
                    return "Publishable key and account password are saved in this app’s protected vault and associated with the displayed project/account.";
                if (HasSavedApiKey)
                    return "Publishable key is saved. Enter the dedicated account password to finish setup.";
                if (HasSavedPassword)
                    return "Account password is saved. Enter the Supabase publishable/anon key to finish setup.";
                return "No Supabase credentials are saved yet.";
            }
        }

        public string ConnectionMessage
        {
            get => _connectionMessage;
            private set
            {
                if (_connectionMessage == value) return;
                _connectionMessage = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSaveConfiguration));
                OnPropertyChanged(nameof(CanTestConnection));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool CanSaveConfiguration => _service != null && !IsBusy;
        public bool CanTestConnection => !IsBusy;
        public ICommand SaveConfigurationCommand { get; }
        public ICommand TestConnectionCommand { get; }

        public string ScopeSummary =>
            $"PI only · {_settings.SourceFile} / {_settings.SourceWorksheet}";

        public string BackgroundScanStatus => !_settings.Enabled
            ? "Paused — this page does not enable the existing background scan."
            : !RecordsSyncService.IsExpectedProjectUrl(_settings.SupabaseUrl)
                ? "Paused — ProGlass Records Sync must use " + RecordsSyncService.ExpectedProjectUrl + "."
                : !RecordsSyncService.CredentialBindingMatches(
                    _settings, _settings.SupabaseUrl, _settings.AuthEmail)
                    ? "Paused — saved credentials are not bound to the current project/account. Re-enter and save both credentials before background sync can start."
                    : !HasSavedApiKey || !HasSavedPassword
                        ? "Paused — the dedicated publishable/anon key and Auth password must both be saved before background sync can start."
                        : "Enabled under the existing PI-only setting. Saving or testing here does not change or start the worker.";

        public string AtomicPublisherStatus => _service == null
            ? "Records Sync service is unavailable."
            : _syncStatus.AtomicBackendAvailable
                ? "Atomic publisher is available."
                : "Unavailable — this build has no reviewed atomic stage/publish contract.";

        public string PendingStatus =>
            "Atomic publication queue is unavailable; this page does not queue or publish PI rows.";

        public string LastSuccessfulSyncStatus => _syncStatus.LastSuccessfulSyncUtc.HasValue
            ? "Last successful PI sync pass: " +
              _syncStatus.LastSuccessfulSyncUtc.Value.ToLocalTime().ToString("dd MMM yyyy · h:mm tt")
            : "No successful PI sync pass recorded.";

        private async Task SaveConfigurationAsync()
        {
            if (_service == null)
            {
                ConnectionMessage = "The Records Sync service is unavailable; configuration was not saved.";
                return;
            }

            IsBusy = true;
            try
            {
                var url = RecordsSyncService.NormalizeProjectUrl(SupabaseUrl);
                if (!RecordsSyncService.IsExpectedProjectUrl(url))
                    throw new InvalidOperationException(
                        "Use the ProGlass Records Sync project URL: " + RecordsSyncService.ExpectedProjectUrl);

                var email = (AuthEmail ?? string.Empty).Trim();
                if (email.Length == 0)
                    throw new ArgumentException("Enter the dedicated Records Sync account email.");

                var existing = _service.GetSettingsSnapshot();
                var bindingMatches = RecordsSyncService.CredentialBindingMatches(existing, url, email);
                var keyEntered = !string.IsNullOrWhiteSpace(ApiKeyInput);
                var passwordEntered = !string.IsNullOrEmpty(PasswordInput);
                if (!bindingMatches && (!keyEntered || !passwordEntered))
                {
                    throw new InvalidOperationException(
                        "Saved credentials are not bound to this project URL and Auth email. Re-enter both the publishable/anon key and account password in ProGlass before saving; stale vault values will not be reused.");
                }

                var apiKey = ResolveApiKey(ApiKeyInput);
                var password = ResolvePassword(PasswordInput);
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new ArgumentException("Enter the Supabase publishable/anon key.");
                if (string.IsNullOrEmpty(password))
                    throw new ArgumentException("Enter the dedicated Records Sync account password.");

                var updated = new RecordsSyncSettings
                {
                    Enabled = existing.Enabled,
                    SupabaseUrl = url,
                    AuthEmail = email,
                    SourceFile = existing.SourceFile,
                    SourceWorksheet = existing.SourceWorksheet,
                    IntervalMinutes = existing.IntervalMinutes,
                };

                await _service.SaveConfigurationAsync(updated, apiKey, password);
                _settings = _service.GetSettingsSnapshot();
                SupabaseUrl = _settings.SupabaseUrl;
                AuthEmail = _settings.AuthEmail;
                ApiKeyInput = string.Empty;
                PasswordInput = string.Empty;
                RefreshSavedCredentialState();
                RefreshConfigurationProperties();
                ConnectionMessage =
                    "Connection settings saved locally. No PI rows were sent, and the existing background-scan setting was not changed. Saving does not enable or trigger publication; use Test connection for a read-only credential check.";
            }
            catch (Exception ex)
            {
                ConnectionMessage = "Could not save connection settings: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task TestConnectionAsync()
        {
            IsBusy = true;
            var apiKeySource = string.IsNullOrWhiteSpace(ApiKeyInput)
                ? "this app’s saved vault"
                : "entered for this test";
            var passwordSource = string.IsNullOrEmpty(PasswordInput)
                ? "this app’s saved vault"
                : "entered for this test";
            try
            {
                var url = RecordsSyncService.NormalizeProjectUrl(SupabaseUrl);
                if (!RecordsSyncService.IsExpectedProjectUrl(url))
                    throw new InvalidOperationException(
                        "Use the ProGlass Records Sync project URL: " + RecordsSyncService.ExpectedProjectUrl);

                var email = (AuthEmail ?? string.Empty).Trim();
                if (email.Length == 0)
                    throw new ArgumentException("Enter the dedicated Records Sync account email.");

                var savedSettings = _service?.GetSettingsSnapshot();
                var bindingMatches = savedSettings != null &&
                    RecordsSyncService.CredentialBindingMatches(savedSettings, url, email);
                var keyEntered = !string.IsNullOrWhiteSpace(ApiKeyInput);
                var passwordEntered = !string.IsNullOrEmpty(PasswordInput);
                if (!bindingMatches && (!keyEntered || !passwordEntered))
                {
                    throw new InvalidOperationException(
                        "Saved credentials are not bound to this project URL and Auth email. Enter both the publishable/anon key and account password in ProGlass before testing; saved values will not be reused across project/account changes.");
                }

                var apiKey = ResolveApiKey(ApiKeyInput);
                var password = ResolvePassword(PasswordInput);
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new ArgumentException("Enter the Supabase publishable/anon key.");
                if (string.IsNullOrEmpty(password))
                    throw new ArgumentException("Enter the dedicated Records Sync account password.");

                using var client = new SupabaseRecordsClient(url, apiKey, email, password);
                await client.TestReadConnectionAsync();
                ConnectionMessage =
                    "Read-only test succeeded: Supabase authenticated the account and accepted a zero-row PI read. No PI rows were read or written; atomic publication remains unavailable.";
            }
            catch (Exception ex)
            {
                ConnectionMessage = "Connection test failed: " + ex.Message +
                    $" Credential source (values hidden): key={apiKeySource}; password={passwordSource}.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string ResolveApiKey(string entered)
        {
            if (!string.IsNullOrWhiteSpace(entered))
                return entered.Trim();
            return SecretVault.TryRetrieve(RecordsSyncService.SecretKey, out var saved)
                ? saved
                : string.Empty;
        }

        private static string ResolvePassword(string entered)
        {
            if (!string.IsNullOrEmpty(entered))
                return entered;
            return SecretVault.TryRetrieve(RecordsSyncService.SecretPasswordKey, out var saved)
                ? saved
                : string.Empty;
        }

        private void RefreshSavedCredentialState()
        {
            HasSavedApiKey = SecretVault.TryRetrieve(RecordsSyncService.SecretKey, out var key) &&
                             !string.IsNullOrWhiteSpace(key);
            HasSavedPassword = SecretVault.TryRetrieve(RecordsSyncService.SecretPasswordKey, out var password) &&
                               !string.IsNullOrEmpty(password);
        }

        private void OnSyncStatusChanged(RecordsSyncStatusSnapshot status)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() => ApplySyncStatus(status)));
                return;
            }

            ApplySyncStatus(status);
        }

        private void ApplySyncStatus(RecordsSyncStatusSnapshot status)
        {
            _syncStatus = status;
            OnPropertyChanged(nameof(BackgroundScanStatus));
            OnPropertyChanged(nameof(AtomicPublisherStatus));
            OnPropertyChanged(nameof(PendingStatus));
            OnPropertyChanged(nameof(LastSuccessfulSyncStatus));
        }

        private void RefreshConfigurationProperties()
        {
            OnPropertyChanged(nameof(ScopeSummary));
            OnPropertyChanged(nameof(BackgroundScanStatus));
            OnPropertyChanged(nameof(AtomicPublisherStatus));
            OnPropertyChanged(nameof(PendingStatus));
            OnPropertyChanged(nameof(LastSuccessfulSyncStatus));
        }

        /// <summary>Detaches from the long-lived application sync service.</summary>
        public void Detach()
        {
            if (_service != null)
                _service.StatusChanged -= OnSyncStatusChanged;
        }
    }
}
