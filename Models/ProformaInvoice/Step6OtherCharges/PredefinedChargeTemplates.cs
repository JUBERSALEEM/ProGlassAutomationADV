using System.Collections.Generic;

namespace ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// Immutable seed data for the predefined Other Charges rows shown in Step 6.
    /// User can enable/disable and edit rate per invoice — templates just provide defaults.
    /// </summary>
    public static class PredefinedChargeTemplates
    {
        public class Template
        {
            public string Key { get; set; } = "";
            public string ChargeName { get; set; } = "";
            public string Description { get; set; } = "";
            public OtherChargeUnit Unit { get; set; }
            public decimal DefaultRate { get; set; }
            public string DefaultSourceKey { get; set; } = ""; // suggested source when checked
        }

        public static List<Template> GetAll() => new()
        {
            new Template
            {
                Key = "ARGON_GAS",
                ChargeName = "Argon Gas (Sqm)",
                Description = "Inert Argon gas infusion inside DGU cavity for thermal insulation.",
                Unit = OtherChargeUnit.Sqm,
                DefaultRate = 45m,
                DefaultSourceKey = "SQM1"
            },
            new Template
            {
                Key = "SILICON_BED",
                ChargeName = "Silicon bed (lm)",
                Description = "Secondary sealant structural silicone bed barrier for double glazing.",
                Unit = OtherChargeUnit.Lm,
                DefaultRate = 6m,
                DefaultSourceKey = "LM1"
            },
            new Template
            {
                Key = "OVERLAP",
                ChargeName = "Overlap (lm)",
                Description = "Stepped laminate offset or double glazed spacer overlap edge.",
                Unit = OtherChargeUnit.Lm,
                DefaultRate = 6m,
                DefaultSourceKey = "LM1"
            },
            new Template
            {
                Key = "SHUTTER_BONDING",
                ChargeName = "Shutter Bonding (lm)",
                Description = "Structural bonding of glazing glass sheets into the window shutter.",
                Unit = OtherChargeUnit.Lm,
                DefaultRate = 25m,
                DefaultSourceKey = "LM_COMBINED"
            },
            new Template
            {
                Key = "MITRING",
                ChargeName = "Mitring (lm)",
                Description = "Beveled edge cutting (45 degrees) for custom joint assemblies.",
                Unit = OtherChargeUnit.Lm,
                DefaultRate = 30m,
                DefaultSourceKey = "LM1"
            },

            // ═══════════════ Phase 7.11b ═══════════════

            new Template
            {
                Key = "POLISH",
                ChargeName = "Polish (lm)",
                Description = "Continuous flat edge polishing, arrising, and buffing. Sub-type controls formula.",
                Unit = OtherChargeUnit.Lm,
                DefaultRate = 6m,
                DefaultSourceKey = "" // Polish uses sub-type, not source dropdown
            },
            new Template
            {
                Key = "CUTOUT",
                ChargeName = "Cutout (Qty)",
                Description = "Water-jet internal precision cutouts for hinges and structural patches.",
                Unit = OtherChargeUnit.Qty,
                DefaultRate = 50m,
                DefaultSourceKey = "TOTAL_PIECES"
            },
            new Template
            {
                Key = "HOLES",
                ChargeName = "Holes (Qty)",
                Description = "Structural glass hole drilling (8mm to 15mm) on CNC drillers.",
                Unit = OtherChargeUnit.Qty,
                DefaultRate = 15m,
                DefaultSourceKey = "TOTAL_PIECES"
            }

            // Phase 7.11c will add: Wastages (color-aggregated)
        };
    }
}