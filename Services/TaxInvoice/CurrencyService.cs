using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.TaxInvoice
{
    public class CurrencyInfo
    {
        public string Code { get; set; } = "AED";
        public string Name { get; set; } = "UAE Dirham";
        public string Symbol { get; set; } = "AED";
        public decimal ExchangeRateToAed { get; set; } = 1.0m;
    }

    /// <summary>
    /// Service managing multi-currency symbols and exchange rates.
    /// Supports dynamic formatting to prevent hardcoding currency symbols.
    /// </summary>
    public class CurrencyService
    {
        private static readonly Dictionary<string, CurrencyInfo> Currencies = new(StringComparer.OrdinalIgnoreCase)
        {
            { "AED", new CurrencyInfo { Code = "AED", Name = "UAE Dirham", Symbol = "AED", ExchangeRateToAed = 1.0m } },
            { "USD", new CurrencyInfo { Code = "USD", Name = "US Dollar", Symbol = "$", ExchangeRateToAed = 3.6725m } },
            { "EUR", new CurrencyInfo { Code = "EUR", Name = "Euro", Symbol = "€", ExchangeRateToAed = 4.00m } },
            { "INR", new CurrencyInfo { Code = "INR", Name = "Indian Rupee", Symbol = "₹", ExchangeRateToAed = 0.044m } }
        };

        public static IEnumerable<CurrencyInfo> GetSupportedCurrencies()
        {
            return Currencies.Values;
        }

        public static CurrencyInfo GetCurrency(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Currencies["AED"];

            return Currencies.TryGetValue(code, out var info) ? info : Currencies["AED"];
        }

        /// <summary>
        /// Formats an amount dynamically in the specified currency.
        /// </summary>
        public static string Format(decimal amount, string currencyCode)
        {
            var info = GetCurrency(currencyCode);
            return $"{info.Symbol} {amount:N2}";
        }
    }
}