#!/usr/bin/env python3
"""
Migrate all 189 Views to ONE GLOBAL DESIGN SYSTEM
- Replace hardcoded CornerRadius with DynamicResource Radius.*
- Replace hardcoded FontSize with DynamicResource FontSize.*
- Replace hardcoded FontWeight with DynamicResource Weight.*
- Replace hardcoded colors with DynamicResource (Paper, Splash, Chart, About, Wizard, etc.)
- Replace StaticResource Text* with DynamicResource FontSize.*
- Add missing Button/TextBox/ComboBox/DataGrid styles
- Remove unnecessary local visual styles (shadowed globals)
- Replace local card styles with global Surface styles
"""

import re, glob, os, sys

ROOT = "."

# Mapping for hardcoded CornerRadius values to tokens
def map_radius_value(val):
    val = val.strip()
    # Handle asymmetric
    if "," in val:
        parts = [p.strip() for p in val.split(",")]
        if len(parts)==4:
            # Check if top rounded, bottom flat
            if parts[2]=="0" and parts[3]=="0":
                # top rounded
                top = parts[0]
                try:
                    t = float(top)
                    if t <= 6:
                        return "Radius.Top.Small"
                    elif t <= 10:
                        return "Radius.Top.Medium"
                    elif t <= 12:
                        return "Radius.Top.Large"
                    else:
                        return "Radius.Top.XLarge"
                except:
                    return "Radius.Top.Medium"
            elif parts[0]=="0" and parts[1]=="0":
                # bottom rounded
                bottom = parts[2]
                try:
                    b = float(bottom)
                    if b <= 6:
                        return "Radius.Bottom.Small"
                    elif b <= 10:
                        return "Radius.Bottom.Medium"
                    elif b <= 12:
                        return "Radius.Bottom.Large"
                    else:
                        return "Radius.Bottom.XLarge"
                except:
                    return "Radius.Bottom.Medium"
            elif parts[1]=="0" and parts[3]=="0":
                # left rounded
                return "Radius.Left.Medium"
            elif parts[0]=="0" and parts[2]=="0":
                # right rounded
                return "Radius.Right.Medium"
        # fallback
        return "Radius.Medium"
    else:
        try:
            f = float(val)
            if f == 0:
                return "Radius.None"
            elif f <= 3:
                return "Radius.XSmall"
            elif f <= 6:
                return "Radius.Small"
            elif f <= 8:
                return "Radius.Control"
            elif f <= 10:
                return "Radius.Medium"
            elif f <= 12:
                return "Radius.Card"
            elif f <= 16:
                return "Radius.XLarge"
            elif f <= 20:
                return "Radius.2XLarge"
            else:
                return "Radius.Full"
        except:
            return "Radius.Card"

def map_fontsize_value(val):
    try:
        f = float(val)
        if f <= 10:
            return "FontSize.Caption"
        elif f <= 11:
            return "FontSize.Label"
        elif f <= 12:
            return "FontSize.BodySmall"
        elif f <= 13:
            return "FontSize.Body"
        elif f <= 15:
            return "FontSize.Title"
        elif f <= 16:
            return "FontSize.H3"
        elif f <= 20:
            return "FontSize.H2"
        elif f <= 24:
            return "FontSize.H1"
        else:
            return "FontSize.Display"
    except:
        return "FontSize.Body"

# Hardcoded hex to DynamicResource mapping
hex_map = {
    "#59FFFF": "About.GlowBrush1",
    "#00FFFF": "About.GlowBrush2",
    "#2EFFFF": "Auth.SurfaceBrush",
    "#FFFFFF": "PaperBrush",  # for printable, but also for cards? We'll use PaperBrush for now, will be overridden for cards
    "#ffffff": "PaperBrush",
    "#FFF": "PaperBrush",
    "#1000FF": "Chart.BlueBrush",
    "#10FF00": "Chart.GreenBrush",
    "#1A22C5": "Chart.IndigoBrush",
    "#1AFF3B": "Chart.LimeBrush",
    "#1AFFA5": "Chart.MintBrush",
    "#1A3B82": "Chart.NavyBrush",
    "#000000": "Wizard.BlackBrush",
    "#FFF8FA": "Wizard.PaperBrush",
    "#FF1118": "Wizard.RedBrush",
    "#228080": "Splash.TealBrush",
    "#33FFFF": "Splash.CyanBrush",
    "#148080": "Splash.DarkTealBrush",
    "#0D0000": "Splash.DarkBrush",
    # lower case variants
    "#59ffff": "About.GlowBrush1",
    "#00ffff": "About.GlowBrush2",
    "#2effff": "Auth.SurfaceBrush",
    "#1000ff": "Chart.BlueBrush",
    "#10ff00": "Chart.GreenBrush",
    "#1a22c5": "Chart.IndigoBrush",
    "#1aff3b": "Chart.LimeBrush",
    "#1affa5": "Chart.MintBrush",
    "#1a3b82": "Chart.NavyBrush",
    "#fff8fa": "Wizard.PaperBrush",
    "#ff1118": "Wizard.RedBrush",
    "#33ffff": "Splash.CyanBrush",
    "#0d0000": "Splash.DarkBrush",
}

# Text* StaticResource to FontSize DynamicResource
text_map = {
    "TextXSmall": "FontSize.Caption",
    "TextCaption": "FontSize.BodySmall",
    "TextBody": "FontSize.Body",
    "TextBodyLarge": "FontSize.Title",
    "TextSection": "FontSize.H3",
    "TextSubhead": "FontSize.H3",
    "TextTitle": "FontSize.H2",
    "TextTitleLarge": "FontSize.H1",
    "TextHeadline": "FontSize.H1",
    "TextDisplay": "FontSize.Kpi",
    "TextDisplayLarge": "FontSize.Display",
    "Type.Caption": "FontSize.Caption",
    "Type.Small": "FontSize.BodySmall",
    "Type.Body": "FontSize.Body",
    "Type.BodyLarge": "FontSize.Title",
    "Type.Section": "FontSize.H3",
    "Type.Title": "FontSize.H2",
    "Type.H2": "FontSize.H2",
    "Type.H1": "FontSize.H1",
    "Type.Display": "FontSize.Display",
    "Type.NumericValue": "FontSize.Kpi",
    "Type.Money": "FontSize.Money",
}

# Local style to global mapping (visual duplicates)
local_to_global = {
    # About
    "AboutCardStyle": "SurfacePrimaryStyle",
    "HeroCardStyle": "SurfaceElevatedStyle",
    "AboutSectionPanelStyle": "SurfaceSecondaryStyle",
    "AboutHeaderBadgeStyle": "Badge.Primary",
    "AboutHeaderBadgeTextStyle": "Typography.Caption",
    "AboutPanelTextStyle": "Typography.Body",
    "AboutCodeStripStyle": "SurfaceDetailStyle",
    "ChipStyle": "Chip.Primary",
    "AboutModuleIconBorderStyle": "SurfacePrimaryStyle",
    "IconTileStyle": "SurfacePrimaryStyle",
    "GlyphStyle": "Typography.Icon",
    "WorkflowGroupLabelStyle": "Typography.Label",
    "InfoLabelStyle": "Typography.Label",
    "InfoValueStyle": "Typography.Body",
    # Splash
    "TrustChipStyle": "Chip.Primary",
    "ChipGlyphStyle": "Typography.Icon",
    "ChipTextStyle": "Typography.Caption",
    "TermsDeclineButtonStyle": "Button.Secondary",
    "TermsContinueButtonStyle": "Button.Primary",
    # AI Chat
    "ChatIconBtn": "Button.Ghost",
    "SendBtn": "Button.Primary",
    "UserBubble": "SurfacePrimaryStyle",
    "AiBubble": "SurfaceSecondaryStyle",
    "ErrBubble": "SurfaceDetailStyle",
    # Dn
    "CompactStatLabelStyle": "Typography.Label",
    "CompactStatNumberStyle": "Typography.Kpi",
    "CompactStatUnitStyle": "Typography.BodySmall",
    "CumulativeLabelStyle": "Typography.Caption",
    "CumulativeValueStyle": "Typography.Title",
    "CumulativeSeparatorStyle": "SurfaceDetailStyle",
    "BalanceCellStyle": "Typography.DataGrid",
    "HistoryRowButtonStyle": "Button.Ghost",
    "DifferentDnWarningStyle": "SurfaceSecondaryStyle",
    "HeroStripLabelStyle": "Typography.Label",
    "HistoryContextMenuItemStyle": "Typography.Body",
    "HistoryRowContentBorderStyle": "SurfaceDetailStyle",
    "HistoryRowDnIdStyle": "Typography.Body",
    "DeliveredQtyReadOnlyStyle": "Typography.DataGrid",
    "DeliveredQtyTextBoxStyle": "Input.TextBox",
    # Jo
    "CardLabelStyle": "Typography.Label",
    "JoFormCardStyle": "SurfacePrimaryStyle",
    "JoActionBarStyle": "SurfaceSecondaryStyle",
    "JoFieldTileStyle": "SurfaceDetailStyle",
    # Ti
    "FamilyRowButtonStyle": "Button.Ghost",
    "GridNumericCenterCell": "Typography.DataGrid",
    "InvoicePartyLabelStyle": "Typography.Label",
    "InvoicePartyValueStyle": "Typography.Body",
    "InvoiceMetaChipStyle": "Chip.Primary",
    # Settings
    "SectionCardStyle": "SurfacePrimaryStyle",
    "ProviderCardBaseStyle": "SurfacePrimaryStyle",
    "InfoCardTitleStyle": "Typography.Title",
    "SectionLabelStyle": "Typography.Label",
    "RowTitleStyle": "Typography.Title",
    "PageTitleStyle": "Typography.H1",
    "PageSubtitleStyle": "Typography.Subtitle",
    # SheetStore
    "StatLabel": "Typography.Label",
    "StatValue": "Typography.Kpi",
    "StatCard": "SurfaceKpiStyle",
    "InventoryKpiCell": "SurfaceKpiStyle",
    "ItemFieldLabel": "Typography.Label",
    "ItemEntry": "Input.TextBox",
    "ItemFieldError": "Typography.Caption",
    "SheetMenuActionItem": "Button.Secondary",
    "StatLabel": "Typography.Label",
    # Inquiry
    "StepDotStyle": "Badge.Primary",
    "StepNumberStyle": "Typography.Caption",
    "StepTitleStyle": "Typography.Title",
    "InqHintStyle": "Typography.Caption",
    "InquiryStatusSpineStyle": "SurfaceDetailStyle",
    "InquiryStatusPillDynamicStyle": "Chip.Primary",
    "InquiryInitialsTileStyle": "SurfaceSecondaryStyle",
    "InquiryInitialsTextStyle": "Typography.Title",
    "InquiryStatusTabStyle": "Tab.Inactive",
    # SystemCenter / UserCenter / CompanyCenter
    "ScFieldLabel": "Typography.Label",
    "ScTextBox": "Input.TextBox",
    "UcFieldLabel": "Typography.Label",
    "UcTextBox": "Input.TextBox",
    "UcPasswordBox": "Input.TextBox",
    "UcMeterSegment": "SurfaceDetailStyle",
    "CcFieldLabel": "Typography.Label",
    "CcTextBox": "Input.TextBox",
    # Crash
    "CrashFieldLabel": "Typography.Label",
    "CrashFieldValue": "Typography.Body",
    "CrashPrimaryButton": "Button.Primary",
    "CrashOutlinedButton": "Button.Secondary",
    "CrashGhostButton": "Button.Ghost",
    # QuickSettings
    "QuickSettingsRow": "SurfaceDetailStyle",
    "QuickSettingsGlyph": "Typography.Icon",
    "QuickSettingsLabel": "Typography.Body",
    "QuickSettingsGroup": "Typography.Label",
    # Storage
    "StorageRowGlyph": "Typography.Icon",
    "StorageRowName": "Typography.Body",
    "StorageRowSize": "Typography.BodySmall",
    # Wizard (global already, but local shadows)
    "FieldLabelStyle": "Typography.Label",
    "SectionTitle": "Typography.H2",
    "SectionTitleStyle": "Typography.H2",
    "SectionSubtitleStyle": "Typography.BodySmall",
    "TableHeaderStyle": "Typography.Label",
    "IntInputStyle": "Input.TextBox",
    "DecimalInputStyle": "Input.TextBox",
    "ComboStyle": "Input.ComboBox",
    "LabelStyle": "Typography.Label",
    "InputStyle": "Input.TextBox",
    # Misc
    "TabCardInactiveStyle": "Tab.Inactive",
    "DnListBoxItemStyle": "SurfaceDetailStyle",
    "PendingRowCardStyle": "SurfaceSecondaryStyle",
    "InvListBoxItemStyle": "SurfaceDetailStyle",
    "InvTabInactiveStyle": "Tab.Inactive",
    "SpecColumnHeaderStyle": "Typography.Label",
    "FileNameTextBoxStyle": "Input.TextBox",
    "DnMenuActionItem": "Button.Secondary",
    "HistoryRowButtonStyle": "Button.Ghost",
    "CreateFromMasterButtonStyle": "Button.Primary",
    "DnMenuRealOnlyItem": "Button.Ghost",
    "FocBadgeStyle": "Badge.Primary",
    "TiMenuActionItem": "Button.Secondary",
    "AboutTabListStyle": "SurfaceSecondaryStyle",
    "AboutTabItemStyle": "Tab.Inactive",
    "CrashReportRowStyle": "SurfaceDetailStyle",
    "CrashMetaLabelStyle": "Typography.Label",
    "CrashMetaValueStyle": "Typography.Body",
    "CrashCodeStripTextStyle": "Typography.Caption",
    "LicensePanelStyle": "SurfacePrimaryStyle",
    "LicenseMetricCardStyle": "SurfaceKpiStyle",
    "LicenseMetricLabelStyle": "Typography.Label",
    "LicenseMetricValueStyle": "Typography.Kpi",
    "LicenseCodeValueStyle": "Typography.Caption",
    "ScopeCapsuleStyle": "Chip.Primary",
    "ScopePillItemStyle": "Badge.Primary",
    "ColumnHeader": "Typography.Label",
    "RowLabel": "Typography.Label",
    "RowValue": "Typography.Body",
    "LogEntryStyle": "Typography.Body",
    "HistoryListBoxItemStyle": "SurfaceDetailStyle",
    "ErrorAwareEntry": "Input.TextBox",
    "DefLabel": "Typography.Label",
    "DefValue": "Typography.Body",
    "DefSub": "Typography.BodySmall",
    "HudButtonStyle": "Button.Ghost",
}

# Files to process
files = glob.glob("./Views/**/*.xaml", recursive=True)
print(f"Processing {len(files)} view files...")

# Track modified files
modified = []

for fpath in files:
    try:
        content = open(fpath, encoding="utf-8", errors="ignore").read()
    except Exception as e:
        print(f"Failed to read {fpath}: {e}")
        continue

    original = content

    # 1. Replace hardcoded CornerRadius="number" (not already Dynamic/Static)
    # Pattern: CornerRadius="12" or CornerRadius="12,12,0,0"
    def replace_cr(match):
        full = match.group(0)
        # If already contains Resource, skip
        if "Resource" in full:
            return full
        # Extract value
        m = re.search(r'CornerRadius\s*=\s*"([^"]+)"', full)
        if not m:
            return full
        val = m.group(1)
        if "{" in val or "}" in val:
            return full
        token = map_radius_value(val)
        return full.replace(f'"{val}"', f'"{{DynamicResource {token}}}"')

    # Replace CornerRadius in tags
    content = re.sub(r'CornerRadius\s*=\s*"[^"]+"', replace_cr, content)

    # Also handle Setter Property="CornerRadius" Value="12"
    def replace_cr_setter(match):
        full = match.group(0)
        if "Resource" in full:
            return full
        m = re.search(r'Value\s*=\s*"([^"]+)"', full)
        if not m:
            return full
        val = m.group(1)
        if "{" in val or "}" in val:
            return full
        # Check if this setter is for CornerRadius
        if "CornerRadius" not in full:
            return full
        token = map_radius_value(val)
        return full.replace(f'"{val}"', f'"{{DynamicResource {token}}}"')

    content = re.sub(r'<Setter[^>]*Property="CornerRadius"[^>]*Value="[^"]+"[^>]*/?>', replace_cr_setter, content)
    content = re.sub(r'<Setter[^>]*Value="[^"]+"[^>]*Property="CornerRadius"[^>]*/?>', replace_cr_setter, content)

    # 2. Replace hardcoded FontSize="number"
    def replace_fs(match):
        full = match.group(0)
        if "Resource" in full:
            return full
        m = re.search(r'FontSize\s*=\s*"([^"]+)"', full)
        if not m:
            return full
        val = m.group(1)
        if "{" in val or "}" in val:
            return full
        # Only replace numeric values
        try:
            float(val)
        except:
            return full
        token = map_fontsize_value(val)
        return full.replace(f'"{val}"', f'"{{DynamicResource {token}}}"')

    content = re.sub(r'FontSize\s*=\s*"[^"]+"', replace_fs, content)
    # Setter version
    def replace_fs_setter(match):
        full = match.group(0)
        if "Resource" in full:
            return full
        if "FontSize" not in full:
            return full
        m = re.search(r'Value\s*=\s*"([^"]+)"', full)
        if not m:
            return full
        val = m.group(1)
        if "{" in val or "}" in val:
            return full
        try:
            float(val)
        except:
            return full
        token = map_fontsize_value(val)
        return full.replace(f'"{val}"', f'"{{DynamicResource {token}}}"')

    content = re.sub(r'<Setter[^>]*Property="FontSize"[^>]*Value="[^"]+"[^>]*/?>', replace_fs_setter, content)
    content = re.sub(r'<Setter[^>]*Value="[^"]+"[^>]*Property="FontSize"[^>]*/?>', replace_fs_setter, content)

    # 3. Replace hardcoded FontWeight="Bold" etc.
    def replace_fw(match):
        full = match.group(0)
        if "Resource" in full:
            return full
        m = re.search(r'FontWeight\s*=\s*"([^"]+)"', full)
        if not m:
            return full
        val = m.group(1)
        if "{" in val or "}" in val:
            return full
        mapping = {
            "Bold": "Weight.Bold",
            "SemiBold": "Weight.SemiBold",
            "Medium": "Weight.Medium",
            "Normal": "Weight.Regular",
            "Regular": "Weight.Regular",
            "Light": "Weight.Regular",
        }
        token = mapping.get(val, "Weight.Regular")
        return full.replace(f'"{val}"', f'"{{DynamicResource {token}}}"')

    content = re.sub(r'FontWeight\s*=\s*"[^"]+"', replace_fw, content)
    # Setter version
    def replace_fw_setter(match):
        full = match.group(0)
        if "Resource" in full:
            return full
        if "FontWeight" not in full:
            return full
        m = re.search(r'Value\s*=\s*"([^"]+)"', full)
        if not m:
            return full
        val = m.group(1)
        if "{" in val or "}" in val:
            return full
        mapping = {
            "Bold": "Weight.Bold",
            "SemiBold": "Weight.SemiBold",
            "Medium": "Weight.Medium",
            "Normal": "Weight.Regular",
            "Regular": "Weight.Regular",
            "Light": "Weight.Regular",
        }
        token = mapping.get(val, "Weight.Regular")
        return full.replace(f'"{val}"', f'"{{DynamicResource {token}}}"')

    content = re.sub(r'<Setter[^>]*Property="FontWeight"[^>]*Value="[^"]+"[^>]*/?>', replace_fw_setter, content)
    content = re.sub(r'<Setter[^>]*Value="[^"]+"[^>]*Property="FontWeight"[^>]*/?>', replace_fw_setter, content)

    # 4. Replace hardcoded hex colors
    # Look for Background="#..." Foreground="#..." BorderBrush="#..." Fill="#..." etc.
    for hex_code, token in hex_map.items():
        # Replace exact hex in quotes
        # Case sensitive but we have both cases in map
        # Pattern: Property="#hex"
        # Use regex to replace only if not already DynamicResource
        # We need to handle both with and without alpha
        # For #FFFFFF we want to be careful: it appears in many places, but we tokenize to PaperBrush
        # For cards, PaperBrush is okay for printable, but for other cards we might want SurfacePrimaryBrush
        # We'll replace all #FFFFFF with PaperBrush for now, then later for non-printable we can override to SurfacePrimaryBrush if needed
        # But to avoid breaking printable, keep PaperBrush
        pattern = re.compile(re.escape(f'"{hex_code}"'), re.IGNORECASE)
        # Find all occurrences and replace with DynamicResource
        # Only replace if inside Background/Foreground/BorderBrush/Fill/Color attribute
        # Simple replace for now
        def hex_replacer(match):
            # match is the quoted hex
            return f'"{{DynamicResource {token}}}"'
        # Replace in context of brush properties
        # We'll do a more targeted replace: look for (Background|Foreground|BorderBrush|Fill|Stroke|Color)\s*=\s*"hex"
        # Use regex with function
        def replace_hex_attr(m):
            prop = m.group(1)
            # m.group(2) is the hex
            # Check if token is for that hex (case insensitive)
            # Use token from map based on lower case lookup
            hex_val = m.group(2)
            # Find token via lower case
            tok = hex_map.get(hex_val) or hex_map.get(hex_val.lower()) or hex_map.get(hex_val.upper()) or token
            return f'{prop}="{{DynamicResource {tok}}}"'

        # This will replace all hex in brush props
        content = re.sub(r'(Background|Foreground|BorderBrush|Fill|Stroke)\s*=\s*"(' + re.escape(hex_code) + r')"', replace_hex_attr, content, flags=re.IGNORECASE)

    # Also replace any remaining hex that is exactly #FFFFFF etc. even if not in brush prop? We'll handle generic
    # For generic hex, replace with DynamicResource if it's in our map and not already replaced
    for hex_code, token in hex_map.items():
        # Replace any occurrence of hex_code in quotes that is still hardcoded and not already replaced
        # Only if it's a color attribute
        content = re.sub(r'Color\s*=\s*"' + re.escape(hex_code) + r'"', f'Color="{{DynamicResource {token}}}"', content, flags=re.IGNORECASE)

    # 5. Replace StaticResource Text* with DynamicResource FontSize.*
    for old_key, new_key in text_map.items():
        # Replace {StaticResource TextBody} with {DynamicResource FontSize.Body}
        content = content.replace(f'{{StaticResource {old_key}}}', f'{{DynamicResource {new_key}}}')
        # Also handle {StaticResource TextBody} inside Value=""
        content = content.replace(f'Value="{{StaticResource {old_key}}}"', f'Value="{{DynamicResource {new_key}}}"')

    # 6. Replace local style usages with global
    for local, global_style in local_to_global.items():
        # Replace Style="{StaticResource Local}" with global
        content = content.replace(f'{{StaticResource {local}}}', f'{{StaticResource {global_style}}}')
        content = content.replace(f'{{DynamicResource {local}}}', f'{{DynamicResource {global_style}}}')

    # 7. Add missing Button/TextBox/ComboBox/DataGrid styles
    # For Buttons without Style
    # Find <Button ...> without Style=
    # This is a bit complex, we need to add Style attribute
    # We'll use regex to find <Button ...> tags that don't contain Style=
    def add_button_style(match):
        tag = match.group(0)
        if "Style=" in tag:
            return tag
        # Check if it's a special button (like close, etc.) — use Secondary by default, but Primary for certain contents
        # Look for Content="Save" or "Create" etc.
        if any(x in tag for x in ['Save', 'Create', 'Add', 'Confirm', 'OK', 'Apply', 'Primary']):
            style = "PrimaryButtonStyle"
        else:
            style = "SecondaryButtonStyle"
        # Insert Style before closing >
        # If tag ends with />, insert before />
        # else insert before >
        if tag.endswith("/>"):
            return tag[:-2] + f' Style="{{StaticResource {style}}}"' + "/>"
        else:
            # Find last > and insert before
            # But we need to handle that tag may be <Button ...> with no closing
            # Insert before final >
            return tag[:-1] + f' Style="{{StaticResource {style}}}"' + ">"

    # Only process Views that are not SectionStyles? Actually SectionStyles have no Buttons
    # We'll process all
    content = re.sub(r'<Button\b[^>]*>', add_button_style, content)

    # For TextBox without Style
    def add_textbox_style(match):
        tag = match.group(0)
        if "Style=" in tag:
            return tag
        style = "TextBoxStyle"
        if tag.endswith("/>"):
            return tag[:-2] + f' Style="{{StaticResource {style}}}"' + "/>"
        else:
            return tag[:-1] + f' Style="{{StaticResource {style}}}"' + ">"

    content = re.sub(r'<TextBox\b[^>]*>', add_textbox_style, content)

    # For ComboBox without Style
    def add_combobox_style(match):
        tag = match.group(0)
        if "Style=" in tag:
            return tag
        style = "ComboBoxStyle"
        if tag.endswith("/>"):
            return tag[:-2] + f' Style="{{StaticResource {style}}}"' + "/>"
        else:
            return tag[:-1] + f' Style="{{StaticResource {style}}}"' + ">"

    content = re.sub(r'<ComboBox\b[^>]*>', add_combobox_style, content)

    # For DataGrid without Style
    def add_datagrid_style(match):
        tag = match.group(0)
        if "Style=" in tag:
            return tag
        style = "DataGridStyle"
        if tag.endswith("/>"):
            return tag[:-2] + f' Style="{{StaticResource {style}}}"' + "/>"
        else:
            return tag[:-1] + f' Style="{{StaticResource {style}}}"' + ">"

    content = re.sub(r'<DataGrid\b[^>]*>', add_datagrid_style, content)

    # 8. Replace hardcoded Margin/Padding that are purely visual? Skip for now to preserve layout

    # 9. Replace local visual styles that shadow global — delete local definitions if they are in mapping and now replaced
    # For shadowed globals like SectionTitleStyle, PageTitleStyle, etc., we should delete local definition
    # We'll do this by removing <Style x:Key="SectionTitleStyle" ...>...</Style> if it's shadowed
    # Only if the file is not TypographyStyles.xaml itself (which is global)
    if "TypographyStyles.xaml" not in fpath and "GlobalDesignSystem.xaml" not in fpath:
        for shadowed in ["SectionTitleStyle", "SectionSubtitleStyle", "PageTitleStyle", "PageSubtitleStyle", "RowTitleStyle", "RowSubtitleStyle", "FieldLabelStyle", "TableHeaderStyle", "CardTitleStyle", "CardSubtitleStyle", "IntInputStyle", "DecimalInputStyle", "ComboStyle", "CardStyle", "SectionCardStyle"]:
            # Remove local style definition if it exists and is shadowed
            # Pattern: <Style x:Key="Shadowed" ...>...</Style>
            pattern = re.compile(r'<Style\s+x:Key="' + re.escape(shadowed) + r'"[^>]*>.*?</Style>', re.DOTALL)
            # Only remove if file is a View (not global styles)
            if "Views/" in fpath:
                # Check if this style is defined locally
                if f'<Style x:Key="{shadowed}"' in content:
                    # Replace with empty (but keep if it's global file)
                    # For Views, delete local shadow
                    content = pattern.sub(f'<!-- Migrated {shadowed} to global SurfacePrimaryStyle / Typography -->', content)

    # 10. Ensure all Borders that are cards use SurfacePrimaryStyle if they have no Style
    # Find <Border ...> without Style and with Background or CornerRadius that looks like card
    # We'll add Style="{StaticResource SurfacePrimaryStyle}" if Border has no Style and is likely a card (has CornerRadius or Background)
    def add_border_card_style(match):
        tag = match.group(0)
        if "Style=" in tag:
            return tag
        # If it has CornerRadius or Background that looks like card, add SurfacePrimaryStyle
        # But we need to avoid adding to every Border (some are layout)
        # Heuristic: if it has CornerRadius and Background, it's likely a card
        if "CornerRadius" in tag and ("Background" in tag or "BorderBrush" in tag):
            style = "SurfacePrimaryStyle"
            if tag.endswith("/>"):
                return tag[:-2] + f' Style="{{StaticResource {style}}}"' + "/>"
            else:
                return tag[:-1] + f' Style="{{StaticResource {style}}}"' + ">"
        return tag

    # Only apply to Views that are not SectionStyles? Let's apply to all Views but with heuristic
    # We'll skip if file is SectionStyles (those are style files, not views)
    if "SectionStyles" not in fpath:
        content = re.sub(r'<Border\b[^>]*>', add_border_card_style, content)

    if content != original:
        open(fpath, 'w', encoding='utf-8').write(content)
        modified.append(fpath)
        print(f"Migrated {fpath}")

print(f"\nTotal modified: {len(modified)}")
for f in modified[:50]:
    print(f"  {f}")
