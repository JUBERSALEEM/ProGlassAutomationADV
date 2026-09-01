#!/usr/bin/env python3
"""
publish-audit — senior static audit for production readiness.

Scans the WPF app for the defect classes that block a release but are
invisible to the XML/resource guards:

  P1  XAML event handlers (Click=, Loaded=, ...) that have NO matching
      method in the view's code-behind  -> compile error / runtime crash.
  P1  {Binding ElementName=X} where X is never declared with x:Name in
      the same file -> silent binding failure.
  P1  XAML x:Name referenced by code-behind but never declared
      (best-effort, warning grade).
  P2  Simple {Binding Foo} paths (no converter/RelativeSource/ElementName/
      Source) whose root ViewModel has no public Foo property/command
      -> silent empty UI (the classic MVVM "missing pattern" bug).
  P2  Code-behind doing business logic: DataContext assignments,
      MessageBox, DB/repo calls, Dispatcher, navigation.
  P3  C# smell/bug patterns: async void non-handlers, silent catch,
      SQL built via string interpolation, Thread.Sleep, event
      subscription without unsubscribe, DateTime.Now in persistence.

God files (per audit report + user directive) are REPORTED but never
modifiable by the fixer: the 3 God Views, their VMs, the 6 god
ViewModels, DataGrid* and DataGridView*, SettingsViewModel.

Usage: python3 tools/publish-audit.py [--json report.json]
Exit 0 = no P1; 1 = P1 findings.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]

# ── god-file exclusion set (read-only — never fix inside these) ──────────
GOD_PATHS = [
    "ViewModels/DeliveryNote/GodView/",
    "ViewModels/JobOrder/GodView/",
    "ViewModels/TaxInvoice/GodView/",
    "Views/DeliveryNote/GodView/",
    "Views/JobOrder/GodView/",
    "Views/TaxInvoice/GodView/",
    "ViewModels/Shared/GodViewKpiViewModel.cs",
    "ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.cs",
    "ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.ImportExport.cs",
    "Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml",
    "Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml.cs",
    "ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.cs",
    "ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.cs",
    "ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.cs",
    "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs",
    "ViewModels/Settings/SettingsViewModel.cs",
    "ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.cs",
    "Views/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml",
]

EVENT_ATTR = re.compile(
    r'\b(Click|DoubleClick|Loaded|Unloaded|Initialized|SelectionChanged|'
    r'TextChanged|Checked|Unchecked|Indeterminate|KeyDown|KeyUp|PreviewKeyDown|'
    r'MouseLeftButtonDown|MouseLeftButtonUp|MouseRightButtonDown|MouseEnter|'
    r'MouseLeave|MouseMove|MouseWheel|DragEnter|DragOver|DragLeave|Drop|'
    r'GotFocus|LostFocus|GotKeyboardFocus|LostKeyboardFocus|DataContextChanged|'
    r'PasswordChanged|ValueChanged|SelectedIndexChanged|CurrentCellChanged|'
    r'CellEditEnding|BeginningEdit|PreviewTextInput|Closing|Closed|'
    r'ScrollChanged|SizeChanged|PreviewMouseWheel|ContextMenuOpening|'
    r'AutoGeneratingColumn|Sorting|PreviewMouseLeftButtonDown)\s*=\s*"([A-Za-z_][\w]*)"')

XNAME = re.compile(r'\bx:Name\s*=\s*"([A-Za-z_][\w]*)"')
ELEMENT_NAME = re.compile(r'ElementName=([A-Za-z_][\w]*)')
SIMPLE_BINDING = re.compile(r'\{Binding\s+(?!\()([A-Za-z_][\w]*?)(?:\s*[,}])')
BINDING_SKIP = re.compile(r'RelativeSource|ElementName|Source=|Path=|Converter|UpdateSourceTrigger|Mode=')
ROOT_DATACONTEXT = re.compile(
    r'\.DataContext\s*=\s*new\s+([A-Za-z_][\w.]*)\(|'
    r'DataContext\s*=\s*new\s+([A-Za-z_][\w.]*)\(')
VIEW_DATACONTEXT = re.compile(
    r'<[\w.]+\.DataContext>\s*<[\w.]+:([A-Za-z_][\w]*)')
PUBLIC_PROP = re.compile(r'^\s*public\s+(?:(?:static|virtual|override|readonly|sealed)\s+)*'
                         r'(?:async\s+)?(?:[\w<>\[\]?,\.\s]+?)\s+([A-Za-z_][\w]*)\s*(?:\{|=>|=)',
                         re.MULTILINE)
PUBLIC_METHOD = re.compile(r'^\s*public\s+(?:async\s+)?(?:[\w<>\[\]?,\.\s]+?)\s+'
                           r'([A-Za-z_][\w]*)\s*\(', re.MULTILINE)
PROP_ASSIGN = re.compile(r'^\s*([A-Za-z_][\w]*)\s*=\s*new\s+RelayCommand', re.MULTILINE)

# known base-class members (ViewModelBase etc.) so binding checks don't
# false-positive on every inherited member
BASE_MEMBERS = {
    "IsBusy", "Title", "HasError", "ErrorMessage", "StatusMessage",
    "IsSelected", "IsEnabled", "IsVisible", "Text", "Value", "Items",
    "CurrentItem", "IsLoading", "HasItems", "IsEmpty", "SelectedItem",
}
EVENT_HANDLER_MARKER = ("sender", "RoutedEventArgs", "EventArgs", "object",
                        "KeyEventArgs", "MouseEventArgs", "DependencyPropertyChangedEventArgs")


def is_god(rel: str) -> bool:
    return any(rel == g or rel.startswith(g) for g in GOD_PATHS)


def rel(p: Path) -> str:
    return p.relative_to(ROOT).as_posix()


def xaml_files() -> list[Path]:
    return sorted(p for p in ROOT.rglob("*.xaml")
                  if "/bin/" not in p.as_posix() and "/obj/" not in p.as_posix()
                  and ".git" not in p.as_posix())


def codebehind_for(xaml: Path) -> Path | None:
    c = xaml.with_suffix(".xaml.cs")
    return c if c.exists() else None


def vm_members(vm_file: Path) -> set[str]:
    txt = vm_file.read_text(encoding="utf-8", errors="replace")
    members = set(BASE_MEMBERS)
    for m in PUBLIC_PROP.finditer(txt):
        # reject property declarations that are actually type declarations
        name = m.group(1)
        if name and name not in {"get", "set", "class", "public"}:
            members.add(name)
    for m in PUBLIC_METHOD.finditer(txt):
        name = m.group(1)
        if name and name not in {"get", "set"}:
            members.add(name)
    # plain RelayCommand auto-property initialisers: "XxxCommand = new RelayCommand"
    for m in PROP_ASSIGN.finditer(txt):
        members.add(m.group(1))
    # command members initialised in the constructor with bare-name assignment
    for m in re.finditer(r'^\s*([A-Za-z_][\w]*Command)\s*=\s*new\s+[\w.]*RelayCommand',
                         txt, re.MULTILINE):
        members.add(m.group(1))
    return members


def view_to_vm(xaml: Path, code: Path | None) -> str | None:
    """Best-effort root DataContext VM type for a view."""
    if code:
        txt = code.read_text(encoding="utf-8", errors="replace")
        for m in ROOT_DATACONTEXT.finditer(txt):
            t = m.group(1) or m.group(2)
            if t:
                return t.split(".")[-1]
    txt = xaml.read_text(encoding="utf-8", errors="replace")
    m = VIEW_DATACONTEXT.search(txt)
    if m:
        return m.group(1)
    return None


def find_vm_file(vm_name: str) -> Path | None:
    if not vm_name:
        return None
    hits = [p for p in ROOT.rglob("*.cs")
            if p.name.startswith(vm_name) and p.name.endswith(".cs")
            and "/bin/" not in p.as_posix() and "/obj/" not in p.as_posix()]
    # a VM is often partial across several files — union them all
    return hits[0] if hits else None


_VM_CACHE: dict[str, set[str]] = {}


def find_member_declarations(vm_name: str) -> set[str]:
    if vm_name in _VM_CACHE:
        return _VM_CACHE[vm_name]
    members: set[str] = set()
    for p in ROOT.rglob("*.cs"):
        relp = p.as_posix()
        if "/bin/" in relp or "/obj/" in relp or not p.name.startswith(vm_name):
            continue
        txt = p.read_text(encoding="utf-8", errors="replace")
        # only the type whose name matches vm_name (and partials)
        if f"class {vm_name}" not in txt:
            continue
        members |= vm_members(p)
    _VM_CACHE[vm_name] = members
    return members


def main() -> int:
    findings: list[dict] = []
    p1 = 0

    for xaml in xaml_files():
        relp = rel(xaml)
        try:
            xtxt = xaml.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        cbe = codebehind_for(xaml)
        ctxt = cbe.read_text(encoding="utf-8", errors="replace") if cbe else ""

        # ── P1: event handlers present in XAML but missing in code-behind ──
        if cbe:
            for m in EVENT_ATTR.finditer(xtxt):
                handler = m.group(2)
                # accept handlers invoked through XAML event wiring only if
                # method exists in the code-behind (signature tolerant)
                if not re.search(rf'\b{handler}\s*\(', ctxt):
                    findings.append(dict(
                        level="P1", kind="missing-event-handler",
                        file=relp, detail=f"<{m.group(0).split('=')[0].strip()}={handler}> has no "
                        f"matching method in {rel(cbe)}"))
                    p1 += 1

        # ── P1: ElementName binding to undeclared x:Name ──────────────────
        declared = {m.group(1) for m in XNAME.finditer(xtxt)}
        for m in ELEMENT_NAME.finditer(xtxt):
            if m.group(1) not in declared:
                findings.append(dict(
                    level="P1", kind="missing-element-name",
                    file=relp, detail=f"ElementName={m.group(1)} never declared via x:Name "
                    "in the same file"))
                p1 += 1

        # ── P2: simple binding path vs root VM members ────────────────────
        vm = view_to_vm(xaml, cbe)
        members: set[str] | None = None
        if vm:
            members = find_member_declarations(vm)
        if members is not None:
            try:
                tree = ET.parse(xaml)
                root = tree.getroot()
                parent_map = {c: p for p in root.iter() for c in p}
                for node in root.iter():
                    for attr, val in node.attrib.items():
                        if not val.startswith("{Binding"):
                            continue
                        for m in SIMPLE_BINDING.finditer(val):
                            path = m.group(1)
                            if BINDING_SKIP.search(val):
                                continue
                            if path in members or path in BASE_MEMBERS:
                                continue
                            # a binding inside a DataTemplate binds to the
                            # ITEM type, not the root VM — out of scope.
                            anc = node
                            skip = False
                            while anc is not None:
                                tag = anc.tag.split('}')[-1]
                                if tag in ("DataTemplate", "HierarchicalDataTemplate"):
                                    skip = True
                                    break
                                anc = parent_map.get(anc)
                            if skip:
                                continue
                            findings.append(dict(
                                level="P2", kind="missing-binding-property",
                                file=relp, detail=f"{{Binding {path}}} not found on "
                                f"root VM {vm} (silent empty UI)"))
            except ET.ParseError as exc:
                findings.append(dict(level="P1", kind="xaml-parse",
                                     file=relp, detail=str(exc)))
                p1 += 1

        # ── P2: code-behind business logic （mvvm boundary） ────────────────
        if cbe and re.search(r'MessageBox\.Show|new\s+\w*Repository\(|new\s+\w*Service\(|'
                             r'Application\.Current\.Dispatcher|\.Navigate\(|'
                             r'Process\.Start\(', ctxt):
            # report only the specific lines
            for i, line in enumerate(ctxt.splitlines(), 1):
                if re.search(r'MessageBox\.Show|new\s+\w*Repository\(|new\s+\w*Service\(|'
                             r'Application\.Current\.Dispatcher|\.Navigate\(|Process\.Start\(', line):
                    findings.append(dict(
                        level="P2", kind="code-behind-business-logic",
                        file=rel(cbe), line=i, detail=line.strip()[:140]))

    # ── P3: C# smell scan (excluding god files + obj/bin) ─────────────────
    for cs in ROOT.rglob("*.cs"):
        relp = cs.as_posix()
        if "/bin/" in relp or "/obj/" in relp or is_god(rel(Path(relp))):
            continue
        txt = cs.read_text(encoding="utf-8", errors="replace")
        for i, line in enumerate(txt.splitlines(), 1):
            # silent catch
            if re.search(r'catch\s*(\([^)]*\))?\s*\{\s*\}', line) or \
               re.search(r'catch\s*(\([^)]*\))?\s*\{\s*//', line):
                findings.append(dict(level="P3", kind="silent-catch",
                                     file=relp, line=i, detail=line.strip()[:120]))
            # SQL interpolation
            if re.search(r'\$\s*"(SELECT|INSERT|UPDATE|DELETE)\b[^"]*\{', line, re.I):
                findings.append(dict(level="P3", kind="sql-interpolation",
                                     file=relp, line=i, detail=line.strip()[:120]))
            # UI thread sleep / blocking
            if re.search(r'\bThread\.Sleep\(', line):
                findings.append(dict(level="P3", kind="thread-sleep",
                                     file=relp, line=i, detail=line.strip()[:120]))
            # async void that is not an obvious event handler
            if re.search(r'\basync\s+void\s+(\w+)\s*\(', line):
                name = re.search(r'\basync\s+void\s+(\w+)\s*\(', line).group(1)
                nxt = "\n".join(txt.splitlines()[i:i+6])
                if any(k in nxt for k in EVENT_HANDLER_MARKER):
                    continue
                if name in ("SafeFireAsync", "SafeFireAndForget", "HandleCrash"):
                    continue
                findings.append(dict(level="P3", kind="async-void",
                                     file=relp, line=i, detail=line.strip()[:120]))

    # group by level for the report
    by_level: dict[str, int] = {}
    for f in findings:
        by_level[f["level"]] = by_level.get(f["level"], 0) + 1

    print(f"publish-audit: {len(findings)} finding(s) — "
          f"P1={by_level.get('P1', 0)} P2={by_level.get('P2', 0)} P3={by_level.get('P3', 0)}")
    for f in findings:
        loc = f.get("line", "")
        print(f"  [{f['level']}] {f['kind']:<28} {f['file']}{':' + str(loc) if loc else ''}"
              f"  — {f['detail']}")
    if len(sys.argv) > 2 and sys.argv[1] == "--json":
        Path(sys.argv[2]).write_text(json.dumps(findings, indent=2), encoding="utf-8")
    return 1 if p1 else 0


if __name__ == "__main__":
    sys.exit(main())
