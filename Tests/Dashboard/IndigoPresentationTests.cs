using System;
using System.Globalization;
using System.Windows.Media;
using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Views.Dashboard.Overview;
using Xunit;

namespace ProGlassAutomation.Tests.Dashboard
{
    public sealed class IndigoPresentationTests
    {
        private static bool IsEmpty(params ChartSliceModel[] slices)
            => (bool)new EmptyDeliverySnapshotConverter().Convert(
                slices, typeof(bool), string.Empty, CultureInfo.InvariantCulture);

        [Fact]
        public void Semantic_status_keys_resolve_to_the_scoped_palette()
        {
            var palette = new IndigoStatusBrushConverter
            {
                Accent = Brushes.Purple, AccentSoft = Brushes.Lavender,
                Success = Brushes.Green, SuccessSoft = Brushes.Honeydew,
                Danger = Brushes.Red, DangerSoft = Brushes.MistyRose,
                Warning = Brushes.Brown, WarningSoft = Brushes.Cornsilk,
                Neutral = Brushes.Gray, NeutralSoft = Brushes.Gainsboro,
            };
            (string Key, Brush Expected)[] cases =
            {
                ("InfoBrush", palette.Accent), ("AccentBrush", palette.Accent),
                ("SuccessBrush", palette.Success), ("DangerBrush", palette.Danger),
                ("WarningBrush", palette.Warning), ("TintIndigoBrush", palette.AccentSoft),
                ("TintGreenBrush", palette.SuccessSoft), ("TintRedBrush", palette.DangerSoft),
                ("TintAmberBrush", palette.WarningSoft), ("TintGrayBrush", palette.NeutralSoft),
                ("UnknownStatusKey", palette.Neutral),
            };
            foreach (var test in cases)
                Assert.Same(test.Expected, palette.Convert(test.Key, typeof(Brush), string.Empty, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void Not_loaded_is_not_a_known_zero_snapshot()
        {
            Assert.False(IsEmpty(Array.Empty<ChartSliceModel>()));
            Assert.False(IsEmpty(new ChartSliceModel()));
        }

        [Fact]
        public void Unavailable_pending_pieces_are_not_reported_as_no_movement()
        {
            Assert.False(IsEmpty(new ChartSliceModel { RawValue = 0, ValueDisplay = "\u2014" }));
        }

        [Fact]
        public void All_known_zero_counters_show_zero_movement()
        {
            Assert.True(IsEmpty(
                new ChartSliceModel { RawValue = 0, ValueDisplay = "0 notes" },
                new ChartSliceModel { RawValue = 0, ValueDisplay = "0 pcs" }));
        }

        [Fact]
        public void Any_nonzero_counter_suppresses_zero_movement()
        {
            Assert.False(IsEmpty(
                new ChartSliceModel { RawValue = 0, ValueDisplay = "0 notes" },
                new ChartSliceModel { RawValue = 1, ValueDisplay = "1 pcs" }));
        }
    }
}
