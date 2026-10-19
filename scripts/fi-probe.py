#!/usr/bin/env python3
"""
Fault-injection harness for scripts/semantic-check.py.

A checker that only ever runs against correct code proves nothing. This
synthesises one instance of each defect class the checker claims to
cover, asserts the checker actually reports it, and then asserts the
tree is clean again. A rule that cannot be made to fail is a rule that
is not running -- and three of these were not.

That is not hypothetical. Writing this file found:

  SEM004  indexed group(1) against a (?:...) NON-capturing group, so it
          raised IndexError on every single file it examined. The rule
          had never run once. The exception was caught and printed
          among the findings, where it read like an ordinary message.
  SEM005  reused the literal-stripping helper, so the part name it was
          searching for -- which is a string literal -- was deleted
          before the search. It reported focus defects in families that
          demonstrably had a focus part.
  SEM005  nested its report inside `if os.path.isdir(theme_dir)`, so a
          family with no Themes/<fam>/ directory fell off the end of the
          function and passed in silence.

Usage:  python3 scripts/fi-probe.py
Exit:   0 if every rule fired and the tree is clean afterwards,
        1 if any rule stayed silent, 2 on harness error.
"""

import os
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# A real family directory, so CSB001 and SEM001-SEM004 are exercised in a
# place the checker definitely scans.
IN_FAMILY = os.path.join("Controls", "Aurora", "__SemanticProbe.cs")

# A family with no Themes/<fam>/ directory at all. SEM005 used to pass
# silently on exactly this shape, which is why the probe lives here and
# not in an existing family.
NO_THEME_FAM = "Controls/ProbeFam"
NO_THEME_PROBE = os.path.join(NO_THEME_FAM, "__SemanticProbe.cs")

CASES = {
    # Thumb lives in System.Windows.Controls.Primitives; the probe
    # declares no using, so this is CS0246.
    "CSB001": (IN_FAMILY,
               "namespace X { class P { void M() { var t = typeof(Thumb); } } }\n"),

    # A frozen Freezable whose property is then animated.
    "SEM001": (IN_FAMILY,
               "namespace X { class P { void M() {\n"
               "  var b = new DoubleAnimation();\n"
               "  tf.Freeze();\n"
               "  tf.BeginAnimation(\"Opacity\", b);\n} } }\n"),

    # Decorator is a content host, so disabling hit-testing on it hides
    # everything inside.
    "SEM002": (IN_FAMILY,
               "namespace System.Windows.Controls { class P : Decorator {\n"
               "  public P() {\n    IsHitTestVisible = false;\n  }\n} }\n"),

    # A transparency flag steering a motion decision.
    "SEM003": (IN_FAMILY,
               "namespace X { class P { bool M() {\n"
               "  if (TransparencySuppressed) { var drift = 1; return true; }\n"
               "  return ReducedMotionRequested;\n} } }\n"),

    # The palette pinned to light or dark instead of the current theme.
    "SEM004": (IN_FAMILY,
               "namespace X { class P { void M() { var c = Palette.For(false); } } }\n"),

    # Focus visual removed, and the family has no focus cue anywhere.
    "SEM005": (NO_THEME_PROBE,
               "using System.Windows;\nnamespace X { class P {\n"
               "  public P() { FocusVisualStyle = null; }\n} }\n"),
}


def run_checker():
    return subprocess.run(
        [sys.executable, os.path.join("scripts", "semantic-check.py")],
        cwd=ROOT, capture_output=True, text=True)


def cleanup():
    for path, _ in CASES.values():
        full = os.path.join(ROOT, path)
        if os.path.exists(full):
            os.remove(full)
    shutil.rmtree(os.path.join(ROOT, NO_THEME_FAM), ignore_errors=True)


def main():
    os.makedirs(os.path.join(ROOT, NO_THEME_FAM), exist_ok=True)

    baseline = run_checker()
    print("baseline:", baseline.stdout.strip().splitlines()[-1].strip(),
          "(exit %d)" % baseline.returncode)
    if baseline.returncode != 0:
        print("\nThe tree is not clean BEFORE injection, so a silent rule")
        print("cannot be told apart from a working one. Fix the baseline")
        print("findings first.")
        return 2

    all_fired = True
    try:
        for rule, (rel_path, code) in CASES.items():
            with open(os.path.join(ROOT, rel_path), "w") as fh:
                fh.write(code)
            result = run_checker()
            os.remove(os.path.join(ROOT, rel_path))

            line = next((l.strip() for l in result.stdout.splitlines()
                         if "__SemanticProbe" in l), "")
            fired = rule in line
            all_fired &= fired
            print("  %-7s %s" % (rule + ":", "FIRES" if fired else "*** SILENT ***"))
            print("          %s" % line[:100])
    finally:
        cleanup()

    after = run_checker()
    restored = after.returncode == 0
    print()
    print("%s | tree clean after injection: %s"
          % ("ALL RULES BITING" if all_fired else "CHECKER IS BLIND", restored))

    if not all_fired or not restored:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())