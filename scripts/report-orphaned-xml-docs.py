#!/usr/bin/env python3
"""
Report orphaned XML documentation comments.

An XML doc comment is "orphaned" when a `///` block is not immediately followed
by a declaration it can document — it sits above another comment, a blank line
then a closing brace, an attribute-less statement, or nothing at all.

WHY THIS MATTERS
----------------
ProGlassAutomation.csproj does NOT set <GenerateDocumentationFile>. If it were
enabled — the usual first step toward API docs, IntelliSense on a published
package, or a doc site — every orphaned block becomes warning CS1587 ("XML
comment is not placed on a valid language element").

That is not a harmless warning here. The project sets:

    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>

and WarningsNotAsErrors lists CS0618, CS0414, CS0649, CS4014, CS86xx, IDE0059,
NU16xx, CA1515, CA1305/07/10/11 — but NOT CS1587. So enabling documentation
generation would turn every orphaned comment into a hard build error, in one
step, across 1,285 files.

This script measures that cost before anybody pays it.

USAGE
-----
    python3 scripts/report-orphaned-xml-docs.py            # summary
    python3 scripts/report-orphaned-xml-docs.py --list     # every location
    python3 scripts/report-orphaned-xml-docs.py --top 20   # worst files

Exit code is 0 when there are no orphaned blocks, 1 when there are — so it can
gate a CI job if the team decides to enable doc generation.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from collections import Counter

# A `///` line: optional leading whitespace then three slashes.
DOC_LINE = re.compile(r"^\s*///")

# WHAT CAN FOLLOW A VALID DOC COMMENT
# -----------------------------------
# The first version of this script tried to enumerate every declaration form a
# doc comment may sit above (modifiers, type + name, attributes). It produced
# 145 hits and a large share were wrong: `/// <summary>` above an enum member
# such as `Unknown = 0,` is perfectly valid documentation and does NOT raise
# CS1587, and the pattern did not match it because an enum member has no type
# in front of it.
#
# Enumerating C# declarations with a regex means re-implementing enough of the
# grammar to be wrong in ways that are hard to see. So the test is inverted:
# a block counts as orphaned only when what follows it cannot possibly be a
# declaration — end of file, a closing brace, or another comment. Everything
# else is assumed valid.
#
# That makes this a LOWER BOUND. It will miss a doc comment sitting above
# ordinary code inside a method body. It will not accuse valid documentation.
# For a number to act on, the authoritative source is the compiler:
# `dotnet build /p:GenerateDocumentationFile=true` and count CS1587. This script
# exists to size the job before anyone turns that on.
NOT_A_DECL = re.compile(r"^\s*(?:\}|//|/\*|\*)")

# A line that is nothing but an attribute, e.g. `[Fact]` or
# `[Obsolete("use X")]`. A line like
# `[MaxLength(30)] public string GlassType { get; set; }` is NOT one — it
# carries the declaration too, and skipping it would hide a valid target.
ATTRIBUTE_ONLY = re.compile(r"^\s*(?:\[[^\]]*\]\s*)+$")

SKIP_DIRS = {"obj", "bin", ".git", ".vs", "node_modules", "packages"}


def scan(path: str) -> list[int]:
    """Return 1-based line numbers of orphaned `///` blocks in one file."""
    try:
        with open(path, encoding="utf-8-sig", errors="replace") as fh:
            lines = fh.read().split("\n")
    except OSError:
        return []

    orphans: list[int] = []
    i = 0
    n = len(lines)

    while i < n:
        if not DOC_LINE.match(lines[i]):
            i += 1
            continue

        start = i
        # Consume the whole `///` block.
        while i < n and DOC_LINE.match(lines[i]):
            i += 1

        # Skip blank lines and any attributes that follow the comment — a doc
        # comment above [Fact] above a method is valid.
        #
        # Only skip a line that is *purely* an attribute. This codebase writes
        # `[MaxLength(30)] public string GlassType { get; set; }` on one line,
        # and treating that as "just an attribute" skips straight past the
        # declaration and reports valid documentation as orphaned.
        j = i
        while j < n:
            stripped = lines[j].strip()
            if stripped == "" or ATTRIBUTE_ONLY.match(lines[j]):
                j += 1
                continue
            break

        # Orphaned only when nothing that could be a declaration follows:
        # end of file, a closing brace, or another comment.
        if j >= n or NOT_A_DECL.match(lines[j]):
            orphans.append(start + 1)

        i = j if j > i else i

    return orphans


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[1])
    parser.add_argument("--list", action="store_true", help="print every location")
    parser.add_argument("--top", type=int, default=10, help="show N worst files")
    args = parser.parse_args()

    root = os.getcwd()
    per_file: Counter[str] = Counter()

    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            if not name.endswith(".cs"):
                continue
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, root).replace(os.sep, "/")
            hits = scan(full)
            if hits:
                per_file[rel] = len(hits)
                if args.list:
                    for line in hits:
                        print(f"{rel}:{line}")

    total = sum(per_file.values())

    if not args.list:
        print(f"C# files with orphaned XML doc comments : {len(per_file)}")
        print(f"Orphaned blocks total                   : {total}")
        print()
        if per_file:
            print(f"Worst {args.top} files:")
            for rel, count in per_file.most_common(args.top):
                print(f"  {count:4}  {rel}")
            print()
        print(
            "Enabling <GenerateDocumentationFile> today would raise CS1587 for "
            "each of these, and CS1587 is not in WarningsNotAsErrors, so with "
            "TreatWarningsAsErrors=true they would be build ERRORS."
        )

    return 1 if total else 0


if __name__ == "__main__":
    sys.exit(main())
