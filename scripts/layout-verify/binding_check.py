#!/usr/bin/env python3
import re, sys
BINDING = re.compile(r'\{Binding\s+([^{}]*)\}', re.S)
MEMBER = re.compile(r'public\s+(?:static\s+|virtual\s+|override\s+|readonly\s+|partial\s+|sealed\s+|async\s+)*[\w<>\[\],\.\?\(\)]+\s+(\w+)\s*(?:\{|=>|;|\()')
SKIP_PREFIX = ('RelativeSource','ElementName','Source=','Path=/','/')
def members(paths):
    f=set()
    for p in paths:
        f |= set(MEMBER.findall(open(p,encoding='utf-8-sig',errors='ignore').read()))
    return f
def bindings(x):
    src=open(x,encoding='utf-8-sig',errors='ignore').read(); out=set()
    for raw in BINDING.findall(src):
        whole=' '.join(raw.split())
        if 'RelativeSource' in whole or 'ElementName' in whole or 'Source=' in whole: continue
        e=whole.strip()
        if not e or e.startswith(SKIP_PREFIX): continue
        e=e.split(',')[0].strip()
        if e.startswith('Path='): e=e[5:]
        e=e.split()[0] if e.split() else ''
        if not e or e.startswith(('.','{')): continue
        r=e.split('.')[0].split('[')[0].strip()
        if r and r[0].isalpha(): out.add(r)
    return out
if __name__=='__main__':
    x,cs=sys.argv[1],sys.argv[2:]
    have=members(cs); used=bindings(x)
    missing=sorted(b for b in used if b not in have)
    for b in sorted(used): print(('FAIL ' if b in missing else 'ok   ')+b)
    print(f"\n{x}: {len(used)} binding root(s), {len(missing)} unresolved")
    if missing: print("UNRESOLVED:", ', '.join(missing))
    sys.exit(1 if missing else 0)
