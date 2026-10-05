#!/usr/bin/env python3
"""
CS0200/CS0191 detector that understands PARTIAL CLASSES.

The earlier scan looked for declarations inside the SAME file as the
assignment. These view models are split across .Part1/.Part2/.Section
files, so a property declared in Part1 and assigned in Part2 was
invisible to it. Group by (namespace, class) across ALL files instead.
"""
import io, os, re, sys, collections

def strip(s):
    s = re.sub(r'@"(?:[^"]|"")*"', '""', s)
    s = re.sub(r'"(?:\\.|[^"\\])*"', '""', s)
    s = re.sub(r'//[^\n]*', '', s)
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    return s

def blocks(t, pat):
    """Yield (start,end) of brace blocks whose header matches pat."""
    for m in re.finditer(pat, t):
        b = t.find('{', m.end() - 1)
        if b < 0: continue
        d = 0
        for j in range(b, len(t)):
            if t[j] == '{': d += 1
            elif t[j] == '}':
                d -= 1
                if d == 0:
                    yield (b, j); break

# ── gather every file per class ──────────────────────────────────────
byclass = collections.defaultdict(list)
for root, dirs, fs in os.walk('.'):
    if any(x in root for x in ('/.git', '/bin', '/obj', '/Tests')): continue
    for f in fs:
        if not f.endswith('.cs'): continue
        p = os.path.join(root, f)
        try: raw = io.open(p, encoding='utf-8-sig').read()
        except Exception: continue
        ns = re.search(r'namespace\s+([\w.]+)', raw)
        for cm in re.finditer(r'\b(?:public|internal|private|protected)\s+(?:sealed\s+|abstract\s+|static\s+)*(?:partial\s+)?class\s+(\w+)', raw):
            byclass[((ns.group(1) if ns else ''), cm.group(1))].append((p, raw))

problems = []
for (ns, cls), files in byclass.items():
    getonly, readonly_f = {}, {}
    for p, raw in files:
        t = strip(raw)
        for m in re.finditer(r'public\s+[\w<>,\[\]\?\.\s]+?\s+(\w+)\s*\{\s*get;\s*\}', t):
            getonly[m.group(1)] = p
        for m in re.finditer(r'(?:private|protected|internal|public)\s+readonly\s+[\w<>,\[\]\?\.]+\s+(\w+)\s*[;=]', t):
            readonly_f[m.group(1)] = p
    if not getonly and not readonly_f: continue

    for p, raw in files:
        t = strip(raw)
        ctor = list(blocks(t, r'(?:public|internal|private|protected)\s+' + re.escape(cls) + r'\s*\([^)]*\)\s*(?::\s*(?:this|base)\s*\([^)]*\)\s*)?'))
        for name, decl in list(getonly.items()) + list(readonly_f.items()):
            kind = 'CS0200' if name in getonly else 'CS0191'
            for m in re.finditer(r'(?<![\w.])' + re.escape(name) + r'\s*=(?!=)', t):
                if any(s <= m.start() <= e for s, e in ctor): continue
                problems.append((kind, p, raw[:m.start()].count('\n') + 1, cls, name, decl))

seen = set(); out = []
for x in problems:
    k = (x[1], x[2], x[4])
    if k in seen: continue
    seen.add(k); out.append(x)

for kind, p, ln, cls, name, decl in sorted(out):
    print(f"{kind}  {p}:{ln}  {cls}.{name}   (declared: {decl})")
print(f"\nTOTAL: {len(out)}")
sys.exit(1 if out else 0)
