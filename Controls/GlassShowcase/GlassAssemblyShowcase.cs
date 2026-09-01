// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/GlassShowcase/GlassAssemblyShowcase.cs
// PROJECT: ProGlassAutomation
// MODULE: Proforma Invoice — Step 4 product visualization
//
// Manufacturing drawing for SGU / DGU / LAM / DGU+LAM.
//   SGU     — wide single pane (the mockup hero sheet)
//   DGU     — packed outer / spacer / inner sandwich
//   LAM     — exploded outer / PVB / inner stack
//   DGU+LAM — cutaway: vertical outer + cavity + laminated pair
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Controls.GlassShowcase
{
    public enum GlassAssemblyKind
    {
        Sgu,
        Dgu,
        Lam,
        DguLam
    }

    public partial class GlassAssemblyShowcase : FrameworkElement
    {
    }
}
