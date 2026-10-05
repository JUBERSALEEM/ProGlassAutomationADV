// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Features/Step9PricingDefaults.cs
// PROJECT: ProGlassAutomation
// MODULE: Step 9 target-pricing defaults (live, app-wide)
//
// PURPOSE
// ───────
// Single runtime source for the numeric defaults that drive the approved
// Step 9 target formula:
//
//     Required target = ((glass ÷ yield) + add-ons) × (1 + margin/100)
//
//   • ExpectedProfitMarginPercent — the DEFAULT profit margin (%) used
//     when an invoice is created. Settings ‣ Features ‣ Proforma Invoice
//     ‣ Step 9 writes it here (and persists it in
//     AppConfig.ProformaInvoiceFields.ExpectedProfitMarginPercent).
//
// WHO READS IT
// ────────────
//   • Step 4 pricing panels seed their "Margin %" boxes from it when a
//     new invoice is started (Step 4 stays per-invoice editable after
//     that seed).
//   • Step 9 reads it for drafts / invoices saved before the value was
//     introduced (auto-adapt, always live). Once an invoice has been
//     saved with its own expected margin, that captured value wins — the
//     middle KPI card then shows the default as it was at creation time,
//     never a silently-changed global.
//
// WHY STATIC
// ──────────
// No DI: the pricing panels are constructed in several places (designer
// stubs, tests, wizard shell), and constructor injection is precisely the
// change that broke composition before. The value is a single decimal on
// a single instance (App.Config), so a static reader cannot diverge.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using System;

namespace ProGlassAutomation.Services.Features
{
    /// <summary>
    /// Live, app-wide numeric defaults for Step 9 target pricing.
    /// </summary>
    public static class Step9PricingDefaults
    {
        /// <summary>Fallback when no config is available (15% keeps the
        /// approved ×1.15 formula byte-identical on fresh installs).</summary>
        public const decimal FallbackExpectedProfitMarginPercent = 15m;

        /// <summary>
        /// The company default expected profit margin (%). Never throws and
        /// never returns a negative number; falls back to 15% when the
        /// config is missing.
        /// </summary>
        public static decimal ExpectedProfitMarginPercent
        {
            get
            {
                try
                {
                    var flags = App.Config?.ProformaInvoiceFields;
                    if (flags == null) return FallbackExpectedProfitMarginPercent;
                    return flags.ExpectedProfitMarginPercent < 0m
                        ? 0m
                        : flags.ExpectedProfitMarginPercent;
                }
                catch
                {
                    // Settings / startup edge: a failed read must never
                    // break a price calculation.
                    return FallbackExpectedProfitMarginPercent;
                }
            }
        }

        /// <summary>Clamps a percent into the usable range for an uplift.</summary>
        public static decimal ClampPercent(decimal percent) =>
            percent < 0m ? 0m : percent > 200m ? 200m : percent;

        /// <summary>
        /// Raised after Settings persists a new expected profit margin, so
        /// Step 9 view models that are NOT pinned to an invoice-specific
        /// value can re-read it live and recalculate.
        /// </summary>
        public static event Action? MarginDefaultChanged;

        /// <summary>Fired by the Settings view-model after saving.</summary>
        public static void NotifyMarginDefaultChanged() => MarginDefaultChanged?.Invoke();
    }
}
