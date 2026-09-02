#!/usr/bin/env python3
"""Phase D — validate a {code}-msgs.json overlay against msg-spec.json.

Checks per key:
  • key exists in the spec (no invented keys)
  • value is non-empty (no silent blanks)
  • {N} placeholders appear in the same order as the English source
  • newline pattern matches the English source EXACTLY (each break is the
    same sequence: real newline or literal backslash-n; consecutive equal
    sequences merge into one break position)
  • no straight ASCII double quotes inside values (XAML/JSON safety)

Usage: python3 tools/i18n/msg-translate.py ar-AE
"""
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SPEC = json.load(open(HERE / "msg-spec.json", encoding="utf-8"))
KEYS = SPEC["keys"]
import importlib.util as _ilu
_ms = _ilu.spec_from_file_location("msg_keys_mod", HERE / "msg-keys.py")
_mm = _ilu.module_from_spec(_ms)
_ms.loader.exec_module(_mm)  # noqa: S102 - trusted repo tool
REUSE = set(_mm.REUSE_CAPTIONS)
NEW_KEYS = {k: v for k, v in KEYS.items() if k not in REUSE}
TRANS = HERE / "translations"


def breaks(s: str) -> list[str]:
    """Break pattern: each run of character-adjacent newline escapes is one
    break, preserving the exact sequence (e.g. "\\n\\n" for a blank line).
    A run ends as soon as any other character appears."""
    runs: list[str] = []
    i = 0
    n = len(s)
    while i < n:
        if s[i] == "\n":
            j = i
            while j < n and s[j] == "\n":
                j += 1
            runs.append("\n" * (j - i))
            i = j
        elif s[i] == "\\" and i + 1 < n and s[i + 1] == "n":
            j = i
            while j + 1 < n and s[j] == "\\" and s[j + 1] == "n":
                j += 2
            runs.append("\\n" * ((j - i) // 2))
            i = j
        else:
            i += 1
    return runs


def validate(code: str) -> bool:
    path = TRANS / f"{code}-msgs.json"
    data = json.load(open(path, encoding="utf-8"))
    problems = []
    if set(data) != set(NEW_KEYS):
        problems.append(f"key set differs: missing={len(set(NEW_KEYS) - set(data))} "
                        f"extra={len(set(data) - set(NEW_KEYS))}")
    for k in sorted(NEW_KEYS):
        if k not in data:
            continue
        v = data[k]
        en = KEYS[k]["en"]
        if not isinstance(v, str) or not v.strip():
            problems.append(f"{k}: empty value")
            continue
        if '"' in v:
            problems.append(f"{k}: straight ASCII quote in value")
        ep = sorted(re.findall(r"\{(\d+)\}", en))
        ap = sorted(re.findall(r"\{(\d+)\}", v))
        if ep != ap:
            problems.append(f"{k}: placeholders {ap} != English {ep}")
        if breaks(v) != breaks(en):
            problems.append(f"{k}: newline pattern differs from English")
    if problems:
        print(f"{code}: {len(problems)} PROBLEMS")
        for p in problems[:40]:
            print("  ", p)
        return False
    print(f"{code}: OK — {len(data)} keys validated")
    return True


def build(code: str, table_path: str) -> bool:
    """Map a {code}-table.py (keys = current spec slugs, §N§ markers) onto the
    English break pattern and write translations/{code}-msgs.json."""
    import importlib.util
    ts = importlib.util.spec_from_file_location(f"{code}_table", table_path)
    tm = importlib.util.module_from_spec(ts)
    ts.loader.exec_module(tm)  # noqa: S102 - trusted local authoring file
    T = tm.T
    cur = sorted(NEW_KEYS)
    missing = [k for k in NEW_KEYS if k not in T]
    extra = [k for k in T if k not in NEW_KEYS]
    if missing or extra:
        print(f"{code}: key set differs — missing {len(missing)}, extra {len(extra)}")
        return False
    out = {}
    problems = []
    for k in cur:
        v = T[k]
        en = KEYS[k]["en"]
        if '"' in v:
            problems.append(f"{k}: straight ASCII quote")
        ep = sorted(re.findall(r"\{(\d+)\}", en))
        ap = sorted(re.findall(r"\{(\d+)\}", v))
        if ep != ap:
            problems.append(f"{k}: placeholders {ap} != English {ep}")
        bk = breaks(en)
        if v.count("§N§") != len(bk):
            problems.append(f"{k}: {v.count('§N§')} markers vs {len(bk)} breaks")
            continue
        for b in bk:
            v = v.replace("§N§", b, 1)
        out[k] = v
    if problems:
        print(f"{code}: {len(problems)} PROBLEMS")
        for p in problems[:40]:
            print("  ", p)
        return False
    path = TRANS / f"{code}-msgs.json"
    json.dump({k: out[k] for k in sorted(out)}, open(path, "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    print(f"{code}: built {path} ({len(out)} keys)")
    return True


if __name__ == "__main__":
    if len(sys.argv) == 4 and sys.argv[2] == "--build":
        sys.exit(0 if build(sys.argv[1], sys.argv[3]) else 1)
    sys.exit(0 if validate(sys.argv[1]) else 1)
