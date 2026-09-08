#!/usr/bin/env python3
"""
Documentation health check. Run before committing.

    python3 scripts/verify-readmes.py

Checks, across every markdown file in the repository:

  1. Every relative link resolves to a file that exists.
     The root README had seven links pointing at `Docs/…` while the folder
     is `docs/…`. That is invisible on a case-insensitive Windows checkout
     and broken on GitHub, so it sat there unnoticed.
  2. `docs/README.md` still matches the `docs/` tree
     (delegates to `gen-docs-index.py --check`).
  3. Every folder README carries the navigation breadcrumb and footer
     (delegates to `readme-nav.py --check`).
  4. Every folder README opens with an H1, so the generated index has a
     real title to show for it.
"""
import os, re, sys, subprocess

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SKIP_DIRS = {'.git', 'obj', 'bin', 'node_modules', '.arena', '.cache',
             '.claude', 'packages'}


def markdown_files():
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for f in filenames:
            if f.endswith(('.md', '.markdown')):
                yield os.path.join(dirpath, f)


def check_links():
    broken = []
    files = list(markdown_files())
    for path in files:
        try:
            txt = open(path, encoding='utf-8-sig', errors='replace').read()
        except Exception:
            continue
        for label, target in re.findall(r'\[([^\]]*)\]\(([^)]+)\)', txt):
            t = target.split('#')[0].strip()
            if not t or t.startswith(('http://', 'https://', 'mailto:', '#')):
                continue
            t = t.replace('%20', ' ')
            resolved = os.path.normpath(os.path.join(os.path.dirname(path), t))
            if not os.path.exists(resolved):
                broken.append((os.path.relpath(path, ROOT), label[:30], target))
    return files, broken


def check_h1():
    missing = []
    for path in markdown_files():
        rel = os.path.relpath(path, ROOT)
        if not rel.endswith('README.md'):
            continue
        txt = open(path, encoding='utf-8-sig', errors='replace').read()
        if not re.search(r'^#\s+\S', txt, re.M):
            missing.append(rel)
    return missing


def delegate(script, *args):
    r = subprocess.run([sys.executable, os.path.join(ROOT, 'scripts', script), *args],
                       capture_output=True, text=True)
    return r.returncode, (r.stdout + r.stderr).strip()


def main():
    rc = 0

    files, broken = check_links()
    print(f"[1] markdown files scanned : {len(files)}")
    print(f"[2] broken relative links  : {len(broken)}")
    for f, label, t in broken[:25]:
        print(f"    {f}: [{label}] -> {t}")
    if len(broken) > 25:
        print(f"    ... +{len(broken) - 25} more")
    if broken:
        rc = 1

    code, out = delegate('gen-docs-index.py', '--check')
    print(f"[3] docs index freshness   : {out}")
    rc |= code

    code, out = delegate('readme-nav.py', '--check')
    print(f"[4] folder README nav      : {out.splitlines()[-1] if out else ''}")
    rc |= code

    missing = check_h1()
    print(f"[5] READMEs without an H1  : {len(missing)}")
    for m in missing:
        print(f"    {m}")
    if missing:
        rc = 1

    print()
    print("OK — documentation is consistent." if rc == 0
          else "FAILED — see above.")
    return rc


if __name__ == '__main__':
    sys.exit(main())
