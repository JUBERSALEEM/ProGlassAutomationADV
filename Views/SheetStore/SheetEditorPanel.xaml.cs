// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/SheetStore/SheetEditorPanel.xaml.cs
// ADDED: 2026-09-06
//
// The panel's DataContext is the AddEditSheetViewModel (the FORM), so
// every field binds directly to its own property with no relative-source
// gymnastics. But Cancel/Save live on the MODULE view model, one level
// up — the form does not own the persistence path.
//
// PanelHost carries that module VM explicitly.
//
// WHY NOT A RelativeSource ANCESTOR WALK: the obvious
// `AncestorType=UserControl, AncestorLevel=2` breaks silently the moment
// anyone wraps the panel in another layout container — the binding just
// resolves to nothing and the buttons quietly stop working, with no
// error and no crash. An explicit dependency property fails loudly at
// the call site instead, and survives re-nesting.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.SheetStore;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.SheetStore
{
    public partial class SheetEditorPanel : UserControl
    {
        public SheetEditorPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The owning module view model, which supplies CloseEditorCommand
        /// and SaveEditorCommand. Set from SheetStoreView.xaml.
        /// </summary>
        public static readonly DependencyProperty PanelHostProperty =
            DependencyProperty.Register(
                nameof(PanelHost),
                typeof(SheetStoreViewModel),
                typeof(SheetEditorPanel),
                new PropertyMetadata(null));

        public SheetStoreViewModel? PanelHost
        {
            get => (SheetStoreViewModel?)GetValue(PanelHostProperty);
            set => SetValue(PanelHostProperty, value);
        }
    }
}
