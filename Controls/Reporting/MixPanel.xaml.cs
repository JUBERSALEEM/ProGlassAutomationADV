// ═══════════════════════════════════════════════════════════════════════
//  FILE: Controls/Reporting/MixPanel.xaml.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — advanced reporting, slice 2
//
//  Reusable Pareto ("top N") table. One control serves every mix analysis
//  in reporting — top specifications, top colours, pending by customer —
//  so the tables read identically wherever they appear. An operator
//  learns the shape once: rank, label, proportional bar, measure, share.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Reporting;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Controls.Reporting
{
    public partial class MixPanel : UserControl
    {
        public MixPanel()
        {
            InitializeComponent();
        }

        /// <summary>Panel heading, e.g. "TOP SPECIFICATIONS".</summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title), typeof(string), typeof(MixPanel),
                new PropertyMetadata(string.Empty));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        /// One-line explanation of what the numbers mean. Worth carrying:
        /// a Pareto without its measure stated ("by area" vs "by pieces")
        /// invites the reader to assume the wrong one.
        /// </summary>
        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(
                nameof(Subtitle), typeof(string), typeof(MixPanel),
                new PropertyMetadata(string.Empty));

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        /// <summary>The ranked rows produced by ReportingAnalyticsBuilder.</summary>
        public static readonly DependencyProperty RowsProperty =
            DependencyProperty.Register(
                nameof(Rows), typeof(IEnumerable<MixRow>), typeof(MixPanel),
                new PropertyMetadata(null));

        public IEnumerable<MixRow> Rows
        {
            get => (IEnumerable<MixRow>)GetValue(RowsProperty);
            set => SetValue(RowsProperty, value);
        }
    }
}
