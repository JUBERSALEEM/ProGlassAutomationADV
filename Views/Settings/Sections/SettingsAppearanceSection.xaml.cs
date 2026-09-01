// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/SettingsAppearanceSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v2 — Appearance page (theme, wallpaper, accent)
// Pure presentation — all state lives in SettingsViewModel.
//
// 2026-09-01: the LANGUAGE & CURRENCY group lives on this page too.
// It drives the global services directly (LocalizationService /
// CurrencyService / GeoService) so switching applies live everywhere.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Localization;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings.Sections
{
    public partial class SettingsAppearanceSection : UserControl
    {
        private bool _syncing;

        public SettingsAppearanceSection()
        {
            InitializeComponent();
            Loaded += (_, _) => RefreshLocaleControls();
        }

        private void RefreshLocaleControls()
        {
            _syncing = true;
            try
            {
                LanguageCombo.ItemsSource = LanguageCatalog.All;
                var lang = LocalizationService.Instance.CurrentLanguage;
                foreach (LanguageInfo item in LanguageCombo.Items)
                    if (item.Code == lang.Code)
                        LanguageCombo.SelectedItem = item;

                CurrencyCombo.ItemsSource = CurrencyCatalog.All;
                var cur = CurrencyService.Instance.DisplayCurrency;
                foreach (CurrencyInfo item in CurrencyCombo.Items)
                    if (item.Code == cur)
                        CurrencyCombo.SelectedItem = item;

                PhoneCountryCombo.ItemsSource = CountryDialCode.All;
                string iso = App.Config?.Localization?.PhoneCountryIso ?? "AE";
                foreach (CountryInfo item in PhoneCountryCombo.Items)
                    if (item.Iso == iso)
                        PhoneCountryCombo.SelectedItem = item;

                AutoSuggestCheck.IsChecked =
                    App.Config?.Localization?.AutoSuggestByRegion != false;

                RefreshFxStatus();

                LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
                LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
                CurrencyService.Instance.CurrencyChanged -= OnCurrencyChanged;
                CurrencyService.Instance.CurrencyChanged += OnCurrencyChanged;
                CurrencyService.Instance.RatesChanged -= OnCurrencyChanged;
                CurrencyService.Instance.RatesChanged += OnCurrencyChanged;
            }
            finally
            {
                _syncing = false;
            }
        }

        private void OnLanguageChanged(object? sender, EventArgs e) =>
            Dispatcher.Invoke(RefreshFxStatus);

        private void OnCurrencyChanged()
        {
            if (Dispatcher.CheckAccess())
                RefreshFxStatus();
            else
                Dispatcher.Invoke(RefreshFxStatus);
        }

        private void RefreshFxStatus()
        {
            if (FxStatusText == null) return;

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

            FxStatusText.Text = $"{hint}  ·  {source}  ·  " +
                                $"{LocalizationService.Instance.T("Currency.LastSync")}: " +
                                $"{CurrencyService.Instance.StatusText()}";
        }

        private void OnLanguageSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_syncing || LanguageCombo.SelectedItem is not LanguageInfo lang) return;
            LocalizationService.Instance.ApplyLanguage(lang.Code);
        }

        private void OnCurrencySelected(object sender, SelectionChangedEventArgs e)
        {
            if (_syncing || CurrencyCombo.SelectedItem is not CurrencyInfo cur) return;
            CurrencyService.Instance.ApplyCurrency(cur.Code);
        }

        private void OnPhoneCountrySelected(object sender, SelectionChangedEventArgs e)
        {
            if (_syncing || PhoneCountryCombo.SelectedItem is not CountryInfo info) return;
            if (App.Config == null) return;
            App.Config.Localization.PhoneCountryIso = info.Iso;
            try { ConfigService.Save(App.Config); } catch { /* config best-effort */ }
        }

        private void OnAutoSuggestToggled(object sender, RoutedEventArgs e)
        {
            if (_syncing || App.Config == null) return;
            App.Config.Localization.AutoSuggestByRegion =
                AutoSuggestCheck.IsChecked == true;
            try { ConfigService.Save(App.Config); } catch { /* config best-effort */ }
        }

        private void OnResetToEnglish(object sender, RoutedEventArgs e)
        {
            LocalizationService.Instance.ApplyLanguage("en-US");
            CurrencyService.Instance.ApplyCurrency("AED");
            RefreshLocaleControls();
        }
    }
}
