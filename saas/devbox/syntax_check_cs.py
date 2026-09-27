#!/usr/bin/env python3
"""
Syntax-checks every .cs file under saas/backend with tree-sitter-c-sharp.

This sandbox/devbox has no .NET SDK and no NuGet egress (see
saas/devbox/README.md), so `dotnet build` cannot run here. This check is the
minimum local gate: it proves every C# file PARSES. Full compile + tests run
in an environment with the .NET 8 SDK (see backend/README.md).

Usage: python3 saas/devbox/syntax_check_cs.py
Exit code 0 = all files parse.
"""
from __future__ import annotations

import sys
from pathlib import Path

import tree_sitter
import tree_sitter_c_sharp

BACKEND = Path(__file__).resolve().parent.parent / "backend"


def main() -> int:
    lang = tree_sitter.Language(tree_sitter_c_sharp.language())
    parser = tree_sitter.Parser(lang)

    files = sorted(BACKEND.rglob("*.cs"))
    if not files:
        print("No .cs files found — nothing to check.")
        return 1

    failures = 0
    for f in files:
        tree = parser.parse(f.read_bytes())
        if tree.root_node.has_error:
            failures += 1
            print(f"[FAIL] {f.relative_to(BACKEND.parent.parent)}")
            # find first ERROR node for a location hint
            stack = [tree.root_node]
            while stack:
                node = stack.pop()
                if node.type == "ERROR":
                    line = node.start_point[0] + 1
                    src = f.read_text(errors="replace").splitlines()
                    snippet = src[line - 1].strip()[:100] if line <= len(src) else ""
                    print(f"       first error near line {line}: {snippet}")
                    break
                stack.extend(node.children)
        else:
            print(f"[ OK ] {f.relative_to(BACKEND.parent.parent)}")

    print(f"\n{len(files) - failures}/{len(files)} files parse cleanly")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
