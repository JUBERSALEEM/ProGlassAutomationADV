// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Reports/StatementGenerator/Preview/PreviewStep.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Statement Generator — Wizard Step 2: PREVIEW (Chat 21 v3)
//
// PURPOSE:
//   Code-behind for the Preview step. Minimal — fully data-driven
//   via bindings to StatementGeneratorViewModel.PreviewVM.
//
// DATA CONTEXT:
//   Automatically inherited from parent Shell's DataContext.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports.StatementGenerator.Preview
{
    public partial class PreviewStep : UserControl
    {
        public PreviewStep()
        {
            InitializeComponent();
        }
    }
}