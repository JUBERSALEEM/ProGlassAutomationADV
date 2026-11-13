# Typed theme consumer contract — nonvisual

ThemeContract.cs is authoritative. Current coverage: 104 semantic roles, 47 required exact WPF style targets. All concrete values/templates belong to each theme. Registered packages: [Bento](../ThemeBento/ThemeBento.Readme.md) (default, owns customization) and [Claymorphism](../ThemeClaymorphism/ThemeClaymorphism.Readme.md). There is no global visual dictionary.

## Resource roles

| Key | Native type |
| --- | --- |
| `HeroBackground` | `Brush` |
| `HeroForeground` | `Brush` |
| `HeroSecondaryText` | `Brush` |
| `HeroAccent` | `Brush` |
| `ApplicationBackground` | `Brush` |
| `SurfacePrimary` | `Brush` |
| `SurfaceSecondary` | `Brush` |
| `SurfaceElevated` | `Brush` |
| `TextPrimary` | `Brush` |
| `TextSecondary` | `Brush` |
| `TextMuted` | `Brush` |
| `AccentPrimary` | `Brush` |
| `AccentSecondary` | `Brush` |
| `TextOnAccent` | `Brush` |
| `AccentSoft` | `Brush` |
| `BorderSubtle` | `Brush` |
| `BorderStrong` | `Brush` |
| `FocusIndicator` | `Brush` |
| `StateHover` | `Brush` |
| `StatePressed` | `Brush` |
| `StateSelected` | `Brush` |
| `AccentHover` | `Brush` |
| `AccentPressed` | `Brush` |
| `DisabledSurface` | `Brush` |
| `DisabledText` | `Brush` |
| `StatusSuccess` | `Brush` |
| `StatusSuccessSurface` | `Brush` |
| `StatusWarning` | `Brush` |
| `StatusWarningSurface` | `Brush` |
| `StatusError` | `Brush` |
| `StatusErrorSurface` | `Brush` |
| `StatusInformation` | `Brush` |
| `StatusInformationSurface` | `Brush` |
| `OverlayBrush` | `Brush` |
| `TransparentBrush` | `Brush` |
| `FontSizeDisplay` | `double` |
| `FontSizeTitle` | `double` |
| `FontSizeSection` | `double` |
| `FontSizeBody` | `double` |
| `FontSizeCaption` | `double` |
| `SpacingSmall` | `double` |
| `SpacingMedium` | `double` |
| `SpacingLarge` | `double` |
| `SpacingSection` | `double` |
| `SpacingPage` | `double` |
| `ControlMinHeight` | `double` |
| `ControlMinWidth` | `double` |
| `ChoiceMarkSize` | `double` |
| `ScrollBarSize` | `double` |
| `DisabledOpacity` | `double` |
| `PopupMaxHeight` | `double` |
| `ReferenceListMaxHeight` | `double` |
| `StudioSidebarWidth` | `double` |
| `WindowInitialWidth` | `double` |
| `WindowInitialHeight` | `double` |
| `WindowMinWidth` | `double` |
| `WindowMinHeight` | `double` |
| `PaddingControl` | `Thickness` |
| `PaddingCard` | `Thickness` |
| `PaddingPlate` | `Thickness` |
| `PaddingPage` | `Thickness` |
| `MarginItem` | `Thickness` |
| `MarginSection` | `Thickness` |
| `MarginInline` | `Thickness` |
| `MarginPanel` | `Thickness` |
| `BorderThicknessControl` | `Thickness` |
| `FocusThickness` | `Thickness` |
| `PaddingChoice` | `Thickness` |
| `PaddingCompact` | `Thickness` |
| `CardCornerRadius` | `CornerRadius` |
| `ControlCornerRadius` | `CornerRadius` |
| `ChipCornerRadius` | `CornerRadius` |
| `FontFamilyBody` | `FontFamily` |
| `FontFamilyMono` | `FontFamily` |
| `FontWeightHeading` | `FontWeight` |
| `FontWeightBody` | `FontWeight` |
| `FontStyleBody` | `FontStyle` |
| `TextDecorationsBody` | `TextDecorationCollection` |
| `PaddingMark` | `Thickness` |
| `PaddingSelection` | `Thickness` |
| `ChoiceCornerRadius` | `CornerRadius` |
| `StrokeWidth` | `double` |
| `SelectionOpacity` | `double` |
| `MarginFocus` | `Thickness` |

Thickness roles represent padding, margins and edges; MarginFocus intentionally extends an adorner, not a negative control size. Radius/font/weight resources use actual native types. Brush allows native gradients; HeroBackground is a gradient, not a SolidColorBrush guarantee. StudioSidebarWidth and ReferenceListMaxHeight are actual layout consumer roles, not screen-local palettes.

## Required styles

| Role | Exact TargetType |
| --- | --- |
| `ThemedWindow` | `Window` |
| `Caption` | `TextBlock` |
| `SectionHeading` | `TextBlock` |
| `PageHeading` | `TextBlock` |
| `Card` | `Border` |
| `Plate` | `Border` |
| `AccentPlate` | `Border` |
| `ElevatedPlate` | `Border` |
| `TokenValue` | `TextBlock` |
| `KeyboardFocus` | `Control` |
| `HeroCard` | `Border` |
| `BentoTile` | `Border` |
| `MetricCard` | `Border` |
| `StatusBadge` | `Border` |
| `StatusText` | `TextBlock` |
| `PrimaryButton` | `Button` |
| `SubtleButton` | `Button` |
| `DestructiveButton` | `Button` |
| `implicit TabControl` | `TabControl` |
| `implicit TabItem` | `TabItem` |
| `implicit TextBlock` | `TextBlock` |
| `implicit Button` | `Button` |
| `implicit TextBox` | `TextBox` |
| `implicit ComboBox` | `ComboBox` |
| `implicit ComboBoxItem` | `ComboBoxItem` |
| `implicit CheckBox` | `CheckBox` |
| `implicit RadioButton` | `RadioButton` |
| `implicit ListBox` | `ListBox` |
| `implicit ListBoxItem` | `ListBoxItem` |
| `implicit ToolTip` | `ToolTip` |
| `implicit ContextMenu` | `ContextMenu` |
| `implicit MenuItem` | `MenuItem` |
| `implicit Thumb` | `Thumb` |
| `implicit ScrollBar` | `ScrollBar` |
| `implicit Separator` | `Separator` |
| `implicit ProgressBar` | `ProgressBar` |

StatusBadge and StatusText accept semantic severity in Tag (Success/Warning/Information/Error/Neutral); the sample view model maps business status to severity. No business palette is embedded in views. Each theme implements its own setters/templates for these roles. Private helpers are allowed but cannot duplicate public or sibling keys. ActivityTrendGeometry is a Bento-private immutable helper, not a public consumer key.

## Loading, customization and lifecycle

Catalog registers only complete actual themes. A factory returns the entire candidate. ThemeService independently validates token types/numeric ranges, style targets and duplicate merged keys, then replaces one package slot. Optional Action<ResourceDictionary, Appearance> configuration runs before validation; its retained active-theme callback never crosses to another theme identity. Theme-specific rules are outside this service. Callbacks should capture immutable values, not page/control lifetime. Failure before activation leaves the prior package/configuration intact. ThemeNotificationException specifically means activation succeeded but one or more consumers failed; do not report unchanged state. Remaining consumers still receive notifications.

Bento owns its specific composer: sealed Style and frozen Geometry identities may be cached, but appearance brushes/key dictionaries are fresh. An authorized future theme owns its own composer and private helpers, not this implementation. Additional controls require complete role/state coverage in every implemented theme. No per-theme application or copied screens.

## Future authorized theme process

1. Obtain approval and implement its stable Theme/ThemeName directory, complete Light/Dark maps and visual templates independently.
2. Supply the current typed consumer roles and style targets, its own research/state/accessibility expectations and private composition.
3. Validate complete candidates without the active app as fallback, including negative styles, duplicate/cross-theme keys, mutation isolation and native switching/input/dialog tests.
4. Register the actual factory. Business Reference and other consuming pages keep using active semantic resources. Bento-specific customization is disabled for other identities; never apply Bento settings as another theme's visual engine.
5. Build/run on Windows and visually compare both modes before approval. No future placeholder was added.

Verification workflow is intentionally inactive at [cols/ci/windows-wpf.yml](../../cols/ci/windows-wpf.yml). It has not run for these changes. Portable checks do not establish C#/XAML compilation, native rendering, accessibility or runtime extensibility.

## Analytics extension

Semantic plate roles describe purpose, not required colors. Each theme owns their treatment. Chart paint roles explicitly require SolidColorBrush because the Skia adapter consumes solid RGBA paints, while existing general Brush roles still support gradients. No actual paint/color lives in this contract.

| Key | Native type |
| --- | --- |
| `SurfacePositive` | `SolidColorBrush` |
| `SurfaceFeature` | `SolidColorBrush` |
| `SurfaceInformation` | `SolidColorBrush` |
| `SurfaceAttention` | `SolidColorBrush` |
| `ChartSeries1` | `SolidColorBrush` |
| `ChartSeries2` | `SolidColorBrush` |
| `ChartSeries3` | `SolidColorBrush` |
| `ChartSeries4` | `SolidColorBrush` |
| `ChartAxis` | `SolidColorBrush` |
| `ChartGrid` | `SolidColorBrush` |
| `ChartTooltipSurface` | `SolidColorBrush` |
| `ChartTooltipText` | `SolidColorBrush` |
| `ChartPlotHeight` | `double` |
| `ChartLineWidth` | `double` |
| `ChartPointSize` | `double` |
| `ChartDonutRadius` | `double` |
| `ChartAreaOpacity` | `double` |
| `BentoRowHeight` | `double` |
| `BentoGutter` | `double` |
| `BentoCompactWidth` | `double` |

New exact style targets: BentoTile/ChartCard/PositivePlate/FeaturePlate/InformationPlate/AttentionPlate → Border; BentoChart, BentoKpi, DataGrid, DataGridRow, DataGridCell and DataGridColumnHeader → their named control types. `BentoGrid` is a Bento-owned Panel, not a resource style. Full reuse/API details are in [step1.f](../step1.f.Analytics/step1.f.6.Analytics-README.md) and [c.11](../../step1.ThemeStudio/step1.c.docs/step1.c.11.Bento-Grid-Engine.md).
