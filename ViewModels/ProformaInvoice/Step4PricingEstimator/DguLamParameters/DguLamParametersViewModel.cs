// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/.../DguLamParameters/DguLamParametersViewModel.cs
// PROJECT: ProGlassAutomation — Step 4 Pricing Estimator
//
// DGU + LAMINATION — a THREE-PANE hybrid.
//
//     6mm HD Grey FT Glass          <- outer pane
//   + 12mm ASP                      <- air spacer (12 / 16 / other)
//   + 6mm Clear FT Glass            <- laminated OUTER pane
//   + 1.52mm Clear PVB              <- interlayer (PVB / SGP / EVA)
//   + 6mm Clear FT Glass            <- laminated INNER pane
//
// This is NOT "a DGU with one extra layer". It carries THREE sheets of
// glass, so any code that assumes outer+inner will under-cost it by a
// whole pane — the same defect class that dropped the inner pane from
// every DGU before it was fixed.
//
// ═══════════════════════════════════════════════════════════════════════
//  PRICING RULES, AND WHERE EACH ONE COMES FROM
// ═══════════════════════════════════════════════════════════════════════
//
// Worked example given by the business:
//     HD Grey outer 48 · laminated outer 25 · laminated inner 25
//     air spacer 45 · PVB bought 100, sold 110 · EVA bought 75, sold 95
//
// 1. THE INTERLAYER IS A PASS-THROUGH, NOT A MARKED-UP COST.
//    "we get in 100 ... given to customer 110" — 110 IS the customer
//    price. The margin on it is the 10 spread, already decided by the
//    person quoting. Feeding 110 into the subtotal before ×(1+margin)
//    would mark it up a SECOND time: +16.50/sqm on a 15% margin, 660 AED
//    on a 40 sqm job. So the sell price is added AFTER the margin step.
//
// 2. THE YIELD FACTOR DIVIDES GLASS ONLY.
//    Yield models cutting wastage on sheets we cut. The interlayer is
//    bought finished per SQM from the laminator, so there is no offcut to
//    recover and dividing it by 0.85 would invent ~18% of cost.
//
// 3. CUTTING AND TEMPERING APPLY TO THE TWO LAMINATED PANES.
//    "we charge cutting and tempering in lamination panel". The outer
//    pane behind the air spacer is covered by the ASP rate, which the
//    business has already stated includes cutting, tempering and
//    production for DGU — charging it again would double-bill.
//
// 4. BUY AND SELL PRICES ARE BOTH STORED.
//    Sell drives the customer quote; buy is what Step 9 costs against.
//    Holding only one would make either the quote or the profit wrong.
//
// If any of these is not how the business actually prices, the formula in
// CalculateRate() is the single place to change — it is written as
// numbered steps for exactly that reason.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguLamParameters
{
    /// <summary>Parameters for a DGU + laminated hybrid unit.</summary>
    public class DguLamParametersViewModel : ViewModelBase
    {
        // ══════════════════════════════════════════════════════════════
        //  DROPDOWN SOURCES
        // ══════════════════════════════════════════════════════════════

        public List<string> ThicknessOptions { get; } = new()
        {
            "3mm", "4mm", "5mm", "6mm", "8mm", "10mm", "12mm", "16mm", "19mm"
        };

        public List<string> TreatmentOptions { get; } = new()
        {
            "FT", "Annealed", "Heat Strengthened", "Heat Soaked"
        };

        /// <summary>Air spacer widths. 12mm and 16mm are the common ones.</summary>
        public List<string> SpacerOptions { get; } = new()
        {
            "6mm", "8mm", "9mm", "12mm", "14mm", "16mm", "18mm", "20mm"
        };

        /// <summary>
        /// Interlayer chemistries. All three are outsourced — ProGlass does
        /// not laminate in-house — so each carries its own bought and sold
        /// price rather than a single rate.
        /// </summary>
        public List<string> InterlayerTypeOptions { get; } = new()
        {
            "PVB", "SGP", "EVA"
        };

        public List<string> InterlayerThicknessOptions { get; } = new()
        {
            "0.38mm", "0.76mm", "1.14mm", "1.52mm", "2.28mm"
        };

        public List<string> ColorOptions { get; } = new()
        {
            "Clear", "Frosted", "Milky White", "Grey", "Bronze", "Blue", "Green"
        };

        // ══════════════════════════════════════════════════════════════
        //  PANE 1 — OUTER (in front of the air spacer)
        // ══════════════════════════════════════════════════════════════

        private string _outerThickness = "6mm";
        public string OuterThickness
        {
            get => _outerThickness;
            set { if (_outerThickness == value) return; _outerThickness = value; Changed(); }
        }

        private string _outerGlassColor = "HD Grey";
        public string OuterGlassColor
        {
            get => _outerGlassColor;
            set { if (_outerGlassColor == value) return; _outerGlassColor = value; Changed(); }
        }

        private string _outerTreatment = "FT";
        public string OuterTreatment
        {
            get => _outerTreatment;
            set { if (_outerTreatment == value) return; _outerTreatment = value; Changed(); }
        }

        private decimal _outerBasePrice = 48m;
        /// <summary>AED per SQM. Auto-filled from Sheet Store selling price.</summary>
        public decimal OuterBasePrice
        {
            get => _outerBasePrice;
            set { if (_outerBasePrice == value) return; _outerBasePrice = value; Changed(); }
        }

        // ══════════════════════════════════════════════════════════════
        //  AIR SPACER
        // ══════════════════════════════════════════════════════════════

        private string _spacerThickness = "12mm";
        public string SpacerThickness
        {
            get => _spacerThickness;
            set { if (_spacerThickness == value) return; _spacerThickness = value; Changed(); }
        }

        private decimal _spacerPrice = 45m;
        /// <summary>
        /// AED per SQM — NOT per linear metre. The business quotes the
        /// spacer per square metre and the rate already includes cutting,
        /// tempering and production for the DGU side of the unit.
        /// Costing it per perimetre overstated it threefold once already.
        /// </summary>
        public decimal SpacerPrice
        {
            get => _spacerPrice;
            set { if (_spacerPrice == value) return; _spacerPrice = value; Changed(); }
        }

        // ══════════════════════════════════════════════════════════════
        //  PANE 2 — LAMINATED OUTER
        // ══════════════════════════════════════════════════════════════

        private string _lamOuterThickness = "6mm";
        public string LamOuterThickness
        {
            get => _lamOuterThickness;
            set { if (_lamOuterThickness == value) return; _lamOuterThickness = value; Changed(); }
        }

        private string _lamOuterColor = "Clear";
        public string LamOuterColor
        {
            get => _lamOuterColor;
            set { if (_lamOuterColor == value) return; _lamOuterColor = value; Changed(); }
        }

        private string _lamOuterTreatment = "FT";
        public string LamOuterTreatment
        {
            get => _lamOuterTreatment;
            set { if (_lamOuterTreatment == value) return; _lamOuterTreatment = value; Changed(); }
        }

        private decimal _lamOuterBasePrice = 25m;
        public decimal LamOuterBasePrice
        {
            get => _lamOuterBasePrice;
            set { if (_lamOuterBasePrice == value) return; _lamOuterBasePrice = value; Changed(); }
        }

        // ══════════════════════════════════════════════════════════════
        //  INTERLAYER — OUTSOURCED
        // ══════════════════════════════════════════════════════════════

        private string _interlayerType = "PVB";
        public string InterlayerType
        {
            get => _interlayerType;
            set
            {
                if (_interlayerType == value) return;
                _interlayerType = value;
                ApplyInterlayerDefaults();
                Changed();
                OnPropertyChanged(nameof(InterlayerMarginDisplay));
            }
        }

        private string _interlayerThickness = "1.52mm";
        public string InterlayerThickness
        {
            get => _interlayerThickness;
            set { if (_interlayerThickness == value) return; _interlayerThickness = value; Changed(); }
        }

        private string _interlayerColor = "Clear";
        public string InterlayerColor
        {
            get => _interlayerColor;
            set { if (_interlayerColor == value) return; _interlayerColor = value; Changed(); }
        }

        private decimal _interlayerBuyPrice = 100m;
        /// <summary>
        /// AED per SQM paid to the laminator. Never shown to the customer —
        /// this is what Step 9 costs the job against.
        /// </summary>
        public decimal InterlayerBuyPrice
        {
            get => _interlayerBuyPrice;
            set
            {
                if (_interlayerBuyPrice == value) return;
                _interlayerBuyPrice = value;
                Changed();
                OnPropertyChanged(nameof(InterlayerMarginDisplay));
            }
        }

        private decimal _interlayerSellPrice = 110m;
        /// <summary>
        /// AED per SQM charged to the customer. Added AFTER the profit
        /// margin step — see rule 1 in the file header.
        /// </summary>
        public decimal InterlayerSellPrice
        {
            get => _interlayerSellPrice;
            set
            {
                if (_interlayerSellPrice == value) return;
                _interlayerSellPrice = value;
                Changed();
                OnPropertyChanged(nameof(InterlayerMarginDisplay));
            }
        }

        /// <summary>
        /// Shows the spread being earned on the outsourced interlayer, so a
        /// mistyped buy or sell price is visible before the quote goes out.
        /// </summary>
        public string InterlayerMarginDisplay
        {
            get
            {
                decimal spread = InterlayerSellPrice - InterlayerBuyPrice;

                if (InterlayerBuyPrice <= 0m)
                    return "Enter the bought price to see the spread.";

                if (spread < 0m)
                    return $"⚠ Selling BELOW cost by AED {Math.Abs(spread):N2}/SQM.";

                decimal pct = Math.Round(spread / InterlayerBuyPrice * 100m, 1);
                return $"Spread AED {spread:N2}/SQM ({pct:0.#}%).";
            }
        }

        /// <summary>
        /// Sensible starting prices per chemistry, from the figures the
        /// business quoted. SGP is left at zero on purpose: no price was
        /// given for it, and inventing one would put a fabricated number on
        /// a customer quote.
        /// </summary>
        private void ApplyInterlayerDefaults()
        {
            switch ((_interlayerType ?? "").ToUpperInvariant())
            {
                case "PVB":
                    _interlayerBuyPrice = 100m;
                    _interlayerSellPrice = 110m;
                    break;

                case "EVA":
                    _interlayerBuyPrice = 75m;
                    _interlayerSellPrice = 95m;
                    break;

                case "SGP":
                    _interlayerBuyPrice = 0m;
                    _interlayerSellPrice = 0m;
                    break;
            }

            OnPropertyChanged(nameof(InterlayerBuyPrice));
            OnPropertyChanged(nameof(InterlayerSellPrice));
        }

        // ══════════════════════════════════════════════════════════════
        //  PANE 3 — LAMINATED INNER
        // ══════════════════════════════════════════════════════════════

        private string _lamInnerThickness = "6mm";
        public string LamInnerThickness
        {
            get => _lamInnerThickness;
            set { if (_lamInnerThickness == value) return; _lamInnerThickness = value; Changed(); }
        }

        private string _lamInnerColor = "Clear";
        public string LamInnerColor
        {
            get => _lamInnerColor;
            set { if (_lamInnerColor == value) return; _lamInnerColor = value; Changed(); }
        }

        private string _lamInnerTreatment = "FT";
        public string LamInnerTreatment
        {
            get => _lamInnerTreatment;
            set { if (_lamInnerTreatment == value) return; _lamInnerTreatment = value; Changed(); }
        }

        private decimal _lamInnerBasePrice = 25m;
        public decimal LamInnerBasePrice
        {
            get => _lamInnerBasePrice;
            set { if (_lamInnerBasePrice == value) return; _lamInnerBasePrice = value; Changed(); }
        }

        // ══════════════════════════════════════════════════════════════
        //  PROCESSING — LAMINATED PANES ONLY (rule 3)
        // ══════════════════════════════════════════════════════════════

        private decimal _lamOuterCuttingCost = 5m;
        public decimal LamOuterCuttingCost
        {
            get => _lamOuterCuttingCost;
            set { if (_lamOuterCuttingCost == value) return; _lamOuterCuttingCost = value; Changed(); }
        }

        private decimal _lamInnerCuttingCost = 5m;
        public decimal LamInnerCuttingCost
        {
            get => _lamInnerCuttingCost;
            set { if (_lamInnerCuttingCost == value) return; _lamInnerCuttingCost = value; Changed(); }
        }

        private decimal _lamOuterTemperingCost = 10m;
        public decimal LamOuterTemperingCost
        {
            get => _lamOuterTemperingCost;
            set { if (_lamOuterTemperingCost == value) return; _lamOuterTemperingCost = value; Changed(); }
        }

        private decimal _lamInnerTemperingCost = 10m;
        public decimal LamInnerTemperingCost
        {
            get => _lamInnerTemperingCost;
            set { if (_lamInnerTemperingCost == value) return; _lamInnerTemperingCost = value; Changed(); }
        }

        // ══════════════════════════════════════════════════════════════
        //  YIELD + MARGIN
        // ══════════════════════════════════════════════════════════════

        private decimal _yieldFactor = 0.85m;
        /// <summary>Glass wastage recovery. Divides GLASS ONLY — see rule 2.</summary>
        public decimal YieldFactor
        {
            get => _yieldFactor;
            set { if (_yieldFactor == value) return; _yieldFactor = value; Changed(); }
        }

        private decimal _marginProfitPercent = 15m;
        public decimal MarginProfitPercent
        {
            get => _marginProfitPercent;
            set { if (_marginProfitPercent == value) return; _marginProfitPercent = value; Changed(); }
        }

        // ══════════════════════════════════════════════════════════════
        //  SPEC NAME
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass
        ///  + 1.52mm Clear PVB + 6mm Clear FT Glass"
        ///
        /// The layer order matters beyond display: Step 9 parses this string
        /// to work out how many panes to cost, so the three "… Glass"
        /// segments and the two separators must appear exactly like this.
        /// </summary>
        public string GeneratedSpecName
        {
            get
            {
                string outer = $"{OuterThickness} {OuterGlassColor} {OuterTreatment} Glass";
                string spacer = $"{SpacerThickness} ASP";
                string lamOuter = $"{LamOuterThickness} {LamOuterColor} {LamOuterTreatment} Glass";
                string interlayer = $"{InterlayerThickness} {InterlayerColor} {InterlayerType}";
                string lamInner = $"{LamInnerThickness} {LamInnerColor} {LamInnerTreatment} Glass";

                return $"{outer} + {spacer} + {lamOuter} + {interlayer} + {lamInner}";
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  RATE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// AED per SQM, built in numbered steps so a business rule change
        /// has one obvious place to land.
        ///
        ///   1. glass    = outer + laminated outer + laminated inner
        ///   2. /yield   — glass only (rule 2)
        ///   3. + spacer — per SQM, includes DGU-side processing
        ///   4. + cutting and tempering on the two laminated panes (rule 3)
        ///   5. × (1 + margin)
        ///   6. + interlayer SELL price — after margin, no double mark-up (rule 1)
        ///
        /// Worked example — 48 / 25 / 25 glass, 45 spacer, 0.85 yield,
        /// 15% margin, 5+5 cutting, 10+10 tempering, PVB sold at 110:
        ///     (98 / 0.85 = 115.29) + 45 + 30 = 190.29
        ///     × 1.15 = 218.84
        ///     + 110 = 328.84 per SQM
        ///
        /// (Verified against C# decimal semantics, not floating point —
        ///  the two differ by a cent here and a cent on a quote is a
        ///  question from the customer.)
        /// </summary>
        public decimal CalculateRate(out string warning)
        {
            warning = "";

            decimal yieldFactor = YieldFactor;

            if (yieldFactor <= 0m)
            {
                warning = $"⚠  Yield factor must be greater than 0 " +
                          $"(currently {yieldFactor:0.###}). Using 1.0 instead.";
                yieldFactor = 1m;
            }

            decimal glass = OuterBasePrice + LamOuterBasePrice + LamInnerBasePrice;

            decimal step2 = glass / yieldFactor;
            decimal step3 = step2 + SpacerPrice;

            decimal processing = LamOuterCuttingCost + LamInnerCuttingCost
                               + LamOuterTemperingCost + LamInnerTemperingCost;

            decimal step4 = step3 + processing;
            decimal step5 = step4 * (1m + (MarginProfitPercent / 100m));
            decimal step6 = step5 + InterlayerSellPrice;

            // An unpriced interlayer is the one mistake that silently
            // undercharges a whole job, so it is called out rather than
            // quietly producing a low rate.
            if (InterlayerSellPrice <= 0m)
            {
                string note = $"⚠  No selling price set for the {InterlayerType} " +
                              "interlayer, so it is NOT included in this rate.";

                warning = warning.Length > 0 ? warning + "  " + note : note;
            }

            return Math.Round(step6, 2);
        }

        /// <summary>Parameterless overload for callers that ignore warnings.</summary>
        public decimal CalculateRate() => CalculateRate(out _);

        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Raises the change notification for the property that called it,
        /// then for the two read-only values derived from every field.
        ///
        /// The caller's name MUST be forwarded explicitly. ViewModelBase
        /// fills the argument from [CallerMemberName], so relying on the
        /// default here would announce "Changed" — a property nothing binds
        /// to — and the edited box would silently stop updating.
        /// </summary>
        private void Changed([CallerMemberName] string? propertyName = null)
        {
            OnPropertyChanged(propertyName);
            OnPropertyChanged(nameof(GeneratedSpecName));
            OnPropertyChanged(nameof(EstimatedRateDisplay));
        }

        public string EstimatedRateDisplay => $"AED {CalculateRate():N2} / SQM";
    }
}
