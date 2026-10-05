# d05 — Design System Reference

**Version:** 3.0 (Chat 10 update — through Phase 10.9a)

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
- **Compact when it matters** — dashboards and dense lists trade
  padding for scannable info density (Phase 10.9a)
- **Smooth pixel scrolling** — website-like feel on dashboards

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

    SuccessBrush   Approved / added / OK / Delivered     (Green)
    WarningBrush   Pending / inventory / caution         (Amber)
    DangerBrush    Alerts / rejected / low stock         (Red)
    InfoBrush      Sent / bought / received              (Blue)
    NeutralBrush   Draft / gray states                   (Gray)

### Tint Backgrounds (7)

Light 50-level pastels in light theme, dark-tinted surfaces in
dark theme. Used for KPI card backgrounds.

    TintIndigoBrush   Primary counts / Dispatched DNs
    TintGreenBrush    Success / approved / Delivered
    TintAmberBrush    Warning / inventory / DN Prepared
    TintBlueBrush     Info / sent / Received
    TintRedBrush      Danger / alert / Pending Balance
    TintGrayBrush     Neutral / draft / Dummy DN
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
| Stat number          | 28   | Bold     | Large KPI card values       |
| Nested tile value    | 18   | Bold     | Phase 10.9a nested tiles    |
| Section title        | 18   | SemiBold | Section groupings           |
| Detail hero value    | 26   | Bold     | DN detail 4-stat tiles      |
| Row title            | 14   | SemiBold | List row primary text       |
| Parent card title    | 14   | Bold     | Phase 10.9a parent cards    |
| Page subtitle        | 13   | Normal   | Under page title            |
| Card title           | 13   | SemiBold | Title inside a card         |
| Section subtitle     | 12   | Normal   | Under section title         |
| Row subtitle         | 12   | Normal   | List row metadata           |
| Card subtitle        | 11   | Normal   | Under card value            |
| Meta text            | 11   | Normal   | Dates, tags, footnotes      |
| Trending text        | 11   | Bold     | Trend badge (on accent bg)  |
| Chat row title       | 11   | Bold     | Phase 10.9a chat activity   |
| Table header         | 11   | Bold     | Column headers              |
| Pill text            | 10   | Bold     | Status pill labels          |
| Nested tile label    | 10   | Bold     | Phase 10.9a tile labels     |
| Chat row subtitle    | 10   | Normal   | Phase 10.9a chat activity   |

---

## 4. Spacing Rules

### Card Radius

    Small cards / pills                10-14 px
    Standard cards                     14-16 px
    Hero cards                         16 px
    Parent cards (Phase 10.9a)         16 px
    Nested tiles (Phase 10.9a)         10 px
    Activity chat rows (Phase 10.9a)   10 px

### Card Padding

    Compact stat cards                 18 x 16
    Standard cards                     24 (all sides)
    Hero cards                         24 (all sides)
    Task cards                         18 x 14
    Parent cards (Phase 10.9a)         18 x 16
    Nested tiles (Phase 10.9a)         12 x 10
    DN detail action bar               14 x 10
    DN detail hero header              18 x 14

### Card Margin

    Between grid cards                 8 px
    Parent cards (Phase 10.9a)         6 px  (tighter)
    Between list items                 0,0,0,10
    Between chat rows                  0,0,0,6

### Element Spacing

    Icon badge → title (horizontal)     10-12 px
    Value → subtitle (vertical)         4-6 px
    Header row → main content           14 px
    Section title → section content     10 px
    Nested tile icon → label            6 px

---

## 5. Card Patterns

Defined in `Themes/Styles/CardStyles.xaml`.

### 5.1 Base Card

Plain white surface with subtle border. Foundation for most content.

    BaseCardStyle

### 5.2 Elevated Card

Base card with soft green-tinted drop shadow.

    ElevatedCardStyle

### 5.3 Tinted Card

Colored pastel background. Driven by VM property `TintColor`
(brush key string).

    TintedCardStyle

Requires VM:

    public string TintColor { get; set; } = "TintIndigoBrush";
    public bool IsAlert { get; set; } = false;

### 5.4 Stat Card

Compact KPI card. Smaller padding.

    StatCardStyle

### 5.5 Task Card

List row card with hover state.

    TaskCardStyle

### 5.6 Priority Card

Task card with 4px colored left accent bar.

    PriorityCardStyle

### 5.7 Hero Cards (5 gradient variants)

    HeroCardAccentStyle    Green gradient (matches app brand)
    HeroCardInfoStyle      Blue gradient (fintech)
    HeroCardWarningStyle   Amber gradient
    HeroCardDangerStyle    Red gradient
    HeroCardPurpleStyle    Purple gradient

Use `GradientHeroCard` control instead when possible.

### 5.8 Parent Card (Phase 10.9a — NEW)

Container card that holds a grid of nested tiles. Used on Overview
dashboard.

    ParentCardStyle (inline in OverviewDashboardView.xaml)

Structure:
- Standard border + CornerRadius=16 + Padding 18,16
- Header row: colored icon badge (34×34, CornerRadius=9) + title
- Body: `UniformGrid` of nested tiles (2×2 or 2×3)

Header icon colors (brand-locked hex):
- 💰 Sales      → `#059669`
- 🏭 Production → `#1E40AF`
- 🚚 Delivery   → `#B45309`
- 📦 Inventory  → `#B91C1C`
- 💬 Activity   → `#4338CA`
- 📈 Chart      → `#0F766E`

### 5.9 Nested Tile (Phase 10.9a — NEW)

Small colored tile inside a parent card. Accent-driven via VM
property `Accent` string ("Green"|"Blue"|"Amber"|"Red").

Structure:
- CornerRadius=10, Padding=12,10, Margin=4
- Icon (14pt) + Label (10pt Bold) row
- Value (18pt Bold) below

Color mapping:

| Accent | Background | Border   | Text (label + value) |
|--------|------------|----------|----------------------|
| Green  | `#ECFDF5`  | `#A7F3D0`| `#065F46` / `#065F46`|
| Blue   | `#EFF6FF`  | `#BFDBFE`| `#1E40AF` / `#1E3A8A`|
| Amber  | `#FFFBEB`  | `#FDE68A`| `#92400E` / `#78350F`|
| Red    | `#FEF2F2`  | `#FECACA`| `#991B1B` / `#7F1D1D`|

IsAlert=true → override border to `#DC2626` + thickness 1.5

VM model:

    class KpiTileModel
    {
        string Icon;         // e.g. "💰"
        string Label;        // e.g. "Total Sales"
        string DisplayValue; // e.g. "AED 12,345"
        string Accent;       // "Green" | "Blue" | "Amber" | "Red"
        bool IsAlert;
    }

### 5.10 Chat Activity Row (Phase 10.9a — NEW)

Chat-style message row inside a scrollable card.

Structure:
- CornerRadius=10, Padding=10,8, Margin=0,0,0,6
- Background = tint from `TintColor` brush key
- 3 columns: [Avatar 32×32 circle] [Title+Subtitle] [Value+Status pill]

Avatar circle uses `StatusColor` brush key for background.
Icon (14pt) centered inside.

---

## 6. Reusable Controls

Defined in `Controls/{ControlName}/`.

### 6.1 GradientHeroCard

Full hero card with everything built-in.

    <hero:GradientHeroCard
        Icon="💰"
        Value="AED 500.00"
        Label="Total Earning"
        TrendArrow="↗"
        TrendText="12%"
        TrendVisibility="Visible"
        GradientStartColor="#0F4C3A"
        GradientEndColor="#22C55E"/>

### 6.2 SparklineControl

Mini line chart with optional filled area.

    <spark:SparklineControl
        Height="40"
        Values="12,15,10,18,22,20,25"
        LineBrush="{DynamicResource AccentBrush}"
        AreaBrush="{DynamicResource AccentBrush}"/>

### 6.3 MiniBarChartControl

Mini bar chart with optional highlighted bar.

    <mbc:MiniBarChartControl
        Height="60"
        Values="30,45,25,50,40,60,55"
        BarBrush="{DynamicResource AccentBrush}"
        HighlightIndex="4"
        HighlightBrush="{DynamicResource AccentDarkBrush}"/>

---

## 7. Standard Card Compositions

### 7.1 KPI Tile (Phase 10.9a Nested)

    <Border CornerRadius="10" Padding="12,10" Margin="4"
            Background="#ECFDF5"
            BorderBrush="#A7F3D0"
            BorderThickness="1">
        <StackPanel>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding Icon}" FontSize="14"/>
                <TextBlock Text="{Binding Label}"
                           FontSize="10" FontWeight="SemiBold"
                           Foreground="#065F46"
                           Margin="6,0,0,0"/>
            </StackPanel>
            <TextBlock Text="{Binding DisplayValue}"
                       FontSize="18" FontWeight="Bold"
                       Foreground="#065F46"
                       Margin="0,6,0,0"/>
        </StackPanel>
    </Border>

### 7.2 Parent Card with Nested Tiles

    <Border Style="{StaticResource ParentCardStyle}">
        <StackPanel>
            <StackPanel Orientation="Horizontal" Margin="0,0,0,14">
                <Border Width="34" Height="34" CornerRadius="9"
                        Background="#059669">
                    <TextBlock Text="💰" FontSize="18"
                               HorizontalAlignment="Center"
                               VerticalAlignment="Center"/>
                </Border>
                <TextBlock Text="SALES"
                           FontSize="14" FontWeight="Bold"
                           Foreground="#065F46"
                           Margin="10,0,0,0"
                           VerticalAlignment="Center"/>
            </StackPanel>
            <ItemsControl ItemsSource="{Binding SalesTiles}"
                          ItemTemplate="{StaticResource TileTemplate}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <UniformGrid Rows="2" Columns="2"/>
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
            </ItemsControl>
        </StackPanel>
    </Border>

### 7.3 Chat Activity Feed (inside parent card)

    <Border MaxHeight="480"
            Background="{DynamicResource MenuHoverBrush}"
            CornerRadius="10"
            Padding="8">
        <ScrollViewer VerticalScrollBarVisibility="Auto"
                      CanContentScroll="False"
                      PanningMode="VerticalOnly">
            <ItemsControl ItemsSource="{Binding RecentActivity}"
                          ItemTemplate="{StaticResource ActivityChatTemplate}"/>
        </ScrollViewer>
    </Border>

### 7.4 DN Detail Hero Card (Phase 10.8b)

Gradient header + 4 stat tiles below.

    <Border Style="{StaticResource SectionCardStyle}">
        <StackPanel>
            <!-- Gradient header -->
            <Border CornerRadius="14,14,0,0" Padding="18,14">
                <Border.Background>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                        <GradientStop Color="#0F4C3A" Offset="0"/>
                        <GradientStop Color="#166534" Offset="1"/>
                    </LinearGradientBrush>
                </Border.Background>
                <!-- Customer + Project + Date info -->
            </Border>

            <!-- 4 stat tiles: Ordered · Prev · This DN · Balance -->
            <Grid Margin="16,14,16,14">
                <!-- 4 columns of stat tiles -->
            </Grid>
        </StackPanel>
    </Border>

### 7.5 DN List Row Card (Phase 10.7a)

3-line card with badges + chips + action buttons.

    Line 1: DN ID (16pt Bold) + Status pill + Type pill + FOC + DUMMY
    Line 2: 📋 JO ID · Customer · 📅 Date
    Line 3: [🚚 Driver chip] [Qty chip]
    Right: [📂 Open button] [🧾 Invoice button — always visible]

Card padding: 20,16
Card CornerRadius: 14
BorderThickness: 4,1,1,1 (left accent bar from TypeBrushKey)

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

### 8.1 Module-specific mappings

**Delivery Note Status:**

| Status      | Status Brush Key | Tint Key         |
|-------------|------------------|------------------|
| Draft       | NeutralBrush     | TintGrayBrush    |
| Dispatched  | AccentBrush      | TintIndigoBrush  |
| Delivered   | SuccessBrush     | TintGreenBrush   |
| Signed      | SuccessBrush     | TintGreenBrush   |
| Received    | InfoBrush        | TintBlueBrush    |

**Delivery Note Type:**

| Type       | Brush Key         |
|------------|-------------------|
| Original   | TintIndigoBrush   |
| Dummy      | TintGrayBrush     |
| Rework     | TintAmberBrush    |
| Return     | TintRedBrush      |

**Job Order Status:**

| Status         | Brush Key      |
|----------------|----------------|
| Draft          | NeutralBrush   |
| Released       | InfoBrush      |
| InProduction   | WarningBrush   |
| Completed      | SuccessBrush   |
| Delivered      | AccentBrush    |
| Overdue        | DangerBrush    |

**Job Priority:**

| Priority | Tint Key         |
|----------|------------------|
| High     | TintRedBrush     |
| Medium   | TintAmberBrush   |
| Low      | TintGreenBrush   |

### 8.2 Phase 10.9a Overview Dashboard Card System

Parent cards use brand-locked hex colors for icon badges and text
(intentionally NOT theme-adaptive — represents "brand identity"):

| Section    | Icon Badge | Title Text | Nested Tile Accent |
|------------|------------|------------|--------------------|
| SALES      | `#059669`  | `#065F46`  | Green              |
| PRODUCTION | `#1E40AF`  | `#1E3A8A`  | Blue               |
| DELIVERY   | `#B45309`  | `#78350F`  | Amber              |
| INVENTORY  | `#B91C1C`  | `#7F1D1D`  | Red                |
| ACTIVITY   | `#4338CA`  | `#3730A3`  | (no tiles)         |
| CHART      | `#0F766E`  | `#134E4A`  | (no tiles)         |

**Why hardcoded hex here:** These represent the visual identity of
each business domain (sales=green, production=blue, delivery=amber,
inventory=red). If the app were to add a dark theme variant, these
would need dark theme overrides — but the intent is "brand color."

---

## 9. Chart Bar Colors

Chart bars (in `ChartBarModel.BarColor`) intentionally use hex
strings for consistent visual identity across themes.

    #6366F1   Indigo (primary series)
    #10B981   Green (success / positive / highlight)
    #F59E0B   Amber (warning)
    #3B82F6   Blue (info)
    #EC4899   Pink (highlight)
    #8B5CF6   Purple (accent)
    #EF4444   Red (danger)
    #6B7280   Gray (neutral)

Use `ColorStringConverter` (not `BrushKeyConverter`) to bind these.

---

## 10. Scrolling Rules (Phase 10.9a)

For smooth pixel-based scrolling like modern websites:

    <ScrollViewer VerticalScrollBarVisibility="Auto"
                  CanContentScroll="False"
                  PanningMode="VerticalOnly">
        <!-- content -->
    </ScrollViewer>

Do NOT combine with `ScrollViewer.VerticalScrollBarVisibility`
attached property (Gotcha #29 — sets property twice).

For scrollable content inside a card (like chat activity feed):
- Outer ScrollViewer wraps the whole page
- Inner ScrollViewer inside the card also uses the same smooth
  scrolling settings

---

## 11. Section Header Patterns

### 11.1 Flat accent header (legacy — pre-Phase 10.8b)

    <Border Background="{DynamicResource AccentBrush}"
            CornerRadius="10,10,0,0"
            Padding="14,8">
        <TextBlock Text="SECTION TITLE"
                   FontSize="11" FontWeight="Bold"
                   Foreground="White"/>
    </Border>

### 11.2 Gradient header (Phase 10.8b — preferred)

Deep green gradient for content sections:

    <Border CornerRadius="14,14,0,0" Padding="16,10">
        <Border.Background>
            <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                <GradientStop Color="#0F4C3A" Offset="0"/>
                <GradientStop Color="#166534" Offset="1"/>
            </LinearGradientBrush>
        </Border.Background>
        <StackPanel>
            <TextBlock Text="SECTION TITLE"
                       FontSize="11" FontWeight="Bold"
                       Foreground="White"/>
            <TextBlock Text="Optional subtitle hint"
                       FontSize="9"
                       Foreground="#CCFFFFFF"
                       Margin="0,2,0,0"/>
        </StackPanel>
    </Border>

Indigo gradient for history/list sections:

    <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
        <GradientStop Color="#4338CA" Offset="0"/>
        <GradientStop Color="#6366F1" Offset="1"/>
    </LinearGradientBrush>

### 11.3 Spec group header (Phase 10.8b)

Used inside items grid to visually separate spec groups:

    <Border Margin="0,8,0,0" Padding="12,8">
        <Border.Background>
            <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                <GradientStop Color="#EEF2FF" Offset="0"/>
                <GradientStop Color="#E0E7FF" Offset="1"/>
            </LinearGradientBrush>
        </Border.Background>
        <StackPanel Orientation="Horizontal">
            <!-- 4px accent bar on left -->
            <Border Width="4" CornerRadius="2" Margin="0,0,10,0">
                <Border.Background>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                        <GradientStop Color="#4338CA" Offset="0"/>
                        <GradientStop Color="#6366F1" Offset="1"/>
                    </LinearGradientBrush>
                </Border.Background>
            </Border>
            <TextBlock Text="{Binding Name}"
                       FontSize="12" FontWeight="Bold"
                       Foreground="#4338CA"
                       VerticalAlignment="Center"/>
        </StackPanel>
    </Border>

---

## 12. Pill Patterns

### 12.1 Status Pill (light tint, dark text — Gotcha #51)

    <Border CornerRadius="8" Padding="8,3"
            Background="{Binding StatusBrushKey,
                         Converter={StaticResource BrushKeyConverter}}">
        <TextBlock Text="{Binding StatusLabel}"
                   FontSize="9" FontWeight="Bold"
                   Foreground="{DynamicResource PrimaryForegroundBrush}"/>
    </Border>

**IMPORTANT:** BrushKeyConverter returns TINT brushes (light
backgrounds). Text on tint pills MUST use `PrimaryForegroundBrush`
(dark). White text = invisible.

### 12.2 Frosted Pill (on gradient header)

For pills displayed on colored gradient headers:

    <Border CornerRadius="8" Padding="8,3"
            Background="#22FFFFFF">
        <TextBlock Text="{Binding Value, Mode=OneWay}"
                   FontSize="10" FontWeight="Bold"
                   Foreground="White"/>
    </Border>

`#22FFFFFF` = 13% white opacity — creates frosted-glass effect.

### 12.3 Solid Status Pill (on chat activity avatar)

    <Border CornerRadius="8" Padding="6,2"
            Background="{Binding StatusColor,
                         Converter={StaticResource BrushKeyConverter}}">
        <TextBlock Text="{Binding StatusLabel}"
                   FontSize="8" FontWeight="Bold"
                   Foreground="White"/>
    </Border>

Here `StatusColor` returns a SOLID brush (not tint), so white text
works.

---

## 13. Button Patterns

### 13.1 Primary Action Button

    <Style x:Key="ActionButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="{DynamicResource AccentBrush}"/>
        <Setter Property="Foreground" Value="{DynamicResource OnAccentForegroundBrush}"/>
        <Setter Property="Padding" Value="16,8"/>
        <Setter Property="FontSize" Value="12"/>
        <Setter Property="FontWeight" Value="Bold"/>
        <!-- ... corner radius 8, hover opacity 0.88 -->
    </Style>

### 13.2 Export Button (Phase 10.8 — outlined)

Smaller outlined button with colored border matching function:

    <Style x:Key="ExportButtonStyle" TargetType="Button">
        <Setter Property="Padding" Value="12,8"/>
        <Setter Property="FontSize" Value="11"/>
        <Setter Property="FontWeight" Value="Bold"/>
        <Setter Property="BorderThickness" Value="1.5"/>
        <!-- corner radius 8, hover opacity 0.82 -->
    </Style>

Usage:

    <Button Content="📄  PDF"
            Style="{StaticResource ExportButtonStyle}"
            Background="{DynamicResource TintRedBrush}"
            BorderBrush="{DynamicResource DangerBrush}"
            Foreground="{DynamicResource DangerBrush}"/>

    <Button Content="📊  Excel"
            Background="{DynamicResource TintGreenBrush}"
            BorderBrush="{DynamicResource SuccessBrush}"
            Foreground="{DynamicResource SuccessBrush}"/>

    <Button Content="📋  CSV"
            Background="{DynamicResource TintIndigoBrush}"
            BorderBrush="{DynamicResource AccentBrush}"
            Foreground="{DynamicResource AccentBrush}"/>

### 13.3 Quick Action Pill (Phase 10.9a Dashboard)

    <Style x:Key="QuickActionPrimary" TargetType="Button">
        <Setter Property="Padding" Value="18,10"/>
        <Setter Property="Margin" Value="0,0,8,0"/>
        <Setter Property="CornerRadius" Value="20"/>
        <Setter Property="FontSize" Value="12"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
    </Style>

Icon + label horizontal layout inside pill shape.

### 13.4 Destructive Action Button

    <Button Content="🗑️  Delete"
            Style="{StaticResource ActionButtonStyle}"
            Background="{DynamicResource DangerBrush}"
            Foreground="White"/>

Delete button should always be visually separated from other
buttons by a divider (Phase 10.8b action bar pattern):

    <Border Width="1" Margin="0,4,12,4"
            Background="{DynamicResource BorderBrush}"/>
    <Button Content="🗑️  Delete" .../>

---

## 14. Do's and Don'ts

### DO

- Use `{DynamicResource}` for theme-adaptive brushes
- Use `{StaticResource}` for style dictionary references
- Reference styles from `Themes/Styles/` — never inline
- Bind card colors via brush key strings (VM sets `"AccentBrush"`)
- Use `BrushKeyConverter` for theme-adaptive card backgrounds
- Use `ColorStringConverter` for chart data colors
- Use `PrimaryForegroundBrush` (dark) for text on tint pills
- Use white text ONLY on solid semantic brushes (SuccessBrush,
  DangerBrush, etc.) — NEVER on tint brushes
- Set `MinHeight` on hero cards for visual weight
- Give KPI grids a consistent 2-column or 4-column layout
- Use `CanContentScroll="False"` + `PanningMode="VerticalOnly"`
  on scrollable content for smooth pixel scrolling
- Extract shared DataTemplates when 4+ views use the same row
  layout (Phase 10.7a pattern)
- Use gradient section headers for premium look (Phase 10.8b)
- Separate destructive buttons with a visual divider

### DON'T

- Don't hardcode hex colors in themeable XAML (except brand-locked
  identity colors like Phase 10.9a parent card badges)
- Don't inline card styling with `Setter` properties
- Don't use `x:Name` on `GradientStop` (unreliable in Freezable)
- Don't set VM color property to hex when using `BrushKeyConverter`
- Don't register the same converter in multiple ResourceDictionaries
- Don't add `: UserControl` or `: ResourceDictionary` to code-behind
  partial classes (CS0263 conflict — Gotcha #16)
- Don't override styles — extend via `BasedOn`
- Don't use white text on tint pills (Gotcha #51)
- Don't set the same property twice on one element (Gotcha #29)
- Don't skip smooth-scrolling settings on long lists

---

## 15. File Locations

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
        PercentToWidthConverter.cs   percent → pixel width (DN pending bar)

---

## 16. Design Evolution — Phase Timeline

| Phase   | Design Change                                              |
|---------|------------------------------------------------------------|
| 8.2a    | Modern UI (ClearType, borders, hero card)                  |
| 8.2b    | Tinted card backgrounds + gradient hero                    |
| 8.2c    | Full DynamicResource migration + 25-brush palette          |
| 8.2d    | Splash custom glass logo + feature row + rotating messages |
| 8.2e    | BrushKeyConverter + brush key indirection                  |
| 8.2f    | Instant theme swap via ThemeManager.ThemeChanged           |
| 8.3     | Design system foundation (Styles + Controls folders)       |
| 10.3b   | Premium card tab headers                                   |
| 10.6a   | Rename dialog on upload (frameless glassy modal)           |
| 10.7a   | DN list row redesign (3-line card + Invoice button)        |
| 10.8    | Export button style (outlined variant)                     |
| 10.8b   | DN detail premium redesign (gradient headers + hero card)  |
| 10.9a   | Dashboard 2-col parent card grid + chat activity + smooth  |
|         | pixel scrolling + compact color system                     |

---

## 17. When To Update This Doc

- Adding a new color token → update Section 2
- Adding a new text style → update Section 3 table
- Adding a new card pattern → update Section 5 + example in 7
- Adding a new reusable control → update Section 6 + example
- Discovering a new gotcha → update Section 14
- Introducing a new module color mapping → update Section 8.1