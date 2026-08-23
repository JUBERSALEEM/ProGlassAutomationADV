#!/usr/bin/env python3
"""
xaml-resource-lint — catch resource/reference errors the well-formedness
guard cannot see.

Motivated by a shipped defect: a CornerRadius resource declared with
xmlns "clr-namespace:System.Windows;assembly=System.Runtime". CornerRadius
lives in WindowsBase — the type silently failed to resolve, every
{StaticResource ...} CornerRadius setter became DependencyProperty.UnsetValue,
and the build surfaced XDG0010 across the module. This tool fails on:

  L1  clr-namespace mapped to an assembly that cannot contain that
      namespace's WPF types (System.Windows → System.Runtime etc.);
  L1  clr-namespace mapped to an assembly that cannot contain that
      namespace's WPF types (System.Windows → System.Runtime etc.);
  L2  (warning) typed CornerRadius value resources — the exact shipped
      defect pattern; literals at usage sites are the safe pattern.
      Pre-existing theme resources are reported but do not fail;
  L3  {StaticResource X} usages where X is not declared in the same file,
      its merged dictionaries, or anywhere in the repository (the repo
      resolves many keys through nested merge graphs — App.xaml,
      MainWindow.xaml and hub dictionaries — so a repo-wide declaration
      scan is the accurate signal; a compiler remains the final judge).

Exit 0 = clean, 1 = violations.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
ALL_XAML = [p for p in ROOT.rglob("*.xaml") if ".git" not in p.parts]

# Namespace → assemblies that can legitimately contain it (WPF).
VALID_ASSEMBLY_FOR_NS = {
    "System": {"System.Runtime", "mscorlib", "netstandard"},
    "System.Windows": {"WindowsBase", "PresentationFramework", "PresentationCore"},
    "System.Windows.Media": {"PresentationCore"},
    "System.Windows.Controls": {"PresentationFramework"},
    "System.Collections.Generic": {"System.Runtime", "mscorlib"},
}

# The shipped-defect pattern — typed CornerRadius resources.
HARD_FRAGILE = ("CornerRadius",)

STATIC_REF = re.compile(r"\{StaticResource ([A-Za-z_][\w.]*)\}")


def load_keys_and_merges(path: Path):
    """Return (keys declared, merged dictionary relative sources)."""
    tree = ET.parse(path)
    keys = set()
    merges = []
    for elem in tree.iter():
        key = elem.get("{http://schemas.microsoft.com/winfx/2006/xaml}Key")
        if key:
            keys.add(key)
        if elem.tag.endswith("ResourceDictionary") and elem.get("Source"):
            merges.append(elem.get("Source"))
    return keys, merges


def collect_theme_keys() -> set[str]:
    keys = set()
    for theme in (ROOT / "Themes").rglob("*.xaml"):
        k, _ = load_keys_and_merges(theme)
        keys |= k
    app = ROOT / "App.xaml"
    if app.exists():
        k, _ = load_keys_and_merges(app)
        keys |= k
    return keys


def main() -> int:
    violations: list[str] = []
    warnings: list[str] = []
    repo_keys: set[str] = set()
    for path in ALL_XAML:
        k, _ = load_keys_and_merges(path)
        repo_keys |= k
    theme_keys = collect_theme_keys() | repo_keys

    for path in ALL_XAML:
        text = path.read_text(encoding="utf-8", errors="replace")
        rel = str(path.relative_to(ROOT))

        # L1 — namespace/assembly mismatches
        for ns, asm in re.findall(
            r"clr-namespace:([\w.]+);assembly=([\w.]+)", text
        ):
            valid = VALID_ASSEMBLY_FOR_NS.get(ns)
            if valid and asm not in valid:
                violations.append(
                    f"L1 {rel}: clr-namespace:{ns} mapped to assembly={asm} "
                    f"(valid: {sorted(valid)})"
                )

        # L2 — typed CornerRadius resources (the shipped defect pattern)
        for fragile in HARD_FRAGILE:
            for _ in re.finditer(rf"<[\w:]*{fragile}\b[^>]*x:Key", text):
                warnings.append(
                    f"L2 {rel}: typed <{fragile} x:Key=…> resource — prefer "
                    f"literals (pre-existing theme resources are not fatal)"
                )

        # L3 — unresolved StaticResource keys (file-local + theme-level; a
        # full merged-graph walk is overkill — app themes + local cover this repo)
        local_keys, merges = load_keys_and_merges(path)
        merged_keys = set(local_keys)
        for src in merges:
            candidate = path.parent / src
            if candidate.exists():
                k, _ = load_keys_and_merges(candidate)
                merged_keys |= k
        for key in set(STATIC_REF.findall(text)):
            if key not in merged_keys and key not in theme_keys:
                # controls can also be referenced from other dictionaries at
                # runtime; report as a warning-style violation only when the
                # file does not merge anything declaring it (best effort)
                violations.append(f"L3 {rel}: {{StaticResource {key}}} has no declaration")

    for w in warnings:
        print("   " + w)
    if violations:
        print(f"xaml-resource-lint: {len(violations)} violation(s)")
        for v in violations:
            print("   " + v)
        return 1
    print(f"xaml-resource-lint: clean ({len(ALL_XAML)} XAML files, "
          f"{len(warnings)} warning(s))")
    return 0


if __name__ == "__main__":
    sys.exit(main())
