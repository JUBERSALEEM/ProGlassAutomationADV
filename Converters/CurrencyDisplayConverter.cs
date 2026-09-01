// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/CurrencyDisplayConverter.cs
// PROJECT: ProGlassAutomation
// MODULE: Global currency (2026-09-01)
//
// Renders a BASE (AED) amount in the ACTIVE DISPLAY currency at the
// current rate — 1 AED → ₹26.xx INR with internet rates, last-fetched
// cache offline. The books and databases NEVER change: conversion is
// display-only, at the converter.
//
// Usage in XAML:
//   <TextBlock Text="{Binding Total, Converter={StaticResource CurrencyConverter}}"/>
//   <TextBlock Text="{Binding Total, Converter={StaticResource CurrencyConverter},
//                            ConverterParameter=prefix}"/>   →  ₹ 125,000.00
//   ...ConverterParameter=suffix      →  125,000.00 ₹
//   ...ConverterParameter=minus       →  - ₹ 125,000.00
//   ...ConverterParameter=plus        →  + ₹ 125,000.00
//   ...ConverterParameter=sqm         →  ₹ 125.00 / m²
//   ...ConverterParameter=surcharge   →  With surcharge ₹ 125,000.00
//   ...ConverterParameter=imported    →  Imported ₹ 125,000.00
//
// Old literal formats ("AED {0:N2}", "{0:N2} AED", "- {0:N2} AED") are
// mapped onto these parameters so switching language+currency changes
// every amount on screen at once.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Localization;
using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    public sealed class CurrencyDisplayConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        decimal amount = ToDecimal(value);
        string mode = ((parameter as string) ?? "plain").Trim().ToLowerInvariant();
        var svc = CurrencyService.Instance;

        string core = mode switch
        {
            "suffix" => svc.Format(amount, layout: CurrencyService.CurrencyLayout.Suffix),
            "sqm" => $"{svc.Format(amount)} / m²",
            "minus" => $"- {svc.Format(amount)}",
            "plus" => $"+ {svc.Format(amount)}",
            "surcharge" => $"With surcharge {svc.Format(amount)}",
            "imported" => $"Imported {svc.Format(amount)}",
            _ => svc.Format(amount),
        };
        return core;
    }

        public object ConvertBack(object? value, Type targetType,
            object? parameter, CultureInfo culture) => Binding.DoNothing;

        private static decimal ToDecimal(object? value) => value switch
        {
            null => 0m,
            decimal d => d,
            double dbl => (decimal)dbl,
            float f => (decimal)f,
            int i => i,
            long l => l,
            short s => s,
            byte b => b,
            string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            _ => 0m,
        };
    }
}
