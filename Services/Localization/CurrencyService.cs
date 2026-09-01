// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Localization/CurrencyService.cs
// PROJECT: ProGlassAutomation
// MODULE: Global currency (2026-09-01) — LIVE rate conversion
//
// CONTRACT
//   • The company's BOOKS stay in the base currency (AED) — accounting
//     integrity is never risked by display conversion. CurrencyService
//     converts AT DISPLAY / EXPORT / PRINT time only.
//   • Rates come from the internet when available
//     (https://open.er-api.com/v6/latest/AED — free, no key, refreshed
//     daily). Every successful fetch is cached to
//     %AppData%\ProGlassAutomation\fx-cache.json WITH ITS TIMESTAMP.
//   • Offline (or fetch failure) → the LAST FETCHED cache is used.
//   • First run with no cache → ShipRates seed table (approximate,
//     labelled Seed) so the switch still works — the status line tells
//     the user the rate is a seeded approximation until a fetch succeeds.
//   • The UI is NEVER blocked: RefreshAsync is fire-and-forget and
//     Convert() is a pure in-memory lookup.
//
// EXAMPLE (user's own words):
//   PI amount 1 AED → switch language/currency to India → ₹26.xx INR at
//   the current internet rate; with no internet the last fetched rate
//   (or seeded fallback) is used.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation.Services.Localization
{
    public sealed class CurrencyService : System.ComponentModel.INotifyPropertyChanged
    {
        public static CurrencyService Instance { get; } = new();

        /// <summary>The currency amounts are DISPLAYED in (ISO 4217).</summary>
        public string DisplayCurrency { get; private set; } = "AED";

        /// <summary>Base currency of the books (always AED).</summary>
        public const string BaseCurrency = "AED";

        /// <summary>True when DisplayCurrency == BaseCurrency (no conversion).</summary>
        public bool IsBase => string.Equals(DisplayCurrency, BaseCurrency,
            StringComparison.OrdinalIgnoreCase);

        private DateTime _ratesFetchedUtc;
        private string _rateSource = "seed";

        /// <summary>Timestamp of the last SUCCESSFUL internet fetch.</summary>
        public DateTime RatesFetchedUtc => _ratesFetchedUtc;

        /// <summary>seed | cache | live — shown to the user on the page.</summary>
        public string RateSource => _rateSource;

        public bool HasLiveRates => _rateSource == "live";

        private readonly Dictionary<string, decimal> _baseToTarget = new(StringComparer.OrdinalIgnoreCase);

        private static readonly string CachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "fx-cache.json");

        /// <summary>Raised on the UI thread when rates or currency change.</summary>
        public event Action? RatesChanged;
        public event Action? CurrencyChanged;

        private CurrencyService() { }

        /// <summary>Loads cache + applies the persisted display currency.
        /// Called once at startup — never blocks (cache read only).</summary>
        public void Initialize()
        {
            LoadCache();
            ApplyCurrency(App.Config?.Localization?.CurrencyCode ?? "AED",
                persist: false);

            // One-time geo suggestion (India → INR, China → CNY…):
            // applies ONLY when the user never picked a currency. A
            // manual pick (settings) is persisted and always wins.
            GeoService.Instance.LocationResolved += OnGeoLocation;
        }

        private void OnGeoLocation(GeoResult geo)
        {
            if (App.Config?.Localization == null) return;
            if (App.Config.Localization.CurrencyCode.Length > 0) return; // user picked
            if (!App.Config.Localization.AutoSuggestByRegion) return;

            var suggested = CurrencyCatalog.Find(geo.CurrencyCode);
            if (suggested != null && !string.Equals(suggested.Code, DisplayCurrency,
                    StringComparison.OrdinalIgnoreCase))
                ApplyCurrency(suggested.Code);
        }

        /// <summary>Refresh from the internet, then notify. Call with
        /// `_ =` (fire-and-forget); never throws — failures fall back to
        /// cache/seed.</summary>
        public async Task RefreshAsync()
        {
            try
            {
                var rates = await FetchLiveRatesAsync().ConfigureAwait(false);
                if (rates != null && rates.Count > 0)
                {
                    _baseToTarget.Clear();
                    foreach (var kv in rates)
                        _baseToTarget[kv.Key] = kv.Value;
                    _baseToTarget[BaseCurrency] = 1m;
                    _ratesFetchedUtc = DateTime.UtcNow;
                    _rateSource = "live";
                    WriteCache();
                    NotifyRates();
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"Currency refresh failed: {ex.Message}",
                    new LogContext { Module = "Localization", Operation = "FxRefresh" });
            }
        }

        /// <summary>Applies the display currency (pinned or geo-suggested).
        /// No conversion happens here — conversion is per amount.</summary>
        public void ApplyCurrency(string code, bool persist = true)
        {
            var info = CurrencyCatalog.Find(code) ?? CurrencyCatalog.Find(BaseCurrency)!;
            if (string.Equals(DisplayCurrency, info.Code, StringComparison.OrdinalIgnoreCase))
                return;

            DisplayCurrency = info.Code;
            OnPropertyChanged(nameof(DisplayCurrency));
            OnPropertyChanged(nameof(IsBase));
            CurrencyChanged?.Invoke();
            RefreshAmountBindings();

            if (persist && App.Config != null)
            {
                App.Config.Localization.CurrencyCode = info.Code;
                try { ConfigService.Save(App.Config); } catch { /* config best-effort */ }
            }
        }

        /// <summary>Convert an amount from the BASE currency into the
        /// DISPLAY currency (pure, in-memory, never throws).</summary>
        public decimal Convert(decimal baseAmount)
        {
            if (IsBase) return baseAmount;
            if (!_baseToTarget.TryGetValue(DisplayCurrency, out var rate))
            {
                if (_baseToTarget.Count == 0) SeedRates();
                if (!_baseToTarget.TryGetValue(DisplayCurrency, out rate))
                    return baseAmount; // unknown currency → identity
            }
            return decimal.Round(baseAmount * rate, 4);
        }

        /// <summary>Format an amount for display in the active display
        /// currency, e.g. "AED 12,500.00", "₹ 26.00", "¥ 1,250".</summary>
        public string Format(decimal baseAmount, bool includeCode = true)
        {
            var info = CurrencyCatalog.Find(DisplayCurrency)
                       ?? CurrencyCatalog.Find(BaseCurrency)!;
            decimal value = Convert(baseAmount);
            string digits = value.ToString($"N{info.Decimals}",
                System.Globalization.CultureInfo.InvariantCulture);

            string symbol = info.Symbol;
            if (includeCode && string.IsNullOrWhiteSpace(symbol) is false && info.Code == "AED")
                symbol = "AED"; // the books' base reads as code for clarity
            return info.SymbolFirst
                ? $"{symbol} {digits}"
                : $"{digits} {symbol}";
        }

        /// <summary>Rate used for the current display currency (1 BASE = X
        /// display). 0 = no rate available.</summary>
        public decimal CurrentRate()
        {
            if (IsBase) return 1m;
            if (_baseToTarget.Count == 0) SeedRates();
            return _baseToTarget.TryGetValue(DisplayCurrency, out var r) ? r : 0m;
        }

        private void SeedRates()
        {
            foreach (var kv in SeededFxRates.Table)
                _baseToTarget[kv.Key] = kv.Value;
            _baseToTarget[BaseCurrency] = 1m;
            if (_ratesFetchedUtc == default)
                _rateSource = "seed";
        }

        private static async Task<Dictionary<string, decimal>?> FetchLiveRatesAsync()
        {
            try
            {
                using var http = new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };
                using var resp = await http
                    .GetAsync($"https://open.er-api.com/v6/latest/{BaseCurrency}")
                    .ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return null;
                using var json = await resp.Content.ReadAsStreamAsync()
                    .ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(json).ConfigureAwait(false);
                if (!doc.RootElement.TryGetProperty("rates", out var rates))
                    return null;
                var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in rates.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number &&
                        prop.Value.TryGetDecimal(out var d) && d > 0)
                        map[prop.Name] = d;
                }
                return map;
            }
            catch
            {
                return null; // offline / blocked → caller uses cache
            }
        }

        private void LoadCache()
        {
            try
            {
                if (!File.Exists(CachePath)) { SeedRates(); return; }
                var json = File.ReadAllText(CachePath);
                var cache = JsonSerializer.Deserialize<FxCache>(json);
                if (cache?.Rates == null || cache.Rates.Count == 0) { SeedRates(); return; }

                _baseToTarget.Clear();
                foreach (var kv in cache.Rates)
                    _baseToTarget[kv.Key] = kv.Value;
                _ratesFetchedUtc = cache.FetchedUtc;
                _rateSource = cache.FetchedUtc == default ? "seed" : "cache";
            }
            catch
            {
                SeedRates();
            }
        }

        private void WriteCache()
        {
            try
            {
                var dir = Path.GetDirectoryName(CachePath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(CachePath, JsonSerializer.Serialize(
                    new FxCache
                    {
                        FetchedUtc = _ratesFetchedUtc,
                        Rates = new Dictionary<string, decimal>(_baseToTarget)
                    },
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* cache best-effort */ }
        }

        private void NotifyRates()
        {
            if (Application.Current?.Dispatcher != null)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(RatesFetchedUtc));
                    OnPropertyChanged(nameof(RateSource));
                    OnPropertyChanged(nameof(HasLiveRates));
                    RatesChanged?.Invoke();
                    RefreshAmountBindings();
                });
            else
            {
                RatesChanged?.Invoke();
                RefreshAmountBindings();
            }
        }

        /// <summary>
        /// Re-evaluates every visible binding after a currency switch so
        /// amounts rendered through CurrencyDisplayConverter repaint
        /// IMMEDIATELY (₹ → ¥ → €) without navigating or restarting.
        /// Bounded walk of the active window; failures are swallowed —
        /// the next navigation refreshes anyway.
        /// </summary>
        private static void RefreshAmountBindings()
        {
            try
            {
                if (Application.Current?.MainWindow is not { } root ||
                    root.Content is not System.Windows.UIElement ui) return;
                RefreshElement(ui, 0);
            }
            catch
            {
                // best-effort live refresh — never break the switch
            }
        }

        private static void RefreshElement(System.Windows.DependencyObject element, int depth)
        {
            if (depth > 12) return;

            if (element is System.Windows.Controls.TextBlock tb)
                RefreshProperty(tb, System.Windows.Controls.TextBlock.TextProperty,
                    System.Windows.Controls.TextBlock.TextProperty);
            else if (element is System.Windows.Controls.ContentControl cc)
                RefreshProperty(cc, System.Windows.Controls.ContentControl.ContentProperty,
                    System.Windows.Controls.ContentControl.ContentProperty);
            else if (element is System.Windows.Controls.DataGridCell cell)
                RefreshProperty(cell, System.Windows.Controls.DataGridCell.ContentProperty,
                    System.Windows.Controls.DataGridCell.ContentProperty);

            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
                RefreshElement(System.Windows.Media.VisualTreeHelper.GetChild(element, i), depth + 1);
        }

        private static void RefreshProperty(
            System.Windows.DependencyObject target,
            System.Windows.DependencyProperty dp,
            System.Windows.DependencyProperty expressionFor)
        {
            var be = System.Windows.Data.BindingOperations
                .GetBindingExpression(target, expressionFor);
            be?.UpdateTarget();
        }

        private sealed class FxCache
        {
            public DateTime FetchedUtc { get; set; }
            public Dictionary<string, decimal> Rates { get; set; } = new();
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this,
                new System.ComponentModel.PropertyChangedEventArgs(name));

        /// <summary>Last-fetch status line for the settings page (already
        /// localized by the caller).</summary>
        public string StatusText()
        {
            if (_rateSource == "live")
                return $"live · {_ratesFetchedUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
            if (_rateSource == "cache" && _ratesFetchedUtc != default)
                return $"last fetched · {_ratesFetchedUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
            return "seeded (offline — first online fetch will replace)";
        }
    }

    /// <summary>
    /// Approximate seed rates (1 AED → X). ONLY used before the first
    /// successful internet fetch; the settings page labels the source so
    /// nobody mistakes a seed for a live quote. Kept in one place so the
    /// whole switch works on a fresh offline install.
    /// </summary>
    public static class SeededFxRates
    {
        public static readonly IReadOnlyDictionary<string, decimal> Table =
            new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["AED"] = 1m,
                ["USD"] = 0.2723m,
                ["EUR"] = 0.2508m,
                ["GBP"] = 0.2131m,
                ["INR"] = 22.72m,     // ≈ 1 AED → ₹22.7 (2026-09 seed)
                ["CNY"] = 1.9600m,
                ["JPY"] = 41.20m,
                ["KRW"] = 370.0m,
                ["SAR"] = 1.0214m,
                ["QAR"] = 0.9913m,
                ["KWD"] = 0.0836m,
                ["BHD"] = 0.1027m,
                ["OMR"] = 0.1048m,
                ["EGP"] = 13.20m,
                ["TRY"] = 9.25m,
                ["RUB"] = 24.60m,
                ["BRL"] = 1.49m,
                ["PKR"] = 75.60m,
                ["BDT"] = 32.30m,
                ["IDR"] = 4 360m,
                ["MYR"] = 1.190m,
                ["PHP"] = 15.35m,
                ["THB"] = 9.43m,
                ["VND"] = 6 920m,
                ["LKR"] = 82.0m,
                ["NPR"] = 36.40m,
                ["AUD"] = 0.416m,
                ["CAD"] = 0.372m,
            };
    }
}
