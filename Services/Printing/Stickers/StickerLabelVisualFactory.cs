using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    /// <summary>Shared visual layout used by both the on-screen preview and printer pages.</summary>
    public static class StickerLabelVisualFactory
    {
        private static readonly Dictionary<string, BitmapSource?> LogoCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex FieldTokenRegex = new(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Renders the editable template to a physical-size Canvas. The same
        /// renderer is used by the on-screen editor and the printer paginator.
        /// </summary>
        public static Canvas CreateTemplateCanvas(
            GlassStickerLabel label,
            StickerLayoutTemplate template,
            int? displayQuantity = null,
            bool editorMode = false,
            string? selectedElementId = null)
        {
            double widthDip = MmToDip(SafeRange(template.LabelWidthMm, 15, 500, 100));
            double heightDip = MmToDip(SafeRange(template.LabelHeightMm,
                StickerPrinterSpecifications.MinimumTemplateLengthMm,
                StickerPrinterSpecifications.MaximumTemplateLengthMm, 75));
            var canvas = new Canvas
            {
                Width = widthDip,
                Height = heightDip,
                Background = ParseBrush(template.Background, Brushes.White),
                ClipToBounds = true,
                Focusable = editorMode,
                SnapsToDevicePixels = true
            };

            foreach (var element in template.Elements ?? new System.Collections.ObjectModel.ObservableCollection<StickerLayoutElement>())
            {
                if (element == null || !element.IsVisible) continue;
                var visual = CreateTemplateElement(label, element, displayQuantity, editorMode,
                    string.Equals(element.Id, selectedElementId, StringComparison.Ordinal));
                if (visual == null) continue;
                Canvas.SetLeft(visual, MmToDip(SafeRange(element.Xmm, -500, 1000, 0)));
                Canvas.SetTop(visual, MmToDip(SafeRange(element.Ymm, -500, 1000, 0)));
                Canvas.SetZIndex(visual, template.Elements.IndexOf(element));
                canvas.Children.Add(visual);
            }
            return canvas;
        }

        public static string ResolveFieldValue(GlassStickerLabel label, string fieldKey,
            int? displayQuantity = null)
        {
            if (label == null || string.IsNullOrWhiteSpace(fieldKey)) return string.Empty;
            return fieldKey.Trim().ToUpperInvariant() switch
            {
                "COMPANYNAME" => label.Company?.CompanyName ?? string.Empty,
                "COMPANYADDRESS" => label.Company?.Address ?? string.Empty,
                "COMPANYTELEPHONE" => label.Company?.Telephone ?? string.Empty,
                "COMPANYMOBILE" => label.Company?.RepresentativeMobile ?? string.Empty,
                "COMPANYWEBSITE" => label.Company?.Website ?? string.Empty,
                "COMPANYEMAIL" => label.Company?.Email ?? string.Empty,
                "ORDERDISPLAY" => label.OrderDisplay,
                "ORDERNUMBER" => label.OrderNumber ?? string.Empty,
                "JOBORDERNUMBER" => label.JobOrderNumber ?? string.Empty,
                "PROFORMANUMBER" => label.ProformaNumber ?? string.Empty,
                "CUSTOMERCOMPANYNAME" => label.CustomerCompanyName ?? string.Empty,
                "GLASSNAME" => label.GlassName ?? string.Empty,
                "GLASSTYPE" => label.GlassType ?? string.Empty,
                "PANEPOSITION" => label.PanePosition ?? string.Empty,
                "WIDTHMM" => label.WidthMm.ToString(CultureInfo.InvariantCulture),
                "HEIGHTMM" => label.HeightMm.ToString(CultureInfo.InvariantCulture),
                "SIZETEXT" => label.SizeText,
                "QUANTITY" => (displayQuantity ?? label.QuantityToDisplay).ToString(CultureInfo.InvariantCulture),
                "REFERENCE" => label.Reference ?? string.Empty,
                "GLASSREFERENCE" => label.GlassReference ?? string.Empty,
                _ => string.Empty
            };
        }

        private static FrameworkElement? CreateTemplateElement(GlassStickerLabel label,
            StickerLayoutElement element, int? displayQuantity, bool editorMode, bool selected)
        {
            UIElement? content = null;
            string dynamicValue = string.Empty;
            switch (element.Kind)
            {
                case StickerTemplateElementKind.Text:
                    dynamicValue = !string.IsNullOrWhiteSpace(element.FieldKey)
                        ? ResolveFieldValue(label, element.FieldKey, displayQuantity)
                        : ResolveInlineTokens(label, element.Text, displayQuantity);
                    bool missingData = element.HideWhenEmpty && string.IsNullOrWhiteSpace(dynamicValue);
                    // Keep editor and print previews faithful: an empty source field
                    // is omitted rather than shown as a literal [FieldName] token.
                    if (missingData) return null;
                    string displayText = string.IsNullOrWhiteSpace(element.FieldKey)
                        ? dynamicValue
                        : element.Prefix + dynamicValue + element.Suffix;
                    var text = new TextBlock
                    {
                        Text = displayText,
                        FontSize = SafeRange(element.FontSizePt, 3, 72, 9) * 96.0 / 72.0,
                        FontWeight = element.Bold ? FontWeights.Bold : FontWeights.Normal,
                        FontStyle = element.Italic ? FontStyles.Italic : FontStyles.Normal,
                        Foreground = ParseBrush(element.Foreground, Brushes.Black),
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = ToTextAlignment(element.TextAlignment),
                        VerticalAlignment = ToVerticalAlignment(element.ContentAlignment),
                        FontFamily = SafeFontFamily(element.FontFamily)
                    };
                    if (element.Underline) text.TextDecorations = TextDecorations.Underline;
                    content = text;
                    break;

                case StickerTemplateElementKind.Logo:
                    var logo = ResolveLogo(label.Company);
                    bool isSampleData = (label.Source ?? string.Empty)
                        .Contains("DESIGN SAMPLE", StringComparison.OrdinalIgnoreCase);
                    if (logo == null && !isSampleData) return null;
                    content = logo == null
                        ? new TextBlock
                        {
                            Text = "SAMPLE LOGO",
                            Foreground = Brushes.Gray,
                            FontSize = 7,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextAlignment = TextAlignment.Center
                        }
                        : new Image { Source = logo, Stretch = Stretch.Uniform };
                    break;

                case StickerTemplateElementKind.Rectangle:
                case StickerTemplateElementKind.Line:
                    // The outer Border is the visible shape; a nested Border
                    // would draw its fill and stroke twice.
                    content = null;
                    break;
            }

            var border = new Border
            {
                Width = MmToDip(SafeRange(element.WidthMm, 0.5, 500, 10)),
                Height = MmToDip(SafeRange(element.HeightMm, 0.5, 500, 10)),
                Padding = new Thickness(MmToDip(SafeRange(element.PaddingMm, 0, 20, 0))),
                Background = ParseBrush(element.Background, Brushes.Transparent),
                BorderBrush = ParseBrush(element.BorderColor, Brushes.Transparent),
                BorderThickness = new Thickness(MmToDip(SafeRange(element.BorderWidthMm, 0, 10, 0))),
                Opacity = SafeRange(element.Opacity, 0, 1, 1),
                Child = content,
                Tag = element,
                ClipToBounds = true,
                SnapsToDevicePixels = true,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            double rotation = SafeRange(element.RotationDegrees, -360, 360, 0);
            if (Math.Abs(rotation) > 0.001)
                border.RenderTransform = new RotateTransform(rotation);
            if (editorMode)
            {
                border.BorderBrush = selected
                    ? Brushes.DodgerBlue
                    : new SolidColorBrush(Color.FromArgb(72, 52, 64, 84));
                border.BorderThickness = new Thickness(selected ? 1.5 : 0.6);
                border.Cursor = element.IsLocked ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.SizeAll;
                border.ToolTip = element.IsLocked ? element.DisplayName + " (locked)" : element.DisplayName;
            }
            return border;
        }

        private static string ResolveInlineTokens(GlassStickerLabel label, string text, int? displayQuantity)
        {
            return FieldTokenRegex.Replace(text ?? string.Empty,
                match => ResolveFieldValue(label, match.Groups[1].Value, displayQuantity));
        }

        private static Brush ParseBrush(string? value, Brush fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value)) return fallback;
                var color = (Color)ColorConverter.ConvertFromString(value);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return fallback;
            }
        }

        private static FontFamily SafeFontFamily(string? value)
        {
            try { return new FontFamily(string.IsNullOrWhiteSpace(value) ? "Segoe UI" : value); }
            catch { return new FontFamily("Segoe UI"); }
        }

        private static TextAlignment ToTextAlignment(StickerTextAlignment alignment) => alignment switch
        {
            StickerTextAlignment.Center => TextAlignment.Center,
            StickerTextAlignment.Right => TextAlignment.Right,
            StickerTextAlignment.Justify => TextAlignment.Justify,
            _ => TextAlignment.Left
        };

        private static VerticalAlignment ToVerticalAlignment(StickerContentAlignment alignment) => alignment switch
        {
            StickerContentAlignment.Top => VerticalAlignment.Top,
            StickerContentAlignment.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center
        };

        public static FrameworkElement Create(GlassStickerLabel label, StickerPrintPreferences preferences,
            int? displayQuantity = null)
        {
            double width = MmToDip(preferences.LabelWidthMm);
            double height = MmToDip(preferences.LabelHeightMm);
            double padding = MmToDip(preferences.MarginMm);
            double fontSize = preferences.FontSizePt * 96.0 / 72.0;
            double contentWidth = Math.Max(40, width - (padding * 2));

            var body = new StackPanel
            {
                Width = contentWidth,
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            AddCompanyHeader(body, label, preferences, contentWidth, fontSize);
            AddCompanyLines(body, label, preferences, fontSize);
            if (HasCompanyHeader(label, preferences)) AddRule(body);

            if (preferences.ShowOrder && !string.IsNullOrWhiteSpace(label.OrderDisplay))
                body.Children.Add(Field("ORDER", label.OrderDisplay, fontSize * 0.78, bold: true));
            if (preferences.ShowCustomer && !string.IsNullOrWhiteSpace(label.CustomerCompanyName))
                body.Children.Add(Field("CUSTOMER", label.CustomerCompanyName, fontSize * 0.84, bold: true));

            if (preferences.ShowGlassName && !string.IsNullOrWhiteSpace(label.GlassName))
                body.Children.Add(Field("GLASS", label.GlassName, fontSize * 1.15, bold: true, topMargin: 2));

            if (preferences.ShowSize)
            {
                body.Children.Add(new TextBlock
                {
                    Text = label.SizeText,
                    FontSize = fontSize * 1.55,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Black,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 1)
                });
            }

            int quantityToDisplay = displayQuantity ?? label.QuantityToDisplay;
            if (preferences.ShowQuantity && quantityToDisplay > 0)
                body.Children.Add(Field("QTY", quantityToDisplay.ToString(), fontSize * 0.9, bold: true));

            if (preferences.ShowReference && !string.IsNullOrWhiteSpace(label.Reference))
                body.Children.Add(Field("REF", label.Reference, fontSize * 0.78, bold: false));
            if (preferences.ShowReference && !string.IsNullOrWhiteSpace(label.GlassReference)
                && !string.Equals(label.GlassReference, label.GlassName, StringComparison.OrdinalIgnoreCase))
                body.Children.Add(Field("GLASS REF", label.GlassReference, fontSize * 0.72, bold: false));
            if (preferences.ShowPanePosition && !string.IsNullOrWhiteSpace(label.PanePosition))
                body.Children.Add(Field("PANE", label.PanePosition, fontSize * 0.72, bold: true));

            var fit = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Child = body,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            return new Border
            {
                Width = width,
                Height = height,
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0.8),
                Padding = new Thickness(padding),
                SnapsToDevicePixels = true,
                Child = fit
            };
        }

        public static double MmToDip(double mm) => mm * 96.0 / 25.4;
        public static double DipToMm(double dip) => dip * 25.4 / 96.0;

        private static double SafeRange(double value, double minimum, double maximum, double fallback)
        {
            return double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
        }

        private static void AddCompanyHeader(Panel body, GlassStickerLabel label,
            StickerPrintPreferences preferences, double contentWidth, double fontSize)
        {
            bool showName = preferences.ShowCompanyName
                && !string.IsNullOrWhiteSpace(label.Company?.CompanyName);
            BitmapSource? logo = preferences.ShowLogo ? ResolveLogo(label.Company) : null;
            if (!showName && logo == null) return;

            if (logo != null && showName)
            {
                var header = new Grid { Width = contentWidth, Margin = new Thickness(0, 0, 0, 1) };
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.Children.Add(new Image
                {
                    Source = logo,
                    Width = Math.Min(MmToDip(20), contentWidth * 0.22),
                    Height = Math.Min(MmToDip(16), fontSize * 3.4),
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                });
                var name = CompanyNameText(label.Company.CompanyName, fontSize * 1.08);
                Grid.SetColumn(name, 2);
                header.Children.Add(name);
                body.Children.Add(header);
                return;
            }

            if (logo != null)
            {
                body.Children.Add(new Image
                {
                    Source = logo,
                    Width = Math.Min(MmToDip(30), contentWidth * 0.35),
                    Height = Math.Min(MmToDip(18), fontSize * 3.8),
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 1)
                });
            }
            if (showName)
                body.Children.Add(CompanyNameText(label.Company.CompanyName, fontSize * 1.08));
        }

        private static TextBlock CompanyNameText(string value, double fontSize) => new()
        {
            Text = value,
            FontSize = fontSize,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        private static void AddCompanyLines(Panel body, GlassStickerLabel label,
            StickerPrintPreferences preferences, double fontSize)
        {
            var company = label.Company;
            if (company == null) return;

            if (preferences.ShowAddress && !string.IsNullOrWhiteSpace(company.Address))
                body.Children.Add(new TextBlock
                {
                    Text = company.Address,
                    FontSize = fontSize * 0.64,
                    Foreground = Brushes.Black,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 1)
                });

            var contacts = new List<string>();
            bool hasTelephone = !string.IsNullOrWhiteSpace(company.Telephone);
            bool hasMobile = !string.IsNullOrWhiteSpace(company.RepresentativeMobile);
            if (preferences.ShowTelephone && hasTelephone)
                contacts.Add("Tel: " + company.Telephone.Trim());
            if (preferences.ShowMobile && hasMobile)
                contacts.Add("Mobile: " + company.RepresentativeMobile.Trim());
            if (preferences.ShowWebsite && !string.IsNullOrWhiteSpace(company.Website))
                contacts.Add(company.Website.Trim());
            if (preferences.ShowEmail && !string.IsNullOrWhiteSpace(company.Email))
                contacts.Add(company.Email.Trim());

            if (contacts.Count > 0)
                body.Children.Add(new TextBlock
                {
                    Text = string.Join("  ·  ", contacts),
                    FontSize = fontSize * 0.59,
                    Foreground = Brushes.Black,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 1)
                });
        }

        private static bool HasCompanyHeader(GlassStickerLabel label, StickerPrintPreferences preferences)
        {
            return (preferences.ShowLogo && ResolveLogo(label.Company) != null)
                || (preferences.ShowCompanyName && !string.IsNullOrWhiteSpace(label.Company?.CompanyName))
                || (preferences.ShowAddress && !string.IsNullOrWhiteSpace(label.Company?.Address))
                || (preferences.ShowTelephone && !string.IsNullOrWhiteSpace(label.Company?.Telephone))
                || (preferences.ShowMobile && !string.IsNullOrWhiteSpace(label.Company?.RepresentativeMobile))
                || (preferences.ShowWebsite && !string.IsNullOrWhiteSpace(label.Company?.Website))
                || (preferences.ShowEmail && !string.IsNullOrWhiteSpace(label.Company?.Email));
        }

        private static void AddRule(Panel body)
        {
            body.Children.Add(new Border
            {
                Height = 0.7,
                Background = Brushes.Black,
                Margin = new Thickness(0, 2, 0, 3)
            });
        }

        private static TextBlock Field(string title, string value, double fontSize,
            bool bold, double topMargin = 0)
        {
            return new TextBlock
            {
                Text = title + ": " + value,
                FontSize = fontSize,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, topMargin, 0, 0)
            };
        }

        private static BitmapSource? ResolveLogo(StickerCompanyProfile? company)
        {
            if (company == null) return null;
            string base64 = company.LogoBase64 ?? string.Empty;
            string path = company.LogoPath ?? string.Empty;
            string key = !string.IsNullOrWhiteSpace(base64)
                ? "base64:" + base64
                : "path:" + path;

            if (LogoCache.TryGetValue(key, out var cached)) return cached;

            BitmapSource? image = null;
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                if (!string.IsNullOrWhiteSpace(base64))
                {
                    byte[] bytes = Convert.FromBase64String(base64);
                    using var stream = new MemoryStream(bytes);
                    bitmap.StreamSource = stream;
                }
                else if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
                }
                else
                {
                    LogoCache[key] = null;
                    return null;
                }
                bitmap.EndInit();
                bitmap.Freeze();
                image = bitmap;
            }
            catch
            {
                // A missing/moved/corrupt logo must not prevent sticker preview or printing.
            }

            LogoCache[key] = image;
            return image;
        }
    }
}
