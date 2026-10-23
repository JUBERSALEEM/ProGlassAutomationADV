using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Documents
{
    public sealed class DocumentRegisterLayoutTests
    {
        private static readonly XNamespace Presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        [Fact]
        public void All_register_tables_use_peer_row_height_and_equal_star_columns()
        {
            var path = Path.Combine(LocateProjectRoot(),
                "Views", "DocumentRegister", "DocumentRegisterView.xaml");
            var document = XDocument.Load(path);
            var grids = document.Descendants(Presentation + "DataGrid").ToArray();

            Assert.Equal(3, grids.Length);
            foreach (var grid in grids)
            {
                // Set metrics on each grid itself so the shared compact-density
                // style cannot collapse the editable register rows or columns.
                Assert.Equal("{StaticResource Sl.DataGrid}",
                    (string?)grid.Attribute("Style"));
                Assert.Equal("56", (string?)grid.Attribute("RowHeight"));
                Assert.Equal("56", (string?)grid.Attribute("MinRowHeight"));
                Assert.Equal("*", (string?)grid.Attribute("ColumnWidth"));
                Assert.Equal("{StaticResource Sl.DataGridHeaderCenter}",
                    (string?)grid.Attribute("ColumnHeaderStyle"));

                var columns = grid.Element(Presentation + "DataGrid.Columns")!
                    .Elements().ToArray();
                Assert.NotEmpty(columns);
                Assert.All(columns, column =>
                    Assert.Null(column.Attribute("Width")));
            }
        }

        private static string LocateProjectRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Views",
                        "DocumentRegister", "DocumentRegisterView.xaml")))
                    return directory.FullName;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the ProGlassAutomation repository root.");
        }
    }
}
