#!/usr/bin/env python3
"""tools/audit-xaml.py — industrial XAML hygiene gate (Phase 1, audit 1.4).

Checks (CI-runnable; exit code 1 on *verified* violations):
  D1  Dead resource keys: registered with x:Key but never referenced from
      any XAML ({StaticResource}/{DynamicResource}) nor from any .cs as a
      quoted string literal. Themes/Languages/*.xaml are excluded (the i18n
      audit owns dictionary completeness).
        • VIEW-LOCAL dead keys  → FAIL (statically provable, zero risk)
        • THEME/APP/TOOL dead   → WARN (may be keyed at runtime by the
          theme engine — requires runtime verification before deletion)
  D2  Duplicate resource keys:
        • same file twice, or both in app-merged scope (App.xaml,
          Themes/**, Styles/**) → FAIL (merged-dictionary shadowing)
        • DarkTheme+LightTheme pairs are the SAME key by design → exempt
        • Tools/LicenseGenerator is an independent tool → exempt
        • view-local duplicates are scoped per-view → informational
  H1  Code-behind event-handler count per view (informational; >10 = smell).
  G1  XAML god files: files > MAX_XAML_LINES (informational).

Usage:
  python3 tools/audit-xaml.py            # report + exit 1 on violations
  python3 tools/audit-xaml.py --quiet    # violations only
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MAX_XAML_LINES = 800
HANDLER_SMELL = 10

SKIP_DIRS = {ROOT / "Themes" / "Languages"}

XAML_FILES = sorted(
    p for p in ROOT.rglob("*.xaml")
    if ".git" not in p.parts and not any(p.is_relative_to(d) for d in SKIP_DIRS)
)
CS_FILES = sorted(p for p in ROOT.rglob("*.cs") if ".git" not in p.parts)

KEY_REG = re.compile(r'\bx:Key="([^"]+)"')
USE_REG = re.compile(r"\{(?:Static|Dynamic)Resource\s+([A-Za-z0-9_.]+)\}")
CS_KEY_REG = re.compile(r'"([A-Za-z][A-Za-z0-9_.]+)"')

HANDLER_REG = re.compile(
    r"private\s+(?:async\s+)?void\s+\w+\(object sender|"
    r"void\s+\w+_(?:Click|Loaded|Unloaded|SelectionChanged|Checked|Unchecked|"
    r"TextChanged|KeyDown|KeyUp|Drop|DragOver|DragEnter|DragLeave|Closing|"
    r"Closed|MouseDown|MouseUp|PreviewMouseDown|PreviewMouseUp|SizeChanged|"
    r"ScrollChanged|GotFocus|LostFocus|PasswordChanged|ContextMenuOpening|"
    r"PreviewKeyDown|PreviewKeyUp|MouseEnter|MouseLeave|IsVisibleChanged|"
    r"SelectionChanged|ValueChanged|PropertyChanged|DropDownOpened|"
    r"DropDownClosed|PreviewTextInput|Paste)\s*\("
)


def scope_of(fp: Path) -> str:
    """app = merged into the app's resource scope; tool = standalone tool;
    view = scoped to one view/control."""
    rel = fp.relative_to(ROOT).as_posix()
    if rel == "App.xaml" or rel.startswith("Themes/") or rel.startswith("Styles/"):
        return "app"
    if rel.startswith("Tools/"):
        return "tool"
    return "view"


def is_theme_pair(files: list[Path]) -> bool:
    names = {f.name for f in files}
    return names == {"DarkTheme.xaml", "LightTheme.xaml"} and all(
        "Themes" in f.parts for f in files)


def main() -> int:
    quiet = "--quiet" in sys.argv

    registrations: dict[str, list[Path]] = {}
    usages: dict[str, list[Path]] = {}
    for fp in XAML_FILES:
        text = fp.read_text(encoding="utf-8", errors="ignore")
        for key in KEY_REG.findall(text):
            registrations.setdefault(key, []).append(fp)
        for key in USE_REG.findall(text):
            usages.setdefault(key, []).append(fp)

    cs_refs: set[str] = set()
    for fp in CS_FILES:
        cs_refs.update(CS_KEY_REG.findall(fp.read_text(encoding="utf-8", errors="ignore")))

    dead_view: list[tuple[str, list[Path]]] = []
    dead_theme: list[tuple[str, list[Path]]] = []
    dup_fail: list[tuple[str, list[Path]]] = []
    dup_warn: list[tuple[str, list[Path]]] = []
    for key, files in sorted(registrations.items()):
        if key not in usages and key not in cs_refs:
            if all(scope_of(f) == "view" for f in files):
                dead_view.append((key, files))
            else:
                dead_theme.append((key, files))
        if len(files) > 1 and not is_theme_pair(files):
            scopes = {scope_of(f) for f in files}
            if len(set(files)) < len(files) or scopes == {"app"}:
                dup_fail.append((key, files))          # same-file or app-merged shadowing
            elif scopes == {"tool"}:
                dup_warn.append((key, files))
            else:
                dup_warn.append((key, files))          # view-local override = informational

    handlers: list[tuple[Path, int]] = []
    for fp in sorted(p for p in ROOT.rglob("*.xaml.cs") if ".git" not in p.parts):
        n = len(HANDLER_REG.findall(fp.read_text(encoding="utf-8", errors="ignore")))
        if n:
            handlers.append((fp, n))

    god_files = []
    for p in XAML_FILES:
        n = len(p.read_text(encoding="utf-8", errors="ignore").splitlines())
        if n > MAX_XAML_LINES:
            god_files.append((p, n))

    if not quiet:
        print(f"audit-xaml: {len(XAML_FILES)} xaml, {len(CS_FILES)} cs, "
              f"{len(registrations)} unique keys registered, {len(usages)} referenced")

    violations = 0

    def dump(title: str, items: list, fail: bool) -> None:
        nonlocal violations
        if items:
            print(f"\n{title} ({len(items)})")
            for key, files in items:
                print(f"   {key}  @ {', '.join(str(f.relative_to(ROOT)) for f in files)}")
            if fail:
                violations += len(items)

    dump("D1 DEAD RESOURCE KEYS — view-local (REMOVE)", dead_view, fail=True)
    dump("D1 DEAD RESOURCE KEYS — theme/app/tool (WARN: verify runtime keying first)",
         dead_theme, fail=False)
    dump("D2 DUPLICATE RESOURCE KEYS — app scope (FIX)", dup_fail, fail=True)
    dump("D2 DUPLICATE RESOURCE KEYS — informational", dup_warn, fail=False)

    if handlers:
        smell = [h for h in handlers if h[1] > HANDLER_SMELL]
        if not quiet or smell:
            print(f"\nH1 code-behind event handlers ({len(handlers)} files with handlers)")
        for fp, n in sorted(handlers, key=lambda t: -t[1])[:12]:
            flag = "  <-- smell (>%d)" % HANDLER_SMELL if n > HANDLER_SMELL else ""
            print(f"   {n:3d}  {fp.relative_to(ROOT)}{flag}")
    if god_files:
        print(f"\nG1 XAML god files (> {MAX_XAML_LINES} lines):")
        for fp, n in sorted(god_files, key=lambda t: -t[1]):
            print(f"   {n:5d}  {fp.relative_to(ROOT)}")

    print(f"\nRESULT: {'FAIL (' + str(violations) + ' violations)' if violations else 'PASS'}")
    return 1 if violations else 0


if __name__ == "__main__":
    sys.exit(main())
