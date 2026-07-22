# Controls/ — Reusable Custom UserControls

## Purpose

Pure-WPF reusable controls for dashboard cards, charts, and hero
elements. No external chart library dependencies — everything drawn
with WPF primitives (Polyline, Rectangle, Path, Border).

Every control is a UserControl with DependencyProperties so it
can be bound and styled like a native WPF control.

## Contents

    GradientHeroCard/     Colored gradient hero card with icon + value + trend
    Sparkline/            Mini line/area chart for stat cards
    MiniBarChart/         Mini bar chart with optional highlight

Each control follows per-feature sub-folder convention:
`{ControlName}.xaml` + `{ControlName}.xaml.cs` inside its own folder.

## GradientHeroCard

Prominent hero card matching screenshot #3 design language (purple/
blue analytics cards). Gradient background with decorative circles,
icon badge top-left, big value in middle, label at bottom, optional
trend badge top-right.

### DependencyProperties

| Property             | Type       | Default    | Description                       |
|----------------------|------------|------------|-----------------------------------|
| Icon                 | string     | ✨         | Emoji shown in badge              |
| Value                | string     | —          | Big value text                    |
| Label                | string     | Total      | Small label under value           |
| TrendArrow           | string     | ↗          | Arrow character in trend badge    |
| TrendText            | string     | (empty)    | Text in trend badge (e.g. 12%)    |
| TrendVisibility      | Visibility | Collapsed  | Show/hide trend badge             |
| GradientStartColor   | Color      | #0F4C3A    | Top-left gradient color           |
| GradientEndColor     | Color      | #22C55E    | Bottom-right gradient color       |

### Preset Gradient Combos

    Green   #0F4C3A → #22C55E   (default, matches splash logo)
    Blue    #1E40AF → #3B82F6   (fintech style)
    Purple  #6D28D9 → #8B5CF6   (analytics)
    Red     #B91C1C → #EF4444   (alerts)
    Amber   #D97706 → #F59E0B   (warnings)

### Usage

    xmlns:hero="clr-namespace:ProGlassAutomation.Controls.GradientHeroCard"

    <hero:GradientHeroCard
        Icon="💰"
        Value="AED 500.00"
        Label="Total Earning"
        TrendArrow="↗"
        TrendText="12%"
        TrendVisibility="Visible"
        GradientStartColor="#6D28D9"
        GradientEndColor="#8B5CF6"
        MinHeight="180"/>

## SparklineControl

Lightweight mini line chart drawn with WPF Path primitives. Used
inside stat cards to show trends alongside a big number.

### DependencyProperties

| Property        | Type   | Default    | Description                        |
|-----------------|--------|------------|------------------------------------|
| Values          | object | null       | Data points (see accepted formats) |
| LineBrush       | Brush  | SteelBlue  | Stroke color                       |
| AreaBrush       | Brush  | SteelBlue  | Fill color (drawn at 25% opacity)  |
| LineThickness   | double | 2.0        | Line stroke thickness              |
| ShowArea        | bool   | true       | Draw filled area under line        |

### Values Formats Accepted

- Comma-separated string: `"1,2,3,4,5"` (also supports `;`, `|`, space)
- `IEnumerable<double>`
- `IEnumerable<decimal>`
- `IEnumerable<int>`

### Usage

    xmlns:spark="clr-namespace:ProGlassAutomation.Controls.Sparkline"

    <spark:SparklineControl
        Height="40"
        Values="12,15,10,18,22,20,25"
        LineBrush="{DynamicResource AccentBrush}"
        AreaBrush="{DynamicResource AccentBrush}"
        LineThickness="2"
        ShowArea="True"/>

### Behavior

- Redraws automatically on SizeChanged
- Redraws when Values, LineBrush, AreaBrush, LineThickness change
- Empty/null Values → renders nothing (safe to bind conditionally)
- Flat data → uses padding to prevent divide-by-zero on range
- Y-axis inverted so higher values appear at top

## MiniBarChartControl

Lightweight bar chart drawn on a Canvas. Simple rounded rectangles.
Optional HighlightIndex to emphasise a specific bar (e.g. "today"
or "peak" value).

### DependencyProperties

| Property         | Type   | Default    | Description                       |
|------------------|--------|------------|-----------------------------------|
| Values           | object | null       | Data points (same formats as Sparkline) |
| BarBrush         | Brush  | SteelBlue  | Default bar color                 |
| HighlightBrush   | Brush  | OrangeRed  | Highlighted bar color             |
| HighlightIndex   | int    | -1         | Bar index to highlight (-1 = none) |
| BarCornerRadius  | double | 3.0        | Rounded corner radius on bar tops |
| BarGap           | double | 3.0        | Gap between bars in pixels        |

### Usage

    xmlns:mbc="clr-namespace:ProGlassAutomation.Controls.MiniBarChart"

    <mbc:MiniBarChartControl
        Height="60"
        Values="30,45,25,50,40,60,55"
        BarBrush="{DynamicResource AccentBrush}"
        HighlightIndex="4"
        HighlightBrush="{DynamicResource AccentDarkBrush}"
        BarCornerRadius="4"
        BarGap="4"/>

### Behavior

- Bar width auto-calculated from container width and count
- Bar height proportional to value / max
- Minimum 2px bar height so zero values still visible
- HighlightIndex out of range → no highlight applied

## Adding a New Control

1. Create sub-folder: `Controls/{ControlName}/`
2. Add `{ControlName}.xaml` and `{ControlName}.xaml.cs`
3. `.xaml.cs` must NOT specify base class (WPF generates it):
   - ✅ `public partial class MyControl`
   - ❌ `public partial class MyControl : UserControl`
4. Expose configurability via DependencyProperty (not fields)
5. Never reference ViewModels — controls are UI-only
6. Use `Application.Current.Resources` for theme brushes if needed
   at runtime (e.g. via `TryFindResource`)
7. Update this README with the new control

## Rules

- **No external chart NuGet packages** — pure WPF primitives only
- **DependencyProperties over CLR properties** for bindability
- **Redraw on relevant DP changes** via `PropertyChangedCallback`
- **Redraw on SizeChanged** for responsive layout
- **Never crash on null/empty data** — safe defaults + early exits
- **No `x:Name` on `GradientStop` inside `LinearGradientBrush`** —
  known WPF quirk with Freezable types, use `FindName` at runtime