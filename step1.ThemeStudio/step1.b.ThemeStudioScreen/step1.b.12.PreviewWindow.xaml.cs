using System.Windows;
namespace ProGlassAutomation.ThemeStudio;
public partial class PreviewWindow : Window
{
    public PreviewWindow() => InitializeComponent();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
