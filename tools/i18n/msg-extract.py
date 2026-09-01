#!/usr/bin/env python3
"""Phase D — extract code-behind MessageBox.Show literals into a master spec.

Reads every ViewModels/**/*.cs file, finds MessageBox.Show(...) calls, and
extracts:
  • the message argument (plain or C# interpolated string)
  • the caption argument (if present)
  • the format-argument expressions of interpolated strings

Output: tools/i18n/msg-spec.json
  { "keys": { "Msg.Slug": { "en": "...", "nargs": N, "sites": [ [file, line] ] } },
    "captions": { "Msg.Slug": "..." },   # caption-only keys
    "calls": [ [file, line, msgKey, captionKey, [argExpr,...]] ] }
"""
import json
import re
import glob
import sys
import unicodedata

ROOT = "ViewModels"


def csharp_unescape(s: str) -> str:
    """Convert C# string-literal escapes to real characters."""
    out = []
    i = 0
    while i < len(s):
        c = s[i]
        if c == "\\" and i + 1 < len(s):
            n = s[i + 1]
            mapping = {"n": "\n", "t": "\t", "r": "\r", '"': '"', "\\": "\\",
                       "0": "\0", "'": "'", "a": "\a", "b": "\b", "f": "\f",
                       "v": "\v"}
            if n in mapping:
                out.append(mapping[n])
                i += 2
                continue
            if n == "u" and i + 5 < len(s):
                out.append(chr(int(s[i + 2:i + 6], 16)))
                i += 6
                continue
            out.append(n)  # unknown escape: keep char
            i += 2
            continue
        out.append(c)
        i += 1
    return "".join(out)


def parse_interpolated(body: str, start_n: int = 0):
    """Parse the *content* of a C# interpolated string (without $"...).

    Returns (text_with_{n}..{n+k}, [expressions]) continuing numbering
    from start_n so concatenated segments number correctly.
    """
    text = []
    args = []
    i = 0
    n = start_n
    while i < len(body):
        c = body[i]
        if c == "{":
            if i + 1 < len(body) and body[i + 1] == "{":
                text.append("{")
                i += 2
                continue
            # find matching close brace (no nesting expected in these)
            j = body.find("}", i + 1)
            if j == -1:
                text.append("{")
                i += 1
                continue
            expr = body[i + 1:j].strip()
            # strip format specifier after ':'
            if ":" in expr:
                expr = expr.split(":", 1)[0].strip()
            text.append("{" + str(n) + "}")
            args.append(expr)
            n += 1
            i = j + 1
            continue
        if c == "}":
            if i + 1 < len(body) and body[i + 1] == "}":
                text.append("}")
                i += 2
                continue
            text.append("}")
            i += 1
            continue
        text.append(c)
        i += 1
    return csharp_unescape("".join(text)), args


def slugify(text: str, used: set, prefix: str) -> str:
    """Human-readable key slug from English text."""
    t = text
    t = t.replace("{0}", "X").replace("{1}", "Y")
    t = re.sub(r"\{[^}]*\}", "", t)
    t = t.replace("\\n", " ").replace("\n", " ")
    t = re.sub(r"[^A-Za-z0-9 ]+", " ", t)
    words = [w for w in t.split() if w]
    # drop stopwords at the start
    stop = {"a", "an", "the", "to", "of", "for", "in", "on", "this", "that",
            "your", "will", "was", "is", "are", "be", "it", "its", "at", "by",
            "from", "with", "and", "or", "has", "have", "had", "can", "not",
            "no", "all", "any", "as", "but", "do", "does", "did", "if", "so",
            "then", "there", "they", "we", "you", "please", "the"}
    while words and words[0].lower() in stop:
        words.pop(0)
    if not words:
        words = ["Message"]
    words = words[:5]
    slug = prefix + "".join(w[:1].upper() + w[1:] for w in words)
    slug = slug[:60]
    base = slug
    k = 2
    while slug in used:
        slug = f"{base}{k}"
        k += 1
    used.add(slug)
    return slug


def main():
    files = sorted(glob.glob(f"{ROOT}/**/*.cs", recursive=True))
    keys = {}
    calls = []
    used = set()
    caption_slugs = {}
    caption_to_key = {}

    for fp in files:
        src = open(fp, encoding="utf-8").read()
        for m in re.finditer(r"(?:MessageBox|App\.Dialogs)\.Show\s*\(([^;]{0,800}?)\)\s*;", src, re.S):
            args_text = m.group(1)
            line = src.count("\n", 0, m.start()) + 1
            # split top-level commas (respect quotes/parens roughly)
            parts = []
            depth = 0
            cur = []
            inquote = False
            i = 0
            while i < len(args_text):
                c = args_text[i]
                if c == '"':
                    inquote = not inquote
                    cur.append(c)
                elif c == "(" and not inquote:
                    depth += 1
                    cur.append(c)
                elif c == ")" and not inquote:
                    depth -= 1
                    cur.append(c)
                elif c == "," and depth == 0 and not inquote:
                    parts.append("".join(cur).strip())
                    cur = []
                else:
                    cur.append(c)
                i += 1
            parts.append("".join(cur).strip())
            if not parts:
                continue

            # message = parts[0], caption = parts[1] if it's a literal
            msg_lit = None
            cap_lit = None
            msg_args = []
            cap_key = None
            msg_key = None

            # Parse parts[0]: sequence of "lit", $"interp", or other expr
            def merge_literals(expr: str):
                """Merge a +-chain of string/interpolated literals.
                Returns (text_or_None, [arg_exprs], is_dynamic)."""
                segs = re.split(r"\s*\+\s*", expr)
                texts = []
                args = []
                for seg in segs:
                    m = re.fullmatch(r'\$?"((?:[^"\\]|\\.)*)"', seg, re.S)
                    if not m:
                        return None, [], True
                    raw = m.group(1)
                    if seg.startswith("$"):
                        txt, a = parse_interpolated(raw, start_n=len(args))
                        args.extend(a)
                    else:
                        txt = csharp_unescape(raw)
                    texts.append(txt)
                return "".join(texts), args, False

            msg_lit, msg_args, msg_dynamic = merge_literals(parts[0])
            if msg_dynamic:
                msg_lit = None
                msg_args = []

            cap_args = []
            if len(parts) > 1:
                m2 = re.fullmatch(r'\$?"((?:[^"\\]|\\.)*)"', parts[1], re.S)
                if m2:
                    raw = m2.group(1)
                    if parts[1].startswith("$"):
                        cap_txt, cap_args = parse_interpolated(raw)
                    else:
                        cap_txt = csharp_unescape(raw)
                    cap_lit = cap_txt
                elif re.fullmatch(r'[A-Za-z_][\w.]*\s*\?\s*.*', parts[1]) and '"' in parts[1]:
                    # ternary with a literal branch, e.g. cond ? "ProGlass Agent" : title
                    tl = re.findall(r'"((?:[^"\\]|\\.)*)"', parts[1])
                    if tl:
                        cap_lit = csharp_unescape(tl[0])
                elif '"' in parts[1] and "?" not in parts[1]:
                    tl = re.findall(r'"((?:[^"\\]|\\.)*)"', parts[1])
                    if tl:
                        cap_lit = csharp_unescape(tl[0])

            if msg_lit is not None:
                key = caption_to_key.get(msg_lit)
                if key is None:
                    key = slugify(msg_lit, used, "Msg.")
                    caption_to_key[msg_lit] = key
                    keys[key] = {"en": msg_lit, "nargs": len(msg_args), "sites": []}
                keys[key]["sites"].append([fp.replace("\\", "/"), line])
                msg_key = key

            if cap_lit is not None and cap_lit != msg_lit:
                cap_key = caption_slugs.get(cap_lit)
                if cap_key is None:
                    cap_key = slugify(cap_lit, used, "Msg.Cap")
                    caption_slugs[cap_lit] = cap_key
                    keys[cap_key] = {"en": cap_lit, "nargs": len(cap_args), "sites": []}
                keys[cap_key]["sites"].append([fp.replace("\\", "/"), line])

            calls.append([fp.replace("\\", "/"), line, msg_key, cap_key, msg_args])

    spec = {
        "keys": keys,
        "calls": calls,
    }
    with open("tools/i18n/msg-spec.json", "w", encoding="utf-8") as f:
        json.dump(spec, f, ensure_ascii=False, indent=1)

    total_calls = len(calls)
    keyed_calls = sum(1 for c in calls if c[2])
    nargs_total = sum(k["nargs"] for k in keys.values())
    print(f"MessageBox.Show calls: {total_calls}  (message-keyed: {keyed_calls})")
    print(f"Unique keys: {len(keys)}  (total format args: {nargs_total})")
    n_keys = sum(1 for k in keys if k.startswith("Msg.") and not k.startswith("Msg.Cap"))
    print(f"  messages: {n_keys}  captions: {len(keys) - n_keys}")


if __name__ == "__main__":
    main()
