using ProGlassAutomation.Services.Licensing;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.Views.Licensing
{
    /// <summary>
    /// Post-activation success popup. Shown immediately after a pasted or
    /// browsed license becomes runnable so the operator sees the edition,
    /// active window and scope in one clean confirmation surface.
    /// </summary>
    public sealed class LicenseActivatedDialog : Window
    {
        public LicenseActivatedDialog(LicenseVerificationResult result)
        {
            var payload = result?.Payload;
            string edition = payload?.Edition.ToString() ?? "License";
            string customer = string.IsNullOrWhiteSpace(payload?.CustomerId)
                ? (payload?.CompanyName ?? "Activated customer")
                : payload!.CustomerId + (string.IsNullOrWhiteSpace(payload.CompanyName)
                    ? string.Empty
                    : " — " + payload.CompanyName);
            DateTime start = payload?.IssuedAt.ToLocalTime() ?? DateTime.Now;
            DateTime end = payload?.EffectiveExpiresAt.ToLocalTime() ?? DateTime.Now;
            int totalDays = payload?.TotalDays ?? 0;
            int remainingDays = 0;
            try
            {
                remainingDays = payload == null
                    ? 0
                    : Math.Max(0, (int)Math.Ceiling((payload.EffectiveExpiresAt - DateTime.UtcNow).TotalDays));
            }
            catch { }

            string scope = string.IsNullOrWhiteSpace(payload?.MachineFingerprint)
                ? (payload == null
                    ? "Portable"
                    : payload.MaxActivations >= 9999
                        ? "Portable · high-volume activation allowance"
                        : $"Portable · up to {payload.MaxActivations} device activation(s)")
                : "Machine-bound · locked to the licensed fingerprint";

            Title = "License activated";
            Width = 620;
            Height = 430;
            MinWidth = 560;
            MinHeight = 390;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(0x08, 0x12, 0x1F));
            Foreground = Brushes.White;
            WindowStyle = WindowStyle.ToolWindow;
            ShowInTaskbar = false;

            var root = new Grid { Margin = new Thickness(22) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hero = new Border
            {
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(22, 20, 22, 18),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x10, 0xA3, 0x5A),
                    Color.FromRgb(0x1D, 0x4E, 0xD8),
                    25),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };

            var heroGrid = new Grid();
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var badge = new Border
            {
                Width = 58,
                Height = 58,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                Margin = new Thickness(0, 0, 18, 0)
            };
            badge.Child = new TextBlock
            {
                Text = "✓",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };
            heroGrid.Children.Add(badge);

            var heroText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(heroText, 1);
            heroText.Children.Add(new TextBlock
            {
                Text = "License activated successfully",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            heroText.Children.Add(new TextBlock
            {
                Text = totalDays > 0
                    ? $"{edition} · {remainingDays} day(s) remaining out of {totalDays}"
                    : edition,
                FontSize = 13,
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(0xEC, 0xFE, 0xFF))
            });
            heroText.Children.Add(new TextBlock
            {
                Text = customer,
                FontSize = 13,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))
            });
            heroGrid.Children.Add(heroText);
            hero.Child = heroGrid;
            root.Children.Add(hero);

            var facts = new Border
            {
                Margin = new Thickness(0, 18, 0, 18),
                Padding = new Thickness(22, 18, 22, 18),
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x1A, 0x2C)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x3A, 0x57)),
                BorderThickness = new Thickness(1)
            };
            Grid.SetRow(facts, 1);

            var factsGrid = new Grid();
            factsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            factsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 5; i++)
                factsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            AddFact(factsGrid, 0, "STARTING FROM", start.ToString("dd MMM yyyy · HH:mm"));
            AddFact(factsGrid, 1, "VALID UNTIL", end.ToString("dd MMM yyyy · HH:mm"));
            AddFact(factsGrid, 2, "LICENSE WINDOW", totalDays > 0 ? totalDays + " day(s)" : "Open-ended");
            AddFact(factsGrid, 3, "ACTIVATION MODE", scope);
            AddFact(factsGrid, 4, "STATUS", LicenseUiText.StatusBadge(result.Status));

            facts.Child = factsGrid;
            root.Children.Add(facts);

            var footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(footer, 2);

            footer.Children.Add(new TextBlock
            {
                Text = "About → License refreshes live with this activation.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                VerticalAlignment = VerticalAlignment.Center
            });

            var close = new Button
            {
                Content = "DONE",
                Padding = new Thickness(18, 8, 18, 8),
                MinWidth = 110,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(0x10, 0xA3, 0x5A)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Transparent
            };
            close.Click += (_, __) => DialogResult = true;
            Grid.SetColumn(close, 1);
            footer.Children.Add(close);
            root.Children.Add(footer);

            Content = root;
        }

        public static void ShowFor(Window? owner, LicenseVerificationResult result)
        {
            var dialog = new LicenseActivatedDialog(result);
            if (owner != null && !ReferenceEquals(owner, dialog))
                dialog.Owner = owner;
            dialog.ShowDialog();
        }

        private static void AddFact(Grid grid, int row, string label, string value)
        {
            var title = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),
                Margin = new Thickness(0, row == 0 ? 0 : 10, 14, 0)
            };
            Grid.SetRow(title, row);
            grid.Children.Add(title);

            var body = new TextBlock
            {
                Text = value,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                Margin = new Thickness(0, row == 0 ? 0 : 10, 0, 0)
            };
            Grid.SetRow(body, row);
            Grid.SetColumn(body, 1);
            grid.Children.Add(body);
        }
    }
}
