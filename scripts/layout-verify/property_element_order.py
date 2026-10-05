#!/usr/bin/env python3
"""
Detect the XAML compile error:
  "Property elements cannot be in the middle of an element content."

WHY THE FIRST VERSION OF THIS CHECKER FAILED
────────────────────────────────────────────
It only inspected 4 parent types (ResourceDictionary, Border,
ContentControl, ContentPresenter). Grid was not among them, so when a
<Border> was inserted ahead of <Grid.RowDefinitions> in
TrendChartControl.xaml the checker reported 0 issues and the build
failed at that exact line. A checker with an allow-list of parents only
finds bugs in the parents someone thought of.

THE ACTUAL WPF RULE (verbatim from the compiler's own message)
──────────────────────────────────────────────────────────────
  "Property elements cannot be in the middle of an element's content.
   They must be BEFORE OR AFTER the content."

So a property element is legal:
  • before every content child            → <Grid.RowDefinitions/> first
  • after every content child             → <Window.Resources/> … <Grid/>
                                            … <ThemedDialogWindow.FooterContent/>
and illegal only when it sits BETWEEN content children — which is what
"in the middle" means, and the only shape that breaks the build.

THE SECOND BUG IN THE FIRST VERSION (fixed here)
────────────────────────────────────────────────
It identified property elements with `name.startswith(parent + '.')`.
A property element may name a BASE type:
    <dialogs:ThemedDialogWindow>
        <Window.Resources/>                          ← owner Window
        <StackPanel/>                                ← content
        <dialogs:ThemedDialogWindow.FooterContent/>   ← owner is the type
An owner that is not the immediate parent was therefore treated as a
CONTENT child, and the perfectly legal trailing property element was
reported as "after content". 20 such false positives sat in this repo,
keeping the pre-push gate permanently red — which is why nobody ran it
and why two uncompilable commits reached the owner. The classification
is now purely syntactic: a local name containing '.' IS a property
element, whoever owns it.

USAGE
    python3 scripts/layout-verify/property_element_order.py <files…>
    python3 scripts/layout-verify/property_element_order.py --selftest
"""
import sys
from xml.etree import ElementTree as ET


def local(tag):
    return tag.split('}')[-1]


def is_property_element(local_name: str) -> bool:
    """`Grid.RowDefinitions` / `Window.Resources` / `X.FooterContent` — any
    local name with a dot is a property element, regardless of which type
    in the inheritance chain owns it."""
    return '.' in local_name


def check_text(text: str, label: str = "<memory>") -> int:
    root = ET.fromstring(text)
    issues = 0

    def walk(el):
        nonlocal issues
        parent = local(el.tag)
        children = list(el)

        flags = [is_property_element(local(ch.tag)) for ch in children]
        content_idx = [i for i, prop in enumerate(flags) if not prop]
        first_content = min(content_idx) if content_idx else -1
        last_content = max(content_idx) if content_idx else -1

        for i, ch in enumerate(children):
            name = local(ch.tag)
            # Illegal ONLY when content surrounds it on both sides.
            if flags[i] and first_content != -1 and first_content < i < last_content:
                print(f"  ORDER {label}: <{name}> comes between content "
                      f"children inside <{parent}> (before index "
                      f"{first_content}, after index {last_content})")
                issues += 1
            walk(ch)

    walk(root)
    return issues


def check(path):
    try:
        with open(path, encoding="utf-8") as fh:
            text = fh.read()
    except OSError as e:
        print(f"  PARSE FAIL {path}: {e}")
        return 1

    try:
        return check_text(text, path)
    except ET.ParseError as e:
        # Well-formedness is a separate gate; do not double-report.
        print(f"  PARSE FAIL {path}: {e}")
        return 1


# ── self-test: the rule that the compiler actually enforces ──────────────
LEGAL_CASES = {
    "property element after ALL content (the 20 false positives)": """
        <Window xmlns:d="x">
            <Window.Resources/>
            <StackPanel/>
            <d:ThemedDialogWindow.FooterContent/>
        </Window>""",
    "property element before all content": """
        <Grid xmlns:d="x">
            <Grid.RowDefinitions/>
            <d:Child/>
        </Grid>""",
    "Style.Triggers after Setters (the documented legal pattern)": """
        <Style xmlns:d="x">
            <Setter/>
            <Style.Triggers/>
        </Style>""",
}

ILLEGAL_CASES = {
    "property element between two content children": """
        <Grid xmlns:d="x">
            <d:ChildA/>
            <Grid.RowDefinitions/>
            <d:ChildB/>
        </Grid>""",
    "the TrendChartControl shape (child, RowDefinitions, ColumnDefinitions, child)": """
        <Grid xmlns:d="x">
            <Border/>
            <Grid.RowDefinitions/>
            <Grid.ColumnDefinitions/>
            <d:Chart/>
        </Grid>""",
}


def selftest() -> int:
    failures = 0
    for name, text in LEGAL_CASES.items():
        n = check_text(text, "selftest")
        ok = n == 0
        print(f"  {'ok  ' if ok else 'FAIL'} legal  — {name}")
        failures += 0 if ok else 1
    for name, text in ILLEGAL_CASES.items():
        n = check_text(text, "selftest")
        ok = n > 0
        print(f"  {'ok  ' if ok else 'FAIL'} illegal — {name}")
        failures += 0 if ok else 1
    print(f"\nselftest: {len(LEGAL_CASES) + len(ILLEGAL_CASES) - failures}"
          f"/{len(LEGAL_CASES) + len(ILLEGAL_CASES)} rule checks passed")
    return 1 if failures else 0


def main() -> int:
    if "--selftest" in sys.argv:
        return selftest()

    files = [p for p in sys.argv[1:] if not p.startswith("-")]
    total = 0
    for p in files:
        total += check(p)

    print(f"\n{len(files)} file(s), {total} property-element order issue(s)")
    return 1 if total else 0


if __name__ == "__main__":
    sys.exit(main())
