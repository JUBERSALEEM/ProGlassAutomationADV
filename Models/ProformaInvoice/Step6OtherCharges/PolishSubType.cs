using System.Collections.Generic;

namespace ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// Polish sub-type controls how LM is calculated per glass row.
    /// Each sub-type has its own perimeter formula.
    /// </summary>
    public enum PolishSubType
    {
        AllSidePolish,           // (W + H) / 1000 * 2 * qty  → full perimeter
        BothHeightSide,          // H / 1000 * 2 * qty        → heights only, both sides
        BothWidthSide,           // W / 1000 * 2 * qty        → widths only, both sides
        AllSidePolishAsPerDwg,   // (W + H) / 1000 * 2 * qty  → same as AllSide + "As Per DWG" note
        Custom                   // user types free-text description + manual qty entry
    }

    /// <summary>
    /// Static metadata for polish sub-types (labels, formula hints).
    /// </summary>
    public static class PolishSubTypeInfo
    {
        public class Info
        {
            public PolishSubType Type { get; set; }
            public string DisplayLabel { get; set; } = "";
            public string FormulaHint { get; set; } = "";
        }

        public static List<Info> All { get; } = new()
        {
            new Info
            {
                Type = PolishSubType.AllSidePolish,
                DisplayLabel = "All Side Polish",
                FormulaHint = "(W+H)/1000 × 2 × Qty"
            },
            new Info
            {
                Type = PolishSubType.BothHeightSide,
                DisplayLabel = "Both Height Side",
                FormulaHint = "H/1000 × 2 × Qty"
            },
            new Info
            {
                Type = PolishSubType.BothWidthSide,
                DisplayLabel = "Both Width Side",
                FormulaHint = "W/1000 × 2 × Qty"
            },
            new Info
            {
                Type = PolishSubType.AllSidePolishAsPerDwg,
                DisplayLabel = "All Side Polish As Per DWG",
                FormulaHint = "(W+H)/1000 × 2 × Qty (As Per DWG)"
            },
            new Info
            {
                Type = PolishSubType.Custom,
                DisplayLabel = "Custom (Manual)",
                FormulaHint = "Enter description + manual qty"
            }
        };

        public static Info GetInfo(PolishSubType type)
        {
            foreach (var info in All)
                if (info.Type == type) return info;
            return All[0];
        }
    }
}