"""
Detect the XAML error "Property element cannot be in the middle of an
element" -- i.e. a property element placed AFTER content that belongs to
the parent's CONTENT PROPERTY.

WHY THE NAIVE RULE IS WRONG
───────────────────────────
"All property elements must precede all children" is NOT the real rule.
<Style><Setter/>...<Style.Triggers>...</Style> is legal and used 295
times in this repo, because Setter goes into Style.Setters (an explicit
collection), not into a content property. Same for
<ControlTemplate><Border/><ControlTemplate.Triggers>.

THE ACTUAL RULE
───────────────
The error fires when an element has a CONTENT PROPERTY (single-slot,
e.g. ResourceDictionary's implicit content, Border.Child,
ContentControl.Content) and a property element appears after a child
that was already assigned to that content slot.

So this checker is deliberately narrow: it validates only the parents
where a single content slot exists and getting the order wrong is a
build break. ResourceDictionary is the case that actually bit us --
moving converters above <ResourceDictionary.MergedDictionaries> in
App.xaml put a content child first and broke the build.

Verified against the shipping App.xaml (pre-break): 0 issues.
"""
import sys
from xml.etree import ElementTree as ET

# Parents whose property elements MUST precede content children.
STRICT = {'ResourceDictionary', 'Border', 'ContentControl', 'ContentPresenter'}

def local(tag):
    return tag.split('}')[-1]

bad = 0
for path in sys.argv[1:]:
    try:
        root = ET.parse(path).getroot()
    except Exception as e:
        print(f"  PARSE FAIL {path}: {e}")
        bad += 1
        continue

    def walk(el):
        global bad
        parent = local(el.tag)
        if parent in STRICT:
            seen_content = None
            for ch in el:
                name = local(ch.tag)
                if name.startswith(parent + '.'):
                    if seen_content:
                        print(f"  ORDER {path}: <{name}> comes after content "
                              f"child <{seen_content}> inside <{parent}>")
                        bad += 1
                elif seen_content is None:
                    seen_content = name
        for ch in el:
            walk(ch)

    walk(root)

print(f"\n{len(sys.argv)-1} file(s), {bad} property-element order issue(s)")
