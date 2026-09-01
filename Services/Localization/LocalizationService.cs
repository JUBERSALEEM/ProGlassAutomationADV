// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Localization/LocalizationService.cs
// PROJECT: ProGlassAutomation
// MODULE: Global language & currency (2026-09-01)
//
// THE SWITCH ENGINE
//   Each language ships as Themes/Languages/{Code}.xaml — a plain
//   ResourceDictionary of <sys:String x:Key="Shell.Title">…</sys:String>.
//   The service merges the ACTIVE dictionary into the application scope
//   (same pattern the theme engine uses for brushes), so every XAML text
//   bound with {DynamicResource Shell.Title} retranslates LIVE the moment
//   the user picks a language — no restart, no view reload.
//
//   • en-US is ALWAYS the fallback: a key missing from the active
//     dictionary (or from the active file entirely) resolves from
//     en-US.xaml, so partial translations never leave a blank label.
//   • RTL languages (Arabic, Urdu) flip the shell FlowDirection via the
//     published "AppFlowDirection" string resource and
//     Window.FlowDirection on the root window.
//   • Language is INDEPENDENT of currency (English UI + INR amounts,
//     Chinese UI + AED amounts, …).
//
//   UI threads only: ApplyLanguage manipulates Application resources.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Logging;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ProGlassAutomation.Services.Localization
{
    public sealed class LocalizationService : INotifyPropertyChanged
    {
        public static LocalizationService Instance { get; } = new();

        private ResourceDictionary? _active;
        private ResourceDictionary? _english;
        private LanguageInfo _current = LanguageCatalog.Primary;
        private bool _userPicked;

        /// <summary>Raised after a language switch completes.</summary>
        public event EventHandler? LanguageChanged;

        private LocalizationService() { }

        public LanguageInfo CurrentLanguage
        {
            get => _current;
            private set
            {
                if (_current.Code == value.Code) return;
                _current = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentLanguageName));
                OnPropertyChanged(nameof(CurrentCulture));
            }
        }

        public string CurrentLanguageName =>
            LanguageCatalog.Display(CurrentLanguage);

        public CultureInfo CurrentCulture =>
            CultureInfo.GetCultureInfo(CurrentLanguage.Code);

        public bool IsRtl => CurrentLanguage.IsRtl;

        /// <summary>App-level config was loaded — apply or suggest.</summary>
        public void Initialize(LocalizationSettings? settings)
        {
            settings ??= new LocalizationSettings();

            string requested = settings.Language ?? LanguageCatalog.Primary.Code;
            if (LanguageCatalog.Find(requested) == null)
                requested = LanguageCatalog.Primary.Code;

            // Location hint (GeoService may not have resolved yet — it is
            // applied again when geo arrives).
            if (settings.AutoSuggestByRegion &&
                LanguageCatalog.Find(requested)?.Code == LanguageCatalog.Primary.Code)
            {
                var suggested = LanguageCatalog.Suggest(settings.LastKnownCountryIso);
                if (suggested.Code != LanguageCatalog.Primary.Code)
                    requested = suggested.Code;
            }

            ApplyLanguage(requested, persist: false);
        }

        /// <summary>Switch the whole app to a shipped language.</summary>
        public void ApplyLanguage(string code, bool persist = true)
        {
            var lang = LanguageCatalog.Find(code) ?? LanguageCatalog.Primary;
            if (lang.Code == _current.Code && _active != null) return;

            if (persist) _userPicked = true;

            try
            {
                var resources = Application.Current?.Resources;
                if (resources == null) return;

                var next = LoadDictionary(lang.Code);
                if (next == null) return;

                // Replace the previously-merged language dictionary.
                if (_active != null && resources.MergedDictionaries.Contains(_active))
                    resources.MergedDictionaries.Remove(_active);

                // English always stays merged as the fallback (last = wins,
                // so the ACTIVE language must be merged AFTER English).
                if (_english == null)
                {
                    _english = LoadDictionary(LanguageCatalog.Primary.Code);
                    if (_english != null)
                        resources.MergedDictionaries.Add(_english);
                }
                resources.MergedDictionaries.Add(next);
                _active = next;

                CurrentLanguage = lang;
                ApplyFlowDirection(resources, lang.IsRtl);

                OnLanguageChanged();

                if (persist && App.Config != null)
                {
                    App.Config.Localization.Language = lang.Code;
                    ConfigService.Save(App.Config);
                }
            }
            catch (Exception ex)
            {
                AppLogService.Error($"Language switch to {code} failed: {ex.Message}", ex,
                    new LogContext { Module = "Localization", Operation = "ApplyLanguage" });
            }
        }

        /// <summary>Translate a key using the active dictionary, falling
        /// back to English. Throws nothing — returns the key itself when
        /// unknown so a missing translation is visible in QA.</summary>
        public string T(string key, string? languageCode = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return "";

            ResourceDictionary? dict = languageCode == null
                ? _active
                : LoadDictionary(languageCode);
            if (dict != null && dict.Contains(key) && dict[key] is string s && s.Length > 0)
                return s;

            if (_english != null && _english.Contains(key) && _english[key] is string es)
                return es;

            return key;
        }

        /// <summary>Indexer for {Binding [Key], Source=…} patterns.</summary>
        public string this[string key] => T(key);

        /// <summary>
        /// Applies the regional language suggestion (India → हिन्दी,
        /// China → 中文, UAE → العربية) when:
        ///   • the user never picked a language manually, AND
        ///   • the auto-suggest setting is on.
        /// Called when GeoService resolves the country (startup cache,
        /// IP lookup or OS region). English remains the primary default —
        /// this only upgrades the SECOND option once, then the user owns
        /// the choice.
        /// </summary>
        public void ApplyRegionalSuggestion(string countryIso)
        {
            if (_userPicked) return;
            if (App.Config?.Localization?.AutoSuggestByRegion != true) return;
            if (string.IsNullOrWhiteSpace(countryIso)) return;

            var suggested = LanguageCatalog.Suggest(countryIso);
            if (suggested.Code == _current.Code) return;

            ApplyLanguage(suggested.Code, persist: true);
            try
            {
                // Remember what geo picked so the switch is visible in
                // settings, not a surprise in the language box.
                if (App.Config != null)
                    ConfigService.Save(App.Config);
            }
            catch { /* best-effort */ }
        }

        /// <summary>True while the current language is the automatically
        /// suggested one (user has not overridden it).</summary>
        public bool IsUserPicked => _userPicked;

        private static ResourceDictionary? LoadDictionary(string code)
        {
            var lang = LanguageCatalog.Find(code) ?? LanguageCatalog.Primary;
            try
            {
                var dict = new ResourceDictionary
                {
                    Source = new Uri($"Themes/Languages/{lang.Code}.xaml",
                        UriKind.Relative)
                };
                return dict;
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Language resource dict {code} failed to load: {ex.Message}",
                    new LogContext { Module = "Localization", Operation = "LoadDictionary" });
                return null;
            }
        }

        private static void ApplyFlowDirection(ResourceDictionary resources, bool isRtl)
        {
            // Publish the direction token so views can bind to it where a
            // FlowDirection inheritance is not enough (e.g. Icon columns).
            resources["AppFlowDirection"] = isRtl ? "RightToLeft" : "LeftToRight";

            if (Application.Current?.MainWindow is Window root)
                root.FlowDirection = isRtl
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight;
        }

        private void OnLanguageChanged()
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
