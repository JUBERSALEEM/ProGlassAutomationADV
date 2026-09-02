#!/usr/bin/env python3
import os, re, sys
KEY=re.compile(r'x:Key="([^"]+)"'); REF=re.compile(r'\{(?:Static|Dynamic)Resource\s+([^}]+?)\s*\}')
TYPEREF=re.compile(r'^\{x:Type'); BUILTIN=('System','{x:','{')
def keys_in(p):
    try: return set(KEY.findall(open(p,encoding='utf-8-sig',errors='ignore').read()))
    except OSError: return set()
def global_keys(root):
    f=set()
    for fn in os.listdir(root):
        if fn.endswith('.xaml'): f|=keys_in(os.path.join(root,fn))
    for sub in ['Themes','Styles','Controls','Views','Agent']:
        for dp,_,fs in os.walk(os.path.join(root,sub)):
            for fn in fs:
                if fn.endswith('.xaml'): f|=keys_in(os.path.join(dp,fn))
    return f
if __name__=='__main__':
    root,views=sys.argv[1],sys.argv[2:]
    avail=global_keys(root); total=0
    for v in views:
        src=open(v,encoding='utf-8-sig',errors='ignore').read()
        local=set(KEY.findall(src)); refs={r.strip() for r in REF.findall(src)}
        bad=sorted(r for r in refs if r not in avail and r not in local
                   and not r.startswith(BUILTIN) and not TYPEREF.match(r))
        print(f"{'ok  ' if not bad else 'FAIL'} {v}: {len(refs)} ref(s), {len(bad)} unresolved")
        for b in bad: print(f"        MISSING: {b}")
        total+=len(bad)
    print(f"\n{len(views)} view(s), {total} unresolved resource reference(s)")
    sys.exit(1 if total else 0)
