#!/usr/bin/env python3
"""
Balance-check C# files with a string- and comment-aware scanner.

Why this exists
---------------
There is no .NET SDK in the environment this work was done in, so nothing here
has been compiled. Brace balance is the weakest substitute for a compiler, but
it is only useful if it is CORRECT — and the obvious one-liner is not:

    s.count('{') == s.count('}')

That counts braces inside string literals, character literals, verbatim
strings, interpolated strings and comments. It produced a false 12-vs-10
mismatch on Tests/Architecture/StatusLiteralRatchetTests.cs, caused by the
escaped ``\\(`` inside a regex literal:

    new(@"\\.Status\\.Equals\\s*\\(\\s*\"", ...)

This scanner tracks where it is and only counts braces in real code. It also
reports the minimum depth, so a file that closes more than it opens is caught
at the line where it happens rather than merely at end-of-file.

This is NOT a compile check. It will not catch a missing semicolon, a wrong
type, an unresolved symbol or a bad overload. It catches one specific class of
editing mistake — a truncated or duplicated block — which is the class most
likely to be introduced by scripted find-and-replace.

Usage
-----
    python3 scripts/check-csharp-balance.py path/to/File.cs [...]
    git diff --name-only BASE..HEAD | grep '\\.cs$' | xargs python3 scripts/check-csharp-balance.py

Exits 0 if every file balances, 1 otherwise.
"""

import sys


def check(path):
    """Return (ok, detail) for one file."""
    try:
        src = open(path, encoding='utf-8', errors='ignore').read()
    except OSError as e:
        return False, f"unreadable: {e}"

    i = 0
    n = len(src)
    line = 1
    brace = 0
    paren = 0
    min_brace = 0
    mode = 'code'          # code | lc | bc | st | vb | ch
    problems = []

    while i < n:
        ch = src[i]
        nxt = src[i + 1] if i + 1 < n else ''
        prev = src[i - 1:i]

        if ch == '\n':
            line += 1

        if mode == 'code':
            if ch == '/' and nxt == '/':
                mode = 'lc'; i += 2; continue
            if ch == '/' and nxt == '*':
                mode = 'bc'; i += 2; continue
            if ch == '"' and prev == '@':
                mode = 'vb'; i += 1; continue
            if ch == '"':
                mode = 'st'; i += 1; continue
            if ch == "'":
                mode = 'ch'; i += 1; continue
            if ch == '{':
                brace += 1
            elif ch == '}':
                brace -= 1
                if brace < min_brace:
                    min_brace = brace
                    problems.append(f"unmatched '}}' at line {line}")
            elif ch == '(':
                paren += 1
            elif ch == ')':
                paren -= 1
                if paren < 0:
                    problems.append(f"unmatched ')' at line {line}")
                    paren = 0
        elif mode == 'lc':
            if ch == '\n':
                mode = 'code'
        elif mode == 'bc':
            if ch == '*' and nxt == '/':
                mode = 'code'; i += 2; continue
        elif mode == 'st':
            if ch == '\\':
                i += 2; continue
            if ch == '"':
                mode = 'code'
        elif mode == 'vb':
            # verbatim: "" is an escaped quote, a lone " ends the string
            if ch == '"' and nxt == '"':
                i += 2; continue
            if ch == '"':
                mode = 'code'
        elif mode == 'ch':
            if ch == '\\':
                i += 2; continue
            if ch == "'":
                mode = 'code'

        i += 1

    if brace != 0:
        problems.append(f"brace depth {brace:+d} at EOF")
    if paren != 0:
        problems.append(f"paren depth {paren:+d} at EOF")
    if mode != 'code':
        problems.append(f"file ends inside {mode}")

    return (not problems), ('; '.join(problems) if problems else 'balanced')


def main(argv):
    files = argv[1:]
    if not files:
        print(__doc__)
        return 2

    ok = True
    for f in files:
        good, detail = check(f)
        ok = ok and good
        print(f"{'OK  ' if good else 'FAIL'} {detail:40s} {f}")

    print(f"\n{len(files)} file(s) checked — "
          f"{'all balanced' if ok else 'FAILURES above'}")
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv))
