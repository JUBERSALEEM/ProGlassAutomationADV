using ProGlassAutomation.Models.Dashboard;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    /// <summary>
    /// Shared bar → donut adapter so all 7 dashboards speak one chart language.
    /// Accent words resolve through theme brushes (Light/Dark safe).
    /// </summary>
    public static class DashboardChartAdapter
    {
        private static readonly string[] Accents =
            { "Blue", "Green", "Amber", "Red", "" };

        public static ObservableCollection<ChartSliceModel> FromBars(
            IEnumerable<ChartBarModel>? bars)
        {
            var list = bars?.Where(b => b != null).ToList()
                       ?? new List<ChartBarModel>();
            return new ObservableCollection<ChartSliceModel>(
                list.Select((b, i) => new ChartSliceModel
                {
                    Label = b.Label ?? "",
                    RawValue = b.RawValue,
                    ValueDisplay = string.IsNullOrWhiteSpace(b.ValueDisplay)
                        ? b.RawValue.ToString("0")
                        : b.ValueDisplay,
                    Accent = Accents[i % Accents.Length]
                }));
        }
    }
}
