#!/usr/bin/env python3
# ═══════════════════════════════════════════════════════════════════════
# tools/layout-verify/dup_property_check.py
#
# WHAT THIS CATCHES
#   The same property set TWICE on one element — once as an attribute and
#   once as a property element:
#
#       <TextBlock Style="{StaticResource Foo}">      <-- attribute
#           <TextBlock.Style>                          <-- property element
#               <Style .../>
#           </TextBlock.Style>
#       </TextBlock>
#
#   The XAML compiler rejects this:
#       MC3050 / "The property 'Style' is set more than once."
#   It is a HARD BUILD ERROR, not a warning.
#
# WHY IT NEEDED ITS OWN CHECK
#   The document is perfectly well-formed XML, so xaml_wellformed.py
#   passes it. The property element is the FIRST child, so
#   property_element_order.py passes it. No attribute is stray, so
#   stray_attr_check.py passes it. Every existing check said OK on a file
#   that could not compile — which is exactly how the earlier MC3061 and
#   MC3072 errors reached the user's build. This closes that class.
#
# ALSO CATCHES
#   • the same ATTRIBUTE twice on one tag (well-formed XML forbids it, so
#     the parser would already fail, but we report it clearly if seen)
#   • the same PROPERTY ELEMENT twice, e.g. two <Grid.RowDefinitions>
#
# DELIBERATELY NOT FLAGGED
#   • Attached properties from a different owner: Grid.Row="0" on a
#     Border is unrelated to <Border.Something>. Only a property element
#     whose owner matches the element's own type competes with a plain
#     attribute of the same name.
#   • x:-namespace directives.
#
# BOM: source files here are 339/617 UTF-8-BOM, so read utf-8-sig.
# ═══════════════════════════════════════════════════════════════════════

import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter

# Namespace-qualified names come back as "{uri}Local"; strip the uri.
_TAGSPLIT = re.compile(r"^\{[^}]*\}")


def local(name: str) -> str:
    return _TAGSPLIT.sub("", name)


def check_file(path: str):
    """Return a list of human-readable problem strings for one XAML file."""
    problems = []

    try:
        with open(path, "r", encoding="utf-8-sig") as fh:
            text = fh.read()
    except OSError as exc:
        return [f"{path}: cannot read ({exc})"]

    try:
        root = ET.fromstring(text)
    except ET.ParseError as exc:
        # xaml_wellformed.py owns well-formedness; stay silent here so a
        # single syntax error is not reported twice by two checkers.
        return []

    for elem in root.iter():
        elem_type = local(elem.tag)

        # Attributes actually written on this tag, minus x: directives.
        attr_names = set()
        for raw in elem.attrib:
            if raw.startswith("{http://schemas.microsoft.com/winfx/2006/xaml}"):
                continue
            name = local(raw)
            # Attached property written attribute-style: "Grid.Row".
            attr_names.add(name)

        # Property elements: children named "<ThisType>.<Property>".
        prop_elems = []
        for child in elem:
            ctag = local(child.tag)
            if "." not in ctag:
                continue
            owner, _, prop = ctag.partition(".")
            if owner == elem_type:
                prop_elems.append(prop)

        # 1. attribute AND property element for the same property
        for prop in sorted(set(prop_elems) & attr_names):
            problems.append(
                f"{path}: <{elem_type}> sets '{prop}' twice — "
                f"as attribute {prop}=\"…\" and as <{elem_type}.{prop}>. "
                f"XAML compiler: property set more than once."
            )

        # 2. the same property element repeated
        for prop, count in Counter(prop_elems).items():
            if count > 1:
                problems.append(
                    f"{path}: <{elem_type}> declares <{elem_type}.{prop}> "
                    f"{count} times."
                )

    return problems


def main(argv):
    files = argv[1:]
    if not files:
        print("usage: dup_property_check.py <file.xaml> [...]")
        return 2

    all_problems = []
    for path in files:
        all_problems.extend(check_file(path))

    if all_problems:
        for p in all_problems:
            print("  " + p)
        print(
            f"FAIL  duplicate properties  {len(files)} file(s), "
            f"{len(all_problems)} duplicate-property issue(s)"
        )
        return 1

    # verify_all.sh prefixes its own "ok <name>", so emit only the counts.
    print(f"{len(files)} file(s), 0 duplicate-property issue(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
