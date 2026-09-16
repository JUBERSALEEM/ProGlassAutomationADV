#!/usr/bin/env python3
"""Guard: every XAML binding must be backed by real code, not just prose.

REGRESSION THIS CATCHES (found 2026-09-16, commit e5c09ec)
─────────────────────────────────────────────────────────
Views/Settings/Sections/Cards/AiSecurityCard.xaml:50 bound

    IsChecked="{Binding KeepApiKeysSaved, Mode=TwoWay}"

but the property had been lost in the phase-3b promotion out of the god class.
Repo-wide the name survived in only three places, none of them a declaration:
two file-header COMMENTS claiming it "moved from SettingsViewModel.Security.cs"
and one SettingsRedesignTests InlineData STRING. WPF fails a TwoWay binding to
a missing member silently, so the switch animated, looked like it worked, and
persisted no preference - on a security control deciding whether API keys
outlive the session. SettingsRedesignTests passed throughout because it asserts
the XAML CONTAINS the binding text, never that a member backs it.

METHOD
──────
Lex all .cs into code only (comments and string/char literal CONTENT removed,
but code inside $"...{...}" interpolations kept), take the union of its
identifiers, then flag any binding root absent from that union. A name that
survives only in prose or in a test's InlineData is exactly the lost-member
signature.

XAML is lexed too: <!-- --> comments are stripped, because usage examples in
file-header banners contain bindings that were never live. Without this the
app-wide scan reported Controls/EmptyState/EmptyStateControl.xaml:19
`{Binding NewCommand}` - a documentation sample sitting above the real
<UserControl> root element.

Deliberately NOT attempted: resolving each file's DataContext. Most cards bind
sub-ViewModels or item templates, so per-file VM matching produced 110 false
positives out of 480 bindings (23%) on Settings alone. "Occurs nowhere in real
code" is context-independent and holds whatever the DataContext turns out to be.

Scope is the whole app: Views/, Controls/, Agent/, MainWindow.xaml.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# Binding roots that are WPF plumbing rather than a member of the DataContext.
SKIP = {"DataContext", "SelectedItem", "SelectedValue", "Tag", "Content", "Items"}

# XAML scopes to audit, relative to the project root.
SCOPES = ["Views", "Controls", "Agent"]
EXTRA_FILES = ["MainWindow.xaml", "App.xaml"]


def _blank_preserving_lines(text):
    """Blank out `text` but keep every newline, so line numbers stay true."""
    return re.sub(r'[^\n]', ' ', text)


def strip_cs(t):
    """Single-pass lexer: keep code, drop comments and string/char CONTENT.

    An earlier version ran four regexes with comments FIRST. That was wrong in
    two ways and destroyed 87% of some files (GroqAiService.cs went
    7926 -> 1001 chars, taking `_settings.GroqModel` with it, which made a
    property consumed in two places look unreferenced):

      1. stripping `//` before string literals truncates any line holding a
         URL - `"https://x"; DoThing();` lost everything after the colon;
      2. a quote orphaned by (1) then paired with a much later quote and
         swallowed all the real code in between.

    Reordering cannot fix this; only a lexer that tracks state can.
    Interpolated strings keep the code inside {...} so `$"{nameof(Foo)}"`
    still counts as a reference to Foo instead of inventing a false positive.
    """
    out = []
    i, n = 0, len(t)
    while i < n:
        c = t[i]
        if c == '/' and i + 1 < n:
            if t[i + 1] == '/':
                j = t.find('\n', i)
                i = n if j < 0 else j
                out.append(' ')
                continue
            if t[i + 1] == '*':
                j = t.find('*/', i + 2)
                end = n if j < 0 else j + 2
                out.append(_blank_preserving_lines(t[i:end]))
                i = end
                continue
        p = i
        prefixes = ''
        while p < n and t[p] in '$@':
            prefixes += t[p]
            p += 1
        if p < n and t[p] in '"\'':
            quote = t[p]
            verbatim = '@' in prefixes
            interpolated = '$' in prefixes
            j = p + 1
            depth = 0
            while j < n:
                ch = t[j]
                if depth:                      # code inside {...} of $"..."
                    if ch == '{':
                        depth += 1
                    elif ch == '}':
                        depth -= 1
                    out.append(ch)
                    j += 1
                    continue
                if verbatim:
                    if ch == quote:
                        if j + 1 < n and t[j + 1] == quote:
                            j += 2
                            continue
                        break
                else:
                    if ch == '\\':
                        j += 2
                        continue
                    if ch == quote:
                        break
                if interpolated and ch == '{':
                    if j + 1 < n and t[j + 1] == '{':
                        j += 2
                        continue
                    depth = 1
                    j += 1
                    continue
                out.append(' ')
                j += 1
            i = min(j + 1, n)
            out.append(' ')
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def strip_xaml(t):
    """Drop <!-- --> comments: header banners carry non-live usage samples.

    Newlines inside a comment are preserved. Collapsing a comment to a single
    space shifts every following line number, and this lint's whole output is
    file:line - it reported AiSecurityCard.xaml:43 for a binding that lives on
    line 50, because a 7-line banner above it vanished. A guard that points at
    the wrong line costs the next developer the time it was meant to save.
    """
    return re.sub(r'<!--.*?-->', lambda m: _blank_preserving_lines(m.group(0)),
                  t, flags=re.S)


def corpus_identifiers():
    words = set()
    files = [p for p in ROOT.rglob("*.cs")
             if not any(x in p.parts for x in ("obj", "bin", ".git"))]
    for p in files:
        words |= set(re.findall(
            r'\b[A-Za-z_]\w*\b',
            strip_cs(p.read_text(encoding="utf-8", errors="ignore"))))
    return words, len(files)


def xaml_targets():
    targets = []
    for scope in SCOPES:
        d = ROOT / scope
        if d.is_dir():
            targets += sorted(d.rglob("*.xaml"))
    for rel in EXTRA_FILES:
        f = ROOT / rel
        if f.is_file():
            targets.append(f)
    return targets


def main():
    words, cs_count = corpus_identifiers()
    targets = xaml_targets()

    total = 0
    missing = {}
    for f in targets:
        t = strip_xaml(f.read_text(encoding="utf-8", errors="ignore"))
        for m in re.finditer(r'\{Binding\s+([^}]*)\}', t):
            inner = m.group(1)
            # Bindings that do not target the DataContext resolve elsewhere.
            if re.search(r'RelativeSource|ElementName|Source=', inner):
                continue
            pm = re.match(r'\s*(?:Path=)?([A-Za-z_][\w.]*)', inner)
            if not pm:
                continue
            root = pm.group(1).split('.')[0]
            if root in SKIP:
                continue
            total += 1
            if root not in words:
                line = t[:m.start()].count("\n") + 1
                missing.setdefault(root, []).append(
                    f"{f.relative_to(ROOT)}:{line}")

    print(f"  .cs files lexed into the corpus : {cs_count}")
    print(f"  distinct code identifiers       : {len(words)}")
    print(f"  XAML files scanned              : {len(targets)}")
    print(f"  DataContext binding roots       : {total}")

    if total == 0:
        print("\n  FAIL: scanned no bindings - this lint's own patterns are stale")
        return 2
    if not missing:
        print("  every binding root occurs in real code")
        print("\n  PASS")
        return 0

    print("\n  UNBACKED BINDINGS (name occurs only in comments/strings:")
    print("  WPF fails these silently, so the control looks live and is not)")
    for k in sorted(missing):
        for loc in missing[k]:
            print(f"    {k:<30} {loc}")
    print(f"\n  FAIL: {len(missing)} unbacked binding root(s)")
    return 1


if __name__ == "__main__":
    sys.exit(main())
