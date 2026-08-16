#!/usr/bin/env python3
# (a) String/comment-aware brace + paren balance scanner for C# files.
import sys

def scan(path):
    src = open(path, encoding='utf-8-sig', errors='strict').read()
    i, n = 0, len(src)
    depth = paren = 0
    line = 1
    stack = []
    errors = []
    while i < n:
        c = src[i]
        if c == '\n':
            line += 1; i += 1; continue
        if c == '/' and i + 1 < n:
            if src[i+1] == '/':
                while i < n and src[i] != '\n': i += 1
                continue
            if src[i+1] == '*':
                i += 2
                while i + 1 < n and not (src[i] == '*' and src[i+1] == '/'):
                    if src[i] == '\n': line += 1
                    i += 1
                i += 2; continue
        if c == '@' and i + 1 < n and src[i+1] == '"':
            i += 2
            while i < n:
                if src[i] == '"':
                    if i + 1 < n and src[i+1] == '"': i += 2; continue
                    i += 1; break
                if src[i] == '\n': line += 1
                i += 1
            continue
        if c == '$' and i + 1 < n and (src[i+1] == '"' or (src[i+1] == '@' and i + 2 < n and src[i+2] == '"')):
            verbatim = src[i+1] == '@'
            i += 3 if verbatim else 2
            brace_in = 0
            while i < n:
                ch = src[i]
                if ch == '\n':
                    line += 1
                    if not verbatim: break
                if ch == '{':
                    if i + 1 < n and src[i+1] == '{': i += 2; continue
                    brace_in += 1; i += 1; continue
                if ch == '}':
                    if i + 1 < n and src[i+1] == '}': i += 2; continue
                    if brace_in: brace_in -= 1
                    i += 1; continue
                if ch == '\\' and not verbatim: i += 2; continue
                if ch == '"' and brace_in == 0:
                    if verbatim and i + 1 < n and src[i+1] == '"': i += 2; continue
                    i += 1; break
                i += 1
            continue
        if c == '"':
            i += 1
            while i < n:
                if src[i] == '\\': i += 2; continue
                if src[i] == '"': i += 1; break
                if src[i] == '\n':
                    errors.append(f"{path}:{line}: unterminated string")
                    line += 1; i += 1; break
                i += 1
            continue
        if c == "'":
            i += 1
            while i < n:
                if src[i] == '\\': i += 2; continue
                if src[i] == "'": i += 1; break
                i += 1
            continue
        if c == '{': depth += 1; stack.append(line)
        elif c == '}':
            depth -= 1
            if depth < 0:
                errors.append(f"{path}:{line}: unmatched closing brace"); return errors
            stack.pop()
        elif c == '(': paren += 1
        elif c == ')':
            paren -= 1
            if paren < 0:
                errors.append(f"{path}:{line}: unmatched closing paren"); return errors
        i += 1
    if depth != 0:
        errors.append(f"{path}: {depth} unclosed brace(s), first at line {stack[0] if stack else '?'}")
    if paren != 0:
        errors.append(f"{path}: paren imbalance {paren}")
    return errors

if __name__ == '__main__':
    bad = 0
    for p in sys.argv[1:]:
        errs = scan(p)
        for e in errs: print("FAIL", e)
        if errs: bad += 1
        else: print("ok  ", p)
    print(f"\n{len(sys.argv)-1} file(s) scanned, {bad} failing")
    sys.exit(1 if bad else 0)
