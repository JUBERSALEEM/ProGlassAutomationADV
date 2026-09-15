#!/usr/bin/env python3
# (a) String/comment-aware brace + paren balance scanner for C# files.
import sys

# ── C# 11 raw string literals ────────────────────────────────────────────
# `"""…"""` (and `$"""…"""`, `$$"""…"""`). This scanner predates them, so a
# SQL/JSON raw literal containing braces or quotes shifted the balance and
# AppSchemaMigrations.cs was reported as "unterminated string" / "paren
# imbalance 5" for months — a false positive that kept the gate red.
def raw_string_at(src, i):
    """(quote_count, index_after_prefix) when a raw string starts at i."""
    j = i
    while j < len(src) and src[j] in '$@':
        j += 1
    if '@' in src[i:j]:
        return None                      # @"" verbatim — handled below
    k = j
    while k < len(src) and src[k] == '"':
        k += 1
    quotes = k - j
    return (quotes, k) if quotes >= 3 else None


def skip_raw_string(src, start, quotes, after_prefix):
    """(index_after_literal, newlines_consumed) for a raw string literal."""
    term = '"' * quotes
    idx = src.find(term, after_prefix)
    if idx == -1:
        return len(src), src.count('\n', start)
    end = idx + quotes
    return end, src.count('\n', start, end)


def scan(path):
    src = open(path, encoding='utf-8-sig', errors='strict').read()
    i, n = 0, len(src)
    depth = paren = 0
    line = 1
    stack = []
    errors = []
    while i < n:
        c = src[i]
        if c == '\n':
            line += 1; i += 1; continue
        if c == '/' and i + 1 < n:
            if src[i+1] == '/':
                while i < n and src[i] != '\n': i += 1
                continue
            if src[i+1] == '*':
                i += 2
                while i + 1 < n and not (src[i] == '*' and src[i+1] == '/'):
                    if src[i] == '\n': line += 1
                    i += 1
                i += 2; continue
        if c in '$@"':
            raw = raw_string_at(src, i)
            if raw:
                quotes, after_prefix = raw
                i, newlines = skip_raw_string(src, i, quotes, after_prefix)
                line += newlines
                continue
        if c == '@' and i + 1 < n and src[i+1] == '"':
            i += 2
            while i < n:
                if src[i] == '"':
                    if i + 1 < n and src[i+1] == '"': i += 2; continue
                    i += 1; break
                if src[i] == '\n': line += 1
                i += 1
            continue
        if c == '$' and i + 1 < n and (src[i+1] == '"' or (src[i+1] == '@' and i + 2 < n and src[i+2] == '"')):
            verbatim = src[i+1] == '@'
            i += 3 if verbatim else 2
            brace_in = 0
            while i < n:
                ch = src[i]
                if ch == '\n':
                    line += 1
                    if not verbatim: break
                if ch == '{':
                    if i + 1 < n and src[i+1] == '{': i += 2; continue
                    brace_in += 1; i += 1; continue
                if ch == '}':
                    if i + 1 < n and src[i+1] == '}': i += 2; continue
                    if brace_in: brace_in -= 1
                    i += 1; continue
                if ch == '\\' and not verbatim: i += 2; continue
                if ch == '"' and brace_in == 0:
                    if verbatim and i + 1 < n and src[i+1] == '"': i += 2; continue
                    i += 1; break
                i += 1
            continue
        if c == '"':
            i += 1
            while i < n:
                if src[i] == '\\': i += 2; continue
                if src[i] == '"': i += 1; break
                if src[i] == '\n':
                    errors.append(f"{path}:{line}: unterminated string")
                    line += 1; i += 1; break
                i += 1
            continue
        if c == "'":
            i += 1
            while i < n:
                if src[i] == '\\': i += 2; continue
                if src[i] == "'": i += 1; break
                i += 1
            continue
        if c == '{': depth += 1; stack.append(line)
        elif c == '}':
            depth -= 1
            if depth < 0:
                errors.append(f"{path}:{line}: unmatched closing brace"); return errors
            stack.pop()
        elif c == '(': paren += 1
        elif c == ')':
            paren -= 1
            if paren < 0:
                errors.append(f"{path}:{line}: unmatched closing paren"); return errors
        i += 1
    if depth != 0:
        errors.append(f"{path}: {depth} unclosed brace(s), first at line {stack[0] if stack else '?'}")
    if paren != 0:
        errors.append(f"{path}: paren imbalance {paren}")
    return errors

SELFTEST_CASES = {
    "raw string with braces/parens/quotes is inert": (
        'var sql = """\n'
        '    CREATE TABLE T (\n'
        '        Name TEXT NOT NULL DEFAULT \'\',\n'
        '        Flag INTEGER\n'
        '    );\n'
        '""";\n'
        'public void M() { if (true) { } }\n', 0),
    "interpolated raw string is inert": (
        'var json = $"""\n'
        '    { "a": { "b": "}" } }\n'
        '""";\n'
        'class C { }\n', 0),
    "genuinely unbalanced code is still caught": (
        'class C {\n    void M() { if (true) { }\n', 1),
}


def selftest():
    import tempfile, os
    failures = 0
    for name, (code, expect_errors) in SELFTEST_CASES.items():
        with tempfile.NamedTemporaryFile("w", suffix=".cs", delete=False,
                                         encoding="utf-8") as fh:
            fh.write(code)
            tmp = fh.name
        try:
            errs = scan(tmp)
        finally:
            os.unlink(tmp)
        ok = (len(errs) > 0) == bool(expect_errors)
        print(f"  {'ok  ' if ok else 'FAIL'} {name}")
        failures += 0 if ok else 1
    print(f"\nselftest: {len(SELFTEST_CASES) - failures}/{len(SELFTEST_CASES)} checks passed")
    return 1 if failures else 0


if __name__ == '__main__':
    if '--selftest' in sys.argv:
        sys.exit(selftest())

    bad = 0
    for p in sys.argv[1:]:
        errs = scan(p)
        for e in errs: print("FAIL", e)
        if errs: bad += 1
        else: print("ok  ", p)
    print(f"\n{max(0, len(sys.argv)-1)} file(s) scanned, {bad} failing")
    sys.exit(1 if bad else 0)
