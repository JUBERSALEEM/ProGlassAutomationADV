#!/usr/bin/env python3
"""
ERR-633EEB repair, v2 (2026-09-02).

v1 (tools/fix_two_phase_ctors.py) MOVED the parameterless ctor body into
the parameterized ctor. That was wrong twice over: it dragged field
DECLARATIONS sitting between the ctors along with it, and it silently
depended on every ctor parameter being DI-registered.

v2 does NOT move code. It extracts the delegating parameterless ctor's
body verbatim into a private InitializeCore() method emitted immediately
after that ctor, and invokes it at the END of the parameterized ctor
(after every field assignment, so the body sees fully-populated state).

Because the parameterless ctor chains via ": this(...)", the
parameterized ctor runs first and InitializeCore() fires exactly once on
BOTH construction paths. Declarations never move.
"""
import io, os, re, sys

def block(t, o):
    d = 0
    for i in range(o, len(t)):
        if t[i] == '{': d += 1
        elif t[i] == '}':
            d -= 1
            if d == 0: return i
    return -1

def strip_strings(s):
    s = re.sub(r'@"(?:[^"]|"")*"', '""', s)
    s = re.sub(r'"(?:\\.|[^"\\])*"', '""', s)
    s = re.sub(r"'(?:\\.|[^'\\])'", "' '", s)
    s = re.sub(r'//[^\n]*', '', s)
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    return s

def process(path, apply):
    raw = io.open(path, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    t = raw.decode('utf-8-sig')
    if 'InitializeCore()' in t:
        return None
    cm = re.search(r'\b(?:public|internal)\s+(?:sealed\s+|abstract\s+)?partial\s+class\s+(\w+)|\b(?:public|internal)\s+(?:sealed\s+|abstract\s+)?class\s+(\w+)', t)
    if not cm: return None
    cls = cm.group(1) or cm.group(2)

    # delegating parameterless ctor
    m = re.search(r'public\s+' + re.escape(cls) + r'\s*\(\s*\)\s*(?:\r?\n)?\s*:\s*this\s*\(', t)
    if not m: return None
    # find the '{' that opens its body (after the base-call arg list)
    d, i = 0, t.index('(', m.end() - 1)
    for j in range(i, len(t)):
        if t[j] == '(': d += 1
        elif t[j] == ')':
            d -= 1
            if d == 0: i = j; break
    b = t.index('{', i)
    e = block(t, b)
    if e < 0: return None
    body = t[b + 1:e]
    if not strip_strings(body).strip():
        return None  # already empty — nothing to do

    # parameterized ctor: must exist, and we append the call at its end
    pm = None
    for c in re.finditer(r'public\s+' + re.escape(cls) + r'\s*\(\s*([^)]*[^)\s])\s*\)\s*(?:\r?\n)?\s*\{', t):
        pm = c
    if not pm: return None
    pb = t.index('{', pm.end() - 1)
    pe = block(t, pb)
    if pe < 0: return None
    if not (pe < b or pb > e):
        return None  # overlapping/nested — skip

    indent = '            '
    call = f'\n{indent}// ERR-633EEB: run shared initialization on EVERY construction\n{indent}// path. DI selects this (greediest) ctor, so without this call the\n{indent}// parameterless ctor\'s setup never ran and commands stayed null.\n{indent}InitializeCore();\n        '
    method = (f'\n\n        // ── Shared initialization (ERR-633EEB) ─────────────────────\n'
              f'        // Extracted verbatim from the parameterless ctor so both it\n'
              f'        // and the DI-selected parameterized ctor initialize fully.\n'
              f'        private void InitializeCore()\n        {{{body}}}')

    # apply edits back-to-front
    edits = sorted([(pb, pe, 'call'), (b, e, 'empty')], key=lambda x: -x[0])
    out = t
    for (s0, s1, kind) in edits:
        if kind == 'call':
            out = out[:s1] + call + out[s1:]
        else:
            out = out[:s0 + 1] + ' }' + method + out[s1 + 1:]
    if apply:
        io.open(path, 'w', encoding='utf-8-sig' if bom else 'utf-8', newline='').write(out)
    return cls

if __name__ == '__main__':
    apply = '--apply' in sys.argv
    n = 0
    for root, _, fs in os.walk('ViewModels'):
        for f in fs:
            if not f.endswith('.cs'): continue
            p = os.path.join(root, f)
            try:
                r = process(p, apply)
            except Exception as ex:
                print('  SKIP(err)', p, ex); continue
            if r:
                print(('FIXED ' if apply else 'WOULD FIX ') + p + '  ' + r); n += 1
    print('total:', n)
