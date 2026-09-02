#!/usr/bin/env python3
"""
Repair the ERR-633EEB "two-phase constructor" defect class.

THE DEFECT
──────────
    public FooViewModel(IBar? bar)        // DI picks THIS (greediest satisfiable)
    {
        _bar = bar!;                      // ...and nothing else
    }

    public FooViewModel()                 // all real initialization lives HERE
        : this(App.ServiceProvider?.GetService<IBar>()!)
    {
        ListVM = new ...;                 // never runs for DI instances
        SaveCommand = new ...;
    }

Microsoft.Extensions.DependencyInjection always selects the greediest
constructor it can satisfy. So every DI-resolved instance is half-built:
child view models and ICommands stay null, and the first consumer NREs.
That is exactly what killed startup in ERR-633EEB.

THE REPAIR
──────────
Move the initialization body out of the parameterless ctor into the
parameterized one, so there is a single initializing path. The
parameterless ctor keeps only its `: this(...)` delegation.

    public FooViewModel(IBar? bar)
    {
        _bar = bar!;
        ListVM = new ...;                 // moved here — runs for EVERY route
        SaveCommand = new ...;
    }

    public FooViewModel()
        : this(App.ServiceProvider?.GetService<IBar>()!)
    { }

This is brace-aware (not regex-sliced) so nested braces, lambdas,
object initializers and string literals survive intact.
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def find_block(text, open_idx):
    """Return (start, end) of a { ... } block, brace-aware.

    Skips braces inside string/char literals and comments so lambdas and
    interpolated strings cannot desynchronise the counter.
    """
    assert text[open_idx] == "{"
    depth = 0
    i = open_idx
    n = len(text)
    while i < n:
        ch = text[i]

        if ch == '"':
            # verbatim string?
            if i >= 1 and text[i - 1] == "@":
                i += 1
                while i < n:
                    if text[i] == '"':
                        if i + 1 < n and text[i + 1] == '"':
                            i += 2
                            continue
                        break
                    i += 1
            else:
                i += 1
                while i < n and text[i] != '"':
                    if text[i] == "\\":
                        i += 1
                    i += 1
        elif ch == "'":
            i += 1
            while i < n and text[i] != "'":
                if text[i] == "\\":
                    i += 1
                i += 1
        elif ch == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] != "\n":
                i += 1
            continue
        elif ch == "/" and i + 1 < n and text[i + 1] == "*":
            i = text.find("*/", i)
            if i < 0:
                return None
            i += 1
        elif ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return (open_idx, i)
        i += 1
    return None


def repair(path, cls):
    with open(path, encoding="utf-8-sig") as fh:
        text = fh.read()
    original = text

    # ── Locate the parameterless ctor and its ": this(...)" delegation ──
    m_empty = re.search(
        r"\n([ \t]*)public\s+" + cls + r"\s*\(\s*\)\s*\r?\n?\s*:\s*this\s*\(",
        text)
    if not m_empty:
        return None, "no parameterless ctor with : this(...) delegation"

    indent = m_empty.group(1)

    # Balance the ": this( ... )" argument list.
    arg_open = text.index("(", m_empty.end() - 1)
    depth = 0
    i = arg_open
    while i < len(text):
        if text[i] == "(":
            depth += 1
        elif text[i] == ")":
            depth -= 1
            if depth == 0:
                break
        i += 1
    args_end = i

    brace = text.find("{", args_end)
    if brace < 0:
        return None, "parameterless ctor body not found"
    blk = find_block(text, brace)
    if not blk:
        return None, "unbalanced parameterless ctor body"

    init_body = text[blk[0] + 1:blk[1]]
    if not init_body.strip():
        return None, "already clean (empty parameterless body)"

    # ── Locate the parameterized ctor ──
    m_param = re.search(
        r"\n([ \t]*)public\s+" + cls + r"\s*\(\s*(?!\s*\))([^)]*?)\)\s*\r?\n?\s*\{",
        text)
    if not m_param:
        return None, "no parameterized ctor"

    # Must NOT already delegate.
    head = text[m_param.start():m_param.end()]
    if ": this(" in head:
        return None, "parameterized ctor already delegates"

    p_brace = text.index("{", m_param.end() - 1)
    p_blk = find_block(text, p_brace)
    if not p_blk:
        return None, "unbalanced parameterized ctor body"

    param_body = text[p_blk[0] + 1:p_blk[1]]

    # Only treat as two-phase if the DI ctor is trivial (assignments only)
    # and the parameterless one carries real work.
    stmts = [s for s in re.split(r";|\n", param_body)
             if s.strip() and not s.strip().startswith("//")]
    if len(stmts) > 40:
        return None, "parameterized ctor already does real work"

    note = (
        f"{indent}// ── ERR-633EEB CLASS FIX (2026-09-02) ──────────────────────\n"
        f"{indent}// Initialization used to live in the PARAMETERLESS ctor while\n"
        f"{indent}// DI selected this parameterized overload (MS.DI always picks\n"
        f"{indent}// the greediest satisfiable ctor). Every DI-resolved instance\n"
        f"{indent}// was therefore half-built — null child view models / null\n"
        f"{indent}// commands — and threw NullReferenceException on first use.\n"
        f"{indent}// The body was moved HERE so all construction routes (DI,\n"
        f"{indent}// Activator fallback, XAML design-time) initialize fully.\n"
    )

    # Merge: parameterized body first (field assignments), then the
    # initialization body relocated from the parameterless ctor.
    merged = param_body.rstrip() + "\n" + init_body.rstrip() + "\n" + indent

    new_text = (
        text[:p_blk[0] + 1]
        + merged
        + text[p_blk[1]:]
    )

    # Recompute the parameterless ctor offsets against the edited text.
    shift = len(merged) - len(param_body)
    if blk[0] > p_blk[1]:
        b0, b1 = blk[0] + shift, blk[1] + shift
    else:
        b0, b1 = blk

    new_text = new_text[:b0 + 1] + "\n" + indent + new_text[b1:]

    # Insert the explanatory note above the parameterized ctor.
    ins = new_text.rindex("\n", 0, new_text.index(
        f"public {cls}(", new_text.index("class " + cls))) + 1
    m_ins = re.search(r"\n([ \t]*)public\s+" + cls + r"\s*\(\s*(?!\s*\))", new_text)
    if m_ins:
        new_text = new_text[:m_ins.start() + 1] + note + new_text[m_ins.start() + 1:]

    if new_text == original:
        return None, "no change"
    return new_text, None


TARGETS = {
    'ViewModels/DeliveryNote/GodView/DeliveryNoteGodViewViewModel.cs': 'DeliveryNoteGodViewViewModel',
    'ViewModels/JobOrder/JobOrderViewModel.cs': 'JobOrderViewModel',
    'ViewModels/JobOrder/GodView/JobOrderGodViewViewModel.cs': 'JobOrderGodViewViewModel',
    'ViewModels/JobProfitability/JobProfitabilityViewModel.cs': 'JobProfitabilityViewModel',
    'ViewModels/ProformaInvoice/Step2ClientProfile/ClientProfileViewModel.cs': 'ClientProfileViewModel',
    'ViewModels/ProformaInvoice/Step3ProjectSpecs/ProjectSpecsViewModel.cs': 'ProjectSpecsViewModel',
    'ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.Part1.cs': 'PricingEstimatorViewModel',
    'ViewModels/Settings/DeliveryNoteFeaturesViewModel.cs': 'DeliveryNoteFeaturesViewModel',
    'ViewModels/Settings/InquiryFeaturesViewModel.cs': 'InquiryFeaturesViewModel',
    'ViewModels/Settings/JobOrderFeaturesViewModel.cs': 'JobOrderFeaturesViewModel',
    'ViewModels/Settings/ProformaInvoiceFeaturesViewModel.cs': 'ProformaInvoiceFeaturesViewModel',
    'ViewModels/Settings/TaxInvoiceFeaturesViewModel.cs': 'TaxInvoiceFeaturesViewModel',
    'ViewModels/SystemCenter/SystemCenterViewModel.cs': 'SystemCenterViewModel',
    'ViewModels/TaxInvoice/GodView/TaxInvoiceGodViewViewModel.cs': 'TaxInvoiceGodViewViewModel',
    'ViewModels/Dashboard/DeliveryNote/DnDashboard/DnDashboardViewModel.cs': 'DnDashboardViewModel',
    'ViewModels/Dashboard/JobOrder/JoDashboard/JoDashboardViewModel.cs': 'JoDashboardViewModel',
    'ViewModels/Dashboard/ProformaInvoice/PiDashboardViewModel.cs': 'PiDashboardViewModel',
    'ViewModels/Dashboard/Overview/OverviewDashboardViewModel.cs': 'OverviewDashboardViewModel',
    'ViewModels/DeliveryNote/DeliveryNoteViewModel.cs': 'DeliveryNoteViewModel',
    'ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.Part2.cs': 'DeliveryNoteDetailViewModel',
    'ViewModels/DeliveryNote/DeliveryNoteList/DeliveryNoteListViewModel.Part1.cs': 'DeliveryNoteListViewModel',
    'ViewModels/JobOrder/JobOrderDetail/JobOrderDetailViewModel.cs': 'JobOrderDetailViewModel',
    'ViewModels/JobOrder/SavedJobOrdersList/SavedJobOrdersListViewModel.Part1.cs': 'SavedJobOrdersListViewModel',
    'ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs': 'ProformaInvoiceViewModel',
    'ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part1.cs': 'SavedInvoicesListViewModel',
    'ViewModels/Reports/SalesReportViewModel.cs': 'SalesReportViewModel',
    'ViewModels/Settings/SettingsViewModel.cs': 'SettingsViewModel',
    'ViewModels/TaxInvoice/TaxInvoiceViewModel.cs': 'TaxInvoiceViewModel',
    'ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.cs': 'TaxInvoiceDetailViewModel',
    'ViewModels/TaxInvoice/TaxInvoiceList/TaxInvoiceListViewModel.cs': 'TaxInvoiceListViewModel',
}

if __name__ == "__main__":
    apply = "--apply" in sys.argv
    done, skipped = [], []
    for rel, cls in sorted(TARGETS.items()):
        p = os.path.join(ROOT, rel)
        if not os.path.exists(p):
            skipped.append((rel, "missing file"))
            continue
        new, err = repair(p, cls)
        if err:
            skipped.append((rel, err))
            continue
        if apply:
            with open(p, "w", encoding="utf-8") as fh:
                fh.write(new)
        done.append(rel)

    print(("APPLIED " if apply else "WOULD FIX ") + str(len(done)))
    for d in done:
        print("   fix  ", d)
    for s, why in skipped:
        print("   skip ", s, "->", why)
