#!/usr/bin/env python3
"""Guard: no semantic surface may be composed from a fully-transparent theme brush.

ApplySemanticSurfaces() clones a *source* brush and re-publishes it under a
Surface.* key. If the source's colour alpha is 0.00 the resulting surface is
permanently invisible no matter what opacity the appearance settings request,
which reads to the user as "the surface/material control does nothing".

The source is usually not a literal: it is a variable fed by a `material switch`
expression, so every arm of that switch must be resolved. Both shapes are linted.
"""
import re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
THEMES = ["Themes/LightTheme.xaml", "Themes/DarkTheme.xaml"]

def brush_alpha(text, key):
    """Max effective alpha of a brush key, or None if it cannot be determined."""
    m = re.search(r'<SolidColorBrush\s+x:Key="%s"[^>]*?Color="([^"]+)"' % re.escape(key), text)
    if m:
        h = re.match(r'#([0-9A-Fa-f]{8})$', m.group(1))
        return int(h.group(1)[0:2], 16) / 255 if h else 1.0
    m = re.search(r'<LinearGradientBrush\s+x:Key="%s".*?</LinearGradientBrush>' % re.escape(key), text, re.S)
    if m:
        vals = []
        for c in re.findall(r'Color="([^"]+)"', m.group(0)):
            h = re.match(r'#([0-9A-Fa-f]{8})$', c)
            vals.append(int(h.group(1)[0:2], 16) / 255 if h else 1.0)
        return max(vals) if vals else None   # a gradient shows if ANY stop is opaque
    return None

def main():
    themes = {t: (ROOT / t).read_text(encoding="utf-8", errors="ignore") for t in THEMES}

    pairs = []   # (file, line, targetKey, sourceKey, how)
    for cs in sorted((ROOT / "Services").glob("ThemeManager*.cs")):
        text = cs.read_text(encoding="utf-8", errors="ignore")

        # 1) literal source: SetBrushResource(res, "target", "source", op)
        for m in re.finditer(r'SetBrushResource\s*\(\s*[^,]+,\s*"([^"]+)"\s*,\s*"([^"]+)"', text):
            pairs.append((cs.name, text[:m.start()].count("\n") + 1, m.group(1), m.group(2), "literal"))

        # 2) variable source fed by a switch: string fooKey = material switch { ... };
        arms = {}
        for m in re.finditer(r'string\s+(\w+)\s*=\s*\w+\s+switch\s*\{(.*?)\};', text, re.S):
            arms[m.group(1)] = [(text[:m.start()].count("\n") + 1, v)
                                for v in re.findall(r'=>\s*"([^"]+)"', m.group(2))]
        for m in re.finditer(r'SetBrushResource\s*\(\s*[^,]+,\s*"([^"]+)"\s*,\s*(\w+)\s*,', text):
            var = m.group(2)
            if var in arms:
                for ln, v in arms[var]:
                    pairs.append((cs.name, ln, m.group(1), v, f"switch arm of {var}"))

    offenders = []
    for f, ln, tgt, src, how in pairs:
        for t, txt in themes.items():
            a = brush_alpha(txt, src)
            if a is not None and a <= 0.001:
                offenders.append((f, ln, tgt, src, how, t))

    print(f"  SetBrushResource source bindings resolved : {len(pairs)}")
    print(f"    literal sources      : {sum(1 for p in pairs if p[4]=='literal')}")
    print(f"    switch-arm sources   : {sum(1 for p in pairs if p[4]!='literal')}")
    print(f"  theme dictionaries resolved               : {len(THEMES)}")
    if not pairs:
        print("\n  FAIL: resolved no bindings - the lint's own patterns are stale")
        return 2
    if offenders:
        print("\n  DEAD SURFACE SOURCES (alpha 0.00 -> the surface can never be seen):")
        for f, ln, tgt, src, how, t in offenders:
            print(f"    {f}:{ln}  {tgt}  <-  {src}   [{how}; {t}]")
        print(f"\n  FAIL: {len(offenders)} offender(s)")
        return 1
    print("  no fully-transparent surface sources found")
    print("\n  PASS")
    return 0

if __name__ == "__main__":
    sys.exit(main())
