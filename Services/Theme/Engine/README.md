# ProGlassThemeEngine

Single publisher after every `ThemePipeline` tick.

```
VisualThemeEngine (family paint: Industrial / Monet / Crystal)
        ↓
ThemeManager.ApplyAccent / surfaces / density
        ↓
ProGlassThemeEngine.Publish()
        ↓
TokenStore (SurfacePrimaryBrush, TextTertiaryBrush, Input*, Tab*, Report*)
```

Do not delete `VisualThemeEngine` or CrystalFluid XAML until `ThemeCompatibilityHub` has zero reverse references in Views.
