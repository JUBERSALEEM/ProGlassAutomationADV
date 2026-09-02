#!/usr/bin/env python3
"""
XAML parse-time validator for ProGlassAutomation.

WHY THIS EXISTS
───────────────
A WPF app fails at RUNTIME for markup mistakes the C# compiler cannot see.
BAML loading throws XamlParseException, and the reported line number often
points at the *consumer* of a broken resource rather than the mistake
itself. That produced a string of "fix one, hit the next" startup crashes:

    ERR-2FFFF1  Cannot find resource named 'SidebarNavItem'
    ERR-918BAB  Unrecognized ModifierKeys 'NONE'

This script statically reproduces the checks WPF performs while loading
BAML, so every instance of these bug classes is found in ONE pass.

CHECKS
──────
  R1  {StaticResource K} that cannot resolve at parse time
  R2  {StaticResource K} pointing at a RUNTIME-merged language key
  R3  Merged style dictionary forward-referencing an App.xaml inline key
  M1  Modifiers="None"  (WPF's ModifierKeysConverter rejects it)
  M2  KeyBinding without a Key/Gesture
  E1  Invalid enum literals for common typed properties
  D1  Duplicate x:Key within one dictionary scope
  C1  x:Class with no matching code-behind file

Exit code 0 = clean, 1 = violations found.
"""

import os
import re
import sys
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Directories that are not part of the shipped app.
SKIP_DIRS = {"Tests", "Tools", "tools", "Licensing", "bin", "obj",
             "node_modules", ".git", "docs", "Docs", "design-system"}

violations = []


def add(code, path, line, msg):
    violations.append((code, os.path.relpath(path, ROOT).replace("\\", "/"),
                       line, msg))


def all_xaml():
    for base, dirs, files in os.walk(ROOT):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for f in files:
            if f.endswith(".xaml"):
                yield os.path.join(base, f)


def read(path):
    with open(path, encoding="utf-8-sig") as fh:
        return fh.read()


def strip_comments(text):
    """Blank out <!-- --> but preserve line numbering."""
    def repl(m):
        return re.sub(r"[^\n]", " ", m.group(0))
    return re.sub(r"<!--.*?-->", repl, text, flags=re.S)


def line_of(text, index):
    return text.count("\n", 0, index) + 1


def rel_to_path(source, base_dir=None):
    """Resolve a ResourceDictionary Source= into a repo path.

    WPF supports three forms and they resolve differently:
      pack://application:,,,/Path/To.xaml   -> repo-root relative
      /Path/To.xaml                         -> repo-root relative
      Relative/To.xaml                      -> relative to the REFERRING file
    Getting the third case wrong makes locally-merged dictionaries look
    unresolvable and floods the report with false positives.
    """
    s = source.strip().replace("\\", "/")

    if s.startswith("pack://"):
        s = s.split(",,,", 1)[-1]
        s = s.split("component/")[-1]
        return os.path.join(ROOT, s.lstrip("/"))

    if s.startswith("/"):
        return os.path.join(ROOT, s.lstrip("/"))

    # Relative to the file that declares the merge.
    return os.path.join(base_dir if base_dir else ROOT, s)


# ══════════════════════════════════════════════════════════════════════
#  Build the application-scope resource key set, honouring merge ORDER.
# ══════════════════════════════════════════════════════════════════════

def keys_in(text):
    return set(re.findall(r'x:Key="([^"]+)"', text))


def merged_sources(text):
    """Sources listed inside <ResourceDictionary.MergedDictionaries>."""
    out = []
    for block in re.findall(
            r"<ResourceDictionary\.MergedDictionaries>(.*?)"
            r"</ResourceDictionary\.MergedDictionaries>", text, flags=re.S):
        out += re.findall(r'<ResourceDictionary[^>]*\sSource="([^"]+)"', block)
    return out


def collect_dictionary(path, seen=None):
    """All keys a dictionary contributes, including nested merges."""
    if seen is None:
        seen = set()
    real = os.path.normpath(path)
    if real in seen or not os.path.exists(real):
        return set()
    seen.add(real)

    text = strip_comments(read(real))
    result = set(keys_in(text))
    base = os.path.dirname(real)
    for src in merged_sources(text):
        result |= collect_dictionary(rel_to_path(src, base), seen)
    return result


APP_XAML = os.path.join(ROOT, "App.xaml")
app_text = strip_comments(read(APP_XAML))
app_merge_end = app_text.find("</ResourceDictionary.MergedDictionaries>")

# Keys declared inline in App.xaml (parsed AFTER merged dictionaries).
APP_INLINE_KEYS = keys_in(app_text[app_merge_end:]) if app_merge_end > 0 else set()

# Keys from merged dictionaries, accumulated in order.
APP_MERGED_KEYS = set()
MERGE_ORDER = []
for src in merged_sources(app_text):
    p = rel_to_path(src, os.path.dirname(APP_XAML))
    MERGE_ORDER.append((src, p, set(APP_MERGED_KEYS)))  # snapshot before
    APP_MERGED_KEYS |= collect_dictionary(p)

APP_SCOPE_KEYS = APP_MERGED_KEYS | APP_INLINE_KEYS

# Language dictionaries are merged at RUNTIME by LocalizationService, so
# their keys are NOT available to {StaticResource} at parse time.
LANG_DIR = os.path.join(ROOT, "Themes", "Languages")
LANG_KEYS = set()
if os.path.isdir(LANG_DIR):
    for f in os.listdir(LANG_DIR):
        if f.endswith(".xaml"):
            LANG_KEYS |= keys_in(strip_comments(read(os.path.join(LANG_DIR, f))))

# Theme dictionaries are swapped at runtime but one is always merged in
# App.xaml, so their keys ARE parse-time resolvable.
for theme in ("LightTheme.xaml", "DarkTheme.xaml"):
    tp = os.path.join(ROOT, "Themes", theme)
    if os.path.exists(tp):
        APP_SCOPE_KEYS |= collect_dictionary(tp)

# Keys written imperatively into Application.Resources at runtime
# (VisualThemeEngine / ThemeManager). Only ever consumed via
# DynamicResource, but collect them so R1 does not false-positive.
RUNTIME_KEYS = set()
for rel in ("Services/Theme/VisualThemeEngine.Part1.cs",
            "Services/Theme/VisualThemeEngine.Part2.cs",
            "Services/Theme/VisualThemeEngine.Part3.cs",
            "Services/ThemeManager.Part1.cs",
            "Services/ThemeManager.Part2.cs",
            "Services/Theme/ThemeStudioService.cs"):
    p = os.path.join(ROOT, rel)
    if os.path.exists(p):
        cs = read(p)
        RUNTIME_KEYS |= set(re.findall(r'resources\[\s*"([^"]+)"\s*\]', cs))
        RUNTIME_KEYS |= set(re.findall(
            r'(?:FreezeAndSet|SetTranslucent)\([^,]+,\s*"([^"]+)"', cs))


# ══════════════════════════════════════════════════════════════════════
#  R3 — merged style dictionaries may not use App.xaml inline keys
# ══════════════════════════════════════════════════════════════════════

for src, path, available_before in MERGE_ORDER:
    if not os.path.exists(path):
        add("R3", APP_XAML, 1, f"App.xaml merges '{src}' which does not exist.")
        continue

    text = strip_comments(read(path))
    own = collect_dictionary(path)

    for m in re.finditer(r"\{StaticResource\s+([A-Za-z0-9_.]+)\}", text):
        key = m.group(1)
        if key in own or key in available_before:
            continue
        if key in APP_INLINE_KEYS:
            add("R3", path, line_of(text, m.start()),
                f"references {{StaticResource {key}}}, but '{key}' is declared "
                f"INLINE in App.xaml. Merged dictionaries are parsed BEFORE "
                f"App.xaml's inline body, so this cannot resolve. Move '{key}' "
                f"into a dictionary merged earlier.")


# ══════════════════════════════════════════════════════════════════════
#  Per-file checks
# ══════════════════════════════════════════════════════════════════════

MERGED_PATHS = {os.path.normpath(p) for _, p, _ in MERGE_ORDER}

# Enum values WPF's type converters accept for frequently-used properties.
ENUMS = {
    "Visibility": {"Visible", "Hidden", "Collapsed"},
    "HorizontalAlignment": {"Left", "Center", "Right", "Stretch"},
    "VerticalAlignment": {"Top", "Center", "Bottom", "Stretch"},
    "Orientation": {"Horizontal", "Vertical"},
    "FontStyle": {"Normal", "Italic", "Oblique"},
    "TextWrapping": {"NoWrap", "Wrap", "WrapWithOverflow"},
    "TextAlignment": {"Left", "Right", "Center", "Justify"},
    "Stretch": {"None", "Fill", "Uniform", "UniformToFill"},
    "ScrollBarVisibility": {"Disabled", "Auto", "Hidden", "Visible"},
}

for path in all_xaml():
    raw = read(path)
    text = strip_comments(raw)

    # ── M1: Modifiers="None" ────────────────────────────────────────
    # WPF's ModifierKeysConverter throws NotSupportedException on the
    # literal "None" even though ModifierKeys.None exists as a value.
    # The correct markup is to OMIT the attribute entirely.
    for m in re.finditer(r'Modifiers\s*=\s*"\s*None\s*"', text, re.I):
        add("M1", path, line_of(text, m.start()),
            'Modifiers="None" — WPF\'s ModifierKeysConverter throws '
            '"Unrecognized ModifierKeys \'NONE\'". Omit the Modifiers '
            "attribute instead.")

    # Gesture="None+Key" has the same root cause.
    for m in re.finditer(r'Gesture\s*=\s*"\s*None\s*\+', text, re.I):
        add("M1", path, line_of(text, m.start()),
            'Gesture="None+..." — remove the "None+" prefix.')

    # ── M2: KeyBinding needs a Key or Gesture ───────────────────────
    for m in re.finditer(r"<KeyBinding\b[^>]*>", text):
        tag = m.group(0)
        if "Key=" not in tag and "Gesture=" not in tag:
            add("M2", path, line_of(text, m.start()),
                "KeyBinding has neither Key nor Gesture.")

    # ── E1: invalid enum literals ───────────────────────────────────
    for prop, valid in ENUMS.items():
        for m in re.finditer(
                r'(?<![A-Za-z0-9_.])' + prop + r'\s*=\s*"([^"{}]+)"', text):
            val = m.group(1).strip()
            if not val or val in valid:
                continue
            add("E1", path, line_of(text, m.start()),
                f'{prop}="{val}" is not a valid value. '
                f"Expected one of: {', '.join(sorted(valid))}.")

    # ── D1: duplicate x:Key in one file ─────────────────────────────
    counts = defaultdict(list)
    for m in re.finditer(r'x:Key="([^"]+)"', text):
        counts[m.group(1)].append(line_of(text, m.start()))
    for key, lines in counts.items():
        if len(lines) > 1:
            add("D1", path, lines[1],
                f"duplicate x:Key '{key}' (also at line {lines[0]}).")

    # ── C1: x:Class must have code-behind ───────────────────────────
    cls = re.search(r'x:Class="([^"]+)"', text)
    if cls and not os.path.exists(path + ".cs"):
        add("C1", path, line_of(text, cls.start()),
            f'x:Class="{cls.group(1)}" but {os.path.basename(path)}.cs '
            "is missing.")

    # ── R1/R2: StaticResource resolution ────────────────────────────
    # Skip dictionaries already covered by the ordered R3 walk.
    if os.path.normpath(path) in MERGED_PATHS:
        continue

    local = set(keys_in(text))
    for src in merged_sources(text):
        local |= collect_dictionary(rel_to_path(src, os.path.dirname(path)))

    resolvable = local | APP_SCOPE_KEYS | RUNTIME_KEYS

    for m in re.finditer(r"\{StaticResource\s+([A-Za-z0-9_.]+)\}", text):
        key = m.group(1)
        if key in resolvable:
            continue
        ln = line_of(text, m.start())

        if key in LANG_KEYS:
            add("R2", path, ln,
                f"{{StaticResource {key}}} — '{key}' lives in a language "
                "dictionary merged at RUNTIME by LocalizationService, so it "
                "cannot resolve at parse time. Use {DynamicResource "
                f"{key}}} instead.")
        else:
            add("R1", path, ln,
                f"{{StaticResource {key}}} cannot be resolved from this "
                "file's scope or the application scope.")


# ══════════════════════════════════════════════════════════════════════
#  Report
# ══════════════════════════════════════════════════════════════════════

SEVERITY = {
    "R1": "FATAL  ", "R2": "FATAL  ", "R3": "FATAL  ",
    "M1": "FATAL  ", "M2": "FATAL  ", "E1": "FATAL  ",
    "C1": "FATAL  ", "D1": "warning",
}

if not violations:
    print("XAML validation: CLEAN — no parse-time faults found.")
    print(f"  app-scope keys : {len(APP_SCOPE_KEYS)}")
    print(f"  runtime keys   : {len(RUNTIME_KEYS)}")
    print(f"  language keys  : {len(LANG_KEYS)}")
    sys.exit(0)

by_code = defaultdict(list)
for v in violations:
    by_code[v[0]].append(v)

print("=" * 72)
print(" XAML PARSE-TIME VALIDATION FAILURES")
print("=" * 72)
for code in sorted(by_code):
    print(f"\n── {code} ({SEVERITY.get(code, '?')}) — "
          f"{len(by_code[code])} issue(s) " + "─" * 20)
    for _, path, line, msg in sorted(by_code[code], key=lambda x: (x[1], x[2])):
        print(f"  {path}:{line}")
        print(f"      {msg}")

fatal = sum(len(v) for c, v in by_code.items() if SEVERITY.get(c) == "FATAL  ")
print(f"\n{'=' * 72}")
print(f" TOTAL: {len(violations)} issue(s), {fatal} fatal")
print("=" * 72)
sys.exit(1 if fatal else 0)
