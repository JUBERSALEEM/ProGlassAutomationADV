namespace ProGlassAutomation.Views.DeliveryNote.DeliveryNoteDetail
{
    public partial class DeliveryNoteDetailView
    {
        public DeliveryNoteDetailView()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                System.Windows.MessageBox.Show(
                    $"DetailView Loaded!\n" +
                    $"DataContext type: {DataContext?.GetType().Name ?? "NULL"}\n" +
                    $"ActualWidth: {ActualWidth}\n" +
                    $"ActualHeight: {ActualHeight}\n" +
                    $"IsVisible: {IsVisible}");
            };
        }
    }
}