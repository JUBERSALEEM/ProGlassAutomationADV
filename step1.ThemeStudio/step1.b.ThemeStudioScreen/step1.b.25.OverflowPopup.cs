using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ProGlassAutomation.Analytics;

namespace ProGlassAutomation.ThemeStudio;

/// <summary>Raises a full-window spotlight while an overflow flyout is open.</summary>
public static class OverflowSpotlight
{
    public static event Action<bool>? Changed;
    public static void Set(bool on) => Changed?.Invoke(on);
}

/// <summary>
/// Compact mosaic host. When the child needs more height than the slot,
/// an Expand button opens a connected popup with a full, scrollable copy.
/// Hover never opens or blurs — that is accidental and unprofessional.
/// </summary>
public sealed class OverflowPopup : ContentControl
{
    private static OverflowPopup? active;
    private readonly Popup popup = new();
    private readonly ScrollViewer scroller = new();
    private readonly ContentControl frame = new();
    private readonly Button close = new();
    private Button? expand;
    private bool overflowing;
    private Window? hostWindow;

    public OverflowPopup()
    {
        Focusable = false;
        ClipToBounds = true;
        Template = BuildTemplate();
        scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        close.Content = "Close";
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => CloseFlyout();
        AutomationProperties.SetName(close, "Close expanded card");
        var chrome = new DockPanel();
        DockPanel.SetDock(close, Dock.Top);
        chrome.Children.Add(close);
        chrome.Children.Add(scroller);
        frame.Content = chrome;
        popup.AllowsTransparency = true;
        popup.StaysOpen = true;
        popup.Placement = PlacementMode.Center;
        popup.Child = frame;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (expand is not null) expand.Click -= OnExpand;
        expand = GetTemplateChild("PART_Expand") as Button;
        if (expand is null) return;
        expand.Click += OnExpand;
        expand.SetResourceReference(PaddingProperty, "PaddingCompact");
        expand.SetResourceReference(FontSizeProperty, "FontSizeCaption");
        if (TryFindResource("SubtleButton") is Style subtle) expand.Style = subtle;
        expand.MinWidth = 0;
        expand.MinHeight = 0;
        SyncExpand();
    }

    protected override Size MeasureOverride(Size available)
    {
        if (popup.IsOpen) return base.MeasureOverride(available);
        if (Content is UIElement child)
        {
            double width = double.IsInfinity(available.Width) ? 240 : Math.Max(0, available.Width);
            child.Measure(new Size(width, double.PositiveInfinity));
            overflowing = !double.IsInfinity(available.Height) && child.DesiredSize.Height > available.Height + 1;
            SyncExpand();
        }
        return base.MeasureOverride(available);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        OverflowSpotlight.Changed -= OnSpotlight;
        OverflowSpotlight.Changed += OnSpotlight;
        if (TryFindResource("Card") is Style card) frame.Style = card;
        if (TryFindResource("SubtleButton") is Style subtle)
        {
            close.Style = subtle;
            if (expand is not null) expand.Style = subtle;
        }
        close.SetResourceReference(PaddingProperty, "PaddingCompact");
        close.SetResourceReference(FontSizeProperty, "FontSizeCaption");
        close.MinWidth = 0;
        close.MinHeight = 0;
        popup.PlacementTarget = this;
        ApplyTemplate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        OverflowSpotlight.Changed -= OnSpotlight;
        DetachEscape();
        CloseFlyout();
    }

    private void OnSpotlight(bool on)
    {
        if (!on && popup.IsOpen) CloseFlyout();
    }

    private void OnExpand(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (popup.IsOpen) CloseFlyout();
        else ShowFlyout();
    }

    private void ShowFlyout()
    {
        if (!overflowing || popup.IsOpen || ActualWidth <= 0 || Content is not FrameworkElement source) return;
        if (source.GetType().GetConstructor(Type.EmptyTypes) is null) return;
        if (Activator.CreateInstance(source.GetType()) is not FrameworkElement copy) return;
        copy.DataContext = source.DataContext;
        if (source is BentoChart chart && copy is BentoChart clone)
        {
            clone.Data = chart.Data;
            clone.PlotHeight = chart.PlotHeight;
        }
        if (active is not null && !ReferenceEquals(active, this)) active.CloseFlyout();
        scroller.Content = copy;
        SizeFlyout();
        popup.IsOpen = true;
        active = this;
        AttachEscape();
        OverflowSpotlight.Set(true);
        SyncExpand();
    }

    private void SizeFlyout()
    {
        var host = Window.GetWindow(this);
        double width;
        double height;
        if (host is not null && host.ActualWidth > 0 && host.ActualHeight > 0)
        {
            width = Math.Max(host.ActualWidth - 48, 320);
            height = Math.Max(host.ActualHeight - 48, 400);
            popup.PlacementTarget = host;
            popup.Placement = PlacementMode.Center;
            popup.HorizontalOffset = 0;
            popup.VerticalOffset = 0;
        }
        else
        {
            width = Math.Max(ActualWidth, 280);
            height = Math.Max(ActualHeight, 400);
            popup.PlacementTarget = this;
            popup.Placement = PlacementMode.Bottom;
        }
        scroller.Width = width;
        scroller.Height = Math.Max(height - 56, 320);
        scroller.MaxHeight = scroller.Height;
        frame.Width = width;
        frame.MinHeight = height;
        frame.MaxHeight = height;
    }

    private void CloseFlyout()
    {
        if (!popup.IsOpen) return;
        popup.IsOpen = false;
        scroller.Content = null;
        DetachEscape();
        if (!ReferenceEquals(active, this)) return;
        active = null;
        OverflowSpotlight.Set(false);
        SyncExpand();
    }

    private void SyncExpand()
    {
        if (expand is null) return;
        expand.Visibility = overflowing ? Visibility.Visible : Visibility.Collapsed;
        expand.IsEnabled = overflowing;
    }

    private void AttachEscape()
    {
        DetachEscape();
        hostWindow = Window.GetWindow(this);
        if (hostWindow is null) return;
        hostWindow.PreviewKeyDown += OnHostKey;
        hostWindow.SizeChanged += OnHostSize;
    }

    private void DetachEscape()
    {
        if (hostWindow is not null)
        {
            hostWindow.PreviewKeyDown -= OnHostKey;
            hostWindow.SizeChanged -= OnHostSize;
        }
        hostWindow = null;
    }

    private void OnHostKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !popup.IsOpen) return;
        CloseFlyout();
        e.Handled = true;
    }

    private void OnHostSize(object sender, SizeChangedEventArgs e)
    {
        if (popup.IsOpen) SizeFlyout();
    }

    private static ControlTemplate BuildTemplate()
    {
        var grid = new FrameworkElementFactory(typeof(Grid));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentSourceProperty, "Content");
        presenter.SetValue(ClipToBoundsProperty, true);
        var button = new FrameworkElementFactory(typeof(Button));
        button.Name = "PART_Expand";
        button.SetValue(ContentProperty, "Expand");
        button.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Right);
        button.SetValue(VerticalAlignmentProperty, VerticalAlignment.Top);
        button.SetValue(MinWidthProperty, 0d);
        button.SetValue(MinHeightProperty, 0d);
        button.SetValue(MarginProperty, new Thickness(4));
        button.SetValue(Panel.ZIndexProperty, 1);
        button.SetValue(AutomationProperties.NameProperty, "Expand card");
        grid.AppendChild(presenter);
        grid.AppendChild(button);
        return new ControlTemplate(typeof(OverflowPopup)) { VisualTree = grid };
    }
}
