// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/CountryPhoneControl.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Global locale (2026-09-01) — phone input with country code
//
//   🇦🇪 +971 │ 50 123 4567      (detected from IP → editable)
//
// CONTRACT
//   • PhoneText (the bound value) is the FULL number WITH the dial code,
//     e.g. "+971-501234567" — that is what customers see and what old
//     versions stored, so existing data stays valid.
//   • Values without a "+code" prefix (legacy data) are preserved
//     untouched until the user edits them; the edit then prefixes the
//     selected country code.
//   • The default country comes from GeoService (IP → cache → OS region)
//     and can be switched per record at any time.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Localization;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Shared
{
    public partial class CountryPhoneControl : UserControl
    {
        public static readonly DependencyProperty PhoneTextProperty =
            DependencyProperty.Register(
                nameof(PhoneText), typeof(string), typeof(CountryPhoneControl),
                new FrameworkPropertyMetadata(
                    "",
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnPhoneTextPropertyChanged));

        /// <summary>Fallback ISO when nothing is cached/detected yet.</summary>
        public static readonly DependencyProperty DefaultCountryIsoProperty =
            DependencyProperty.Register(
                nameof(DefaultCountryIso), typeof(string), typeof(CountryPhoneControl),
                new PropertyMetadata("", OnDefaultCountryPropertyChanged));

        public string PhoneText
        {
            get => (string)GetValue(PhoneTextProperty);
            set => SetValue(PhoneTextProperty, value);
        }

        public string DefaultCountryIso
        {
            get => (string)GetValue(DefaultCountryIsoProperty);
            set => SetValue(DefaultCountryIsoProperty, value);
        }

        private bool _updatingText;      // programmatic TextBox writes
        private bool _userTouchedCountry;

        private static readonly Regex PrefixPattern =
            new(@"^\s*\+?(\d{1,4})[\s\-–_]*(.*)$", RegexOptions.Compiled);

        public CountryPhoneControl()
        {
            InitializeComponent();
            CountryBox.ItemsSource = CountryDialCode.All;

            CountryBox.SelectionChanged += OnCountrySelectionChanged;
            NumberBox.TextChanged += OnNumberTextChanged;
            NumberBox.PreviewTextInput += OnNumberPreviewInput;
            NumberBox.PreviewKeyDown += OnNumberPreviewKeyDown;
            DataObject.AddPastingHandler(NumberBox, OnNumberPaste);

            Loaded += (_, _) =>
            {
                SelectDefaultCountry();
                SyncFromPhoneText(PhoneText ?? "");
            };

            // Geo may resolve AFTER this control loads — adopt the
            // detected country unless the user already picked one.
            GeoService.Instance.LocationResolved += _ =>
            {
                if (!_userTouchedCountry)
                    Dispatcher.Invoke(SelectDefaultCountry);
            };
        }

        // ── Public helpers ──────────────────────────────────────────────

        /// <summary>Full number with the active dial code
        /// ("+971-501234567") — same as PhoneText, but recomposed even
        /// when the caller never edited the number, so saves always get
        /// the detected country code.</summary>
        public string ComposeFullNumber()
        {
            string local = (NumberBox.Text ?? "").Trim();
            if (local.Length == 0) return "";
            if (local.StartsWith("+")) return local;
            var country = SelectedCountry ?? CountryDialCode.Find("AE");
            return $"{country.DialCode}-{local}";
        }

        private CountryInfo? SelectedCountry => CountryBox.SelectedItem as CountryInfo;

        // ── DP callbacks ────────────────────────────────────────────────

        private static void OnPhoneTextPropertyChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CountryPhoneControl)d).SyncFromPhoneText(e.NewValue as string ?? "");
        }

        private static void OnDefaultCountryPropertyChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CountryPhoneControl)d).SelectDefaultCountry();
        }

        // ── Sync engine ─────────────────────────────────────────────────

        private void SyncFromPhoneText(string text)
        {
            _updatingText = true;
            try
            {
                var (country, local) = Split(text);
                if (country != null)
                {
                    if (CountryBox.SelectedItem as CountryInfo != country)
                        CountryBox.SelectedItem = country;
                    NumberBox.Text = local;
                }
                else
                {
                    NumberBox.Text = text; // legacy/unprefixed — keep as-is
                }
            }
            finally
            {
                _updatingText = false;
            }
        }

        private static (CountryInfo? country, string local) Split(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return (null, "");

            var m = PrefixPattern.Match(text);
            if (m.Success && m.Groups[1].Value.Length > 1)
            {
                var country = CountryDialCode.FindByDial("+" + m.Groups[1].Value);
                if (country != null)
                    return (country, m.Groups[2].Value.Trim());
            }
            if (text.StartsWith("+"))
            {
                var country = CountryDialCode.FindByDial(text);
                if (country != null)
                    return (country, "");
            }
            return (null, text);
        }

        private void SelectDefaultCountry()
        {
            if (_userTouchedCountry || CountryBox.SelectedItem != null) return;

            string iso =
                App.Config?.Localization?.PhoneCountryIso
                ?? GeoService.Instance.Current?.CountryIso
                ?? DefaultCountryIso
                ?? "";
            var country = CountryDialCode.Find(iso)
                          ?? CountryDialCode.Find(GeoService.Instance.Current?.CountryIso)
                          ?? CountryDialCode.Find("AE");
            if (country != null)
                CountryBox.SelectedItem = country;
        }

        // ── User edits ──────────────────────────────────────────────────

        private void OnCountrySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _userTouchedCountry = true;
            if (_updatingText || NumberBox == null) return;
            Recompose();
        }

        private void OnNumberTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingText) return;
            Recompose();
        }

        private void Recompose()
        {
            string local = (NumberBox.Text ?? "").Trim();
            string composed = local.Length == 0 ? "" : ComposeFullNumber();
            if (PhoneText == composed) return;
            SetCurrentValue(PhoneTextProperty, composed);
        }

        // Digits only (allow space, hyphen, parentheses, plus)
        private static void OnNumberPreviewInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
                if (!char.IsDigit(c) && c != ' ' && c != '-' && c != '(' && c != ')' && c != '+')
                {
                    e.Handled = true;
                    return;
                }
        }

        private static void OnNumberPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) == 0)
                e.Handled = true; // keep +971- numbers untouched by spaces
        }

        private static void OnNumberPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.UnicodeText) &&
                e.DataObject.GetData(DataFormats.UnicodeText) is string text)
            {
                foreach (char c in text)
                    if (!char.IsDigit(c) && c != '+' && c != '-' && c != ' ' &&
                        c != '(' && c != ')')
                    {
                        e.CancelCommand();
                        return;
                    }
            }
        }
    }
}
