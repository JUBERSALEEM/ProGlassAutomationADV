// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Localization/CountryDialCode.cs
// PROJECT: ProGlassAutomation
// MODULE: Global locale — country dial codes for phone inputs
//
// The +971 / +91 / +86 ... table used by the PhoneCountryBox control and
// by GeoService (country IP → dial code). Flags are Unicode regional
// indicator emoji — rendered by Segoe UI Emoji on Windows 10/11.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;

namespace ProGlassAutomation.Services.Localization
{
    public sealed class CountryInfo
    {
        public string Iso { get; init; } = "";
        public string Name { get; init; } = "";
        public string DialCode { get; init; } = "";
        public string Flag { get; init; } = "";

        /// <summary>Picker text ("🇦🇪 United Arab Emirates +971").</summary>
        public string DisplayName => $"{Flag} {Name} {DialCode}";
    }

    /// <summary>Static table of the countries included in the phone box.</summary>
    public static class CountryDialCode
    {
        public static readonly IReadOnlyList<CountryInfo> All =
        [
            new() { Iso = "AE", Name = "United Arab Emirates",  DialCode = "+971", Flag = "🇦🇪" },
            new() { Iso = "IN", Name = "India",                 DialCode = "+91",  Flag = "🇮🇳" },
            new() { Iso = "CN", Name = "China",                 DialCode = "+86",  Flag = "🇨🇳" },
            new() { Iso = "US", Name = "United States",         DialCode = "+1",   Flag = "🇺🇸" },
            new() { Iso = "GB", Name = "United Kingdom",        DialCode = "+44",  Flag = "🇬🇧" },
            new() { Iso = "SA", Name = "Saudi Arabia",          DialCode = "+966", Flag = "🇸🇦" },
            new() { Iso = "QA", Name = "Qatar",                 DialCode = "+974", Flag = "🇶🇦" },
            new() { Iso = "KW", Name = "Kuwait",                DialCode = "+965", Flag = "🇰🇼" },
            new() { Iso = "BH", Name = "Bahrain",               DialCode = "+973", Flag = "🇧🇭" },
            new() { Iso = "OM", Name = "Oman",                  DialCode = "+968", Flag = "🇴🇲" },
            new() { Iso = "EG", Name = "Egypt",                 DialCode = "+20",  Flag = "🇪🇬" },
            new() { Iso = "PK", Name = "Pakistan",              DialCode = "+92",  Flag = "🇵🇰" },
            new() { Iso = "BD", Name = "Bangladesh",            DialCode = "+880", Flag = "🇧🇩" },
            new() { Iso = "ID", Name = "Indonesia",             DialCode = "+62",  Flag = "🇮🇩" },
            new() { Iso = "JP", Name = "Japan",                 DialCode = "+81",  Flag = "🇯🇵" },
            new() { Iso = "KR", Name = "South Korea",           DialCode = "+82",  Flag = "🇰🇷" },
            new() { Iso = "ES", Name = "Spain",                 DialCode = "+34",  Flag = "🇪🇸" },
            new() { Iso = "FR", Name = "France",                DialCode = "+33",  Flag = "🇫🇷" },
            new() { Iso = "DE", Name = "Germany",               DialCode = "+49",  Flag = "🇩🇪" },
            new() { Iso = "IT", Name = "Italy",                 DialCode = "+39",  Flag = "🇮🇹" },
            new() { Iso = "TR", Name = "Turkey",                DialCode = "+90",  Flag = "🇹🇷" },
            new() { Iso = "RU", Name = "Russia",                DialCode = "+7",   Flag = "🇷🇺" },
            new() { Iso = "BR", Name = "Brazil",                DialCode = "+55",  Flag = "🇧🇷" },
            new() { Iso = "MX", Name = "Mexico",                DialCode = "+52",  Flag = "🇲🇽" },
            new() { Iso = "ZA", Name = "South Africa",          DialCode = "+27",  Flag = "🇿🇦" },
            new() { Iso = "AU", Name = "Australia",             DialCode = "+61",  Flag = "🇦🇺" },
            new() { Iso = "CA", Name = "Canada",                DialCode = "+1",   Flag = "🇨🇦" },
            new() { Iso = "NG", Name = "Nigeria",               DialCode = "+234", Flag = "🇳🇬" },
            new() { Iso = "MY", Name = "Malaysia",              DialCode = "+60",  Flag = "🇲🇾" },
            new() { Iso = "PH", Name = "Philippines",           DialCode = "+63",  Flag = "🇵🇭" },
            new() { Iso = "TH", Name = "Thailand",              DialCode = "+66",  Flag = "🇹🇭" },
            new() { Iso = "VN", Name = "Vietnam",               DialCode = "+84",  Flag = "🇻🇳" },
            new() { Iso = "LK", Name = "Sri Lanka",             DialCode = "+94",  Flag = "🇱🇰" },
            new() { Iso = "NP", Name = "Nepal",                 DialCode = "+977", Flag = "🇳🇵" },
        ];

        public static CountryInfo? Find(string? iso) =>
            string.IsNullOrWhiteSpace(iso) ? null : FindByIso(iso.ToUpperInvariant());

        private static CountryInfo? FindByIso(string iso)
        {
            foreach (var c in All)
                if (c.Iso == iso) return c;
            return null;
        }

        public static CountryInfo? FindByDial(string? dial)
        {
            string d = (dial ?? "").Trim();
            if (d.Length == 0) return null;
            foreach (var c in All)
                if (c.DialCode == d) return c;
            return null;
        }

        public static CountryInfo DefaultFor(string? iso) =>
            Find(iso) ?? All[0];
    }
}
