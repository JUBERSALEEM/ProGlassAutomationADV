#!/usr/bin/env python3
"""
Regenerate docs/README.md — the documentation index — from the docs tree.

WHY THIS IS A SCRIPT AND NOT A HAND-WRITTEN FILE
    docs/ held 138 top-level markdown files while docs/README.md indexed
    nine of them. A hand-maintained index silently rots the moment someone
    adds a report and forgets to list it, which is exactly what happened.
    This derives the index from the filesystem, so it cannot drift: run it
    after adding or renaming a document and the index is correct.

    python3 scripts/gen-docs-index.py            # rewrite docs/README.md
    python3 scripts/gen-docs-index.py --check    # exit 1 if it would change

Each entry's description is the document's own first heading, so the index
describes a doc in the doc's own words rather than in a summary that goes
stale.
"""
import os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS = os.path.join(ROOT, "docs")
OUT = os.path.join(DOCS, "README.md")

# Ordered category rules: first match wins. Tuned to the actual filenames in
# docs/ — the goal is that a reader looking for "how does delivery work" or
# "what did the last security audit say" lands in one short list.
CATEGORIES = [
    ("Start here — customer & operator", [
        r"^INSTALL\.md$", r"^FIRST_RUN\.md$", r"^LICENSE\.md$", r"^BACKUP\.md$",
        r"^UPDATE\.md$", r"^OTA\.md$", r"^TROUBLESHOOTING\.md$", r"^RUNBOOK\.md$",
    ]),
    ("Architecture & the money contract", [
        r"^ARCHITECTURE\.md$", r"^ROADMAP\.md$", r"^ERP_UPGRADE",
        r"^MONEY_PATH_ERROR_POLICY\.md$", r"^DESIGN_SYSTEM\.md$",
        r"^PRODUCTION_HARDENING", r"^SQLITE_HARDENING", r"^DATABASE_MIGRATION",
        r"^LAN_SHARED_DATABASE", r"^PROFESSIONAL_AUDIT_TRAIL",
    ]),
    ("Business workflow — module by module", [
        r"^INQUIRY", r"^PROFORMA", r"^STEP9", r"^JOB_ORDER", r"^JOB_PROFITABILITY",
        r"^DELIVERY_NOTE", r"^TAX_INVOICE", r"^SHEET_DETAIL", r"^STATEMENT",
        r"^REPORTS", r"^SENIOR_ANALYSIS_ADVANCED_REPORTING",
    ]),
    ("Dashboards & data presentation", [
        r"^DASHBOARD", r"^Dashboard", r"^DATAGRID", r"^LIST_PAGE",
        r"^OVERVIEW_BOTTOM",
    ]),
    ("Design system, themes & UI/UX", [
        r"^THEME", r"^LIGHT_DARK", r"^UI_", r"^Ui", r"^LIQUID_GLASS", r"^IOS_", r"^WATER_ON_CARDS",
        r"^WHITE_WATER", r"^APPEARANCE", r"^BUTTON_MIGRATION", r"^SETTINGS",
        r"^SCREEN_RECORDING", r"^DEMO_HUB", r"^FEATURES_MODULE", r"^MODULE_FEATURE",
        r"^SENIOR_ANALYSIS_STEP4", r"^SENIOR_ANALYSIS_BUILD_ERRORS",
        r"^SENIOR_ANALYSIS_HARDCODED", r"^SENIOR_ANALYSIS_SCREEN_RECORDING",
        r"^LOCALIZATION",
    ]),
    ("Licensing & security", [
        r"^SecurityAudit", r"^LICENSING", r"^LICENSE_GENERATOR",
        r"^OFFLINE_MAX_SECURITY", r"^PLAN_BASED_LICENSING",
    ]),
    ("Print & export", [
        r"^PRINT_LAYOUT", r"^PDF_LOGO",
    ]),
    ("Release, CI & readiness", [
        r"^CI\.md$", r"^INSTALLER\.md$", r"^RELEASE", r"^E2E_WALKTHROUGH",
        r"^ProductionReadiness", r"^RATING", r"^BALANCE_TO_10",
        r"^CODE_QUALITY", r"^UPGRADE_NOTES", r"^PHASE_PLAN",
    ]),
    ("Audits & senior analyses (dated)", [
        r"^AUDIT", r"^SENIOR_", r"^CURRENT_PRODUCT", r"^INDUSTRIAL",
        r"^FILE_ANALYSIS", r"^EMPTY_CATCH", r"^PHASE\d",
    ]),
]

HEADER = """# Documentation Index

Everything written about ProGlass Automation, in one place.

**This file is generated** by `scripts/gen-docs-index.py` from the `docs/`
tree. Do not hand-edit the tables — add or rename a document and re-run the
script, otherwise the index drifts away from the folder again (it previously
listed 9 of 138 documents).

```
python3 scripts/gen-docs-index.py          # rewrite this file
python3 scripts/gen-docs-index.py --check  # fail if it is out of date
```

## Reading order

| If you are… | Read |
|---|---|
| installing the app | `INSTALL.md` → `FIRST_RUN.md` |
| new to the codebase | [`ARCHITECTURE.md`](ARCHITECTURE.md) → [`WORKFLOW.md`](WORKFLOW.md) → [`DESIGN_SYSTEM.md`](DESIGN_SYSTEM.md) |
| touching a money path | [`MONEY_PATH_ERROR_POLICY.md`](MONEY_PATH_ERROR_POLICY.md) first, always |
| working on one module | the *Business workflow* section below |
| cutting a release | [`RELEASE_READINESS_V2_STATUS.md`](RELEASE_READINESS_V2_STATUS.md) → [`CI.md`](CI.md) → [`E2E_WALKTHROUGH.md`](E2E_WALKTHROUGH.md) |

> [`WORKFLOW.md`](WORKFLOW.md) is the end-to-end business chain — Inquiry →
> Proforma → Job Order → Delivery → Tax Invoice → Statement — with the
> actual code path for each handoff. Start there if you want to understand
> what this application *does* rather than how a folder is arranged.

"""

def folder_readmes():
    """Every README.md in the repo outside docs/, with its folder's purpose."""
    out = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames
                       if d not in {'.git', 'obj', 'bin', 'docs', 'node_modules',
                                    '.arena', '.cache'}]
        if 'README.md' not in filenames:
            continue
        full = os.path.join(dirpath, 'README.md')
        rel = os.path.relpath(full, ROOT)
        if rel == 'README.md':
            continue
        folder = os.path.dirname(rel)
        out.append((folder, rel, first_heading(full)))
    return sorted(out)


FOOTER_HEAD = """
## Folder READMEs

Every significant folder in the repository carries its own `README.md`
describing what lives there. `%(n)d` of them:
"""

FOOTER = """
## Undated vs dated

Documents whose name carries a date (`*_2026-08-19.md`) are point-in-time
audits and analyses. They are history: accurate for the code as it stood
that day, and **not** a description of the current build. The undated
documents — `ARCHITECTURE.md`, `WORKFLOW.md`, `DESIGN_SYSTEM.md`,
`MONEY_PATH_ERROR_POLICY.md`, the customer guides — are the ones kept
current. When the two disagree, the undated document wins.

`scripts/verify-readmes.py` checks that every link in every README resolves
and that this index still matches the `docs/` tree.
"""


def first_heading(path):
    try:
        txt = open(path, encoding="utf-8-sig", errors="replace").read()
    except Exception:
        return ""
    for line in txt.splitlines():
        m = re.match(r"^#\s+(.+?)\s*$", line)
        if m:
            t = m.group(1).strip().strip("*").strip()
            t = re.sub(r"[`*]", "", t)
            return t if len(t) < 90 else t[:87] + "…"
    return ""


def categorize(name):
    for cat, patterns in CATEGORIES:
        for p in patterns:
            if re.search(p, name):
                return cat
    return "Other documents"


def build():
    files = sorted(f for f in os.listdir(DOCS)
                   if f.endswith(".md") and f != "README.md")
    buckets = {}
    for f in files:
        buckets.setdefault(categorize(f), []).append(f)

    order = [c for c, _ in CATEGORIES] + ["Other documents"]
    parts = [HEADER]
    for cat in order:
        names = buckets.get(cat)
        if not names:
            continue
        parts.append(f"## {cat}\n")
        parts.append("| Document | What it covers |")
        parts.append("|---|---|")
        for n in names:
            desc = first_heading(os.path.join(DOCS, n)) or "—"
            parts.append(f"| [`{n}`]({n}) | {desc} |")
        parts.append("")

    subdirs = sorted(d for d in os.listdir(DOCS)
                     if os.path.isdir(os.path.join(DOCS, d)))
    if subdirs:
        parts.append("## Sub-folders\n")
        parts.append("| Folder | Contents |")
        parts.append("|---|---|")
        counts = {}
        for d in subdirs:
            n = sum(1 for _, _, fs in os.walk(os.path.join(DOCS, d))
                    for x in fs if x.endswith((".md", ".yml", ".json")))
            counts[d] = n
        notes = {
            "CI": "Pipeline definitions (`build-test.yml`).",
            "ci": "Pipeline definitions — duplicate casing of `CI/`; see `CI.md`.",
            "Chat5": "Working notes and data from an earlier session.",
            "Chat5-9": "Working notes, prompts and data from an earlier session.",
            "ui-redesign": "UI redesign working artefacts.",
        }
        for d in subdirs:
            note = notes.get(d, "Supporting artefacts.")
            parts.append(f"| `{d}/` | {note} ({counts[d]} files) |")
        parts.append("")

    fr = folder_readmes()
    parts.append(FOOTER_HEAD % {"n": len(fr)})
    parts.append("| Folder | What lives there |")
    parts.append("|---|---|")
    for folder, rel, desc in fr:
        # this index lives in docs/, so folder READMEs are one level up
        parts.append(f"| [`{folder}/`](../{rel}) | {desc or '—'} |")
    parts.append("")

    parts.append(FOOTER)
    return "\n".join(parts).rstrip() + "\n", len(files)


def main():
    text, n = build()
    current = open(OUT, encoding="utf-8").read() if os.path.exists(OUT) else ""
    if "--check" in sys.argv:
        if current != text:
            print(f"docs/README.md is out of date ({n} docs indexed). "
                  f"Run: python3 scripts/gen-docs-index.py")
            return 1
        print(f"docs/README.md up to date ({n} docs indexed).")
        return 0
    open(OUT, "w", encoding="utf-8").write(text)
    print(f"docs/README.md regenerated — {n} documents indexed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
