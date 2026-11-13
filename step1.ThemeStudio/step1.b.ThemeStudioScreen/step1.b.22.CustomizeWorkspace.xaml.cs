using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.ThemeStudio;
public partial class CustomizeWorkspace : UserControl
{
    private bool? narrow;
    public CustomizeWorkspace() => InitializeComponent();
    private void WorkspaceGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool next = e.NewSize.Width < 1100;
        if (narrow == next) return;
        narrow = next;
        if (next)
        {
            WorkspaceGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            WorkspaceGrid.ColumnDefinitions[1].Width = new GridLength(0);
            WorkspaceGrid.ColumnDefinitions[2].Width = new GridLength(0);
            while (WorkspaceGrid.RowDefinitions.Count < 3) WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(WorkspaceGrid.Children[0], 0); Grid.SetColumn(WorkspaceGrid.Children[0], 0);
            Grid.SetRow(WorkspaceGrid.Children[1], 1); Grid.SetColumn(WorkspaceGrid.Children[1], 0);
            Grid.SetRow(WorkspaceGrid.Children[2], 2); Grid.SetColumn(WorkspaceGrid.Children[2], 0);
        }
        else
        {
            if (WorkspaceGrid.RowDefinitions.Count > 0) WorkspaceGrid.RowDefinitions.Clear();
            WorkspaceGrid.ColumnDefinitions[0].Width = new GridLength(280);
            WorkspaceGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            WorkspaceGrid.ColumnDefinitions[2].Width = new GridLength(320);
            Grid.SetRow(WorkspaceGrid.Children[0], 0); Grid.SetColumn(WorkspaceGrid.Children[0], 0);
            Grid.SetRow(WorkspaceGrid.Children[1], 0); Grid.SetColumn(WorkspaceGrid.Children[1], 1);
            Grid.SetRow(WorkspaceGrid.Children[2], 0); Grid.SetColumn(WorkspaceGrid.Children[2], 2);
        }
    }
    private void PreviewCard_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string id && DataContext is ThemeStudioViewModel vm)
            vm.SelectedComponentId = id;
    }
}
