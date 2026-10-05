#!/usr/bin/env python3
"""
csharp_syntax_check — catch C# compile errors BEFORE the build does.

WHY THIS EXISTS
    docs/.../2026-09-15: two commits shipped test-code that could not
    compile, and nobody noticed because the checks in this repo only
    looked at XAML plus a hand-rolled brace counter:

      * `"Style=\\"\\{StaticResource …\\}\\""` — a regular (non-verbatim)
        string with `\\{`: CS1009 "Unrecognized escape sequence".
      * `Assert.Contains("Mode=OneWay", m.Value, $"...")` — xunit has NO
        Contains(string, string, string) overload, so the compiler picks
        Contains<T>(T, IEnumerable<T>) / (T, IAsyncEnumerable<T>) and
        reports CS1503. Four of these had been sitting in the audit tests
        since the previous commit, so the WHOLE test project never ran —
        every audit lock in Tests/ was dead code.

    A brace counter cannot see either one. This does.

WHAT IT CHECKS
    1. REAL SYNTAX — every .cs file is parsed with the tree-sitter C#
       grammar. ERROR / MISSING nodes are reported with line + the exact
       text. This is a true parser, not a heuristic: it catches broken
       escape sequences, unterminated strings, unbalanced constructs and
       half-applied edits.
    2. CONTEXTUAL-KEYWORD NOISE — tree-sitter (unlike Roslyn) treats some
       contextual keywords as reserved, so a variable named `required`
       (`decimal required = 0m; required += x;`) parses as an ERROR even
       though it is valid C#. Those are re-parsed with the identifier
       renamed; if the error disappears the finding is reported as INFO
       and does NOT fail the build.
    3. XUNIT OVERLOAD TRAPS — Assert.Contains / Assert.DoesNotContain /
       Assert.StartsWith / Assert.EndsWith called with a third string
       argument (a "because" message). xunit v2 has no such overload;
       the compiler reports CS1503 against a collection overload. The fix
       is always `Assert.True(<condition>, message)`.

DEPENDENCY
    Stage 1 needs the optional `tree_sitter` + `tree_sitter_c_sharp`
    packages. They are NOT required: if they are missing, stage 1 prints
    a SKIP banner and stages 2/3 (pure Python) still run, so the gate
    stays useful on a machine without them.

        pip install tree_sitter tree_sitter_c_sharp

USAGE
    python3 scripts/layout-verify/csharp_syntax_check.py [files…]
    python3 scripts/layout-verify/csharp_syntax_check.py --strict
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

VERBOSE = "-v" in sys.argv

# C# contextual keywords that tree-sitter's grammar can treat as reserved.
# A hit is only excused when renaming the identifier makes the error go away.
CONTEXTUAL = [
    "required", "scoped", "file", "init", "with", "record", "when",
    "async", "await", "yield", "value", "dynamic", "nameof", "unmanaged",
    "notnull", "global", "partial", "where", "select", "from", "join",
]

ASSERT_WITH_MESSAGE = re.compile(
    r'Assert\.(Contains|DoesNotContain|StartsWith|EndsWith)\s*\(\s*'
    r'(?P<a>[^;]*?)'                       # whole arg list, non-greedy
    r'\)\s*;',
    re.S,
)


def gather(patterns: list[str]) -> list[str]:
    """Files from argv, or every tracked .cs file."""
    paths = [p for p in sys.argv[1:] if not p.startswith("-")]
    if paths:
        return paths

    out: list[str] = []
    for p in Path(".").rglob("*.cs"):
        s = str(p)
        if ".git" in s or s.startswith("build/") or s.startswith("obj/") or s.startswith("bin/"):
            continue
        out.append(s)
    return sorted(out)


def split_top_level(arglist: str) -> list[str]:
    """Split a C# argument list on commas that are not inside (), [], {}, <>,
    strings or chars."""
    args, depth, cur, quote = [], 0, [], None
    i = 0
    while i < len(arglist):
        c = arglist[i]
        if quote:
            cur.append(c)
            if c == "\\" and quote == '"' and i + 1 < len(arglist):
                cur.append(arglist[i + 1])
                i += 2
                continue
            if c == quote:
                quote = None
            i += 1
            continue
        if c in "\"'":
            quote = c
            cur.append(c)
        elif c in "([{<":
            depth += 1
            cur.append(c)
        elif c in ")]}>":
            depth -= 1
            cur.append(c)
        elif c == "," and depth == 0:
            args.append("".join(cur).strip())
            cur = []
        else:
            cur.append(c)
        i += 1
    if cur:
        args.append("".join(cur).strip())
    return args


def looks_like_string_literal(a: str) -> bool:
    return bool(re.match(r'^(\$?@?"|@\$")', a.strip()))


def check_xunit_overloads(path: str, src: str) -> list[str]:
    """Stage 3 — Assert.<X>(..., message) shapes xunit v2 does not have."""
    findings: list[str] = []
    for m in ASSERT_WITH_MESSAGE.finditer(src):
        args = split_top_level(m.group("a"))
        if len(args) != 3:
            continue
        second, third = args[1], args[2]

        # third arg is a genuine message? (string / interpolated string)
        if not looks_like_string_literal(third):
            continue
        # StringComparison / comparer overloads are legitimate — but note
        # they take the comparison as the SECOND argument only when the
        # first is a string. Keep it simple: only a message-looking third
        # argument combined with a non-message second is the trap.
        if "StringComparison" in second or "Comparer" in second:
            continue

        line = src[: m.start()].count("\n") + 1
        findings.append(
            f"{path}:{line}\n"
            f"   XUNIT OVERLOAD: Assert.{m.group(1)}(…) with a 3rd 'because' "
            f"argument.\n"
            f"   xunit v2 has no Assert.{m.group(1)}(string, string, string) "
            f"overload — the compiler reports CS1503 against a collection "
            f"overload (IEnumerable<T> / IAsyncEnumerable<T> / "
            f"IEqualityComparer<T>).\n"
            f"   FIX: Assert.True(<condition>, message);")
    return findings


def parse_csharp(src: bytes):
    try:
        import tree_sitter_c_sharp
        from tree_sitter import Language, Parser
    except Exception:
        return None

    parser = Parser(Language(tree_sitter_c_sharp.language()))
    tree = parser.parse(src)

    errors = []
    stack = [tree.root_node]
    while stack:
        n = stack.pop()
        if n.type in ("ERROR", "MISSING"):
            errors.append((n.start_byte, n.end_byte, n.type))
        for c in n.children:
            stack.append(c)
    return errors


def rename_contextual_keywords(src: bytes) -> bytes:
    out = src
    for kw in CONTEXTUAL:
        out = re.sub(rb"\b" + kw.encode() + rb"\b", b"__probe_ident__", out)
    return out


def check_syntax(path: str, src_text: str) -> tuple[list[str], list[str]]:
    """Returns (real_findings, excused_findings)."""
    src = src_text.encode("utf-8")
    errors = parse_csharp(src)
    if errors is None:
        return [], []          # grammar unavailable — reported by the caller

    if not errors:
        return [], []

    # Distinct lines only — one bad expression yields several ERROR nodes.
    lines = sorted({src[:s].count(b"\n") + 1 for s, _, _ in errors})

    # Excuse the lot when every error vanishes once contextual keywords are
    # renamed: that is a grammar limitation, not a defect in the file.
    renamed = parse_csharp(rename_contextual_keywords(src))
    if renamed is not None and not renamed:
        excused = [
            f"{path}:{ln}\n"
            f"   CONTEXTUAL KEYWORD: tree-sitter's grammar treats a C# "
            f"contextual keyword (e.g. a variable named `required`) as "
            f"reserved.\n"
            f"   Renaming the identifier parses cleanly, so this is a "
            f"grammar limitation, not a build error. Roslyn is authoritative "
            f"here — no action needed."
            for ln in lines
        ]
        return [], excused

    findings = []
    for ln in lines:
        snippet = src_text.splitlines()[ln - 1].strip() if ln - 1 < len(src_text.splitlines()) else ""
        findings.append(
            f"{path}:{ln}\n"
            f"   SYNTAX ERROR: {snippet[:110]}\n"
            f"   The C# parser cannot parse this — usual causes: an illegal "
            f"escape sequence in a non-verbatim string (CS1009, e.g. "
            f'`"\\{{"` — needs `"\\\\{{"` or a verbatim string), an '
            f"unterminated string, or a half-applied edit.")
    return findings, []


def main() -> int:
    files = gather([])
    real: list[str] = []
    excused: list[str] = []
    grammar_ok = None
    scanned = 0

    for path in files:
        try:
            text = Path(path).read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError):
            continue
        scanned += 1

        found, skipped = check_syntax(path, text)
        if grammar_ok is None:
            grammar_ok = parse_csharp(b"class C { }") is not None
        real.extend(found)
        excused.extend(skipped)

        real.extend(check_xunit_overloads(path, text))

    if grammar_ok is None or not grammar_ok:
        print("SKIP  syntax parse — install tree_sitter + tree_sitter_c_sharp "
              "(pip install tree_sitter tree_sitter_c_sharp)")
    else:
        print(f"ok    syntax parse     {scanned} C# file(s)")

    if VERBOSE:
        for e in excused:
            print("info  " + e)

    for f in real:
        print("\n" + f)

    print(f"\n{len(real)} compile-risk finding(s), "
          f"{len(excused)} excused as parser limitations")
    return 1 if real else 0


if __name__ == "__main__":
    sys.exit(main())
