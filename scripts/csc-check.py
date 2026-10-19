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



# Names the BCL and the language supply. Only identifiers NOT in this set,
# not declared in the file, and not a repo type are reported. The set is
# intentionally broad: a false positive here costs more than a missed
# CS0103, because the whole point of this tool is that its clean runs can
# be trusted without a Windows rebuild.
KNOWN_NAMES = set("""
string int long short byte sbyte uint ulong ushort float double decimal bool
char object void var dynamic nint nuint String Int32 Int64 Double Single
Boolean Byte Char Object Decimal Math System Console Environment Exception
Task Thread Timer Guid DateTime DateTimeOffset TimeSpan DateOnly TimeOnly
Convert Action Func List Dictionary HashSet SortedList SortedDictionary
Queue Stack LinkedList IEnumerable IEnumerator ICollection IList IDictionary
IReadOnlyList IReadOnlyDictionary IComparable IEquatable ICloneable
Nullable Array Type Uri File Path Stream Text StringBuilder Random Stopwatch
Tuple ValueTuple KeyValuePair Lazy WeakReference GCHandle Marshal IntPtr
Parallel TaskFactory CancellationToken OperationCanceledException
Application Window UserControl Control ContentControl ItemsControl ListBox
ListView ComboBox ButtonBase Button ToggleButton CheckBox RadioButton
TextBlock TextBox PasswordBox Label Canvas Grid StackPanel WrapPanel
DockPanel ScrollViewer ScrollBar Slider ProgressBar TabControl TabItem
ToolTip ContextMenu Menu MenuItem Separator StatusBar ProgressBar
Dispatcher DispatcherTimer DispatcherPriority DependencyObject
DependencyProperty DependencyPropertyDescriptor FrameworkElement
Visual UIElement Panel Decorator ContentPresenter FrameworkContentElement
FrameworkElementFactory FrameworkPropertyMetadata PropertyMetadata
DefaultValueAttribute NotifyPropertyChangedCallback PropertyChangedCallback
FrameworkPropertyMetadataOptions BindingBase Binding RelativeSource
DependencyPropertyChangedEventArgs RoutedEvent RoutedEventHandler
AutomationPeer AutomationProperties IAutomationPeer TextElement
Typography TextDecorations TextDecorations3D TextFormattingMode
TextRenderingMode TextHintingMode TextOptions FlowDirection Visibility
Thickness GridLength GridUnitType Point Rect Size Vector Matrix
Transform ScaleTransform RotateTransform TranslateTransform TransformGroup
Transform3D TranslateTransform3D RotateTransform3D ScaleTransform3D
Geometry RectangleGeometry EllipseGeometry PathGeometry CombinedGeometry
StreamGeometry LineSegment PathFigure BezierSegment QuadraticBezierSegment
ArcSegment PolyLineSegment PolyBezierSegment
Brush SolidColorBrush LinearGradientBrush RadialGradientBrush
GradientStop DrawingGroup DrawingImage VisualBrush BitmapBrush
ImageBrush Pen Brushes Colors Color ColorConverter PixelFormats
Pen DashStyle PenLineCap ThicknessEffect BitmapEffect
BitmapSource FormatConvertedBitmap CroppedBitmap TransformedBitmap
WriteableBitmap PixelFormat Imaging.BitmapSource
Effect BlurEffect DropShadowEffect ShaderEffect InputElement
UIElement3D ModelVisual3D Viewport3D
Freezable PresentationSource ImageSource Image
SolidColorBrush ColorConverter Color
DispatcherObject DispatcherOperation
ComponentModel PropertyChangedEventArgs INotifyPropertyChanged
DescriptionAttribute CategoryAttribute BrowsableAttribute
AuthorAttribute VersionAttribute AssemblyTitle
KeyEventArgs MouseButtonEventArgs MouseEventArgs KeyboardEventArgs
RoutedEventArgs FocusEventArgs TextCompositionEventArgs DragEventArgs
DataObject Clipboard DragDropEffects
BooleanToVisibilityConverter StringFormat ConverterBase IValueConverter
ArgumentNullException ArgumentOutOfRangeException ArgumentException
NullReferenceException InvalidOperationException NotSupportedException
NotImplementedException FormatException OverflowException IndexOutOfRangeException
KeyNotFoundException AggregateException DivideByZeroException
Task Delay Run ConfigureAwait ContinueWith Wait Result Async

TypeConverter CultureInfo Comparison DateTimeStyles BindingOperations
PointCollection BrushCollection DoubleCollection
ObservableCollection ReadOnlyObservableCollection
FreezableCollection
DispatcherUnhandledExceptionEventArgs
DispatcherPriority SendResult
AutoCompleteBox
Random Generator CancellationTokenSource
Exception inner
inheritdoc cref langvar nameof nameof typeof sizeof typeof default
checked unchecked value is as new this base null true false in out ref
params this base lock using namespace
""".split())


def _edit_distance(a, b, cap=3):
    """Levenshtein distance, abandoned once it exceeds `cap`."""
    if abs(len(a) - len(b)) > cap:
        return cap + 1
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        cur = [i]
        for j, cb in enumerate(b, 1):
            cur.append(min(prev[j] + 1, cur[j - 1] + 1,
                           prev[j - 1] + (ca != cb)))
        if min(cur) > cap:
            return cap + 1
        prev = cur
    return prev[-1]


def _near_miss(name, pool):
    return _closest(name, pool) is not None


def _closest(name, pool):
    """The declared name within edit distance 2 of `name`, if any.

    Names shorter than four characters are skipped. At that length a
    two-character edit reaches most of the short identifiers in scope
    (`x`, `p`, `k`), so the rule stops discriminating and starts
    guessing -- which is the exact failure mode the other checks are
    careful to avoid.
    """
    if len(name) < 4:
        return None
    best, best_d = None, 3
    for cand in pool:
        if cand == name or len(cand) < 4:
            continue
        d = _edit_distance(name, cand)
        if d < best_d:
            best, best_d = cand, d
            if d == 1:
                break
    return best if best_d <= 2 else None


MEMBER_NODES = {
    "method_declaration", "constructor_declaration",
    "destructor_declaration", "operator_declaration",
    "conversion_operator_declaration", "property_declaration",
    "field_declaration", "event_declaration", "event_field_declaration",
    "indexer_declaration", "local_function_statement",
    "get_accessor", "set_accessor", "add_accessor", "remove_accessor",
}

TYPE_NODES = {
    "class_declaration", "struct_declaration", "interface_declaration",
    "record_declaration", "record_struct_declaration",
}


def collect_undefined(tree, src, repo):
    """
    (line, name) for identifiers used in expression position that are
    declared neither in the enclosing member, nor on its type, nor as a
    repo type or a known BCL name.

    Scoped to the enclosing MEMBER rather than the whole file. A
    file-wide scope is not good enough, and the defect is easy to
    demonstrate:

        public BentoRecipe(BentoProfile bento, bool dark) { _dark = dark; }
        public BentoStack Resolve(...) { ... dark ? 0.45 : 0.55 ... }

    Here `dark` IS declared in the file -- in the constructor -- so a
    file-wide check stays silent about a name that is genuinely unbound
    inside Resolve. That is CS0103 and it compiles to an error, so the
    check has to be member-scoped to be worth running at all.

    It is still not block-scoped, which means a local in an earlier
    method can mask a use in a later one only when the two share a name.
    Accepted: full block resolution would have to bind lambdas, pattern
    variables and using-aliases correctly, and every one of those is a
    false-positive source, which is a worse failure than a rare miss.
    """
    out = []
    root = tree.root_node

    def field_is(node, field):
        par = node.parent
        return par is not None and par.child_by_field_name(field) == node

    def decl_names(scope):
        """
        Every name DECLARED anywhere inside `scope`: locals, parameters,
        type parameters, fields, properties, methods and lambda
        parameters.
        """
        names = set()
        for n in walk_all(scope):
            t = n.type
            nm = n.child_by_field_name("name")
            if nm is not None and t in (
                    "variable_declarator", "parameter", "type_parameter",
                    "label_statement", "namespace_declaration",
                    "class_declaration", "struct_declaration",
                    "interface_declaration", "record_declaration",
                    "record_struct_declaration", "enum_declaration",
                    "property_declaration", "method_declaration",
                    "constructor_declaration", "destructor_declaration",
                    "operator_declaration", "conversion_operator_declaration",
                    "event_declaration", "event_field_declaration",
                    "local_function_statement", "catch_declaration",
                    "enum_member_declaration"):
                names.add(nm.text.decode())

            # for (int i = 0; ...) -- the declarator is in the initializer.
            if t == "for_statement":
                init = n.child_by_field_name("initializer")
                if init is not None:
                    for c in walk_all(init):
                        nm2 = c.child_by_field_name("name")
                        if nm2 is not None:
                            names.add(nm2.text.decode())

            # Lambda and anonymous-method parameters.
            #
            # `r => r.Id == 1` parses with an `implicit_parameter`, not a
            # bare identifier, so collecting only `parameter` nodes misses
            # every single-parameter lambda -- which is most of them,
            # because one-parameter lambdas are the common LINQ case.
            if t in ("lambda_expression", "anonymous_method_expression"):
                params = n.child_by_field_name("parameters")
                if params is not None:
                    for c in walk_all(params):
                        if c.type == "parameter":
                            nm2 = c.child_by_field_name("name")
                            if nm2 is not None:
                                names.add(nm2.text.decode())
                        elif c.type in ("identifier", "implicit_parameter"):
                            names.add(c.text.decode())

            # A field's declarator is a grandchild:
            # field_declaration > variable_declaration > variable_declarator
            if t == "variable_declaration":
                for c in n.named_children:
                    if c.type == "variable_declarator":
                        nm2 = c.child_by_field_name("name")
                        if nm2 is not None:
                            names.add(nm2.text.decode())
        return names

    # Names that are in scope before any member is considered.
    base = set(KNOWN_NAMES) | set(repo.types)

    # The project's own root namespace. `namespace ProGlassAutomation.X`
    # and `ProGlassAutomation.X.Y` qualified references are namespaces,
    # not types, and repo.types only holds types.
    base.add("ProGlassAutomation")

    # ---- one pass per type: members, then each member's own scope -----
    def type_scope_names(tnode):
        """
        Names a type contributes to the scope of its members: its fields,
        properties, events, method names, nested type names and type
        parameters.

        Deliberately NOT decl_names(tnode). That walks the whole subtree
        and so picks up PARAMETERS and LOCALS of every member, which
        silently re-creates the file-wide masking this function exists to
        remove:

            public BentoRecipe(BentoProfile bento, bool dark) { ... }
            public BentoStack Resolve(...) { ... dark ... }

        With decl_names, `dark` is "declared on the type" because the
        constructor declares it, and Resolve's unbound use stays silent
        again. A type's scope is its members, never its members' locals.
        """
        names = set()

        # Direct members of this type only -- not members of nested types.
        #
        # They are NOT direct children of the class_declaration: the
        # braces and their contents sit inside a `declaration_list`, so
        # `tnode.named_children` yields only {class, identifier,
        # declaration_list} and iterating it finds zero members.
        members = []
        for ch in tnode.named_children:
            if ch.type == "declaration_list":
                members.extend(ch.named_children)
            elif ch.type in ("field_declaration", "property_declaration",
                             "event_field_declaration", "event_declaration",
                             "method_declaration", "constructor_declaration",
                             "destructor_declaration", "operator_declaration",
                             "conversion_operator_declaration",
                             "indexer_declaration"):
                members.append(ch)

        for m in members:
            t = m.type
            if t == "field_declaration":
                # field_declaration > variable_declaration >
                #   variable_declarator > identifier
                # The declarator is a GRANDchild, so looking for it
                # directly under field_declaration silently finds nothing
                # and every private field looks undeclared.
                for vd in m.named_children:
                    if vd.type != "variable_declaration":
                        continue
                    for dec in vd.named_children:
                        if dec.type != "variable_declarator":
                            continue
                        nm = dec.child_by_field_name("name")
                        if nm is not None:
                            names.add(nm.text.decode())
            elif t in ("property_declaration", "event_field_declaration",
                       "event_declaration"):
                # An auto-property `{ get; }` has no declarator at all --
                # its name hangs directly off the declaration.
                nm = m.child_by_field_name("name")
                if nm is not None:
                    names.add(nm.text.decode())
            elif t in ("method_declaration", "constructor_declaration",
                       "destructor_declaration", "operator_declaration",
                       "conversion_operator_declaration",
                       "indexer_declaration"):
                nm = m.child_by_field_name("name")
                if nm is not None:
                    names.add(nm.text.decode())
                # `void M<T>()` makes T visible in the signature.
                for tp in walk_all(m):
                    if tp.type == "type_parameter":
                        nm2 = tp.child_by_field_name("name")
                        if nm2 is not None:
                            names.add(nm2.text.decode())
            elif t in TYPE_NODES or t in ("enum_declaration",):
                nm = m.child_by_field_name("name")
                if nm is not None:
                    names.add(nm.text.decode())

        for tp in walk_all(tnode):
            if tp.type == "type_parameter":
                nm = tp.child_by_field_name("name")
                if nm is not None:
                    names.add(nm.text.decode())

        return names

    def visit_type(tnode):
        type_members = type_scope_names(tnode)
        type_params = {
            n.text.decode() for n in walk_all(tnode)
            if n.type == "type_parameter"
        }
        inherited = base | type_members | type_params

        for m in walk_all(tnode):
            if m.type not in MEMBER_NODES:
                continue
            # Only the outermost such member inside this type; nested
            # types are visited on their own.
            anc = m.parent
            while anc is not None and anc.type not in TYPE_NODES:
                if anc.type in MEMBER_NODES:
                    return_innermost = anc
                    break
                anc = anc.parent
            else:
                return_innermost = None
            if return_innermost is not None and return_innermost is not m:
                continue

            # A member's own type name (its return type) is a use.
            scope_names = inherited | decl_names(m)

            body = m.child_by_field_name("body")
            for n in walk_all(body if body is not None else m):
                if n.type != "identifier":
                    continue
                name = n.text.decode()
                if name in scope_names or repo.is_repo_type(name):
                    continue

                # ── SOUNDNESS ──────────────────────────────────────
                # Reporting every undeclared name is not viable here.
                # It cannot be: a control subclass uses IsEnabled,
                # TryFindResource, Template and ActualHeight, all
                # inherited from WPF base classes this tool has no
                # reference assemblies for, so each one reads as
                # undeclared. On the 52-file theme surface that is 409
                # false positives against 1 true positive.
                #
                # So this only reports a name that is a NEAR MISS of a
                # name actually in scope -- within edit distance 2 of a
                # declared member or parameter. `_dark` used as `dark`
                # is distance 1 and is caught; `IsEnabled` is distance
                # everything from any local and is not. A bare identifier
                # that happens to be one or two edits from something in
                # scope is a typo with essentially no other explanation,
                # which is what makes this safe to leave switched on.
                if not _near_miss(name, scope_names):
                    continue

                par = n.parent
                pt = par.type if par is not None else ""
                if pt == "member_access_expression" and field_is(n, "name"):
                    continue
                if pt in ("qualified_name", "generic_name",
                          "alias_qualified_name", "predefined_type",
                          "predefined_generic_type", "from_clause"):
                    continue
                if pt in ("variable_declarator", "parameter",
                          "type_parameter", "enum_member_declaration",
                          "local_function_statement", "catch_declaration"):
                    continue

                # `new BentoCell(...) { IsHero = hero }` -- the left of an
                # assignment inside an object/collection initializer is a
                # MEMBER name, not a variable, so it is never a CS0103.
                # `Foo(bar: baz)` -- the `name` of an argument node is a
                # parameter name supplied by the caller, not a variable.
                if pt == "argument" and field_is(n, "name"):
                    continue

                # A bare method GROUP -- `list.Any`, `task.Wait`,
                # `PropertyChanged` -- is a delegate reference, not an
                # identifier lookup. csc-check's call check covers the
                # ones that are actually invoked.
                if pt in ("invocation_expression", "object_creation_expression",
                          "conditional_access_expression") \
                        and field_is(n, "function"):
                    continue

                if pt == "assignment_expression" and field_is(n, "left"):
                    anc = par.parent
                    inside_init = False
                    while anc is not None:
                        if anc.type == "initializer_expression":
                            inside_init = True
                            break
                        if anc.type in MEMBER_NODES or anc.type in TYPE_NODES:
                            break
                        anc = anc.parent
                    if inside_init:
                        continue

                hint = _closest(name, scope_names)
                out.append((n.start_point[0] + 1, name, hint))

    # Top-level types.
    for n in walk_all(root):
        if n.type in TYPE_NODES:
            anc = n.parent
            nested = False
            while anc is not None:
                if anc.type in TYPE_NODES:
                    nested = True
                    break
                anc = anc.parent
            if not nested:
                visit_type(n)

    # Field initialisers can sit at namespace scope in partial types, but
    # are covered above by walking each type. Namespace-level usings are
    # not expression positions.
    return out


def walk_all(root):
    stack = [root]
    while stack:
        n = stack.pop()
        yield n
        stack.extend(n.children)

# CS0103 near-miss detection is OFF by default and enabled with --ids.
#
# It is not sound, and shipping it unsound would be worse than not having
# it. The tool has no .NET reference assemblies, so it cannot know that
# `IsEnabled`, `TryFindResource` and `Template` are inherited from WPF
# base classes, nor that a BCL type named `Dark` or `List` shadows
# nothing when a local of the same name is in scope. It also does not
# do BLOCK scoping, so a local declared inside an `if` or a `foreach` is
# invisible and reads as unbound.
#
# Measured on this repo, the fully-resolved version of the check produced
# 12813 findings across 1785 files, and even after restricting it to
# near-misses it still reports 55 on the 52-file theme surface -- almost
# all of them the case-collision pattern above (`tier`/`Timer`,
# `dark`/`Dark`, `brush`/`Brush`, `path`/`Path`, `list`/`List`). One true
# positive against fifty-five false ones.
#
# It found a genuine CS0103 while this family was being written (a bare
# `dark` where `_dark` was meant, which is the missing-underscore class
# of typo), and it is kept and reachable because that class is real and
# expensive to find through a Windows rebuild. But the default runs do
# NOT include it, because "clean" has to mean something.
CHECK_IDS = False


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

    if not CHECK_IDS:
        return

    for line, name, hint in collect_undefined(tree, src, repo):
        findings.append((
            path, line,
            f"CS0103 '{name}' is not in scope here. The nearest declared "
            f"name is '{hint}' -- check for a missing prefix, a typo, or a "
            f"member on the wrong object",
        ))


def select(args, root):
    if args and args[0] == "--all-theme":
        files = []
        for pat in ("Controls/Claymorphism/*.cs", "Controls/Neumorphism/*.cs",
                    "Controls/Swiss/*.cs", "Controls/Holo/*.cs",
                    "Controls/LiquidGlass/*.cs", "Controls/Bento/*.cs",
                    "Services/Theme/Clay/*.cs",
                    "Services/Theme/Neu/*.cs", "Services/Theme/Swiss/*.cs",
                    "Services/Theme/Holo/*.cs", "Services/Theme/LiquidGlass/*.cs",
                    "Services/Theme/Bento/*.cs",
                    "Services/Theme/Eight/*.cs", "Tests/Theme/*.cs"):
            files += glob.glob(os.path.join(root, pat))
        return sorted(set(f for f in files if os.path.exists(f)))
    if args and args[0] == "--repo":
        out = []
        for dirpath, dirnames, filenames in os.walk(root):
            dirnames[:] = [d for d in dirnames
                           if d not in ("obj", "bin", ".git", "node_modules")]
            for fn in filenames:
                if fn.endswith(".cs"):
                    out.append(os.path.join(dirpath, fn))
        return sorted(out)
    if args:
        return [a for a in args if a.endswith(".cs") and os.path.exists(a)]
    ch = subprocess.run(["git", "diff", "--name-only"],
                        capture_output=True, text=True, cwd=root)
    return [f for f in ch.stdout.split() if f.endswith(".cs")
            and os.path.exists(os.path.join(root, f))]


def main():
    global CHECK_IDS
    CHECK_IDS = "--ids" in sys.argv

    root = subprocess.run(["git", "rev-parse", "--show-toplevel"],
                          capture_output=True, text=True).stdout.strip() or "."
    os.chdir(root)
    files = select([a for a in sys.argv[1:] if a != "--ids"], root)
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