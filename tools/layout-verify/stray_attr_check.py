"""
Detect attribute text that landed in ELEMENT CONTENT instead of the
opening tag -> MC3061 at compile time.

This is well-formed XML (it is just character data), which is exactly
why xaml_wellformed.py passed it. Only a content-aware check finds it.
"""
import sys,re
from xml.etree import ElementTree as ET

bad=0
for path in sys.argv[1:]:
    try:
        it=ET.iterparse(path,events=('start','end'))
        # need line numbers -> reparse manually with a simple scanner
        src=open(path,encoding='utf-8').read()
    except Exception as e:
        print(f"  PARSE FAIL {path}: {e}"); bad+=1; continue

    # Walk the tree; any element whose .text or a child's .tail contains
    # `="` is holding stray attribute text.
    try: root=ET.parse(path).getroot()
    except Exception as e:
        print(f"  PARSE FAIL {path}: {e}"); bad+=1; continue

    def check(el,where):
        global bad
        for label,txt in (('text',el.text),):
            if txt and re.search(r'\w+\s*=\s*"',txt):
                snippet=' '.join(txt.split())[:70]
                print(f"  STRAY ATTR {path}: <{el.tag.split('}')[-1]}> content -> {snippet}")
                bad+=1
        for ch in el:
            if ch.tail and re.search(r'\w+\s*=\s*"',ch.tail):
                snippet=' '.join(ch.tail.split())[:70]
                print(f"  STRAY ATTR {path}: after <{ch.tag.split('}')[-1]}> -> {snippet}")
                bad+=1
            check(ch,where)
    check(root,path)
print(f"\n{len(sys.argv)-1} file(s), {bad} stray-attribute issue(s)")

# ─────────────────────────────────────────────────────────────────────
# WHY THIS EXISTS
#
# A bulk regex that replaced <Border.Background><SolidColorBrush .../>
# </Border.Background> with an attribute string emitted the replacement
# into ELEMENT CONTENT instead of the opening tag:
#
#     <Border CornerRadius="10"
#             Padding="{TemplateBinding Padding}">
#         Background="{DynamicResource AccentBrush}"     <-- character data
#         <ContentPresenter/>
#     </Border>
#
# That is still WELL-FORMED XML — it is just text inside an element — so
# xaml_wellformed.py passed all 124 files while the WPF compiler failed
# with MC3061 ("Border does not have an associated TypeConverter to parse
# the string ..."). Structural validity is not semantic validity.
#
# Run this after ANY scripted edit that rewrites property-element syntax
# into attribute syntax.
