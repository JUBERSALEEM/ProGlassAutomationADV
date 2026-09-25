#!/usr/bin/env python3
import os, glob, re, sys
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
SECTIONS_DIR = os.path.join(ROOT, 'Views', 'Settings', 'Sections')
CARDS_DIR = os.path.join(SECTIONS_DIR, 'Cards')
SHELL_PATH = os.path.join(ROOT, 'Views', 'Settings', 'SettingsView.xaml')
CARD_STYLES_PATH = os.path.join(ROOT, 'Views', 'Settings', 'SettingsCardStyles.xaml')
SECTION_STYLES_PATH = os.path.join(ROOT, 'Views', 'Settings', 'SettingsSectionStyles.xaml')

passed = 0
failed = 0

def test(name, condition, msg=""):
    global passed, failed
    if condition:
        passed += 1
        print(f"PASS  {name}")
    else:
        failed += 1
        print(f"FAIL  {name}: {msg}")

# 1. Shell hosts one page via template selector
shell_text = open(SHELL_PATH).read()
test("Shell_Hosts_One_Page_Via_Template_Selector",
     "ContentTemplateSelector" in shell_text and
     "EqualityToVisibilityConverter" not in shell_text and
     "sections:Settings" not in shell_text)

# 2. Sections carry no rhythmic margin literals
lit_margin_re = re.compile(r'Margin="([^"]+)"')
margin_fails = []
for f in glob.glob(f"{SECTIONS_DIR}/*.xaml"):
    txt = open(f).read()
    for m in lit_margin_re.finditer(txt):
        val = m.group(1)
        if val.startswith("{"): continue
        parts = val.split(',')
        vals = []
        for p in parts:
            try: vals.append(abs(int(p.strip())))
            except: vals.append(99)
        if any(v > 4 for v in vals):
            margin_fails.append(f"{os.path.basename(f)}: Margin=\"{val}\"")
test("Sections_Carry_No_Rhythmic_Margin_Literals", len(margin_fails) == 0, ", ".join(margin_fails[:3]))

# 3. Spacing scale defines module gaps
card_styles_text = open(CARD_STYLES_PATH).read()
required_scale_keys = [
    "SettingsCardMargin", "SettingsRowMargin", "SettingsTrailingGap",
    "SettingsStackGap8", "SettingsInlineGap8R", "SettingsPagePadding"
]
missing_scale = [k for k in required_scale_keys if f'x:Key="{k}"' not in card_styles_text]
test("Spacing_Scale_Defines_The_Module_Gaps", len(missing_scale) == 0, f"Missing: {missing_scale}")

# 4. Sections carry no visual literals
hex_re = re.compile(r'#[0-9A-Fa-f]{6,8}')
corner_re = re.compile(r'CornerRadius="[0-9]')
font_re = re.compile(r'FontSize="[0-9]')
visual_fails = []
for f in glob.glob(f"{SECTIONS_DIR}/*.xaml"):
    txt = open(f).read()
    base = os.path.basename(f)
    if hex_re.search(txt): visual_fails.append(f"{base}: hex color")
    if corner_re.search(txt): visual_fails.append(f"{base}: CornerRadius literal")
    if font_re.search(txt): visual_fails.append(f"{base}: FontSize literal")
test("Sections_Carry_No_Visual_Literals", len(visual_fails) == 0, ", ".join(visual_fails[:3]))

# 5. Interactive controls carry automation name
control_re = re.compile(r'^\s*<(ToggleButton|Slider|CheckBox|ListBox)\b')
a11y_fails = []
for f in glob.glob(f"{SECTIONS_DIR}/*.xaml"):
    lines = open(f).readlines()
    for i, line in enumerate(lines):
        if control_re.search(line):
            window = "\n".join(lines[i:i+4])
            if "AutomationProperties.Name" not in window:
                a11y_fails.append(f"{os.path.basename(f)}:{i+1}")
test("Every_Interactive_Control_Carries_An_Automation_Name", len(a11y_fails) == 0, ", ".join(a11y_fails[:3]))

# 6. File size budgets
shell_lines = len(open(SHELL_PATH).readlines())
test("Shell_Stays_Under_420_Lines", shell_lines <= 420, f"Got {shell_lines}")

section_line_fails = []
for f in glob.glob(f"{SECTIONS_DIR}/*.xaml"):
    cnt = len(open(f).readlines())
    if cnt > 400: section_line_fails.append(f"{os.path.basename(f)}: {cnt}")
test("Sections_Stay_Under_400_Lines", len(section_line_fails) == 0, ", ".join(section_line_fails))

card_line_fails = []
if os.path.exists(CARDS_DIR):
    for f in glob.glob(f"{CARDS_DIR}/*.xaml"):
        cnt = len(open(f).readlines())
        if cnt > 520: card_line_fails.append(f"{os.path.basename(f)}: {cnt}")
test("Cards_Stay_Under_520_Lines", len(card_line_fails) == 0, ", ".join(card_line_fails))

# 7. Card grammar keys
grammar_keys = [
    "SettingsPageCardStyle", "SettingsPageTitleStyle", "SettingsPagePadding",
    "SettingsCardTitleStyle", "SettingsCardHeaderNoteStyle", "SettingsGroupCaptionStyle",
    "SettingsRowTitleStyle", "SettingsRowMetaStyle", "SettingsRowSeparatorStyle",
    "SettingsInnerTileStyle", "SettingsIconTileStyle", "SettingsIconGlyphStyle",
    "SettingsWashTileStyle", "SettingsDangerCardStyle", "SettingsDangerCardTitleStyle",
    "SettingsDangerCardNoteStyle", "SettingsChipStyle", "SettingsChipTextStyle",
    "SettingsStatusDotStyle", "SettingsSegmentItemStyle", "SettingsSegmentTrackStyle",
    "SettingsSegmentTrackFillStyle"
]
missing_grammar = [k for k in grammar_keys if f'x:Key="{k}"' not in card_styles_text]
test("Settings_module_grammar_exposes_card_design_system", len(missing_grammar) == 0, f"Missing: {missing_grammar}")

# 8. Behaviour keys in SectionStyles
section_styles_text = open(SECTION_STYLES_PATH).read()
behaviour_keys = [
    "RoundedPasswordBoxStyle", "AccentChipStyle", "ProviderSelectButtonStyle",
    "DownloadProgressBarStyle", "ModernSliderStyle", "SettingsNavButtonStyle",
    "SettingsNavFocusStyle", "SettingsActionButtonStyle"
]
missing_beh = [k for k in behaviour_keys if f'x:Key="{k}"' not in section_styles_text]
test("SettingsSectionStyles_exposes_behaviour_keys", len(missing_beh) == 0, f"Missing: {missing_beh}")

# 9. Voice page contract
voice_text = open(os.path.join(SECTIONS_DIR, "SettingsVoiceSection.xaml")).read()
test("Voice_page_contract",
     "ModelOption_MouseLeftButtonDown" in voice_text and
     'Tag="{Binding Key}"' in voice_text and
     "WhisperModelOptions" in voice_text and
     "WhisperStatusChipBackgroundKey" in voice_text and
     "WhisperStatusChipForegroundKey" in voice_text)

# 10. Density and Mode segmented controls
app_text = open(os.path.join(SECTIONS_DIR, "SettingsAppearanceSection.xaml")).read()
# Note: Mode and Density were split into cards in v3
mode_card_path = os.path.join(CARDS_DIR, "AppearanceModeCard.xaml")
dens_card_path = os.path.join(CARDS_DIR, "AppearanceDensityCard.xaml")
comb_app_text = app_text
if os.path.exists(mode_card_path): comb_app_text += open(mode_card_path).read()
if os.path.exists(dens_card_path): comb_app_text += open(dens_card_path).read()

test("Appearance_density_and_mode_controls",
     "SettingsSegmentTrackFillStyle" in comb_app_text and
     "DisplayDensityOptions" in comb_app_text and
     "SettingsSegmentTrackStyle" in comb_app_text and
     'Tag="Light"' in comb_app_text and 'Tag="Dark"' in comb_app_text)

# 11. Shell page header live mode chip
test("Shell_page_header_chip_reports_live_colour_mode",
     "SettingsChipStyle" in shell_text and
     "ThemeModeChipBackgroundKey" in shell_text and
     "ThemeModeChipForegroundKey" in shell_text and
     "Settings.ModeBadgeLight" in shell_text and
     "Settings.ModeBadgeDark" in shell_text)

print(f"\nRESULTS: {passed} passed, {failed} failed")
if failed > 0: sys.exit(1)
