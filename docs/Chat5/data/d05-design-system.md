# d05 — Design System Reference

**Purpose:** Complete catalog of the ProGlass Automation design
system: color tokens, typography scale, spacing rules, card patterns,
reusable controls, and usage examples.

Consumed by any AI or human contributor who needs to add or modify
UI code. Prevents style drift across modules.

---

## 1. Design Philosophy

- **Modern SaaS aesthetic** — inspired by Stripe, Linear, Notion
- **Card-based composition** — every UI section lives in a card
- **Generous whitespace** — nothing feels cramped
- **Bold hierarchy** — large numbers, semibold titles, muted labels
- **Semantic color** — colors carry meaning (green=success, red=danger)
- **Instant theme swap** — light + dark work equally well
- **Chart identity preserved** — data-driven colors stay consistent
  across themes (they represent categories, not surfaces)

---

## 2. Color Tokens (25-brush palette)

All brushes defined in `Themes/LightTheme.xaml` and
`Themes/DarkTheme.xaml`. Both dictionaries expose identical keys.

### Surfaces (6)

    WindowBackgroundBrush         Outer page background
    ModuleBackgroundBrush         Card / panel background
    ElevatedSurfaceBrush          Hero cards, prominent surfaces
    SideMenuBackgroundBrush       Sidebar background
    MenuHoverBrush                Hover state, subtle fills
    OverlayButtonBackgroundBrush  Floating action buttons

### Text (5)

    PrimaryForegroundBrush        Main body text
    SecondaryForegroundBrush      Labels, subtitles
    MutedForegroundBrush          Footnotes, hints
    OnAccentForegroundBrush       Text on colored backgrounds
    OverlayButtonForegroundBrush  Floating button text

### Accents — Green Family (4)

    AccentBrush         Primary green (buttons, active tabs)
    AccentHoverBrush    Brighter green on hover
    AccentDarkBrush     Deep green (hero gradient start)
    AccentLightBrush    Bright green (hero gradient end, highlights)

### Semantic (5)

    SuccessBrush   Approved / added / OK           (Green)
    WarningBrush   Pending / inventory / caution   (Amber)
    DangerBrush    Alerts / rejected / low stock   (Red)
    InfoBrush      Sent / bought / neutral info    (Blue)
    NeutralBrush   Draft / gray states             (Gray)

### Tint Backgrounds (7)

Light 50-level pastels in light theme, dark-tinted surfaces in
dark theme. Used for KPI card backgrounds.

    TintIndigoBrush   Primary counts
    TintGreenBrush    Success / approved
    TintAmberBrush    Warning / inventory
    TintBlueBrush     Info / sent
    TintRedBrush      Danger / alert
    TintGrayBrush     Neutral / draft
    TintPurpleBrush   Accent / used

### Borders (2)

    BorderBrush    Standard 1px border
    DividerBrush   Horizontal/vertical dividers

---

## 3. Typography Scale

Defined in `Themes/Styles/TypographyStyles.xaml`.

| Level                | Size | Weight   | Use For                     |
|----------------------|------|----------|-----------------------------|
| Hero number          | 38   | Bold     | Hero card values            |
| Page title           | 30   | Bold     | Page-level titles           |
| Stat number          | 28   | Bold     | KPI card values             |
| Medium number        | 20   | Bold     | Secondary numbers           |
| Section title        | 18   | SemiBold | Section groupings           |
| Row title            | 14   | SemiBold | List row primary text       |
| Page subtitle        | 13   | Normal   | Under page title            |
| Card title           | 13   | SemiBold | Title inside a card         |
| Section subtitle     | 12   | Normal   | Under section title         |
| Row subtitle         | 12   | Normal   | List row metadata           |
| Card subtitle        | 11   | Normal   | Under card value            |
| Meta text            | 11   | Normal   | Dates, tags, footnotes      |
| Trending text        | 11   | Bold     | Trend badge (on accent bg)  |
| Table header         | 11   | Bold     | Column headers              |
| Pill text            | 10   | Bold     | Status pill labels          |

---

## 4. Spacing Rules

### Card Radius

    Small cards / pills           10-14 px
    Standard cards                14-16 px
    Hero cards                    16 px

### Card Padding

    Compact stat cards            18 x 16
    Standard cards                24 (all sides)
    Hero cards                    24 (all sides)
    Task cards                    18 x 14

### Card Margin

    Between grid cards            8 px
    Between list items            0,0,0,10

### Element Spacing

    Icon badge → title (horizontal)     12 px
    Value → subtitle (vertical)         6 px
    Header row → main content           14 px
    Section title → section content     10 px

---

## 5. Card Patterns

Defined in `Themes/Styles/CardStyles.xaml`.

### Base Card

Plain white surface with subtle border. Foundation for most content.

    BaseCardStyle

### Elevated Card

Base card with soft green-tinted drop shadow. Use for prominent
content that should feel "lifted" off the page.

    ElevatedCardStyle

### Tinted Card

Colored pastel background. Background color driven by VM property
`TintColor` (brush key string). Perfect for KPI cards.

    TintedCardStyle

Requires VM property:

    public string TintColor { get; set; } = "TintIndigoBrush";
    public bool IsAlert { get; set; } = false;  // adds red border

### Stat Card

Compact KPI card. Same as BaseCard but smaller padding.

    StatCardStyle

### Task Card

List row card with hover state. Border highlights accent color on
mouse-over.

    TaskCardStyle

### Priority Card

Task card with 4px colored left accent bar. Color driven by VM
property `PriorityColor` (brush key string).

    PriorityCardStyle

Requires VM property:

    public string PriorityColor { get; set; } = "DangerBrush";

### Hero Cards (5 gradient variants)

Prominent gradient cards for hero sections. Use `GradientHeroCard`
reusable control instead of these styles when possible — the
control includes icon badge, big value, label, and trend badge
built-in.

    HeroCardAccentStyle    Green gradient (matches app brand)
    HeroCardInfoStyle      Blue gradient (fintech)
    HeroCardWarningStyle   Amber gradient
    HeroCardDangerStyle    Red gradient
    HeroCardPurpleStyle    Purple gradient

---

## 6. Reusable Controls

Defined in `Controls/{ControlName}/`.

### GradientHeroCard

Full hero card with everything built-in. Use this instead of
`HeroCardStyle` when you need the standard hero layout.

    <hero:GradientHeroCard
        Icon="💰"
        Value="AED 500.00"
        Label="Total Earning"
        TrendArrow="↗"
        TrendText="12%"
        TrendVisibility="Visible"
        GradientStartColor="#0F4C3A"
        GradientEndColor="#22C55E"/>

### SparklineControl

Mini line chart with optional filled area. Use inside stat cards
to show trend alongside a value.

    <spark:SparklineControl
        Height="40"
        Values="12,15,10,18,22,20,25"
        LineBrush="{DynamicResource AccentBrush}"
        AreaBrush="{DynamicResource AccentBrush}"/>

### MiniBarChartControl

Mini bar chart with optional highlighted bar. Use for weekly/daily
activity summaries.

    <mbc:MiniBarChartControl
        Height="60"
        Values="30,45,25,50,40,60,55"
        BarBrush="{DynamicResource AccentBrush}"
        HighlightIndex="4"
        HighlightBrush="{DynamicResource AccentDarkBrush}"/>

---

## 7. Standard Card Compositions

### KPI Card with Sparkline

    <Border Style="{StaticResource TintedCardStyle}">
        <StackPanel>
            <!-- Icon + title row -->
            <StackPanel Orientation="Horizontal">
                <Border Style="{StaticResource IconBadgeAccentStyle}">
                    <TextBlock Text="📄" FontSize="20"
                               Foreground="{DynamicResource OnAccentForegroundBrush}"
                               HorizontalAlignment="Center"
                               VerticalAlignment="Center"/>
                </Border>
                <TextBlock Text="Total Invoices"
                           Style="{StaticResource CardTitleStyle}"
                           VerticalAlignment="Center"
                           Margin="12,0,0,0"/>
            </StackPanel>

            <!-- Big value -->
            <TextBlock Text="1,247"
                       Style="{StaticResource StatNumberStyle}"
                       Margin="0,14,0,0"/>

            <!-- Subtitle -->
            <TextBlock Text="Draft 23  ·  Sent 41  ·  Approved 1,183"
                       Style="{StaticResource CardSubtitleStyle}"/>

            <!-- Sparkline -->
            <spark:SparklineControl
                Height="34"
                Margin="0,12,0,0"
                Values="12,15,18,14,22,25,20"
                LineBrush="{DynamicResource AccentBrush}"
                AreaBrush="{DynamicResource AccentBrush}"/>
        </StackPanel>
    </Border>

### Task Card with Priority + Tags

    <Border Style="{StaticResource PriorityCardStyle}">
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Column="0">
                <TextBlock Text="Design landing page mockup"
                           Style="{StaticResource RowTitleStyle}"/>
                <TextBlock Text="Website Redesign · Nov 24, 2024"
                           Style="{StaticResource RowSubtitleStyle}"/>
                <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                    <Border Style="{StaticResource TagPillStyle}">
                        <TextBlock Text="Design"
                                   Style="{StaticResource TagPillTextStyle}"/>
                    </Border>
                    <Border Style="{StaticResource TagPillStyle}">
                        <TextBlock Text="UI/UX"
                                   Style="{StaticResource TagPillTextStyle}"/>
                    </Border>
                </StackPanel>
            </StackPanel>

            <Border Grid.Column="1"
                    Style="{StaticResource PriorityPillHighStyle}"
                    VerticalAlignment="Top">
                <TextBlock Text="High" Style="{StaticResource PillTextStyle}"/>
            </Border>
        </Grid>
    </Border>

### Activity Feed Row

    <Border Padding="14,12" Margin="0,0,0,8"
            Background="{Binding TintColor,
                         Converter={StaticResource BrushKeyConverter}}"
            CornerRadius="12">
        <Grid>
            <!-- Icon badge -->
            <Border Style="{StaticResource IconBadgeSmallStyle}"
                    Background="{Binding StatusColor,
                                 Converter={StaticResource BrushKeyConverter}}"
                    Margin="0,0,14,0">
                <TextBlock Text="📄" FontSize="18"
                           HorizontalAlignment="Center"
                           VerticalAlignment="Center"/>
            </Border>

            <!-- Title + subtitle -->
            <StackPanel>
                <TextBlock Text="{Binding Title}"
                           Style="{StaticResource RowTitleStyle}"/>
                <TextBlock Text="{Binding SubTitle}"
                           Style="{StaticResource RowSubtitleStyle}"/>
            </StackPanel>

            <!-- Value + status pill -->
            <StackPanel HorizontalAlignment="Right">
                <TextBlock Text="{Binding ValueDisplay}"
                           Style="{StaticResource MediumNumberStyle}"/>
                <Border Style="{StaticResource StatusPillSuccessStyle}"
                        Margin="0,6,0,0"
                        HorizontalAlignment="Right">
                    <TextBlock Text="{Binding StatusLabel}"
                               Style="{StaticResource PillTextStyle}"/>
                </Border>
            </StackPanel>
        </Grid>
    </Border>

---

## 8. Color Mapping Cheatsheet

When choosing a color for a VM-driven card element:

| Data Concept          | Brush Key           | Tint Key           |
|-----------------------|---------------------|--------------------|
| Total / counts        | AccentBrush         | TintIndigoBrush    |
| Success / approved    | SuccessBrush        | TintGreenBrush     |
| Warning / pending     | WarningBrush        | TintAmberBrush     |
| Danger / rejected     | DangerBrush         | TintRedBrush       |
| Info / sent           | InfoBrush           | TintBlueBrush      |
| Neutral / draft       | NeutralBrush        | TintGrayBrush      |
| Accent / used         | AccentBrush         | TintPurpleBrush    |

Example VM code:

    var totalInvoices = new KpiCardModel
    {
        Icon         = "📄",
        Title        = "Total Invoices",
        DisplayValue = "1,247",
        BadgeColor   = "AccentBrush",       // green icon
        TintColor    = "TintIndigoBrush"    // indigo card bg
    };

    var approved = new KpiCardModel
    {
        Icon         = "✅",
        Title        = "Approved",
        DisplayValue = "1,183",
        BadgeColor   = "SuccessBrush",
        TintColor    = "TintGreenBrush"
    };

---

## 9. Chart Bar Colors

Chart bars (in `ChartBarModel.BarColor`) intentionally use hex
strings for consistent visual identity across themes. Standard
palette for chart bars:

    #6366F1   Indigo (primary series)
    #10B981   Green (success / positive)
    #F59E0B   Amber (warning)
    #3B82F6   Blue (info)
    #EC4899   Pink (highlight)
    #8B5CF6   Purple (accent)
    #EF4444   Red (danger)
    #6B7280   Gray (neutral)

Use `ColorStringConverter` (not `BrushKeyConverter`) to bind these.

---

## 10. Do's and Don'ts

### DO

- Use `{DynamicResource}` for theme-adaptive brushes
- Use `{StaticResource}` for style dictionary references
- Reference styles from `Themes/Styles/` — never inline
- Bind card colors via brush key strings (VM sets `"AccentBrush"`)
- Use `BrushKeyConverter` for theme-adaptive card backgrounds
- Use `ColorStringConverter` for chart data colors
- Set `MinHeight` on hero cards for visual weight
- Give KPI cards a consistent 3-column or 4-column grid

### DON'T

- Don't hardcode hex colors in XAML (breaks theme swap)
- Don't inline card styling with inline `Setter` properties
- Don't use `x:Name` on `GradientStop` (unreliable in Freezable types)
- Don't set VM color property to hex when using `BrushKeyConverter`
- Don't register the same converter in multiple ResourceDictionaries
  (causes "incompatible type" error)
- Don't add `: UserControl` or `: ResourceDictionary` to code-behind
  partial class declarations (causes CS0263 conflict)
- Don't override styles that already exist — extend via `BasedOn`

---

## 11. File Locations

    Themes/
        LightTheme.xaml              25-brush palette (light values)
        DarkTheme.xaml               25-brush palette (dark values)
        Styles/
            TypographyStyles.xaml    Text hierarchy
            IconBadgeStyles.xaml     Icon badge shapes + colors
            PillStyles.xaml          Status/priority/tag pills
            CardStyles.xaml          Card containers + hero gradients
            README.md                Style catalog

    Controls/
        GradientHeroCard/            Hero card with gradient
        Sparkline/                   Mini line chart
        MiniBarChart/                Mini bar chart
        README.md                    Control catalog

    Converters/
        BrushKeyConverter.cs         string → theme brush
        ColorStringConverter.cs      hex → SolidColorBrush

---

## 12. When To Update This Doc

- Adding a new color token → update Section 2
- Adding a new text style → update Section 3 table
- Adding a new card pattern → update Section 5 + example
- Adding a new reusable control → update Section 6 + example
- Discovering a new gotcha → update Section 10