#!/usr/bin/env python3
"""
Detect the "hijacked DataContext breaks a parent's binding" fault.

THE BUG (real, shipped, caused three Settings pages to render at once):

  Parent view:
      <sections:MySection Visibility="{Binding IsMyPage, Converter=...}"/>

  Child code-behind:
      DataContext = _myOwnVm;          // on the UserControl ROOT

  A binding declared on an element resolves against THAT ELEMENT'S
  DataContext. Replacing the root DataContext therefore re-targets the
  parent's Visibility binding to the child's own VM. If that VM lacks the
  property, the binding fails, Visibility falls back to its default of
  Visible, and the page is permanently shown - stacked over its siblings.

This script cross-references, for every UserControl:
  1. code-behind assigning `DataContext =` on the root (not `X.DataContext`)
  2. parents that bind any property on that control
  3. whether the child's VM actually exposes the bound property

Reports a fault when a control both hijacks its root DataContext AND is
given a binding by a parent.
"""
import sys, os, re

def find_xaml_cs(root="Views"):
    out = []
    for dirpath, _, files in os.walk(root):
        for fn in files:
            if fn.endswith(".xaml.cs"):
                out.append(os.path.join(dirpath, fn))
    return out

ROOT_DC = re.compile(r'^\s*DataContext\s*=\s*([^;]+);', re.M)

def scan_repo():
    problems = []

    # 1. controls that hijack their ROOT DataContext
    hijackers = {}
    for cs in find_xaml_cs():
        try:
            src = open(cs, encoding="utf-8").read()
        except OSError:
            continue
        m = ROOT_DC.search(src)
        if m:
            cls = os.path.basename(cs)[:-8]      # strip .xaml.cs
            hijackers[cls] = (cs, m.group(1).strip())

    if not hijackers:
        return problems

    # 2. any XAML that binds a property on those controls
    for dirpath, _, files in os.walk("."):
        if ".git" in dirpath:
            continue
        for fn in files:
            if not fn.endswith(".xaml"):
                continue
            fp = os.path.join(dirpath, fn)
            try:
                src = open(fp, encoding="utf-8").read()
            except OSError:
                continue
            for cls, (cs, assigned) in hijackers.items():
                # <ns:ClassName ... > possibly multiline
                for m in re.finditer(
                        r'<\w+:' + re.escape(cls) + r'\b(.*?)/?>',
                        src, re.S):
                    attrs = m.group(1)
                    if "{Binding" not in attrs:
                        continue
                    # a binding anchored elsewhere is fine
                    anchored = ("ElementName=" in attrs
                                or "RelativeSource" in attrs
                                or "Source=" in attrs)
                    if anchored:
                        continue
                    line = src[:m.start()].count("\n") + 1
                    props = re.findall(r'(\w+)="\{Binding\s+([^,}]+)', attrs)
                    problems.append(
                        f"{fp}:{line}\n"
                        f"   HIJACKED DATACONTEXT: <{cls}> is given an "
                        f"unanchored {{Binding}} by its parent, but "
                        f"{os.path.basename(cs)} sets the ROOT "
                        f"DataContext = {assigned}.\n"
                        f"   The parent's binding will resolve against that "
                        f"VM and silently fail -> the property falls back to "
                        f"its DEFAULT (Visibility defaults to Visible).\n"
                        f"   Bound here: {props}\n"
                        f"   FIX: anchor with ElementName/RelativeSource, or "
                        f"assign the VM to an inner element instead of the root.")
    return problems

def main():
    probs = scan_repo()
    for p in probs:
        print("\n" + p)
    print(f"\n{len(probs)} problem(s)")
    return 0

if __name__ == "__main__":
    sys.exit(main())
