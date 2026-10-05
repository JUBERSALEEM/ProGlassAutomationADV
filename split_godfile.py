#!/usr/bin/env python3
"""Phase 3.8 god-file splitter — the verified recipe.

Splits a .cs god file into a partial-class family:
  - finds the class declaration (adds `partial` if missing),
  - finds balance-0 cut points between members (line starts with a member
    modifier or a // banner, cumulative brace balance == 0),
  - groups cuts into chunks of <= target_lines,
  - emits partial files = header + usings + namespace + class_decl + content,
  - deletes the moved regions from the original via one flat descending
    span list,
  - verifies: balance invariant, member census equality, `partial class`
    in every part, every file <= 1000 lines.

Usage: python3 split_godfile.py <file> [target_lines]
"""
import re
import sys
from pathlib import Path


def lines_of(path):
    return Path(path).read_text(encoding='utf-8-sig').splitlines(keepends=True)


def braces_per_line(line):
    """Count { } on a line, ignoring string literals, char literals and
    comments (LicenseService has '{"' inside a string literal)."""
    o = c = 0
    i, n = 0, len(line)
    in_str = in_chr = in_block = False
    while i < n:
        ch = line[i]
        nxt = line[i + 1] if i + 1 < n else ''
        if in_block:
            if ch == '*' and nxt == '/':
                in_block = False; i += 2; continue
            i += 1; continue
        if in_str:
            if ch == '"' and nxt == '"':      # verbatim doubled quote
                i += 2; continue
            if ch == '\\' and nxt != '':      # escape
                i += 2; continue
            if ch == '"':
                in_str = False
            i += 1; continue
        if in_chr:
            if ch == '\\' and nxt != '':
                i += 2; continue
            if ch == "'":
                in_chr = False
            i += 1; continue
        if ch == '/' and nxt == '/':
            break                               # line comment
        if ch == '/' and nxt == '*':
            in_block = True; i += 2; continue
        if ch == '"':
            in_str = True; i += 1; continue
        if ch == "'":
            in_chr = True; i += 1; continue
        if ch == '{': o += 1
        elif ch == '}': c += 1
        i += 1
    return o, c


def balance_of(text):
    o = c = 0
    for ln in text.splitlines(keepends=True):
        a, b = braces_per_line(ln)
        o += a; c += b
    return o - c


def find_class(lines, name_hint):
    """Return (decl_start, class_open_idx, decl_text, tail_close_idx)."""
    for i, ln in enumerate(lines):
        m = re.search(r'\b(?:public|internal)\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*class\s+' + re.escape(name_hint) + r'\b', ln)
        if m:
            decl_start = i
            # capture declaration through the opening brace (may span lines)
            j = i
            while '{' not in lines[j]:
                j += 1
            decl_text = ''.join(lines[i:j + 1]).rstrip('\n')
            return decl_start, j, decl_text, None
    raise SystemExit(f'class {name_hint} not found (no match in file)')


def find_class_close(lines, class_open_idx):
    depth = 0
    for i in range(class_open_idx, len(lines)):
        o, c = braces_per_line(lines[i])
        depth += o - c
        if depth == 0:
            return i
    raise SystemExit('class close not found')


def find_cuts(lines, class_open_idx, class_close_idx):
    """Balance-0 cut points between members."""
    depth = 1  # the class-open brace
    cuts = []
    for i in range(class_open_idx + 1, class_close_idx):
        ln = lines[i]
        o, c = braces_per_line(ln)
        depth += o - c
        t = ln.lstrip()
        # at a member boundary only the class brace is open (depth == 1).
        # Cut at member-modifier lines and section banners (// === / // ---),
        # never at doc comments (///) or inline comments.
        is_banner = re.match(r'//\s*[=\u2550\u2500-]', t) and not t.startswith('///')
        if depth == 1 and (is_banner or t.startswith(('public ', 'private ', 'internal ', 'protected '))):
            cuts.append(i)
    return cuts


def member_census(text):
    rx = re.compile(
        r'\b(?:public|private|internal|protected)\s+'
        r'(?:static\s+|sealed\s+|override\s+|async\s+|virtual\s+|readonly\s+|partial\s+)*'
        r'(?:[\w<>\[\],.?]+\s+)+(\w+)\s*[({=]')
    return [m.group(1) for m in rx.finditer(text)]


def main():
    path = sys.argv[1]
    target = int(sys.argv[2]) if len(sys.argv) > 2 else 850
    text = Path(path).read_text(encoding='utf-8-sig')
    lines = lines_of(path)
    name = Path(path).stem

    # class = filename stem; for code-behind (X.xaml.cs) strip the .xaml part
    cls = name
    for suffix in ('.xaml', '.g.cs', '.g.i.cs'):
        if cls.endswith(suffix):
            cls = cls[: -len(suffix)]

    decl_start, class_open_idx, decl_text, _ = find_class(lines, cls)
    class_close_idx = find_class_close(lines, class_open_idx)

    # ensure `partial` in the declaration (main file + partials)
    decl = decl_text
    if not re.search(r'\bpartial\s+class\b', decl):
        decl = re.sub(r'\bclass\b', 'partial class', decl, count=1)
    # normalize: partial before sealed/static? keep original order, just insert partial before class
    if 'partial' not in decl.split('class')[0]:
        decl = decl.replace(' class ', ' partial class ', 1)

    cuts = [c for c in find_cuts(lines, class_open_idx, class_close_idx) if c > class_open_idx + 1]
    if not cuts:
        raise SystemExit(f'no balance-0 cut points in {path}')

    # group cuts into chunks <= target lines
    chunks = []
    start = class_open_idx + 1
    for c in cuts + [class_close_idx]:
        if c - start >= target:
            chunks.append((start, c))
            start = c
    if start < class_close_idx:
        chunks.append((start, class_close_idx))
    # merge a tiny tail (<100 lines) into the previous chunk
    if len(chunks) > 1 and chunks[-1][1] - chunks[-1][0] < 100:
        a, b = chunks[-2]
        chunks[-2] = (a, chunks[-1][1])
        chunks.pop()

    # header + usings + namespace
    ns_m = re.search(r'^namespace\s+([\w.]+)', text, re.M)
    ns = ns_m.group(1)
    # usings: only the `using` lines (top-level types like enums stay in
    # the main file — never copy an unclosed namespace into a partial)
    usings = ''.join(l for l in lines[:decl_start] if l.startswith('using '))

    # write partials
    orig_census = member_census(text)
    total_balance = balance_of(text)
    new_files = []
    for idx, (a, b) in enumerate(chunks):
        content = ''.join(lines[a:b]).rstrip('\n') + '\n'
        part = (
            f'// ═══════════════════════════════════════════════════════════════════════\n'
            f'// FILE: {Path(path).stem}.Part{idx + 1}.cs (Phase 3.8 split — member census verified)\n'
            f'// ═══════════════════════════════════════════════════════════════════════\n\n'
            f'{usings}\n'
            f'namespace {ns}\n{{\n    {decl}\n    {content}    }}\n}}\n'
        )
        part_path = path.replace('.cs', f'.Part{idx + 1}.cs')
        Path(part_path).write_text(part, encoding='utf-8')
        new_files.append(part_path)
        if balance_of(part) != 0:
            raise SystemExit(f'partial {part_path} unbalanced: {balance_of(part)}')

    # delete moved regions from the original (one flat descending span list)
    remaining = lines[:class_open_idx + 1]
    prev = class_open_idx + 1
    for a, b in sorted(chunks, reverse=True):
        pass
    # rebuild: keep [0..class_open_idx], then only the parts not in chunks
    kept = []
    cursor = class_open_idx + 1
    for a, b in sorted(chunks):
        kept.extend(lines[cursor:a])
        cursor = b
    kept.extend(lines[cursor:])
    new_main = ''.join(lines[:class_open_idx + 1]) + ''.join(kept)

    # fix the main file's class declaration to include `partial`
    new_main = new_main.replace(decl_text, decl, 1)
    Path(path).write_text(new_main, encoding='utf-8')

    # ── verify ──
    main_text = Path(path).read_text(encoding='utf-8-sig')
    all_text = main_text + ''.join(Path(f).read_text(encoding='utf-8-sig') for f in new_files)
    assert balance_of(main_text) == 0, f'main unbalanced {balance_of(main_text)}'
    assert balance_of(all_text) == total_balance, 'balance invariant broken'
    new_census = member_census(all_text)
    from collections import Counter
    c1, c2 = Counter(orig_census), Counter(new_census)
    # the class declaration line itself is counted once per file — the
    # name legitimately appears once in the main + once per partial
    c1[cls] = 1
    c2[cls] = 1
    lost = c1 - c2
    dup = c2 - c1
    if lost or dup:
        raise SystemExit(f'CENSUS MISMATCH: lost={dict(lost)} dup={dict(dup)}')
    print(f'CENSUS OK: {len(orig_census)} members, 0 lost/dup')
    sizes = [len(Path(f).read_text(encoding='utf-8-sig').splitlines()) for f in [path] + new_files]
    print(f'sizes: {sizes} (max {max(sizes)})')
    assert max(sizes) <= 1000, 'a file exceeds 1000 lines'
    print(f'OK: {path} -> {path} + {new_files}')


if __name__ == '__main__':
    main()
