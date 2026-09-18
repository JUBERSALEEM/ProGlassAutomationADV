// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/PersonalProformaInvoiceView.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P5 (2026-09-18) — Glass Blush Personal Proforma Invoice.
// Declarative only: binds to the existing ProformaInvoiceViewModel
// (same bindings/commands/AutomationIds/i18n as the Professional PI shell).
// No business logic, no handlers, no state.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Personal
{
    /// <summary>
    /// Glass Blush Personal Proforma Invoice (glass-02 reference):
    /// glowing 9-step strip + glass step panel on the SAME wizard VM.
    /// </summary>
    public partial class PersonalProformaInvoiceView : UserControl
    {
        public PersonalProformaInvoiceView()
        {
            InitializeComponent();
        }
    }
}
