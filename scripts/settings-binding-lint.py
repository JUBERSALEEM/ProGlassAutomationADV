#!/usr/bin/env python3
"""Guard: every Settings binding must be backed by real code, not just prose.

Regression this catches (found 2026-09-16): AiSecurityCard.xaml bound
KeepApiKeysSaved TwoWay, but the property had been lost in the phase-3b
promotion out of the god class. Repo-wide the name survived only in two
file-header COMMENTS claiming it "moved from SettingsViewModel.Security.cs"
and in one SettingsRedesignTests InlineData STRING. WPF fails a TwoWay
binding to a missing member silently, so the switch animated, looked like it
worked, and persisted nothing. The existing test passed throughout because it
asserts the XAML contains the binding text, never that a member backs it.

Method: build a corpus of .cs CODE with comments and string literals removed,
then flag any binding root that does not occur in it. A name that occurs only
in prose or in a test's InlineData is exactly the lost-member signature.

Deliberately NOT attempted: resolving each file's DataContext. Most Settings
cards bind sub-VMs or item templates, so per-file VM matching produces ~23%
false positives. "Occurs nowhere in real code" is context-independent and
holds whatever the DataContext turns out to be.
"""
import re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

def strip_comments_and_strings(t):
    t = re.sub(r'/\*.*?\*/', ' ', t, flags=re.S)          # block comments
    t = re.sub(r'//[^\n]*', ' ', t)                        # line comments
    t = re.sub(r'@"(?:[^"]|"")*"', ' ', t)                 # verbatim strings
    t = re.sub(r'\$?"(?:\\.|[^"\\])*"', ' ', t)            # normal/interpolated
    t = re.sub(r"'(?:\\.|[^'\\])*'", ' ', t)               # chars
    return t

def main():
    code = []
    for f in ROOT.rglob("*.cs"):
        if any(p in f.parts for p in ("obj", "bin", ".git")): continue
        code.append(strip_comments_and_strings(f.read_text(encoding="utf-8", errors="ignore")))
    corpus = "\n".join(code)
    words = set(re.findall(r'\b[A-Za-z_]\w*\b', corpus))

    SKIP = {"DataContext","SelectedItem","SelectedValue","Tag","Content","Items"}
    total, missing = 0, {}
    for f in sorted((ROOT / "Views" / "Settings").rglob("*.xaml")):
        t = f.read_text(encoding="utf-8", errors="ignore")
        for m in re.finditer(r'\{Binding\s+([^}]*)\}', t):
            inner = m.group(1)
            if re.search(r'RelativeSource|ElementName|Source=', inner): continue
            pm = re.match(r'\s*(?:Path=)?([A-Za-z_][\w.]*)', inner)
            if not pm: continue
            root = pm.group(1).split('.')[0]
            if root in SKIP: continue
            total += 1
            if root not in words:
                line = t[:m.start()].count("\n") + 1
                missing.setdefault(root, []).append(f"{f.relative_to(ROOT)}:{line}")

    print(f"  .cs files in corpus                : {len(code)}")
    print(f"  Settings XAML binding roots checked: {total}")
    if not missing:
        print("  every binding root occurs in real code")
        print("\n  PASS")
        return 0
    print("\n  UNBACKED BINDINGS (name occurs only in comments/strings - silent failure):")
    for k in sorted(missing):
        for loc in missing[k]:
            print(f"    {k:<28} {loc}")
    print(f"\n  FAIL: {len(missing)} unbacked binding root(s)")
    return 1

if __name__ == "__main__":
    sys.exit(main())
