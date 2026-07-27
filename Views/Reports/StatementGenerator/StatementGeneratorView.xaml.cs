// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Reports/StatementGenerator/StatementGeneratorView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// NOTE:
//   PreviewVM is exposed on StatementGeneratorViewModel as a public
//   property — bound directly in XAML via {Binding PreviewVM.XXX}.
//   No code-behind wiring needed.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports.StatementGenerator
{
    public partial class StatementGeneratorView : UserControl
    {
        public StatementGeneratorView()
        {
            InitializeComponent();
        }
    }
}