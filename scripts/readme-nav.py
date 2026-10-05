#!/usr/bin/env python3
"""
Give every folder README the same navigation and the same opening, so a
reader who lands deep in the tree can always get back to the workflow.

WHY
    30 folder READMEs had drifted into 30 different shapes: some opened
    with a bare `# Behaviors`, some with an emoji banner, none linked back
    to the repository root or to the documentation index. Landing in
    `Services/SheetStore/SheetStoreHistory/README.md` from a search result
    was a dead end.

WHAT IT DOES
    Inserts one breadcrumb line under the H1 and one navigation footer,
    both wrapped in `<!-- pg-nav -->` markers so the script is idempotent —
    run it as often as you like, after adding a folder README.

        python3 scripts/readme-nav.py           # apply
        python3 scripts/readme-nav.py --check   # exit 1 if any file needs it
"""
import os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MARK = "<!-- pg-nav -->"

SKIP_DIRS = {'.git', 'obj', 'bin', 'node_modules', '.arena', '.cache',
             '.claude', 'packages'}


def folder_readmes():
    out = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        if 'README.md' in filenames:
            full = os.path.join(dirpath, 'README.md')
            rel = os.path.relpath(full, ROOT)
            if rel not in ('README.md', os.path.join('docs', 'README.md')):
                out.append(rel)
    return sorted(out)


def nav_for(rel):
    depth = rel.count(os.sep)          # Views/Dashboard/README.md -> 2
    up = "../" * depth
    return (
        f"{MARK}\n"
        f"> **[Repository root]({up}README.md)** · "
        f"[All documents]({up}docs/README.md) · "
        f"[Application workflow]({up}docs/WORKFLOW.md) · "
        f"[Design system]({up}docs/DESIGN_SYSTEM.md)\n"
        f"{MARK}"
    )


def footer_for(rel):
    depth = rel.count(os.sep)
    up = "../" * depth
    folder = os.path.dirname(rel)
    return (
        f"\n{MARK}\n---\n\n"
        f"*This README documents `{folder}/`. "
        f"For the end-to-end business chain see "
        f"[`docs/WORKFLOW.md`]({up}docs/WORKFLOW.md); for every other "
        f"document see [`docs/README.md`]({up}docs/README.md).*\n"
        f"{MARK}\n"
    )


def strip_nav(txt):
    """Remove any previously inserted nav block and tidy the whitespace it
    leaves behind. Without the tidy, each run adds blank lines and the file
    grows without bound."""
    txt = re.sub(rf"\n*{MARK}\n> \*\*\[Repository root\].*?\n{MARK}\n*", "\n", txt, flags=re.S)
    txt = re.sub(rf"\n*{MARK}\n---\n\n\*This README documents.*?\n{MARK}\n*", "\n", txt, flags=re.S)
    txt = re.sub(r"\n{3,}", "\n\n", txt)
    return txt.rstrip() + "\n"


def apply(rel):
    """Return True if the file changed. Idempotent: a second run is a no-op."""
    path = os.path.join(ROOT, rel)
    raw = open(path, encoding="utf-8-sig", errors="replace").read()
    body = strip_nav(raw)

    lines = body.split("\n")
    h1 = next((i for i, l in enumerate(lines) if re.match(r"^#\s+\S", l)), None)
    if h1 is None:
        print(f"  ! no H1 in {rel} - skipped")
        return False

    lines.insert(h1 + 1, "\n" + nav_for(rel))
    rebuilt = "\n".join(lines).rstrip() + "\n" + footer_for(rel)
    rebuilt = re.sub(r"\n{3,}", "\n\n", rebuilt)

    if rebuilt == raw:
        return False
    open(path, "w", encoding="utf-8").write(rebuilt)
    return True


def main():
    files = folder_readmes()
    if "--check" in sys.argv:
        stale = []
        for rel in files:
            txt = open(os.path.join(ROOT, rel), encoding='utf-8-sig',
                       errors='replace').read()
            if txt.count(MARK) < 2:
                stale.append(rel)
        if stale:
            print(f"{len(stale)} folder README(s) missing navigation:")
            for s in stale:
                print(f"  {s}")
            print("Run: python3 scripts/readme-nav.py")
            return 1
        print(f"All {len(files)} folder READMEs carry navigation.")
        return 0

    changed = sum(1 for rel in files if apply(rel))
    print(f"{changed}/{len(files)} folder READMEs updated with navigation.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
