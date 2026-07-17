using ProGlassAutomation.ViewModels.AI;
using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProGlassAutomation.Views.AI
{
    public partial class AiChatView : UserControl
    {
        public AiChatView()
        {
            InitializeComponent();

            DataContextChanged += AiChatView_DataContextChanged;
        }

        private void AiChatView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is AiChatViewModel oldVm)
                oldVm.Messages.CollectionChanged -= Messages_CollectionChanged;

            if (e.NewValue is AiChatViewModel newVm)
                newVm.Messages.CollectionChanged += Messages_CollectionChanged;
        }

        // Auto-scroll to bottom when new message arrives
        private void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && MessagesScroll != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessagesScroll.ScrollToEnd();
                }), DispatcherPriority.Background);
            }
        }

        // Enter key -> send message
        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is AiChatViewModel vm)
            {
                if (vm.SendCommand.CanExecute(null))
                    vm.SendCommand.Execute(null);
                e.Handled = true;
            }
        }

        // Click outside chat panel closes it
        private void DimOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AiChatViewModel vm)
                vm.IsOpen = false;
        }
    }
}