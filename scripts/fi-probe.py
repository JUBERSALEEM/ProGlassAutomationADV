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

# SEM006/SEM007 need XAML as well as C#: the defect is the relationship
# between a template declaration and the code that looks it up, so
# neither half can be injected alone.
PROBE_XAML_DIR = "Themes/ProbeFam"
PROBE_XAML = os.path.join(PROBE_XAML_DIR, "__SemanticProbe.xaml")
PROBE_XAML2 = os.path.join(PROBE_XAML_DIR, "__SemanticProbe2.xaml")

_RD = ('<ResourceDictionary\n'
       '    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"\n'
       '    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">\n'
       '%s\n'
       '</ResourceDictionary>\n')

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

    # Template= on a Border. Border is FrameworkElement-derived, not
    # Control-derived, so it has no Template property. This is the exact
    # MC3072 that shipped in ClayControls.xaml, and a markup-compile
    # failure produces no assembly, so every clr-namespace type in the
    # solution reports XDG0008 in the designer. One attribute, nine
    # families looking broken.
    "SEM006": [(PROBE_XAML, _RD % (
        '    <ControlTemplate x:Key="ProbeThumbTemplate"\n'
        '                     TargetType="{x:Type Control}">\n'
        '        <Border />\n'
        '    </ControlTemplate>\n'
        '    <Style x:Key="ProbeStyle" TargetType="{x:Type Control}">\n'
        '        <Setter Property="Template">\n'
        '            <Setter.Value>\n'
        '                <ControlTemplate TargetType="{x:Type Control}">\n'
        '                    <Border x:Name="ProbeThumb" Width="28" Height="28"\n'
        '                            Template="{StaticResource ProbeThumbTemplate}" />\n'
        '                </ControlTemplate>\n'
        '            </Setter.Value>\n'
        '        </Setter>\n'
        '    </Style>\n'))],

    # The same root cause seen from the C# side, and it fails SILENTLY:
    # `as` yields null, the null guard skips the bind, and the part is
    # never painted. No exception, no log, no compile error. Deliberately
    # no Template= on the Border here, so this proves SEM007 on its own
    # rather than riding on SEM006.
    # A member that the declared type provably does not have. This is
    # the class that shipped 43 errors in a single build -- see the
    # SEM008 docstring. Deliberately a deny list, because csc-check's
    # allow list is what let a real CS0115 through for months.
    "SEM008": [(NO_THEME_PROBE,
                "namespace X { class P {\n"
                "  void M(FrameworkElement root) {\n"
                "    root.Template = null;\n"
                "    root.Background = null;\n"
                "  }\n} }\n")],

    # The *KeyFrame classes are abstract; only the
    # Linear/Easing/Discrete/Spline variants are concrete.
    "SEM009": [(NO_THEME_PROBE,
                "namespace X { class P {\n"
                "  void M() {\n"
                "    var f = new DoubleKeyFrame(1.0, default);\n"
                "  }\n} }\n")],

    "SEM007": [(PROBE_XAML2, _RD % (
        '    <ControlTemplate x:Key="ProbeSliderTemplate"\n'
        '                     TargetType="{x:Type Control}">\n'
        '        <Border x:Name="ProbeThumb" Width="28" Height="28" />\n'
        '    </ControlTemplate>\n')),
               (NO_THEME_PROBE,
                "namespace X { class P {\n"
                "  void M() {\n"
                "    var t = Template?.FindName(\"ProbeThumb\", this) as Control;\n"
                "  }\n} }\n")],
}


def run_checker():
    return subprocess.run(
        [sys.executable, os.path.join("scripts", "semantic-check.py")],
        cwd=ROOT, capture_output=True, text=True)


def cleanup():
    for case in CASES.values():
        # A case is one file or several; SEM007 needs a template and the
        # code that consumes it, injected together.
        files = case if isinstance(case, list) else [case]
        for path, _ in files:
            full = os.path.join(ROOT, path)
            if os.path.exists(full):
                os.remove(full)
    shutil.rmtree(os.path.join(ROOT, NO_THEME_FAM), ignore_errors=True)
    shutil.rmtree(os.path.join(ROOT, PROBE_XAML_DIR), ignore_errors=True)


# csc-check is a separate tool with its own defect class, so it gets its
# own probe rather than being squeezed into the semantic-check table.
# This one matters more than most: the CS0115 override check was dead for
# its entire life because "OnSizeChanged" sat in the allow-list of known
# virtuals. A poisoned allow-list does not report one false negative, it
# removes the check for that name permanently, so the guard has to be an
# injection -- reading the list tells you nothing.
CS0115_FILE = os.path.join("Controls", "ProbeFam", "__Cs0115Probe.cs")
CS0115_SRC = (
    "using System.Windows;\n"
    "using System.Windows.Controls;\n"
    "namespace X {\n"
    "    public class P : ContentControl {\n"
    "        protected override void OnSizeChanged(SizeChangedInfo info)\n"
    "        {\n"
    "            base.OnSizeChanged(info);\n"
    "        }\n"
    "    }\n"
    "}\n")


def run_csc(path):
    return subprocess.run(
        [sys.executable, os.path.join("scripts", "csc-check.py"), path],
        cwd=ROOT, capture_output=True, text=True)


def probe_cs0115():
    """csc-check must report a bogus override, and must exit non-zero."""
    full = os.path.join(ROOT, CS0115_FILE)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    try:
        with open(full, "w") as fh:
            fh.write(CS0115_SRC)
        r = run_csc(CS0115_FILE)
    finally:
        if os.path.exists(full):
            os.remove(full)
        shutil.rmtree(os.path.join(ROOT, "Controls", "ProbeFam"),
                      ignore_errors=True)

    fired = "CS0115" in r.stdout and "OnSizeChanged" in r.stdout
    # A finding that prints but exits 0 is a finding nobody will act on,
    # so the exit code is part of what is being asserted here.
    failed = r.returncode != 0
    print("  CS0115:  %s (exit %d)"
          % ("FIRES" if fired else "*** SILENT ***", r.returncode))
    if not failed and fired:
        print("           ^ reported but exit code is 0 -- it will be ignored")
    return fired and failed


def main():
    os.makedirs(os.path.join(ROOT, NO_THEME_FAM), exist_ok=True)
    os.makedirs(os.path.join(ROOT, PROBE_XAML_DIR), exist_ok=True)

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
        for rule, case in CASES.items():
            files = case if isinstance(case, list) else [case]
            for rel_path, code in files:
                with open(os.path.join(ROOT, rel_path), "w") as fh:
                    fh.write(code)
            result = run_checker()
            for rel_path, _ in files:
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

    print("\ncsc-check (separate tool):")
    cs0115_ok = probe_cs0115()
    print()
    if not all_fired or not restored or not cs0115_ok:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())