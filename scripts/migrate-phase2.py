#!/usr/bin/env python3
"""
Phase 2 migration: aggressive cleanup of remaining hardcoded colors and visual-only local styles
"""

import re, glob, os

def read_file(p):
    try:
        return open(p, encoding="utf-8", errors="ignore").read()
    except:
        return ""

# Find all hex colors in Views
hex_pattern = re.compile(r'(Color|Background|Foreground|BorderBrush|Fill|Stroke)\s*=\s*"(#(?:[0-9A-Fa-f]{3,8}))"', re.IGNORECASE)
# Also GradientStop Color
hex_pattern2 = re.compile(r'<GradientStop\s+Color="(#(?:[0-9A-Fa-f]{3,8}))"', re.IGNORECASE)

files = glob.glob("./Views/**/*.xaml", recursive=True)
all_hex = set()
for f in files:
    txt = read_file(f)
    for m in hex_pattern.finditer(txt):
        all_hex.add(m.group(2).upper())
    for m in hex_pattern2.finditer(txt):
        all_hex.add(m.group(1).upper())

print(f"Found {len(all_hex)} unique hex colors in Views:")
for h in sorted(all_hex):
    print(f"  {h}")

# Map known hex to tokens (including alpha variants)
# We'll create tokens for all remaining hex that are not already tokenized
# For known ones, we have mapping, for unknown, create generic token

known_map = {
    "#59FFFFFF": "About.GlowBrush1.Alpha59",
    "#00FFFFFF": "TransparentWhite",
    "#2EFFFFFF": "White.Alpha2E",
    "#1000FF00": "Chart.GreenBrush.Alpha10",
    "#10FF0000": "Chart.RedBrush.Alpha10",
    "#1A22C55E": "Chart.IndigoBrush.Alpha1A.5E",  # weird, but tokenize
    "#1AFF3B30": "Chart.LimeBrush.Alpha1A.30",
    "#1AFFA500": "Chart.MintBrush.Alpha1A",
    "#1A3B82F6": "Chart.NavyBrush.Alpha1A.F6",
    "#22808080": "Splash.TealBrush.Alpha22.80",
    "#33FFFFFF": "White.Alpha33",
    "#14808080": "Splash.DarkTealBrush.Alpha14.80",
    "#0D000000": "Black.Alpha0D",
    "#00000000": "Transparent",
    "#4CFFFFFF": "White.Alpha4C",
    "#12FFFFFF": "White.Alpha12",
    "#00FFFFFF": "TransparentWhite",
    "#28808080": "Splash.TealBrush.Alpha28.80",
    "#18808080": "Splash.TealBrush.Alpha18.80",
    "#59FFFF": "About.GlowBrush1",
    "#00FFFF": "About.GlowBrush2",
    "#2EFFFF": "Auth.SurfaceBrush",
    "#FFFFFF": "PaperBrush",
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
}

# Read GlobalDesignSystem to see existing tokens
gds_path = "./Themes/Styles/GlobalDesignSystem.xaml"
gds_content = read_file(gds_path)

# Add missing tokens to GlobalDesignSystem
missing_tokens = []
for hex_code in all_hex:
    # Normalize: if 6-digit, add FF alpha for token definition
    # Check if token already exists in GDS
    # Look for Color="#hex" in GDS
    if hex_code.upper() in gds_content.upper():
        continue
    # Find mapping
    token = known_map.get(hex_code.upper()) or known_map.get(hex_code)
    if not token:
        # Create generic token name from hex
        clean = hex_code.replace("#", "")
        token = f"Color.{clean}"
    # Define brush for this token if not exists
    # For 8-digit hex, it's ARGB, for 6-digit, assume FF alpha
    if len(hex_code) == 7:  # #RRGGBB
        color_val = hex_code
        brush_def = f'    <SolidColorBrush x:Key="{token}" Color="{color_val}"/>'
    elif len(hex_code) == 9:  # #AARRGGBB
        color_val = hex_code
        brush_def = f'    <SolidColorBrush x:Key="{token}" Color="{color_val}"/>'
    elif len(hex_code) == 4:  # #RGB
        color_val = hex_code
        brush_def = f'    <SolidColorBrush x:Key="{token}" Color="{color_val}"/>'
    elif len(hex_code) == 5:  # #ARGB
        color_val = hex_code
        brush_def = f'    <SolidColorBrush x:Key="{token}" Color="{color_val}"/>'
    else:
        color_val = hex_code
        brush_def = f'    <SolidColorBrush x:Key="{token}" Color="{color_val}"/>'
    missing_tokens.append((hex_code, token, brush_def))

print(f"\nMissing tokens to add to GlobalDesignSystem: {len(missing_tokens)}")
for hex_code, token, brush_def in missing_tokens:
    print(f"  {hex_code} -> {token}")

# Add missing tokens to GDS file before </ResourceDictionary>
if missing_tokens:
    # Find last </ResourceDictionary> and insert before
    insert_text = "\n    <!-- Auto-generated color tokens for remaining hardcoded hex in Views -->\n"
    for hex_code, token, brush_def in missing_tokens:
        insert_text += brush_def + "\n"
    # Insert before final </ResourceDictionary>
    if "</ResourceDictionary>" in gds_content:
        gds_content = gds_content.replace("</ResourceDictionary>", insert_text + "\n</ResourceDictionary>")
        open(gds_path, 'w', encoding='utf-8').write(gds_content)
        print(f"\nAdded {len(missing_tokens)} tokens to {gds_path}")

# Now replace hex in Views with DynamicResource
modified = []
for fpath in files:
    txt = read_file(fpath)
    original = txt
    # Replace Color="#hex" and Background="#hex" etc.
    for hex_code, token, _ in missing_tokens:
        # Replace case-insensitive
        # Pattern for Color="#hex"
        # Use regex to replace all occurrences of "#hex" in quotes with DynamicResource
        # Only for brush properties
        pattern = re.compile(re.escape(f'"{hex_code}"'), re.IGNORECASE)
        # Replace with DynamicResource token
        # But need to handle that hex_code may appear in different cases
        # We'll replace any quoted hex that matches this hex (case insensitive) with DynamicResource
        # Only if it's in a brush context? For simplicity, replace all quoted occurrences
        txt = pattern.sub(f'"{{DynamicResource {token}}}"', txt)
        # Also handle without quotes? No, always in quotes

    # Also handle known_map that were not in missing_tokens (already exist)
    for hex_code, token in known_map.items():
        pattern = re.compile(re.escape(f'"{hex_code}"'), re.IGNORECASE)
        txt = pattern.sub(f'"{{DynamicResource {token}}}"', txt)

    # Also handle generic hex replacement for any remaining #RRGGBB that is still hardcoded in Views
    # Find any remaining Background="#..." etc. that still has hex and not DynamicResource
    # We'll replace with PaperBrush or appropriate? For now, replace #FFFFFF with PaperBrush, others with SurfacePrimaryBrush
    def replace_remaining_hex(match):
        prop = match.group(1)
        hex_val = match.group(2)
        # If it's #FFFFFF or #FFF, use PaperBrush
        if hex_val.upper() in ["#FFFFFF", "#FFF", "#FFFFFFFF"]:
            return f'{prop}="{{DynamicResource PaperBrush}}"'
        # If it's transparent
        if hex_val.upper() in ["#00000000", "#00FFFFFF", "#00FFFFFF"]:
            return f'{prop}="{{DynamicResource Transparent}}"'
        # Otherwise, use SurfacePrimaryBrush as fallback
        return f'{prop}="{{DynamicResource SurfacePrimaryBrush}}"'

    txt = re.sub(r'(Background|Foreground|BorderBrush|Fill|Stroke|Color)\s*=\s*"(#(?:[0-9A-Fa-f]{3,8}))"', replace_remaining_hex, txt, flags=re.IGNORECASE)
    # GradientStop
    txt = re.sub(r'<GradientStop\s+Color="(#(?:[0-9A-Fa-f]{3,8}))"', lambda m: f'<GradientStop Color="{{DynamicResource PaperBrush}}"', txt, flags=re.IGNORECASE)

    if txt != original:
        open(fpath, 'w', encoding='utf-8').write(txt)
        modified.append(fpath)

print(f"\nModified {len(modified)} files for hex replacement:")
for f in modified:
    print(f"  {f}")

# Phase 2b: Delete visual-only local styles that are in mapping and have been replaced
# For each file in bypass list, delete local styles that are visual-only and in mapping

# Define visual-only detection: style without Template and without DataTrigger containing business logic keywords
def is_visual_only(style_text):
    # If contains <ControlTemplate or <DataTemplate, it's functional
    if "<ControlTemplate" in style_text or "<DataTemplate" in style_text:
        return False
    # If contains DataTrigger with business logic keywords, functional
    business_keywords = ["HasBalance", "IsMasterDn", "IsSelectedDnCurrent", "IsSelected", "IsCurrentDn", "HasBalance", "IsMaster", "IsActive", "IsEnabled", "IsMouseOver"]
    # Actually IsMouseOver is visual, not business, but we check for specific business
    # We'll check if DataTrigger Binding contains DataContext or business property
    if "DataTrigger" in style_text and "DataContext" in style_text:
        return False
    # If contains only setters for visual props, it's visual-only
    # Count setters
    # If it has more than 3 setters and no triggers, likely visual-only
    # For simplicity, if it has Template or DataTrigger with DataContext, keep, else visual-only
    if "DataTrigger" in style_text and "Binding" in style_text:
        # Check if binding is to business property (not just IsMouseOver)
        # If binding contains IsSelected, IsCurrent, HasBalance, IsMaster, etc., it's functional
        for kw in ["IsSelected", "IsCurrent", "HasBalance", "IsMaster", "IsActive", "IsEnabled", "IsMouseOver", "IsPressed"]:
            if kw in style_text and "DataContext" in style_text:
                return False
    # Otherwise, visual-only
    return True

# Local to global mapping (same as before)
local_to_global = {
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
    "TrustChipStyle": "Chip.Primary",
    "ChipGlyphStyle": "Typography.Icon",
    "ChipTextStyle": "Typography.Caption",
    "TermsDeclineButtonStyle": "Button.Secondary",
    "TermsContinueButtonStyle": "Button.Primary",
    "ChatIconBtn": "Button.Ghost",
    "SendBtn": "Button.Primary",
    "UserBubble": "SurfacePrimaryStyle",
    "AiBubble": "SurfaceSecondaryStyle",
    "ErrBubble": "SurfaceDetailStyle",
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
    "CardLabelStyle": "Typography.Label",
    "JoFormCardStyle": "SurfacePrimaryStyle",
    "JoActionBarStyle": "SurfaceSecondaryStyle",
    "JoFieldTileStyle": "SurfaceDetailStyle",
    "FamilyRowButtonStyle": "Button.Ghost",
    "GridNumericCenterCell": "Typography.DataGrid",
    "InvoicePartyLabelStyle": "Typography.Label",
    "InvoicePartyValueStyle": "Typography.Body",
    "InvoiceMetaChipStyle": "Chip.Primary",
    "SectionCardStyle": "SurfacePrimaryStyle",
    "ProviderCardBaseStyle": "SurfaceSecondaryStyle",
    "InfoCardTitleStyle": "Typography.Title",
    "SectionLabelStyle": "Typography.Label",
    "RowTitleStyle": "Typography.Title",
    "PageTitleStyle": "Typography.H1",
    "PageSubtitleStyle": "Typography.Subtitle",
    "StatLabel": "Typography.Label",
    "StatValue": "Typography.Kpi",
    "StatCard": "SurfaceKpiStyle",
    "InventoryKpiCell": "SurfaceKpiStyle",
    "ItemFieldLabel": "Typography.Label",
    "ItemEntry": "Input.TextBox",
    "ItemFieldError": "Typography.Caption",
    "SheetMenuActionItem": "Button.Secondary",
    "StepDotStyle": "Badge.Primary",
    "StepNumberStyle": "Typography.Caption",
    "StepTitleStyle": "Typography.Title",
    "InqHintStyle": "Typography.Caption",
    "InquiryStatusSpineStyle": "SurfaceDetailStyle",
    "InquiryStatusPillDynamicStyle": "Chip.Primary",
    "InquiryInitialsTileStyle": "SurfaceSecondaryStyle",
    "InquiryInitialsTextStyle": "Typography.Title",
    "InquiryStatusTabStyle": "Tab.Inactive",
    "ScFieldLabel": "Typography.Label",
    "ScTextBox": "Input.TextBox",
    "UcFieldLabel": "Typography.Label",
    "UcTextBox": "Input.TextBox",
    "UcPasswordBox": "Input.TextBox",
    "UcMeterSegment": "SurfaceDetailStyle",
    "CcFieldLabel": "Typography.Label",
    "CcTextBox": "Input.TextBox",
    "CrashFieldLabel": "Typography.Label",
    "CrashFieldValue": "Typography.Body",
    "CrashPrimaryButton": "Button.Primary",
    "CrashOutlinedButton": "Button.Secondary",
    "CrashGhostButton": "Button.Ghost",
    "QuickSettingsRow": "SurfaceDetailStyle",
    "QuickSettingsGlyph": "Typography.Icon",
    "QuickSettingsLabel": "Typography.Body",
    "QuickSettingsGroup": "Typography.Label",
    "StorageRowGlyph": "Typography.Icon",
    "StorageRowName": "Typography.Body",
    "StorageRowSize": "Typography.BodySmall",
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
    "TabCardInactiveStyle": "Tab.Inactive",
    "DnListBoxItemStyle": "SurfaceDetailStyle",
    "PendingRowCardStyle": "SurfaceSecondaryStyle",
    "InvListBoxItemStyle": "SurfaceDetailStyle",
    "InvTabInactiveStyle": "Tab.Inactive",
    "SpecColumnHeaderStyle": "Typography.Label",
    "FileNameTextBoxStyle": "Input.TextBox",
    "DnMenuActionItem": "Button.Secondary",
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

# Process bypass files
bypass_files = [
    "./Views/Settings/SettingsSectionStyles.xaml",
    "./Views/DeliveryNote/DeliveryNoteDetail/Sections/DnSectionStyles.xaml",
    "./Views/About/AboutStyles.xaml",
    "./Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml",
    "./Views/JobProfitability/JobProfitabilityView.xaml",
    "./Views/SheetStore/SheetDetailWindow.xaml",
    "./Views/Reports/StatementGenerator/Styles/WizardStyles.xaml",
    "./Views/JobProfitability/JobFullDetailView.xaml",
    "./Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml",
    "./Views/Inquiry/InquiryListView.xaml",
    "./Views/JobOrder/JobOrderDetail/Sections/JoSectionStyles.xaml",
    "./Views/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonView.xaml",
    "./Views/SheetStore/SheetStoreView.xaml",
    "./Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceView.xaml",
    "./Views/ProformaInvoice/Step9ProfitLoss/ServiceProfitabilityView.xaml",
    "./Views/Inquiry/InquiryEditorWindow.xaml",
    "./Views/Login/AuthSurfaceStyles.xaml",
    "./Views/Reports/OrderReport/Review/ReviewStep.xaml",
    "./Views/TaxInvoice/TaxInvoiceDetail/Sections/TiSectionStyles.xaml",
    "./Views/AI/AiChatView.xaml",
    "./Views/Splash/SplashView.xaml",
]

modified2 = []
for fpath in bypass_files:
    if not os.path.exists(fpath):
        continue
    txt = read_file(fpath)
    original = txt
    # Find all local styles
    styles = re.findall(r'<Style\s+x:Key="([^"]+)"[^>]*>.*?</Style>', txt, re.DOTALL)
    # Actually need to get full style text
    for match in re.finditer(r'<Style\s+x:Key="([^"]+)"[^>]*>.*?</Style>', txt, re.DOTALL):
        key = match.group(1)
        style_text = match.group(0)
        if key in local_to_global:
            if is_visual_only(style_text):
                # Replace with comment and alias to global
                global_style = local_to_global[key]
                # Create alias style that BasedOn global
                alias = f'<Style x:Key="{key}" TargetType="Control" BasedOn="{{StaticResource {global_style}}}"/>'
                # Actually need correct TargetType? Use generic or original TargetType
                # Extract TargetType from original
                tt_match = re.search(r'TargetType="([^"]+)"', style_text)
                tt = tt_match.group(1) if tt_match else "Control"
                alias = f'<Style x:Key="{key}" TargetType="{tt}" BasedOn="{{StaticResource {global_style}}}"/>'
                txt = txt.replace(style_text, f'<!-- Migrated {key} to global {global_style} -->\n    {alias}')

    if txt != original:
        open(fpath, 'w', encoding='utf-8').write(txt)
        modified2.append(fpath)

print(f"\nModified {len(modified2)} bypass files for visual-only local styles:")
for f in modified2:
    print(f"  {f}")
