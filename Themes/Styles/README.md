# Themes/Styles/ — Shared Style Library

## Purpose

Reusable XAML styles that establish a consistent design language
across every module. Any new module should use these styles instead
of defining its own inline card/pill/typography styles.

All 4 dictionaries are registered globally in App.xaml. Styles are
referenced via `{StaticResource StyleName}` from any View or
ResourceDictionary.

## Contents

    TypeScaleTokens.xaml      Global font-size scale (11 tokens, canonical)
    TypographyStyles.xaml     Text hierarchy (13 styles)
    IconBadgeStyles.xaml      Colored icon squares (10 styles)
    PillStyles.xaml           Status + priority + tag pills (16 styles)
    CardStyles.xaml           Card containers + hero gradients (13 styles)

Each `.xaml` has a matching `.xaml.cs` stub required by WPF's
ResourceDictionary partial-class generation. The stubs contain
only `InitializeComponent()` — no logic.

## Load Order (App.xaml)

The dictionaries are merged in this order to satisfy dependencies:

    1. LightTheme.xaml           (base brushes)
    2. TypeScaleTokens.xaml      (font-size tokens, no dependencies)
    3. TypographyStyles.xaml     (no dependencies)
    4. IconBadgeStyles.xaml      (uses theme brushes)
    5. PillStyles.xaml           (uses theme brushes)
    6. CardStyles.xaml           (uses theme brushes + BrushKeyConverter)

## Type Scale Tokens

`TypeScaleTokens.xaml` is the canonical font-size scale. Every
`FontSize` below 30 DIPs uses `{StaticResource Text…}`:

    TextXSmall      11      fine print / legend glyphs
    TextCaption     12      column headers / labels / hints / pills
    TextBody        13      body / inputs / buttons / table cells
    TextBodyLarge   14      list-row primary text / menu items
    TextSection     15      card titles / section headers
    TextSubhead     16      emphasis sub-headings
    TextTitle       18      page titles
    TextTitleLarge  20      dashboard / hero titles
    TextHeadline    22      big section headlines
    TextDisplay     24      large numbers
    TextDisplayLarge 28     hero KPI numbers

## Typography Styles

| Style Key             | Font Size | Weight     | Use For                        |
|-----------------------|-----------|------------|--------------------------------|
| HeroNumberStyle       | 38        | Bold       | Hero card big values           |
| StatNumberStyle       | 28        | Bold       | KPI card values                |
| MediumNumberStyle     | 20        | Bold       | Secondary numbers              |
| PageTitleStyle        | 30        | Bold       | Page-level titles              |
| PageSubtitleStyle     | 13        | Normal     | Under page title               |
| SectionTitleStyle     | 18        | SemiBold   | Section groupings              |
| SectionSubtitleStyle  | 12        | Normal     | Under section title            |
| CardTitleStyle        | 13        | SemiBold   | Title inside a card            |
| CardSubtitleStyle     | 11        | Normal     | Under card value               |
| MetaTextStyle         | 11        | Normal     | Dates, tags, footnotes         |
| RowTitleStyle         | 14        | SemiBold   | List row primary text          |
| RowSubtitleStyle      | 12        | Normal     | List row metadata              |
| TrendingTextStyle     | 11        | Bold       | Trend badge text (on accent)   |
| TableHeaderStyle      | 11        | Bold       | Table column headers           |

## Icon Badge Styles

Small rounded colored squares (44x44 default) for card corners.
Contains an emoji or icon inside.

Sizes:

    IconBadgeSmallStyle    36x36    (tight layouts)
    IconBadgeBaseStyle     44x44    (default)
    IconBadgeLargeStyle    52x52    (hero cards)

Color variants (all inherit sizing from base):

    IconBadgeAccentStyle           AccentBrush (primary green)
    IconBadgeSuccessStyle          SuccessBrush
    IconBadgeWarningStyle          WarningBrush
    IconBadgeDangerStyle           DangerBrush
    IconBadgeInfoStyle             InfoBrush
    IconBadgeNeutralStyle          NeutralBrush
    IconBadgeTintedAccentStyle     Outlined variant with accent border

Usage example:

    <Border Style="{StaticResource IconBadgeAccentStyle}">
        <TextBlock Text="📄" FontSize="20"
                   Foreground="{DynamicResource OnAccentForegroundBrush}"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"/>
    </Border>

## Pill Styles

Small rounded pills for status labels, priority indicators, and
category tags.

Text styles:

    PillTextStyle       White text on solid pill (bold, 10pt)
    PillTextDarkStyle   Dark text on outline/tag pill (semibold, 10pt)
    TagPillTextStyle    Category tag text (semibold, 10pt)

Solid color pills (all inherit from StatusPillBaseStyle):

    StatusPillAccentStyle    Primary
    StatusPillSuccessStyle   Approved / OK / Added
    StatusPillWarningStyle   Pending / Caution
    StatusPillDangerStyle    Rejected / Deleted / Alert
    StatusPillInfoStyle      Sent / Info
    StatusPillNeutralStyle   Draft / Unknown

Priority pills (map to semantic colors):

    PriorityPillHighStyle    → StatusPillDangerStyle
    PriorityPillMediumStyle  → StatusPillSuccessStyle
    PriorityPillLowStyle     → StatusPillNeutralStyle

Other pills:

    OutlinePillStyle         Transparent bg + colored border
    TagPillStyle             Muted bg + dark text (categories)
    TrendingBadgeUpStyle     Green pill for positive trends
    TrendingBadgeDownStyle   Red pill for negative trends

Usage example:

    <Border Style="{StaticResource StatusPillSuccessStyle}">
        <TextBlock Text="Approved" Style="{StaticResource PillTextStyle}"/>
    </Border>

    <Border Style="{StaticResource TagPillStyle}">
        <TextBlock Text="Design" Style="{StaticResource TagPillTextStyle}"/>
    </Border>

## Card Styles

Every module's UI is built from cards. Never inline the styling —
always reference these.

Base cards:

    BaseCardStyle       Plain white card, 16px radius, 1px border
    ElevatedCardStyle   Base card + soft green-tinted shadow
    StatCardStyle       Compact KPI card (smaller padding)

Data-driven cards:

    TintedCardStyle     Background bound to VM TintColor brush key
                        Alert trigger: 2px red border when IsAlert=true
    PriorityCardStyle   Task card with 4px colored left accent bar

Interactive cards:

    TaskCardStyle       Vertical list row card, hover highlights border
    ListRowCardStyle    Table row card, hover swaps background

Hero gradient cards (colored background, use with white text inside):

    HeroCardAccentStyle    Green gradient (AccentDark → AccentLight)
    HeroCardInfoStyle      Blue gradient (#1E40AF → #3B82F6)
    HeroCardWarningStyle   Amber gradient (#D97706 → #F59E0B)
    HeroCardDangerStyle    Red gradient (#B91C1C → #EF4444)
    HeroCardPurpleStyle    Purple gradient (#6D28D9 → #8B5CF6)

Usage example — TintedCardStyle:

    <!-- VM property: TintColor = "TintIndigoBrush" -->
    <Border Style="{StaticResource TintedCardStyle}">
        <StackPanel>
            <TextBlock Text="Total Sales"
                       Style="{StaticResource CardTitleStyle}"/>
            <TextBlock Text="AED 12,500"
                       Style="{StaticResource StatNumberStyle}"
                       Margin="0,14,0,0"/>
        </StackPanel>
    </Border>

Usage example — HeroCardInfoStyle:

    <Border Style="{StaticResource HeroCardInfoStyle}" MinHeight="180">
        <StackPanel>
            <TextBlock Text="Total Orders" FontSize="13"
                       Foreground="#DDFFFFFF"/>
            <TextBlock Text="$961"
                       Style="{StaticResource HeroNumberStyle}"
                       Margin="0,16,0,0"/>
        </StackPanel>
    </Border>

## Adding a New Style

1. Choose the correct dictionary file:
   - Font sizes / weights → TypographyStyles.xaml
   - Colored icon squares → IconBadgeStyles.xaml
   - Rounded labels → PillStyles.xaml
   - Card containers → CardStyles.xaml
2. Follow existing naming convention:
   `{Purpose}{Variant}Style` — e.g. `StatusPillDangerStyle`
3. Base new styles on existing ones via `BasedOn="{StaticResource ...}"`
   whenever possible
4. Use `{DynamicResource ThemeBrush}` for colors — never hardcode hex
5. Update this README with the new style key + description

## Rules

- **Never hardcode colors** — use theme brushes via `DynamicResource`
- **Never inline card styling** in module views — use these styles
- **Chart/data colors** are the exception — those use hex strings
  via `ColorStringConverter` for consistent visual identity
- **VM-driven colors** (tint, badge, status) use brush key strings
  via `BrushKeyConverter` for theme adaptation