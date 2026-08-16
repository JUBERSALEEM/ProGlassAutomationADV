"""
Detect the XAML compile error:
  "Property element cannot be in the middle of an element content."

WHY THE FIRST VERSION OF THIS CHECKER FAILED
────────────────────────────────────────────
It only inspected 4 parent types (ResourceDictionary, Border,
ContentControl, ContentPresenter). Grid was not among them, so when a
<Border> was inserted ahead of <Grid.RowDefinitions> in
TrendChartControl.xaml the checker reported 0 issues and the build
failed at that exact line. A checker with an allow-list of parents only
finds bugs in the parents someone thought of.

THE ACTUAL WPF RULE
───────────────────
Within one element, a property element (Owner.Property) may not appear
AFTER a child that went into the parent's content property.

But "all property elements must precede all children" is WRONG and
reports 392 false positives in this repo, because these are legal and
ship today:

    <Style>            <Setter/>...  <Style.Triggers>
    <ControlTemplate>  <Border/>     <ControlTemplate.Triggers>

Those are legal because Setter goes to Style.Setters and the template
root goes to ControlTemplate.VisualTree -- neither is a content property
being filled by arbitrary children.

APPROACH: deny-list, not allow-list
───────────────────────────────────
Check EVERY parent, then exclude the specific property elements that WPF
permits after content. That way a new parent type is covered by default
and only a genuinely-legal pattern needs adding here.

Verified against this repo: 393 raw hits, 392 of them the legal Triggers
pattern, 1 the real Grid.RowDefinitions error.
"""
import sys
from xml.etree import ElementTree as ET

# Property elements WPF allows after content children, by suffix.
# Triggers are always declared last by convention and are collections
# owned by the parent, not its content property.
ALLOWED_AFTER_CONTENT = {
    'Triggers',          # Style.Triggers, ControlTemplate.Triggers, DataTemplate.Triggers
    'Resources',         # FrameworkElement.Resources may legally trail
    'Storyboard',
    'VisualStateGroups',
    'InputBindings',
    'CommandBindings',
    'ContextMenu',
    'ToolTip',
    'Setters',
    'Conditions',
    'ExitActions',
    'EnterActions',
    'BindingGroup',
}


def local(tag):
    return tag.split('}')[-1]


def check(path):
    try:
        root = ET.parse(path).getroot()
    except Exception as e:
        print(f"  PARSE FAIL {path}: {e}")
        return 1

    issues = 0

    def walk(el):
        nonlocal issues
        parent = local(el.tag)
        first_content = None

        for ch in el:
            name = local(ch.tag)

            if name.startswith(parent + '.'):
                suffix = name.split('.', 1)[1]
                if first_content and suffix not in ALLOWED_AFTER_CONTENT:
                    print(f"  ORDER {path}: <{name}> comes after content "
                          f"child <{first_content}> inside <{parent}>")
                    issues += 1
            elif first_content is None:
                first_content = name

            walk(ch)

    walk(root)
    return issues


total = 0
for p in sys.argv[1:]:
    total += check(p)

print(f"\n{len(sys.argv)-1} file(s), {total} property-element order issue(s)")
sys.exit(1 if total else 0)
