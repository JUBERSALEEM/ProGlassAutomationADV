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
    """Single-pass lexer: keep code, drop comments and string/char CONTENT.

    The previous version ran four regexes, comments first. That was wrong in
    two ways and lost 87% of some files (GroqAiService.cs went 7926 -> 1001
    chars, taking `_settings.GroqModel` with it):

      1. stripping `//` before strings truncates any line containing a URL -
         `"https://x"; DoThing();` lost everything after the colon;
      2. a quote left behind by (1) then paired with a much later quote and
         swallowed all the real code in between.

    Order cannot fix this; only a lexer that tracks state can. Interpolated
    strings keep the code inside {...} so `nameof(Foo)` still counts as a
    reference to Foo - dropping it would invent false positives.
    """
    out = []
    i, n = 0, len(t)
    while i < n:
        c = t[i]
        # ── comments ───────────────────────────────────────────────
        if c == '/' and i + 1 < n:
            if t[i + 1] == '/':
                j = t.find('\n', i)
                i = n if j < 0 else j
                out.append(' '); continue
            if t[i + 1] == '*':
                j = t.find('*/', i + 2)
                i = n if j < 0 else j + 2
                out.append(' '); continue
        # ── string / char literals (with optional $ and @ prefixes) ─
        p = i
        prefixes = ''
        while p < n and t[p] in '$@':
            prefixes += t[p]; p += 1
        if p < n and t[p] in '"\'':
            quote = t[p]
            verbatim = '@' in prefixes
            interpolated = '$' in prefixes
            j = p + 1
            depth = 0
            while j < n:
                ch = t[j]
                if depth:                       # code inside {...} of $"..."
                    if ch == '{': depth += 1
                    elif ch == '}': depth -= 1
                    out.append(ch); j += 1; continue
                if verbatim:
                    if ch == quote:
                        if j + 1 < n and t[j + 1] == quote: j += 2; continue
                        break
                else:
                    if ch == '\\': j += 2; continue
                    if ch == quote: break
                if interpolated and ch == '{':
                    if j + 1 < n and t[j + 1] == '{': j += 2; continue
                    depth = 1; j += 1; continue
                out.append(' '); j += 1
            i = min(j + 1, n)
            out.append(' '); continue
        out.append(c); i += 1
    return ''.join(out)

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
