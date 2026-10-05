// ═══════════════════════════════════════════════════════════════════════
// FILE: LicenseActivationView.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.Logging;
using ThemeManager = ProGlassAutomation.Services.ThemeManager;

namespace ProGlassAutomation.Views.Licensing
{
        public partial class LicenseActivationView : UserControl
    {
            private Border MakeCard(CornerRadius corner)
        {
            var card = new Border
            {
                Margin = new Thickness(0, 0, 0, 16),
                Padding = new Thickness(20),
                CornerRadius = corner,
                BorderThickness = new Thickness(1)
            };
            card.SetResourceReference(Border.BackgroundProperty, "ModuleBackgroundBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "DividerBrush");
            return card;
        }

        private TextBlock CardLabel(string text) => CardCaption(text);

        private TextBlock CardCaption(string text)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = Sz("TextBody", 13),
                FontWeight = FontWeights.Bold
            };
            label.SetResourceReference(TextElement.ForegroundProperty, "AccentBrush");
            return label;
        }

        private TextBlock Para(string text, double size)
        {
            var para = new TextBlock
            {
                Text = text,
                FontSize = size,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 12)
            };
            para.SetResourceReference(TextElement.ForegroundProperty, "PrimaryForegroundBrush");
            return para;
        }

        private RadioButton MakePlanCard(string title, string caption)
        {
            var body = new StackPanel();
            var titleText = new TextBlock
            {
                Text = title,
                FontSize = Sz("TextBody", 13),
                FontWeight = FontWeights.Bold
            };
            titleText.SetResourceReference(TextElement.ForegroundProperty, "PrimaryForegroundBrush");
            body.Children.Add(titleText);

            var captionText = new TextBlock
            {
                Text = caption,
                FontSize = Sz("TextCaption", 12),
                Margin = new Thickness(0, 2, 0, 0)
            };
            captionText.SetResourceReference(TextElement.ForegroundProperty, "SecondaryForegroundBrush");
            body.Children.Add(captionText);

            var radio = new RadioButton
            {
                Content = body,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 10)
            };

            // Rounded plan card template: accent ring on hover /
            // selection, divider ring otherwise.
            var template = new ControlTemplate(typeof(RadioButton));
            var fef = new FrameworkElementFactory(typeof(Border), "PlanCard");
            fef.SetValue(Border.BackgroundProperty, B("ModuleBackgroundBrush", Brushes.Transparent));
            fef.SetValue(Border.BorderBrushProperty, B("DividerBrush", Brushes.Gray));
            fef.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
            fef.SetValue(Border.CornerRadiusProperty, Cr("CardCornerRadius", new CornerRadius(12)));
            fef.SetValue(Border.PaddingProperty, new Thickness(16, 14, 16, 14));
            fef.SetValue(Border.MinWidthProperty, 150d);
            fef.SetValue(Border.SnapsToDevicePixelsProperty, true);
            fef.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            template.VisualTree = fef;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BorderBrushProperty,
                B("AccentBrush", Brushes.DodgerBlue), "PlanCard"));
            template.Triggers.Add(hover);

            var checkedTrigger = new Trigger { Property = RadioButton.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty,
                B("AccentBrush", Brushes.DodgerBlue), "PlanCard"));
            checkedTrigger.Setters.Add(new Setter(Border.BorderThicknessProperty,
                new Thickness(2), "PlanCard"));
            template.Triggers.Add(checkedTrigger);

            radio.Template = template;
            return radio;
        }

        private Button MakeButton(string text, bool primary)
        {
            var button = new Button
            {
                Content = text,
                Padding = new Thickness(18, 8, 18, 8),
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                MinWidth = primary ? 170 : 130
            };
            if (primary)
            {
                button.SetResourceReference(Control.BackgroundProperty, "AccentBrush");
                button.SetResourceReference(Control.ForegroundProperty, "OnAccentForegroundBrush");
                button.BorderThickness = new Thickness(0);
            }
            else
            {
                button.Background = Brushes.Transparent;
                button.SetResourceReference(Control.BorderBrushProperty, "AccentBrush");
                button.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
                button.BorderThickness = new Thickness(1.5);
            }
            return button;
        }

        // ── Theme-resource helpers (never throw) ──────────────────────

        private double Sz(string key, double fallback) =>
            TryFindResource(key) is double d ? d : fallback;

        private Brush B(string key, Brush fallback) =>
            TryFindResource(key) as Brush ?? fallback;

        private CornerRadius Cr(string key, CornerRadius fallback) =>
            TryFindResource(key) is CornerRadius c ? c : fallback;
    }
}
