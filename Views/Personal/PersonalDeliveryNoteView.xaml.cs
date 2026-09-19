// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/PersonalDeliveryNoteView.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P9 (2026-09-18) — Glass Blush Personal Delivery Note (glass-06).
// Binds to the SAME DeliveryNoteViewModel. InitializeComponent only.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Personal
{
    /// <summary>
    /// Glass Blush Personal Delivery Note (glass-06 reference): glass
    /// list card + glass detail card (driver / plate / progress / items)
    /// with the full Professional detail hosted below the reference
    /// header block (documented hybrid).
    /// </summary>
    public partial class PersonalDeliveryNoteView : UserControl
    {
        public PersonalDeliveryNoteView()
        {
            InitializeComponent();
        }
    }
}
