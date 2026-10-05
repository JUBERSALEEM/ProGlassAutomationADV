# ProGlassThemeEngine

<!-- pg-nav -->
> **[Repository root](../../../README.md)** · [All documents](../../../docs/README.md) · [Application workflow](../../../docs/WORKFLOW.md) · [Design system](../../../docs/DESIGN_SYSTEM.md)
<!-- pg-nav -->

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

<!-- pg-nav -->
---

*This README documents `Services/Theme/Engine/`. For the end-to-end business chain see [`docs/WORKFLOW.md`](../../../docs/WORKFLOW.md); for every other document see [`docs/README.md`](../../../docs/README.md).*
<!-- pg-nav -->
