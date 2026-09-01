// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.AppearanceSection.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings — Appearance page's LANGUAGE & CURRENCY group.
//
// 2026-09-02 (Phase 3.7): state moved OUT of
// Views/Settings/Sections/SettingsAppearanceSection.xaml.cs into this
// partial so the section's view is pure XAML + Loaded/Unloaded glue.
// Behavior preserved exactly from the old code-behind:
//   • Language / currency / phone-country selection applies through the
//     global services (LocalizationService / CurrencyService) so changes
//     go live everywhere, and persists via App.Config + ConfigService.
//   • FxStatusText keeps tracking LanguageChanged / CurrencyChanged /
//     RatesChanged. WPF bindings marshal PropertyChanged to the UI
//     thread, so the old Dispatcher dance is only kept for the direct
//     refresh path (called from service events that may fire off-UI).
//   • ResetToEnglishCommand = old OnResetToEnglish click handler.
//
// Re-entrancy guard: every setter compares by identity key (Code / Iso /
// bool equality) and bails when unchanged, so a service event echoing a
// value back through the binding can never loop Apply* calls.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Settings
{
    // ── Language & currency group (Appearance page) ──────────────────
    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        public IReadOnlyList<LanguageInfo> Languages => LanguageCatalog.All;
        public IReadOnlyList<CurrencyInfo> Currencies => CurrencyCatalog.All;
        public IReadOnlyList<CountryInfo> PhoneCountries => CountryDialCode.All;

        private LanguageInfo? _selectedLanguage;
        private CurrencyInfo? _selectedCurrency;
        private CountryInfo? _phoneCountry;
        private bool _autoSuggestByRegion = true;
        private string _fxStatusText = string.Empty;

        public LanguageInfo? SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (value == null || value.Code == _selectedLanguage?.Code) return;
                _selectedLanguage = value;
                OnPropertyChanged();
                LocalizationService.Instance.ApplyLanguage(value.Code);
            }
        }

        public CurrencyInfo? SelectedCurrency
        {
            get => _selectedCurrency;
            set
            {
                if (value == null || value.Code == _selectedCurrency?.Code) return;
                _selectedCurrency = value;
                OnPropertyChanged();
                CurrencyService.Instance.ApplyCurrency(value.Code);
            }
        }

        public CountryInfo? PhoneCountry
        {
            get => _phoneCountry;
            set
            {
                if (value == null || value.Iso == _phoneCountry?.Iso) return;
                _phoneCountry = value;
                OnPropertyChanged();
                if (App.Config == null) return;
                App.Config.Localization.PhoneCountryIso = value.Iso;
                try { ConfigService.Save(App.Config); } catch { /* config best-effort */ }
            }
        }

        public bool AutoSuggestByRegion
        {
            get => _autoSuggestByRegion;
            set
            {
                if (value == _autoSuggestByRegion) return;
                _autoSuggestByRegion = value;
                OnPropertyChanged();
                if (App.Config == null) return;
                App.Config.Localization.AutoSuggestByRegion = value;
                try { ConfigService.Save(App.Config); } catch { /* config best-effort */ }
            }
        }

        public string FxStatusText
        {
            get => _fxStatusText;
            private set { _fxStatusText = value; OnPropertyChanged(); }
        }

        public ICommand ResetToEnglishCommand { get; } =
            new RelayCommand(_ => ResetToEnglish());

        private void ResetToEnglish()
        {
            LocalizationService.Instance.ApplyLanguage("en-US");
            CurrencyService.Instance.ApplyCurrency("AED");
            InitializeAppearanceSection();
        }

        /// <summary>Called from the section's Loaded (and again whenever
        /// the control is re-added to the tree). Re-syncs the pickers
        /// from current service state and attaches the live FX-status
        /// listeners (detach-first, so repeated loads never stack).</summary>
        public void InitializeAppearanceSection()
        {
            // Theme Studio — draft profile + debounced save timer + commands.
            InitializeThemeStudio();

            // Sync pickers from service state — direct field writes so a
            // plain re-sync never re-applies (avoids redundant saves).
            var lang = LocalizationService.Instance.CurrentLanguage;
            _selectedLanguage = LanguageCatalog.All
                .FirstOrDefault(l => l.Code == lang.Code);
            OnPropertyChanged(nameof(SelectedLanguage));

            var cur = CurrencyService.Instance.DisplayCurrency;
            _selectedCurrency = CurrencyCatalog.All
                .FirstOrDefault(c => c.Code == cur);
            OnPropertyChanged(nameof(SelectedCurrency));

            var iso = App.Config?.Localization?.PhoneCountryIso ?? "AE";
            _phoneCountry = CountryDialCode.All
                .FirstOrDefault(c => c.Iso == iso);
            OnPropertyChanged(nameof(PhoneCountry));

            AutoSuggestByRegion = App.Config?.Localization?.AutoSuggestByRegion != false;

            RefreshAppearanceFxStatus();

            var loc = LocalizationService.Instance;
            loc.LanguageChanged -= OnAppearanceLanguageChanged;
            loc.LanguageChanged += OnAppearanceLanguageChanged;
            var curSvc = CurrencyService.Instance;
            curSvc.CurrencyChanged -= OnAppearanceCurrencyChanged;
            curSvc.CurrencyChanged += OnAppearanceCurrencyChanged;
            curSvc.RatesChanged -= OnAppearanceCurrencyChanged;
            curSvc.RatesChanged += OnAppearanceCurrencyChanged;
        }

        /// <summary>Called from the section's Unloaded and from Dispose.
        /// Detaches the live listeners (idempotent).</summary>
        public void TeardownAppearanceSection()
        {
            var loc = LocalizationService.Instance;
            loc.LanguageChanged -= OnAppearanceLanguageChanged;
            var curSvc = CurrencyService.Instance;
            curSvc.CurrencyChanged -= OnAppearanceCurrencyChanged;
            curSvc.RatesChanged -= OnAppearanceCurrencyChanged;
        }

        private void OnAppearanceLanguageChanged(object? sender, EventArgs e) =>
            RefreshAppearanceFxStatus();

        private void OnAppearanceCurrencyChanged() =>
            RefreshAppearanceFxStatus();

        private void RefreshAppearanceFxStatus()
        {
            // Service events may fire on a worker thread (FX refresh).
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(RefreshAppearanceFxStatusCore);
                return;
            }
            RefreshAppearanceFxStatusCore();
        }

        private void RefreshAppearanceFxStatusCore()
        {
            var cur = CurrencyCatalog.Find(CurrencyService.Instance.DisplayCurrency);
            string code = cur?.Code ?? "AED";
            decimal rate = CurrencyService.Instance.CurrentRate();
            string source = CurrencyService.Instance.RateSource switch
            {
                "live" => LocalizationService.Instance.T("Currency.LiveOnly"),
                "cache" => LocalizationService.Instance.T("Currency.CacheFallback"),
                _ => LocalizationService.Instance.T("Currency.SeedFallback"),
            };
            string hint = LocalizationService.Instance.T("Currency.ConvertHint")
                .Replace("{rate}", rate.ToString("N2", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{currency}", code);

            FxStatusText = $"{hint}  ·  {source}  ·  " +
                           $"{LocalizationService.Instance.T("Currency.LastSync")}: " +
                           $"{CurrencyService.Instance.StatusText()}";
        }
    }
}
