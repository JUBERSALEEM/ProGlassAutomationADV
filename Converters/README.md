# Converters

Global XAML value converters. Registered once in `App.xaml` and
available to all views.

---

## Purpose

Convert ViewModel values into UI-friendly types when data binding
directly doesn't fit — e.g., hex string -> Color, bool -> Visibility.

---

## Available Converters

| Converter | Input -> Output | Used By |
|---|---|---|
| `ColorStringConverter` | Hex string ("#10B981") -> Color | Sheet Store History badges |
| `BooleanToVisibilityConverter` | bool -> Visibility (built-in WPF) | All modules (empty states, toggles) |

---

## ColorStringConverter

Converts a hex color string into a WPF `Color` for `SolidColorBrush`.

### XAML

```xml
<Border>
    <Border.Background>
        <SolidColorBrush Color="{Binding HexColor,
                                Converter={StaticResource ColorStringConverter}}"/>
    </Border.Background>
</Border>
```

### ViewModel

```csharp
public string HexColor => "#10B981";  // green
```

### Fallback

Returns `Colors.Gray` if input is null, empty, or invalid.

---

## Global Registration

All converters register in `App.xaml`:

```xml
<Application.Resources>
    <ResourceDictionary>
        <BooleanToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
        <conv:ColorStringConverter x:Key="ColorStringConverter"/>
    </ResourceDictionary>
</Application.Resources>
```

Any XAML can then use `{StaticResource ColorStringConverter}` without
importing.

---

## When To Add a Converter

Add one when:
- Transformation doesn't fit in a ViewModel computed property
- Multiple views need the same transformation
- The value type differs from what XAML expects
  (int -> Visibility, DateTime -> relative time, etc.)

---

## Adding a New Converter

### 1. Create the class

```csharp
// Converters/MyConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    public class MyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            // transform value -> return display representation
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
```

### 2. Register in `App.xaml`

```xml
<conv:MyConverter x:Key="MyConverter"/>
```

### 3. Use in any XAML

```xml
<TextBlock Text="{Binding SomeValue,
                  Converter={StaticResource MyConverter}}"/>
```

### 4. Document in this README

Add row to the "Available Converters" table above.

---

## Planned Converters

- `InverseBooleanToVisibilityConverter` — Visible when `false`
- `NullToVisibilityConverter` — Visible when `null` (placeholders)
- `RelativeTimeConverter` — DateTime -> "2 hours ago"
- `CurrencyFormatConverter` — decimal -> "12,345 AED"
- `ThicknessMmConverter` — int -> "6mm"

---

## Design Principles

1. **Stateless** — no instance fields
2. **Fast** — called on every UI refresh
3. **Safe** — handle null/invalid input with fallback
4. **One-way default** — implement `ConvertBack` only if two-way binding needed
5. **Global scope** — register once in `App.xaml`, use everywhere