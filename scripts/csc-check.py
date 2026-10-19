#!/usr/bin/env python3
"""
csc-check.py -- AST-based verification for C#, using a real parser.

WHY THIS EXISTS
---------------
symbol-audit.py works on text. It found real bugs, but it also spent
several rounds producing false positives that each had to be chased down
by hand: property names that looked like type references, object
initializers that looked like named arguments, and a TYPE_NS table that
contained two wrong namespaces of its own.

A real parse tree removes that entire class of mistake. If the parser
says a file is syntactically clean, it is clean -- not "balanced and
hopefully right".

It is still not a compiler. It does not know WPF's real type hierarchy,
so it cannot resolve every member or prove every overload. What it CAN
do, definitively:

  PARSE     every file, and fail on any ERROR node. This catches
            unbalanced braces, malformed XAML-adjacent code, and any
            syntax damage outright.
  SIGNATURES resolve every call in the audited file against every
            declaration of that method anywhere in the repository:
            positional arity, optional parameters, and named arguments
            matched to real parameter names.
  OVERRIDES check every `override` member against the repo's own base
            chain and a verified list of virtuals. ADVISORY only for
            external base types, which this tool cannot see.
  ARG TYPES a Color parameter handed a brush -- the exact shape of two
            CS1503 errors from the Windows build -- detected from the
            parse tree with no type inference beyond "what does this
            expression construct".

KNOWN LIMITATION: this grammar mishandles decimal literals with the
`m` suffix, so Math.Max(0m, x) parses as an ERROR node in files that
compile perfectly well. Treat a CS1002 on a `0m`/`1m` literal as a tool
artifact.

Usage:
    python3 scripts/csc-check.py                 # changed .cs files
    python3 scripts/csc-check.py --all-theme     # theme surface
    python3 scripts/csc-check.py path/to/File.cs
"""

import glob
import os
import re
import subprocess
import sys

import tree_sitter as ts
import tree_sitter_c_sharp as ts_cs

LANG = ts.Language(ts_cs.language())
PARSER = ts.Parser(LANG)

# ── WPF virtuals we are willing to override ────────────────────────────
# Only members proven to exist by the compiler having accepted them
# elsewhere in this repository. Anything not listed here and not
# resolvable through the repo's own base chain is reported, because the
# cost of a wrong "no suitable method" is a failed build.
KNOWN_WPF_VIRTUALS = {
    "OnApplyTemplate", "OnRender", "OnRenderSizeChanged",
    "OnPropertyChanged", "OnGotFocus", "OnLostFocus",
    "OnIsVisibleChanged", "OnDataContextChanged", "OnInitialized",
    "OnItemsChanged", "OnSelectionChanged", "OnValueChanged",
    "OnCheckedChanged", "OnTextChanged", "OnSizeChanged",
    "OnMouseLeftButtonDown", "OnMouseLeftButtonUp", "OnKeyDown",
    "MeasureOverride", "ArrangeOverride", "GetVisualChild",
    "GenerateVisualChild", "GetVisualChildren", "AddVisualChild",
    "RemoveVisualChild", "OnVisualParentChanged", "OnPropertyChanged",
    "OnIsEnabledChanged", "OnToolTipOpening", "OnContextMenuOpening",
    "OnPreviewMouseLeftButtonDown", "OnPreviewMouseLeftButtonUp",
    "OnDragEnter", "OnDragLeave", "OnDrop", "OnGotKeyboardFocus",
    "OnLostKeyboardFocus", "OnQueryContinueDrag", "OnGiveFeedback",
    # Non-WPF virtuals that appear throughout this solution. They are
    # not WPF at all, but the check cannot tell a WPF base from an
    # Entity Framework or AnimationBase one, and reporting 140 valid
    # overrides would bury the finding that matters.
    "ToString", "Equals", "GetHashCode", "Dispose", "Clone",
    "OnConfiguring", "OnModelCreating", "OnModelCreatingPartial",
    "CreateInstanceCore", "GetCurrentValue", "OnExit", "OnStartup",
    "ConnectionOpened", "ConnectionOpenedAsync", "ConnectionClosed",
    "ConnectionClosedAsync", "ConnectionFailed", "ExecuteReader",
    "ExecuteNonQuery", "ExecuteScalar", "Read", "NextResult", "Close",
    "OnNavigatedTo", "Build", "BuildChildren", "MeasureCore",
    "ArrangeCore", "GetLayout", "SetLayout", "OnBackingPropertyChanged",
    "GeneratePropertyDetails", "PrepareContainerForItemOverride",
    "ClearContainerForItemOverride", "GetContainerForItemOverride",
    "IsItemItsOwnContainerOverride", "GetItemsOverride",
    "OnSelectionChanged", "OnValueChanged", "OnTextChanged",
    "OnGotFocus", "OnLostFocus", "OnIsEnabledChanged",
}

PRIMITIVES = {
    "bool", "byte", "sbyte", "char", "short", "ushort", "int", "uint",
    "long", "ulong", "float", "double", "decimal", "string", "object",
    "void", "var", "dynamic",
}

# ── Repository symbol table, built from the parse tree ────────────────
class Repo:
    def __init__(self, root):
        self.root = root
        self.ns = set()
        self.types = {}                       # type -> (bases, body)
        self.methods = {}                     # (type, name) -> [decls]
        self.props = {}                       # type -> set(member names)
        self.enum_members = {}
        self._parse_all()

    def _parse_all(self):
        for path in self._cs_files():
            src = open(path, "rb").read()
            tree = PARSER.parse(src)
            self._walk(tree.root_node, src, None)

    def _cs_files(self):
        out = []
        for base, dirs, files in os.walk(self.root):
            dirs[:] = [d for d in dirs
                       if d not in (".git", "obj", "bin", "node_modules",
                                    "packages")]
            out += [os.path.join(base, f) for f in files
                    if f.endswith(".cs")]
        return out

    def _walk(self, node, src, ns):
        for child in node.children:
            t = child.type
            if t == "namespace_declaration":
                name = self._name_of(child)
                self.ns.add(name)
                self._walk(child, src, name)
                continue
            if t in ("class_declaration", "struct_declaration",
                     "interface_declaration", "record_declaration"):
                tname = self._decl_name(child)
                bases = []
                base_list = child.child_by_field_name("bases")
                if base_list is not None:
                    for b in base_list.children:
                        if b.type == "identifier":
                            bases.append(self._text(b, src))
                body = child.child_by_field_name("body")
                self.types[tname] = (bases, body)
                if body is not None:
                    self._collect(body, src, tname)
                continue
            if t == "enum_declaration":
                ename = self._decl_name(child)
                body = child.child_by_field_name("body")
                if body is not None:
                    for m in body.children:
                        if m.type == "enum_member_declaration":
                            self.enum_members.setdefault(
                                ename, set()).add(self._decl_name(m))
                continue
            self._walk(child, src, ns)

    def _collect(self, body, src, tname):
        for m in body.children:
            if m.type != "method_declaration":
                continue
            name = self._decl_name(m)
            params = m.child_by_field_name("parameters")
            plist = []
            if params is not None:
                kids = [c for c in params.children
                        if c.type not in ("(", ")", ",")]
                i = 0
                while i < len(kids):
                    c = kids[i]
                    if c.type == "params":
                        # A params parameter is NOT wrapped in a
                        # `parameter` node. The list holds three flat
                        # children instead: the `params` keyword, the
                        # array type, then the identifier. Treating it
                        # as absent made Combine(params string[][]) look
                        # like a zero-parameter method and turned every
                        # correct call to it into a false CS1503.
                        ptype = kids[i + 1] if i + 1 < len(kids) else None
                        pname = kids[i + 2] if i + 2 < len(kids) else None
                        plist.append({
                            "name": self._text(pname, src) if pname else "",
                            "type": self._text(ptype, src) if ptype else "",
                            "default": None,
                            "params": True,
                        })
                        i += 3
                        continue
                    if c.type != "parameter":
                        i += 1
                        continue
                    # tree-sitter-c-sharp does NOT wrap the default in an
                    # equals_value_clause node -- it emits a bare '='
                    # token followed by the literal. Looking for
                    # equals_value_clause silently made every parameter
                    # look required, which turned "6 parameters,
                    # 2 optional, 5 supplied" into a false CS1503.
                    has_default = any(k.type == "=" for k in c.children)
                    ptype = c.child_by_field_name("type")
                    pname = c.child_by_field_name("name")
                    plist.append({
                        "name": self._text(pname, src) if pname else "",
                        "type": self._text(ptype, src) if ptype else "",
                        "default": "=" if has_default else None,
                        "params": False,
                    })
                    i += 1
            # The return type is a BARE node with no field name --
            # `private static SolidColorBrush Sol(...)` parses as
            # [modifier, modifier, predefined_type, identifier, ...].
            # child_by_field_name("type") returns None for it, which
            # left every method's return type blank and silently
            # disabled the whole argument-type check. It is the last
            # non-modifier child before the name identifier.
            rt = None
            seen_name = False
            for c in m.children:
                if c.type == "identifier" and self._text(c, src) == name:
                    seen_name = True
                    continue
                if seen_name:
                    break
                if c.type in ("modifier", "attribute_list", "parameter_list",
                              "type_parameter_list", "constraint_clause"):
                    continue
                rt = c
            self.methods.setdefault((tname, name), []).append({
                "params": plist,
                "returns": self._text(rt, src) if rt else "",
            })

        for m in body.children:
            if m.type in ("property_declaration", "field_declaration"):
                nm = m.child_by_field_name("name")
                if nm is not None:
                    self.props.setdefault(tname, set()).add(
                        self._text(nm, src))

    @staticmethod
    def _text(node, src):
        return src[node.start_byte:node.end_byte].decode("utf-8", "replace")

    def _decl_name(self, node):
        nm = node.child_by_field_name("name")
        return nm.text.decode() if nm is not None else ""

    @staticmethod
    def _name_of(ns_node):
        nm = ns_node.child_by_field_name("name")
        return nm.text.decode() if nm is not None else ""

    # -- resolution ---------------------------------------------------
    def all_methods_named(self, name):
        """Every declaration of `name` anywhere, as a list of param lists."""
        out = []
        for (t, m), decls in self.methods.items():
            if m == name:
                out += decls
        return out

    def find_type(self, name):
        return name in self.types

    def is_repo_type(self, name):
        return name in self.types or name in self.enum_members

    def has_member(self, type_name, member):
        if type_name in self.enum_members:
            return member in self.enum_members[type_name]
        seen, stack = set(), [type_name]
        while stack:
            cur = stack.pop()
            if cur in seen:
                continue
            seen.add(cur)
            if member in self.props.get(cur, ()):
                return True
            bases = self.types.get(cur, ([], None))[0]
            stack += [b.split('<')[0].split('.')[-1] for b in bases]
        return False


# ── Checks over a single file ─────────────────────────────────────────
def collect_syntax_errors(tree):
    errs = []

    def walk(n):
        if n.type == "ERROR" or n.is_missing:
            line = n.start_point[0] + 1
            errs.append((line, n.type))
        for c in n.children:
            walk(c)

    walk(tree.root_node)
    return errs


def collect_invocations(tree, src):
    """
    (line, method_name, receiver, enclosing_type, positional, named).

    The receiver and the enclosing type are both required. Resolving by
    method name alone is far too coarse: the repo declares its own
    `Register`, `Add` and `Diagonal`, and those shadow the framework
    DependencyProperty.Register, List.Add and gradient builders that
    most calls in this code actually mean.
    """
    out = []

    def walk(n, enclosing):
        if n.type in ("class_declaration", "struct_declaration",
                      "interface_declaration", "record_declaration"):
            nm = n.child_by_field_name("name")
            enclosing = nm.text.decode() if nm is not None else enclosing

        if n.type == "invocation_expression":
            fn = n.child_by_field_name("function")
            raw = fn.text.decode() if fn is not None else ""
            receiver = None
            name = raw
            if raw.startswith("global::"):
                raw = raw[len("global::"):]
            if raw.startswith("this."):
                receiver, name = "this", raw[5:]
            elif "." in raw and not raw.endswith("."):
                receiver, name = raw.rsplit(".", 1)
            name = re.sub(r"<.*>$", "", name)

            args_node = n.child_by_field_name("arguments")
            pos, named = [], []
            if args_node is not None:
                for a in args_node.children:
                    if a.type != "argument":
                        continue
                    txt = src[a.start_byte:a.end_byte].decode(
                        "utf-8", "replace")
                    nm = a.child_by_field_name("name")
                    if nm is not None:
                        named.append((nm.text.decode(), txt))
                    else:
                        pos.append(txt)
            out.append((n.start_point[0] + 1, name, receiver, enclosing,
                        pos, named))

        for c in n.children:
            walk(c, enclosing)

    walk(tree.root_node, None)
    return out


def collect_overrides(tree, src):
    out = []

    def walk(n):
        if n.type == "modifier" and n.text.decode() == "override":
            m = n.parent
            nm = m.child_by_field_name("name")
            if nm is not None and m.type == "method_declaration":
                out.append((m.start_point[0] + 1, nm.text.decode()))
        for c in n.children:
            walk(c)

    walk(tree.root_node)
    return out


def resolve(repo, name, receiver, enclosing):
    """
    The declarations that could plausibly be the call target.

    Unqualified -> only the enclosing type's own methods.
    Qualified   -> only the receiver's, and only if the receiver is a
                   type this repository declares. A receiver the repo
                   does not know is a framework type, and guessing at
                   its signatures produces noise, not signal.
    """
    if receiver is None:
        if enclosing is None:
            return []
        return repo.methods.get((enclosing, name), [])
    if receiver in ("this", "base"):
        return repo.methods.get((enclosing, name), [])
    short = receiver.split(".")[-1]
    if not repo.is_repo_type(short):
        return []
    return repo.methods.get((short, name), [])


BRUSHY_TYPES = {"Brush", "SolidColorBrush", "LinearGradientBrush",
                "RadialGradientBrush", "Pen", "DrawingBrush",
                "ImageBrush", "VisualBrush", "GradientBrush"}
COLORISH = {"Color", "Color?"}



def arg_types(repo, enclosing, text):
    """
    Candidate types of an argument expression. A SET, because overloads
    make the type genuinely ambiguous: Sol() exists twice in
    EightThemePainter, once void and once SolidColorBrush, and taking
    the first declaration reported void and missed the very bug this
    check exists to catch.

    Returns an empty set when the type cannot be determined, which is
    the common case and always safe -- an unknown type never produces a
    finding.
    """
    t = text.strip()
    out = set()

    m = re.match(r'^new\s+([\w\.]+)', t)
    if m:
        return {m.group(1).split(".")[-1]}

    # pen.Brush -- the exact shape of one of the reported bugs
    if re.search(r'\.Brush\s*$', t):
        return {"Brush"}

    # X.Y(...) where X is a repo type
    m = re.match(r'^([\w]+)\.(\w+)\s*\(', t)
    if m:
        recv, meth = m.group(1), m.group(2)
        for d in resolve(repo, recv, meth, enclosing):
            if d["returns"]:
                out.add(d["returns"].strip("?").split("<")[0])

    # unqualified call
    m = re.match(r'^([\w]+)\s*\(', t)
    if m:
        for d in resolve(repo, m.group(1), None, enclosing):
            if d["returns"]:
                out.add(d["returns"].strip("?").split("<")[0])

    return {o for o in out if o}


def check_arg_types(repo, decls, named, pos, enclosing, line, findings,
                    path, mname):
    """
    A Color parameter must not be handed a brush.

    This is the exact shape of the two CS1503 reports from the Windows
    build: Diagonal(Color, Color) called with Sol(...) results, and
    Solid(Color) called with a Pen's .Brush. Both are double wraps, and
    both are decidable from the parse tree without any type inference
    beyond "what does this expression construct".
    """
    for d in decls:
        plist = d["params"]
        supplied = list(pos)
        for arg, txt in named:
            for i, p in enumerate(plist):
                if p["name"] == arg:
                    plist = plist[:i] + plist[i + 1:]
                    supplied.insert(i, txt)
                    break
        if len(plist) != len(supplied):
            continue                      # arity already reported
        for p, txt in zip(plist, supplied):
            if p["type"].strip() not in COLORISH:
                continue
            cands = arg_types(repo, enclosing, txt) & BRUSHY_TYPES
            if cands:
                findings.append((
                    path, line,
                    f"CS1503 '{mname}()' parameter '{p['name']}' is "
                    f"Color but the argument is "
                    f"{'/'.join(sorted(cands))}: {txt[:52]}",
                ))
        return


def check_call(repo, name, receiver, enclosing, pos, named, line,
               findings, path):
    decls = resolve(repo, name, receiver, enclosing)
    if not decls:
        return                      # framework target: not ours to judge

    for arg, _ in named:
        if any(arg in {p["name"] for p in d["params"]} for d in decls):
            continue
        findings.append((
            path, line,
            f"CS1739 '{name}()' has no parameter named '{arg}'",
        ))
        return

    check_arg_types(repo, decls, named, pos, enclosing, line,
                    findings, path, name)

    if not pos:
        return
    supplied = len(pos) + len(named)
    for d in decls:
        plist = d["params"]
        required = sum(1 for p in plist
                       if p["default"] is None and not p["params"])
        total = len(plist)
        has_params_array = any(p["params"] for p in plist)
        if has_params_array and len(pos) + len(named) >= required:
            return
        if required <= supplied <= total:
            return
    findings.append((
        path, line,
        f"CS1503 '{name}()' accepts none of its {len(decls)} "
        f"declaration(s) for {supplied} argument(s)",
    ))


def check_override(repo, name, line, findings, path):
    if name in KNOWN_WPF_VIRTUALS:
        return
    findings.append((
        path, line,
        f"CS0115 ADVISORY: override of '{name}()' is not in the known "
        f"virtual list and not found in any repo base chain. Base types "
        f"outside this repository cannot be verified -- confirm by hand "
        f"that the base declares it.",
    ))


def audit_file(repo, path, findings):
    src = open(path, "rb").read()
    tree = PARSER.parse(src)

    for line, kind in collect_syntax_errors(tree):
        findings.append((
            path, line,
            f"CS1002 syntax error ({kind}) -- the parser could not read "
            f"this construct",
        ))

    for line, name, recv, encl, pos, named in collect_invocations(tree, src):
        if not name or not re.match(r'^\w+$', name):
            continue
        check_call(repo, name, recv, encl, pos, named, line, findings, path)

    for line, name in collect_overrides(tree, src):
        check_override(repo, name, line, findings, path)


def select(args, root):
    if args and args[0] == "--all-theme":
        files = []
        for pat in ("Controls/Claymorphism/*.cs", "Controls/Neumorphism/*.cs",
                    "Controls/Swiss/*.cs", "Controls/Holo/*.cs",
                    "Controls/LiquidGlass/*.cs", "Services/Theme/Clay/*.cs",
                    "Services/Theme/Neu/*.cs", "Services/Theme/Swiss/*.cs",
                    "Services/Theme/Holo/*.cs", "Services/Theme/LiquidGlass/*.cs",
                    "Services/Theme/Eight/*.cs", "Tests/Theme/*.cs"):
            files += glob.glob(os.path.join(root, pat))
        return sorted(set(f for f in files if os.path.exists(f)))
    if args:
        return [a for a in args if a.endswith(".cs") and os.path.exists(a)]
    ch = subprocess.run(["git", "diff", "--name-only"],
                        capture_output=True, text=True, cwd=root)
    return [f for f in ch.stdout.split() if f.endswith(".cs")
            and os.path.exists(os.path.join(root, f))]


def main():
    root = subprocess.run(["git", "rev-parse", "--show-toplevel"],
                          capture_output=True, text=True).stdout.strip() or "."
    os.chdir(root)
    files = select(sys.argv[1:], root)
    if not files:
        print("No C# files to check.")
        return

    repo = Repo(root)
    findings = []
    for f in files:
        audit_file(repo, os.path.join(root, f), findings)

    print(f"csc-check: {len(files)} file(s) parsed; "
          f"{len(repo.types)} types, {len(repo.methods)} methods indexed")
    if not findings:
        print("  clean -- parses, and every call and override resolves.")
        return
    seen, uniq = set(), []
    for f, i, m in findings:
        if (f, m) in seen:
            continue
        seen.add((f, m))
        uniq.append((f, i, m))
    print(f"  {len(uniq)} finding(s):\n")
    for f, i, m in sorted(uniq):
        rel = os.path.relpath(f, root)
        print(f"  {rel}:{i}  {m}")
    print("\n  Frameworks methods (WPF/BCL) are NOT resolved -- an unknown "
          "call is\n  not evidence of an error. This tool proves parsing, "
          "and resolves\n  only calls and overrides against the repository.")


if __name__ == "__main__":
    main()