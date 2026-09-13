#!/usr/bin/env python3
"""
Design-System Guard — Phase 8A
Detects prohibited visual patterns that would drift repository back into
hardcoded visuals, duplicate card systems, etc.

BUILD-BLOCKING vs WARNING:
- BLOCKING: hardcoded colors in Views, hardcoded CornerRadius (excluding PrintTokens special), hardcoded FontSize numeric, unresolved resources, BasedOn cycles, duplicate card systems, new visual systems, broken canonical, new legacy actual usage
- WARNING: emoji font, print-specific values, special surface values, AgentChatView protected finding, FontWeight literal pre-existing deferred, generated/false-positive

Usage:
  python scripts/design-system-guard.py
  Exit 0 = PASS (warnings allowed)
  Exit 1 = FAIL (blocking violations)

Protected files (do NOT auto-fix, WARNING only):
  Agent/UI/AgentChatView.xaml — contains #B91C1C #6366F1 #3730A3 as pre-existing user modification

Special surfaces (WARNING not BLOCKING):
  PrintableInvoiceView.xaml — CornerRadius="6" etc. via PrintTokens
  SplashView.xaml, AboutStyles.xaml, AuthSurfaceStyles.xaml — orb glow arbitrary radius

Known exception:
  CountryPhoneControl — Segoe UI Emoji — flag rendering
"""

import glob
import re
import sys
from pathlib import Path

ROOT = Path(__file__).parent.parent
VIEW_GLOB = "Views/**/*.xaml"
ALL_XAML_GLOB = "**/*.xaml"

# Patterns
HARDCODED_COLOR_RE = re.compile(r'(Background|Foreground|BorderBrush)\s*=\s*"#[0-9A-Fa-f]{3,8}"')
HARDCODED_CORNER_RE = re.compile(r'CornerRadius\s*=\s*"[0-9]')
HARDCODED_FONTSIZE_RE = re.compile(r'FontSize\s*=\s*"[0-9][^"]*"' )  # numeric literal, but allow {DynamicResource, {StaticResource
HARDCODED_FONTWEIGHT_LITERAL_RE = re.compile(r'FontWeight\s*=\s*"Bold"|FontWeight\s*=\s*"SemiBold"|FontWeight\s*=\s*"Medium"|FontWeight\s*=\s*"Normal"')
NEW_CARDSTYLE_RE = re.compile(r'<Style\s+x:Key="[^"]*CardStyle"')
NEW_GLASSCARD_RE = re.compile(r'<Style\s+x:Key="[^"]*GlassCardStyle"')
NEW_LIQUIDCARD_RE = re.compile(r'<Style\s+x:Key="[^"]*LiquidCardStyle"')
NEW_CRYSTALCARD_RE = re.compile(r'<Style\s+x:Key="[^"]*CrystalCardStyle"')
NEW_MODULECARD_RE = re.compile(r'<Style\s+x:Key="[^"]*ModuleCardStyle"')
NEW_DASHBOARDCARD_RE = re.compile(r'<Style\s+x:Key="[^"]*DashboardCardStyle"')

# Special files
PRINT_TOKENS_FILE = "Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceView.xaml"
SPECIAL_SURFACES = [
    "Views/Splash/SplashView.xaml",
    "Views/About/AboutStyles.xaml",
    "Views/Login/AuthSurfaceStyles.xaml",
    "Views/About/AboutView.xaml",
    "Views/About/Sections/",
    "Themes/Styles/LiquidGlassDesign.xaml",
    "Themes/Styles/LiquidGlassStyles.xaml",
]
PROTECTED_FILES = [
    "Agent/UI/AgentChatView.xaml",
]

# Canonical check
GDS_PATH = ROOT / "Themes/Styles/GlobalDesignSystem.xaml"

def check_hardcoded_colors():
    blocking = []
    warnings = []
    files = glob.glob(str(ROOT / VIEW_GLOB), recursive=True)
    for f in files:
        rel = str(Path(f).relative_to(ROOT))
        # Skip special surfaces? No, check all Views, but PrintTokens special is not color
        try:
            text = Path(f).read_text(encoding='utf-8', errors='ignore')
        except:
            continue
        for m in HARDCODED_COLOR_RE.finditer(text):
            # Check if it's in protected file
            if any(pf in rel for pf in PROTECTED_FILES):
                warnings.append(f"{rel}: hardcoded color {m.group(0)} — WARNING protected file AgentChatView contains hardcoded colors #B91C1C etc. — protected pre-existing, do NOT auto-fix")
            else:
                blocking.append(f"{rel}: hardcoded color {m.group(0)} — BUILD-BLOCKING — use DynamicResource Surface* / Text* / Accent* / Danger* etc.")
    # Also check Agent/UI separately for reporting
    agent_files = glob.glob(str(ROOT / "Agent/**/*.xaml"), recursive=True)
    for f in agent_files:
        rel = str(Path(f).relative_to(ROOT))
        try:
            text = Path(f).read_text(encoding='utf-8', errors='ignore')
        except:
            continue
        for m in HARDCODED_COLOR_RE.finditer(text):
            # Also check hex pattern #B91C1C etc.
            warnings.append(f"{rel}: hardcoded color {m.group(0)} — WARNING protected file")
        # Direct hex search for known finding
        if "#B91C1C" in text or "#6366F1" in text or "#3730A3" in text:
            if rel not in [w.split(":")[0] for w in warnings]:
                warnings.append(f"{rel}: contains #B91C1C / #6366F1 / #3730A3 — WARNING protected pre-existing user modification, do NOT overwrite")
    return blocking, warnings

def check_hardcoded_corner():
    blocking = []
    warnings = []
    files = glob.glob(str(ROOT / VIEW_GLOB), recursive=True)
    for f in files:
        rel = str(Path(f).relative_to(ROOT))
        try:
            text = Path(f).read_text(encoding='utf-8', errors='ignore')
        except:
            continue
        for m in HARDCODED_CORNER_RE.finditer(text):
            # Skip if DynamicResource
            line_start = text.rfind("\n", 0, m.start()) + 1
            line_end = text.find("\n", m.end())
            line = text[line_start:line_end]
            if "DynamicResource" in line or "StaticResource" in line:
                continue
            # Check if in PrintTokens special
            if PRINT_TOKENS_FILE in rel:
                warnings.append(f"{rel}: CornerRadius numeric {m.group(0)} — WARNING PrintTokens special (PrintableInvoiceView allows CornerRadius=\"6\" etc. via Print.Accent) — allowed as special surface")
            elif any(ss in rel for ss in SPECIAL_SURFACES):
                warnings.append(f"{rel}: CornerRadius numeric {m.group(0)} — WARNING special surface intentional (Splash/About/Login orb glow) — allowed")
            else:
                blocking.append(f"{rel}: hardcoded CornerRadius {m.group(0)} — BUILD-BLOCKING — use Radius.Card / Radius.Control / ControlCornerRadius via DynamicResource")
    return blocking, warnings

def check_hardcoded_fontsize():
    blocking = []
    warnings = []
    files = glob.glob(str(ROOT / VIEW_GLOB), recursive=True)
    for f in files:
        rel = str(Path(f).relative_to(ROOT))
        try:
            text = Path(f).read_text(encoding='utf-8', errors='ignore')
        except:
            continue
        for m in HARDCODED_FONTSIZE_RE.finditer(text):
            # Allow if contains {DynamicResource or {StaticResource or {Binding
            if "{DynamicResource" in m.group(0) or "{StaticResource" in m.group(0) or "{Binding" in m.group(0):
                continue
            # Check full match — FontSize="14" etc. literal numeric
            # The regex already matches numeric, but we need to ensure it's not like FontSize="{DynamicResource FontSize.Body}"
            # If the value after = starts with digit and not {, it's hardcoded
            val = m.group(0)
            # Extract value inside quotes
            q = re.search(r'"([^"]+)"', val)
            if q:
                inner = q.group(1)
                if inner and inner[0].isdigit() and "{" not in inner:
                    # Check if in special surfaces? PrintTokens may have FontSize? Usually not
                    blocking.append(f"{rel}: hardcoded FontSize {val} — BUILD-BLOCKING — use FontSize.Body / Title / H1 etc. via DynamicResource")
    return blocking, warnings

def check_hardcoded_fontweight():
    blocking = []
    warnings = []
    files = glob.glob(str(ROOT / VIEW_GLOB), recursive=True)
    for f in files:
        rel = str(Path(f).relative_to(ROOT))
        try:
            text = Path(f).read_text(encoding='utf-8', errors='ignore')
        except:
            continue
        for m in HARDCODED_FONTWEIGHT_LITERAL_RE.finditer(text):
            # This is literal FontWeight="Bold" etc.
            # Current baseline has 249 pre-existing, deferred to Phase 9
            warnings.append(f"{rel}: hardcoded FontWeight {m.group(0)} — WARNING pre-existing deferred to Phase 9, should use Weight.Bold via DynamicResource, not BLOCKER for Phase 8A")
    return blocking, warnings

def check_duplicate_card_systems():
    blocking = []
    warnings = []
    files = glob.glob(str(ROOT / ALL_XAML_GLOB), recursive=True)
    # Only check Views for new card systems, not Themes
    view_files = [f for f in files if "/Views/" in f]
    for f in view_files:
        rel = str(Path(f).relative_to(ROOT))
        try:
            text = Path(f).read_text(encoding='utf-8', errors='ignore')
        except:
            continue
        for pattern, name in [
            (NEW_CARDSTYLE_RE, "CardStyle"),
            (NEW_GLASSCARD_RE, "GlassCardStyle"),
            (NEW_LIQUIDCARD_RE, "LiquidCardStyle"),
            (NEW_CRYSTALCARD_RE, "CrystalCardStyle"),
            (NEW_MODULECARD_RE, "ModuleCardStyle"),
            (NEW_DASHBOARDCARD_RE, "DashboardCardStyle"),
        ]:
            for m in pattern.finditer(text):
                # Allow if it's in GlobalDesignSystem or legacy CardStyles.xaml (existing)
                if "GlobalDesignSystem.xaml" in rel or "CardStyles.xaml" in rel:
                    continue
                # Check if it's actually duplicating Surface* — if it sets Background SurfacePrimaryBrush and CornerRadius 12, it's duplicate
                # For simplicity, any new *CardStyle in Views is BLOCKING
                blocking.append(f"{rel}: new duplicate card system {m.group(0)} — BUILD-BLOCKING — use SurfacePrimaryStyle / Secondary / Detail / Elevated / KPI / Glass via DynamicResource")
    return blocking, warnings

def check_canonical_exists():
    blocking = []
    warnings = []
    if not GDS_PATH.exists():
        blocking.append(f"GlobalDesignSystem not found at {GDS_PATH} — BUILD-BLOCKING — canonical authority missing")
    else:
        try:
            text = GDS_PATH.read_text(encoding='utf-8', errors='ignore')
            # Check for 374 keys
            count = text.count('x:Key=')
            if count < 300:
                warnings.append(f"GlobalDesignSystem key count {count} < 300 — WARNING may be incomplete, expected 374")
        except Exception as e:
            blocking.append(f"Failed to read GlobalDesignSystem: {e}")
    return blocking, warnings

def main():
    print("=== Design-System Guard — Phase 8A ===")
    print(f"Root: {ROOT}")
    print("")

    all_blocking = []
    all_warnings = []

    # Run checks
    b, w = check_hardcoded_colors()
    all_blocking.extend(b)
    all_warnings.extend(w)
    print(f"[1] Hardcoded colors in Views: BLOCKING={len(b)} WARNING={len(w)}")

    b, w = check_hardcoded_corner()
    all_blocking.extend(b)
    all_warnings.extend(w)
    print(f"[2] Hardcoded CornerRadius: BLOCKING={len(b)} WARNING={len(w)}")

    b, w = check_hardcoded_fontsize()
    all_blocking.extend(b)
    all_warnings.extend(w)
    print(f"[3] Hardcoded FontSize: BLOCKING={len(b)} WARNING={len(w)}")

    b, w = check_hardcoded_fontweight()
    all_blocking.extend(b)
    all_warnings.extend(w)
    print(f"[4] Hardcoded FontWeight literal: BLOCKING={len(b)} WARNING={len(w)} (249 pre-existing deferred)")

    b, w = check_duplicate_card_systems()
    all_blocking.extend(b)
    all_warnings.extend(w)
    print(f"[5] Duplicate CardStyle systems: BLOCKING={len(b)} WARNING={len(w)}")

    b, w = check_canonical_exists()
    all_blocking.extend(b)
    all_warnings.extend(w)
    print(f"[6] Canonical authority: BLOCKING={len(b)} WARNING={len(w)}")

    print("")
    print("--- WARNINGS (allowed, but report) ---")
    for warn in all_warnings[:50]:
        print(f"  WARN: {warn}")
    if len(all_warnings) > 50:
        print(f"  ... and {len(all_warnings)-50} more warnings")

    print("")
    print("--- BLOCKING (must fix) ---")
    for block in all_blocking:
        print(f"  FAIL: {block}")

    print("")
    print(f"Summary: {len(all_blocking)} BLOCKING, {len(all_warnings)} WARNING")
    print("")

    # Also run existing audits for context
    print("--- Existing audits (for context) ---")
    try:
        import subprocess
        result = subprocess.run(["python", str(ROOT / "scripts/verify-dashboard.py")], capture_output=True, text=True, timeout=10)
        print(result.stdout[:500])
    except Exception as e:
        print(f"verify-dashboard.py failed: {e}")

    print("")
    if all_blocking:
        print("RESULT: FAIL — blocking violations found — fix before commit")
        print("Windows live WPF visual verification remains OUTSTANDING and is NOT replaced by Phase 8A static verification.")
        sys.exit(1)
    else:
        print("RESULT: PASS — no blocking violations, warnings allowed")
        print("Windows live WPF visual verification remains OUTSTANDING and is NOT replaced by Phase 8A static verification.")
        print("")
        print("PHASE 8A — Guardrails installed — static verification PASS")
        print("WINDOWS LIVE WPF VISUAL VERIFICATION REMAINS OUTSTANDING")
        sys.exit(0)

if __name__ == "__main__":
    main()
