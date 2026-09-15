#!/usr/bin/env python3
"""
Detect the "hijacked DataContext breaks a parent's binding" fault.

THE BUG (real, shipped twice):

  Parent view:
      <sections:MySection Visibility="{Binding IsMyPage, Converter=...}"/>

  Child code-behind:
      DataContext = _myOwnVm;          // on the UserControl ROOT

  A binding declared on an element resolves against THAT ELEMENT'S
  DataContext. Replacing the root DataContext therefore re-targets the
  parent's binding to the child's own VM. If that VM lacks the property,
  the binding fails and the property falls back to its default:
    * Visibility defaults to Visible   -> the page is shown stacked on
      top of its siblings (the three-Settings-pages-at-once incident), or
    * a Style/DataTrigger Setter never activates -> the section keeps its
      base value, i.e. Collapsed -> a permanently BLANK page under a
      working tab strip (the 2026-09-15 About → License incident).

  The second flavour was missed by the first version of this script: the
  About shell switched sections with an inline <X.Style> DataTrigger, so
  the {Binding} lived in a PROPERTY ELEMENT, not in an attribute of the
  element the script was inspecting. Both forms are now scanned.

This script cross-references, for every UserControl:
  1. code-behind assigning `DataContext =` on the root (not `X.DataContext`)
  2. parents that bind any property on that control — attributes AND
     property elements (<X.Style>, <X.Triggers>, …)
  3. whether the binding is anchored (ElementName / RelativeSource /
     Source), which makes it immune to the hijack

Reports a fault when a control both hijacks its root DataContext AND is
given an unanchored binding by a parent.

Usage:
    python3 scripts/layout-verify/datacontext_scan.py [--root DIR] [--quiet]
"""
import argparse
import os
import re
import sys

ROOT_DC = re.compile(r'^\s*DataContext\s*=\s*([^;]+);', re.M)

# {Binding ...} whose first path segment is quoted; used to list what a
# parent binds so the report names the property that will silently fail.
BINDING = re.compile(r'\{Binding\s+([^,}\s]+)')

# Element must be FULLY spelled: prefix:Class  (a bare <Class> in the same
# namespace is fine too, but a prefixed one is what views actually use).
# The lookahead keeps <X.Style> / <X.RenderTransform> property elements out
# of the match — only the real element declaration is inspected.
ELEMENT_OPEN = r'<\w+:%s(?=[\s/>])'


def find_xaml_cs(root):
    out = []
    for dirpath, _, files in os.walk(os.path.join(root, "Views")):
        for fn in files:
            if fn.endswith(".xaml.cs"):
                out.append(os.path.join(dirpath, fn))
    return out


def hijackers(root):
    """{ClassName: (path, assigned-expression)} for root DataContext writes."""
    found = {}
    for cs in find_xaml_cs(root):
        try:
            src = open(cs, encoding="utf-8").read()
        except OSError:
            continue
        m = ROOT_DC.search(src)
        if m:
            cls = os.path.basename(cs)[:-8]      # strip .xaml.cs
            found[cls] = (cs, m.group(1).strip())
    return found


def _open_tag_end(src, i):
    """Index of the '>' that closes the tag starting at `i` (quote-aware)."""
    j = i + 1
    quote = None
    while j < len(src):
        c = src[j]
        if quote:
            if c == quote:
                quote = None
        elif c in "\"'":
            quote = c
        elif c == ">":
            return j
        j += 1
    return len(src) - 1


def element_block(src, start):
    """Full element text (property elements included) from an opening tag,
    up to its self-closing '/>' or its matching close tag."""
    tag = re.match(r'<\w+:(\w+)', src[start:]).group(1)
    open_end = _open_tag_end(src, start)
    opening = src[start:open_end + 1]
    if opening.rstrip().endswith("/>"):
        return opening, open_end + 1

    close = re.compile(r'</\w+:%s\s*>' % re.escape(tag))
    open_same = re.compile(r'<\w+:%s(?=[\s/>])' % re.escape(tag))

    i, depth = open_end + 1, 0
    while i < len(src):
        if src.startswith("<!--", i):
            k = src.find("-->", i)
            i = len(src) if k == -1 else k + 3
            continue

        nm = close.search(src, i)
        om = open_same.search(src, i)

        if nm and (om is None or nm.start() < om.start()):
            if depth == 0:
                return src[start:nm.end()], nm.end()
            depth -= 1
            i = nm.end()
        elif om:
            depth += 1
            i = _open_tag_end(src, om.start()) + 1
        else:
            break

    return src[start:], len(src)


def scan_repo(root, excludes=()):
    problems = []

    hk = hijackers(root)
    if not hk:
        return problems

    for dirpath, _, files in os.walk(root):
        if ".git" in dirpath:
            continue
        for fn in files:
            if not fn.endswith(".xaml"):
                continue
            fp = os.path.join(dirpath, fn)
            if any(x in fp.replace(os.sep, "/") for x in excludes):
                continue
            try:
                src = open(fp, encoding="utf-8").read()
            except OSError:
                continue

            for cls, (cs, assigned) in hk.items():
                for m in re.finditer(ELEMENT_OPEN % re.escape(cls), src):
                    block, _ = element_block(src, m.start())
                    if "{Binding" not in block:
                        continue

                    # A binding anchored to a name / ancestor / explicit
                    # source does not depend on the child's DataContext.
                    if ("ElementName=" in block or "RelativeSource" in block
                            or "Source=" in block):
                        continue

                    bound = BINDING.findall(block)
                    line = src[:m.start()].count("\n") + 1
                    problems.append(
                        f"{fp}:{line}\n"
                        f"   HIJACKED DATACONTEXT: <{cls}> is given an "
                        f"unanchored {{Binding}} by its parent, but "
                        f"{os.path.basename(cs)} sets the ROOT "
                        f"DataContext = {assigned}.\n"
                        f"   The parent's binding resolves against that VM and "
                        f"silently fails -> the property falls back to its "
                        f"DEFAULT (Visibility -> Visible, a DataTrigger "
                        f"Setter -> never applied).\n"
                        f"   Bound here: {bound}\n"
                        f"   FIX: anchor with ElementName/RelativeSource, or "
                        f"assign the VM to an inner element instead of the "
                        f"root (see AboutLicenseCard.CardRoot).")
    return problems


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".",
                    help="repository root to scan (default: .)")
    ap.add_argument("--quiet", action="store_true",
                    help="print only the count")
    ap.add_argument("--strict", action="store_true",
                    help="exit non-zero when a fault is found (for CI)")
    ap.add_argument("--exclude", default="",
                    help="comma-separated path fragments to skip "
                         "(known-benign baseline)")
    args = ap.parse_args()

    excludes = [x.strip() for x in args.exclude.split(",") if x.strip()]
    probs = scan_repo(args.root, excludes)
    if not args.quiet:
        for p in probs:
            print("\n" + p)
    print(f"\n{len(probs)} problem(s)")
    return 1 if (args.strict and probs) else 0


if __name__ == "__main__":
    sys.exit(main())
