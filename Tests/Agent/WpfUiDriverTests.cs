using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.Utilities;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class WpfUiDriverTests
    {
        [Fact]
        public void FindById_does_not_throw_when_grid_has_column_definitions()
        {
            Exception? thrown = null;
            DependencyObject? hit = null;
            DependencyObject? missing = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition());
                    grid.ColumnDefinitions.Add(new ColumnDefinition());
                    grid.RowDefinitions.Add(new RowDefinition());
                    var btn = new Button();
                    AutomationProperties.SetAutomationId(btn, "ProformaInvoice.ContinueButton");
                    grid.Children.Add(btn);

                    hit = WpfUiDriver.FindById(grid, "ProformaInvoice.ContinueButton");
                    missing = WpfUiDriver.FindById(grid, "does.not.exist");
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(thrown);
            Assert.NotNull(hit);
            Assert.IsType<Button>(hit);
            Assert.Null(missing);
        }

        [Fact]
        public void VisualTreeHelper_is_not_called_on_column_definition()
        {
            Exception? thrown = null;
            int count = -1;
            var thread = new Thread(() =>
            {
                try
                {
                    var col = new ColumnDefinition();
                    count = UiTreeHelper.SafeChildrenCount(col);
                    Assert.False(UiTreeHelper.IsVisual(col));
                    Assert.Null(UiTreeHelper.SafeGetParent(col));
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(thrown);
            Assert.Equal(0, count);
        }
    }
}
