#!/usr/bin/env python3
# ═══════════════════════════════════════════════════════════════════════════
#  FILE: scripts/forensics/opt_jobs_sizes_trace.py
#  PROJECT: ProGlassAutomation
#  MODULE: Forensics — RC-009 second-pass audit harness
#
#  WHY THIS EXISTS
#  ---------------------------------------------------------------------------
#  The Optimization Studio is WPF/net8.0-windows. It cannot be executed on a
#  Linux audit host, and "the code compiles" is not evidence that the SIZES
#  tab receives data. This harness is a LINE-BY-LINE port of the exact
#  production chain that runs between
#
#      USER TICKS A JOB  →  SIZES tab renders
#
#  and it measures the quantity that survives each hop:
#
#      Q0 Step 5 rows (GlassItemRowSnapshot.W1/H1/W2/H2/Qty)
#      Q1 ProductionDistributionEngine cutting items   (ProductionWorkItem.Qty)
#      Q2 BuildUpLayerVm.Pieces                        (pane layer tuples)
#      Q3 merged dict                                  (AutoRebuild merge)
#      Q4 OptimizationViewModel.Pieces                 (PieceEntryVm rows)
#      Q5 SIZES tab: SizeGroups + CutPlanRows          (what the UI binds)
#      Q6 TryBuildInputs expansion                     (what the engine cuts)
#
#  Every function below is annotated with the C# file + line it mirrors, so a
#  reviewer can check the port against the source rather than trust it.
#
#  WHAT IT IS *NOT*
#  ---------------------------------------------------------------------------
#  It is NOT the application. It proves the ALGORITHM conserves (or loses)
#  quantity and dimension. It cannot prove which binary the operator runs —
#  see §20 of the audit report for that evidence.
# ═══════════════════════════════════════════════════════════════════════════

from __future__ import annotations

import re
import sys
from dataclasses import dataclass, field
from typing import Dict, Iterable, List, Optional, Tuple

# ────────────────────────────────────────────────────────────────────────────
#  CompositionKey.Normalize
#  mirror: Services/Optimization/CompositionKey.cs:37-45
# ────────────────────────────────────────────────────────────────────────────
def composition_key_normalize(composition: Optional[str]) -> str:
    if composition is None or composition.strip() == "":
        return ""
    s = composition.strip().lower()
    while "  " in s:
        s = s.replace("  ", " ")
    if s.endswith(" glass"):
        s = s[: -len(" glass")].rstrip()
    return s


# ────────────────────────────────────────────────────────────────────────────
#  Step 5 snapshot rows
#  mirror: Models/ProformaInvoice/SavedInvoice/ProformaInvoiceSnapshot.cs:436-446
# ────────────────────────────────────────────────────────────────────────────
@dataclass
class GlassItemRowSnapshot:
    serial_number: int
    glass_ref: str
    w1: int
    h1: int
    w2: int
    h2: int
    qty: int


@dataclass
class SpecGroupSnapshot:
    group_number: int
    selected_spec_name: str
    selected_glass_type: str
    item_rows: List[GlassItemRowSnapshot] = field(default_factory=list)
    outer_sheet: Tuple[int, int] = (0, 0)
    inner_sheet: Tuple[int, int] = (0, 0)


# ────────────────────────────────────────────────────────────────────────────
#  Pane occurrences
#  mirror: Services/Production/ProductionDistributionEngine.cs:430-481
# ────────────────────────────────────────────────────────────────────────────
PANE1, PANE2 = "Pane1", "Pane2"


@dataclass
class PaneOccurrence:
    position: str
    slot: str
    composition: str          # PaneOccurrence.Composition ("" when unparsed)


def resolve_pane_occurrences(build_type: str, spec_name: str,
                             parsed_compositions: List[str]) -> List[PaneOccurrence]:
    """parsed_compositions mirrors PnLSpecParser.ResolveAllPanes(...) → PaneDisplayName."""
    panes = parsed_compositions

    def at(i: int) -> str:
        return panes[i] if 0 <= i < len(panes) else ""

    if build_type == "Dgu":
        return [PaneOccurrence("Outer", PANE1, at(0)),
                PaneOccurrence("Inner", PANE2, at(len(panes) - 1))]
    if build_type == "Lam":
        return [PaneOccurrence("Lam Outer", PANE1, at(0)),
                PaneOccurrence("Lam Inner", PANE2, at(len(panes) - 1))]
    if build_type == "DguLam":
        return [PaneOccurrence("Outer", PANE1, at(0)),
                PaneOccurrence("Lam Outer", PANE2, at(1)),
                PaneOccurrence("Lam Inner", PANE2, at(2))]
    return [PaneOccurrence("Glass", PANE1, at(0))]          # Sgu


# ────────────────────────────────────────────────────────────────────────────
#  ProductionWorkItem (Cutting stage only — the rest never reaches SIZES)
#  mirror: Services/Production/ProductionDistributionEngine.cs:277-320 (DistributeGroup)
#          Services/Production/ProductionDistributionEngine.cs:704-740 (BaseItem)
# ────────────────────────────────────────────────────────────────────────────
@dataclass
class ProductionWorkItem:
    spec_group_number: int
    position: str
    width_mm: int
    height_mm: int
    qty: int
    glass_ref: str


def is_empty_row(r: GlassItemRowSnapshot) -> bool:
    # mirror: ProductionDistributionEngine.cs:363-365
    return (r.glass_ref.strip() == "" and r.w1 <= 0 and r.h1 <= 0
            and r.w2 <= 0 and r.h2 <= 0)


def distribute_cutting(group: SpecGroupSnapshot, build_type: str,
                       occurrences: List[PaneOccurrence],
                       warnings: List[str]) -> List[ProductionWorkItem]:
    rows = sorted([r for r in group.item_rows if not is_empty_row(r)],
                  key=lambda r: r.serial_number)
    if not rows:
        return []

    multi_pane = len(occurrences) > 1
    items: List[ProductionWorkItem] = []

    for row in rows:
        for occ in occurrences:
            w = row.w1 if occ.slot == PANE1 else row.w2
            h = row.h1 if occ.slot == PANE1 else row.h2

            # STEP 4/5 SIZE RULE — equal-size fallback
            # mirror: ProductionDistributionEngine.cs:290-297
            if (w <= 0 or h <= 0) and multi_pane and occ.slot == PANE2 \
                    and row.w1 > 0 and row.h1 > 0:
                w, h = row.w1, row.h1

            if w <= 0 or h <= 0:
                warnings.append(
                    f"Spec {group.group_number}: row {row.serial_number} "
                    f"({row.glass_ref or '(no ref)'}) has no "
                    f"{'W1/H1' if occ.slot == PANE1 else 'W2/H2'} size — "
                    f"the {occ.position} pane was skipped.")
                continue

            if row.qty <= 0:
                warnings.append(
                    f"Spec {group.group_number}: row {row.serial_number} has no "
                    "quantity — produced as 1 piece.")
            qty = 1 if row.qty <= 0 else row.qty

            items.append(ProductionWorkItem(group.group_number, occ.position,
                                            w, h, qty, row.glass_ref))
    return items


# ────────────────────────────────────────────────────────────────────────────
#  BuildUpLayerVm / SpecOptionVm / JobOptionVm
#  mirror: ViewModels/Optimization/OptimizationViewModel.Jobs.cs:118-163
# ────────────────────────────────────────────────────────────────────────────
@dataclass
class BuildUpLayerVm:
    kind: str = "Pane"
    position: str = ""
    spec_label: str = ""
    title: str = ""
    total_pcs: int = 0
    count_label: str = ""
    area_label: str = ""
    is_selected: bool = False
    pieces: List[Tuple[str, str, int, int, int]] = field(default_factory=list)

    @property
    def is_pane(self) -> bool:                 # Jobs.cs:152
        return self.kind == "Pane"


@dataclass
class SpecOptionVm:
    spec_group_number: int
    spec_name: str
    build_type: str
    layers: List[BuildUpLayerVm] = field(default_factory=list)


@dataclass
class JobOptionVm:
    job_order_id: str
    specs: List[SpecOptionVm] = field(default_factory=list)
    step4_sheets: List[Tuple[int, int, str, str]] = field(default_factory=list)
    warnings: List[str] = field(default_factory=list)
    is_selected: bool = False

    @property
    def total_pcs(self) -> int:                # Jobs.cs:113
        return sum(l.total_pcs for s in self.specs for l in s.layers
                   if l.kind == "Pane")


# ────────────────────────────────────────────────────────────────────────────
#  LoadJobsInternalAsync — job card + spec build-up construction
#  mirror: ViewModels/Optimization/OptimizationViewModel.Jobs.cs:334-470
# ────────────────────────────────────────────────────────────────────────────
def parse_thickness_label(composition: str) -> str:
    # mirror: Jobs.cs:625-629
    m = re.match(r"^\s*(\d+(?:\.\d+)?)\s*mm", composition or "")
    return (m.group(1) + "mm") if m else ""


def build_job_option(job_order_id: str,
                     groups: List[SpecGroupSnapshot],
                     build_types: Dict[int, str],
                     parsed: Dict[int, List[str]],
                     sheets: Dict[int, Tuple[Tuple[int, int], Tuple[int, int]]]) \
        -> Optional[JobOptionVm]:
    warnings: List[str] = []
    cut: List[ProductionWorkItem] = []
    for g in groups:
        bt = build_types[g.group_number]
        occ = resolve_pane_occurrences(bt, g.selected_spec_name,
                                       parsed[g.group_number])
        cut.extend(distribute_cutting(g, bt, occ, warnings))

    # Q1 — the cutting pool the JOBS tab reads
    cut = [i for i in cut if i.width_mm > 0 and i.height_mm > 0]

    opt = JobOptionVm(job_order_id=job_order_id, warnings=warnings)

    for group in sorted(groups, key=lambda g: g.group_number):
        build_type = build_types[group.group_number]
        occurrences = resolve_pane_occurrences(
            build_type, group.selected_spec_name, parsed[group.group_number])
        spec = SpecOptionVm(group.group_number, group.selected_spec_name, build_type)

        pane_index = 0
        for occ in occurrences:
            pane_items = [i for i in cut
                          if i.spec_group_number == group.group_number
                          and i.position.lower() == occ.position.lower()]
            if not pane_items:                      # Jobs.cs:391
                continue

            composition = composition_key_normalize(       # Jobs.cs:398-401
                group.selected_spec_name if occ.composition.strip() == ""
                else occ.composition)

            layer = BuildUpLayerVm(
                kind="Pane",
                position=occ.position,
                spec_label=f"Spec {group.group_number}",
                title=composition,
                is_selected=True,                    # Jobs.cs:417 — pre-ticked
            )

            # Jobs.cs:425-444 — merge identical sizes
            by_size: Dict[Tuple[int, int], List[ProductionWorkItem]] = {}
            for it in pane_items:
                by_size.setdefault((it.width_mm, it.height_mm), []).append(it)
            for (w, h) in sorted(by_size):
                grp_items = by_size[(w, h)]
                qty = sum(i.qty for i in grp_items)
                labels = []
                for i in grp_items:
                    if i.glass_ref.strip() and i.glass_ref not in labels:
                        labels.append(i.glass_ref)
                label = ", ".join(labels)
                layer.pieces.append((composition, label if label.strip() else "—",
                                     w, h, qty))

            layer.total_pcs = sum(p[4] for p in layer.pieces)
            area = sum(p[2] / 1000.0 * p[3] / 1000.0 * p[4] for p in layer.pieces)
            layer.area_label = f"{area:.2f} m²"
            layer.count_label = f"{layer.total_pcs} pcs"

            spec.layers.append(layer)
            pane_index += 1
            _add_build_up_info_box(spec, build_type, pane_index)

        if spec.layers:                              # Jobs.cs:452
            opt.specs.append(spec)

        if spec.layers:                              # Jobs.cs:459-476
            outer, inner = sheets.get(group.group_number, ((0, 0), (0, 0)))

            def compose_for_slot(slot: str) -> str:
                for o in occurrences:                # FirstOrDefault
                    if o.slot == slot:
                        return composition_key_normalize(
                            group.selected_spec_name if o.composition.strip() == ""
                            else o.composition)
                return ""

            if outer[0] > 0 and outer[1] > 0:
                opt.step4_sheets.append(
                    (outer[0], outer[1], compose_for_slot(PANE1),
                     f"Spec {group.group_number} outer"))
            if inner[0] > 0 and inner[1] > 0:
                opt.step4_sheets.append(
                    (inner[0], inner[1], compose_for_slot(PANE2),
                     f"Spec {group.group_number} inner"))

    if not opt.specs:                                # Jobs.cs:497 — the gate
        return None
    return opt


def _add_build_up_info_box(spec: SpecOptionVm, build_type: str, pane_index: int) -> None:
    # mirror: Jobs.cs:602-632
    need_spacer = (build_type in ("Dgu", "DguLam") and pane_index == 1)
    need_interlayer = ((build_type == "Lam" and pane_index == 1)
                       or (build_type == "DguLam" and pane_index == 2))
    if need_spacer:
        spec.layers.append(BuildUpLayerVm(kind="Spacer", title="ASP", is_selected=False))
    elif need_interlayer:
        spec.layers.append(BuildUpLayerVm(kind="Interlayer", title="PVB",
                                          is_selected=False))


# ────────────────────────────────────────────────────────────────────────────
#  PieceEntryVm
#  mirror: ViewModels/Optimization/OptimizationViewModel.Parts.cs:30-85
# ────────────────────────────────────────────────────────────────────────────
def parse_positive_int(text: Optional[str]) -> Optional[int]:
    if text is None or text.strip() == "":
        return None
    t = text.strip()
    try:
        v = int(t)
        return v if v > 0 else None
    except ValueError:
        pass
    try:
        d = float(t)
    except ValueError:
        return None
    if d > 0 and d == int(d) and d <= 2147483647:
        return int(d)
    return None


@dataclass
class PieceEntryVm:
    glass_ref: str = "6mm clear ft glass"
    piece_label: str = ""
    position: str = ""
    width_text: str = ""
    height_text: str = ""
    qty_text: str = "1"

    def try_parse(self) -> Optional[Tuple[int, int, int]]:
        w = parse_positive_int(self.width_text)
        h = parse_positive_int(self.height_text)
        q = parse_positive_int(self.qty_text)
        if w is None or h is None or q is None:
            return None
        return (w, h, q)


# ────────────────────────────────────────────────────────────────────────────
#  OptimizationViewModel — the SIZES-relevant slice
# ────────────────────────────────────────────────────────────────────────────
ALL_CUTTING_GROUPS = "ALL — every glass group"      # Sizes.cs:82
CUT_PLAN_DISPLAY_CAP = 500                          # Sizes.cs:62


class OptimizationViewModel:
    # DEFAULTS = the code as it now stands in the repository (RC-010/011/013
    # applied). Every pre-fix reproduction below passes the old flags
    # EXPLICITLY, so a default flip can never silently turn a defect
    # demonstration into a no-op.
    def __init__(self, rc009_force_selected: bool = False,
                 rc009_fallback: bool = False,
                 per_job_reset: bool = True,
                 fix_unlabelled_group: bool = True,
                 fix_dropdown_refresh: bool = True,
                 auto_import_on_job_tick: bool = True):
        self.is_headless_host = False
        self.available_jobs: List[JobOptionVm] = []
        self.job_spec_breakdown: List[SpecOptionVm] = []
        self.pane_checklist: List[BuildUpLayerVm] = []
        self.pieces: List[PieceEntryVm] = []
        self.size_groups: List[dict] = []
        self.cut_plan_rows: List[dict] = []
        self.cutting_groups: List[str] = []
        self.selected_cutting_group: str = ALL_CUTTING_GROUPS
        self.piece_summary = "No pieces yet"
        self.has_pieces = False
        self.has_cut_plan = False
        self.run_message = ""
        self.kpi_pieces = "0"
        self.kpi_groups = "0"
        self.stocks: List[dict] = []

        # behaviour switches — lets the harness A/B the RC-009 workarounds
        self._rc009_force = rc009_force_selected
        self._rc009_fallback = rc009_fallback
        # RC-013, the replacement for RC-009's blanket force: a job's panes
        # return to their pre-ticked default only when THAT job transitions
        # unselected -> selected (OnJobSelectionChanged -> ResetPaneTicksToDefault).
        self._per_job_reset = per_job_reset
        self._fix_unlabelled = fix_unlabelled_group
        self._fix_dropdown = fix_dropdown_refresh
        # 6c7224f1 added the AutoRebuildPiecesFromSelectedJobs() call to
        # RebuildSelectedJobBreakdown. Before that commit the rebuild filled
        # JobSpecBreakdown + PaneChecklist + Stocks and NOTHING filled Pieces —
        # the operator had to press "Add selected panes → cutting list".
        self._auto_import = auto_import_on_job_tick

    # ── job tick ─────────────────────────────────────────────────────────
    def tick_job(self, job: JobOptionVm, selected: bool = True) -> None:
        """mirror: CheckBox IsChecked → JobOptionVm.IsSelected setter
                   → PropertyChanged → OnJobSelectionChanged (Jobs.cs:596-600)"""
        if job.is_selected == selected:
            return
        job.is_selected = selected
        if selected and self._per_job_reset:
            # mirror: Jobs.cs OnJobSelectionChanged -> ResetPaneTicksToDefault(job)
            # Safe from re-entrancy: a job only now becoming selected is not in
            # job_spec_breakdown yet, so its layers carry no pane-tick handler.
            for spec in job.specs:
                for layer in spec.layers:
                    if layer.is_pane:
                        layer.is_selected = True
        self.rebuild_selected_job_breakdown()

    # ── RebuildSelectedJobBreakdown ──────────────────────────────────────
    # mirror: Jobs.cs:694-762
    def rebuild_selected_job_breakdown(self) -> None:
        if self.is_headless_host:
            return

        self.job_spec_breakdown.clear()
        for job in [j for j in self.available_jobs if j.is_selected]:
            for spec in job.specs:
                for layer in spec.layers:
                    if self._rc009_force and layer.is_pane:
                        layer.is_selected = True          # Jobs.cs:730 (RC-009)
                self.job_spec_breakdown.append(spec)

        self.pane_checklist = [l for s in self.job_spec_breakdown
                               for l in s.layers if l.is_pane]
        self.selected_pane_count = sum(1 for l in self.pane_checklist if l.is_selected)

        self.sync_stocks_from_selected_jobs()
        if self._auto_import:                      # Jobs.cs:762 (added 6c7224f1)
            self.auto_rebuild_pieces_from_selected_jobs()

    # ── pane tick (checklist) ────────────────────────────────────────────
    def tick_pane(self, layer: BuildUpLayerVm, selected: bool) -> None:
        # mirror: OnLayerSelectionChanged (Jobs.cs:765-771)
        if layer.is_selected == selected:
            return
        layer.is_selected = selected
        self.selected_pane_count = sum(
            1 for s in self.job_spec_breakdown for l in s.layers
            if l.is_pane and l.is_selected)
        self.auto_rebuild_pieces_from_selected_jobs()

    def set_all_panes_selected(self, selected: bool) -> None:
        # mirror: Jobs.cs:779-786
        for s in self.job_spec_breakdown:
            for l in s.layers:
                if l.is_pane:
                    l.is_selected = selected
        self.selected_pane_count = sum(
            1 for s in self.job_spec_breakdown for l in s.layers
            if l.is_pane and l.is_selected)
        self.auto_rebuild_pieces_from_selected_jobs()

    # ── SyncStocksFromSelectedJobs ───────────────────────────────────────
    # mirror: Stocks.cs:207-252
    def sync_stocks_from_selected_jobs(self) -> int:
        sheets = [s for j in self.available_jobs if j.is_selected
                  for s in j.step4_sheets]
        manual = [s for s in self.stocks if s["source"].lower() == "manual"]
        self.stocks = list(manual)
        seen = set()
        for m in manual:
            seen.add((m["w"], m["h"], m["composition"].strip()))
        for (w, h, composition, source) in sheets:
            if w <= 0 or h <= 0:
                continue
            comp = (composition or "").strip()
            key = (w, h, comp)
            if key in seen:
                continue
            seen.add(key)
            self.stocks.append({"w": w, "h": h, "composition": comp,
                                "source": "Step 4 · " + source,
                                "is_used_in_run": True})
        return len(self.stocks)

    # ── AutoRebuildPiecesFromSelectedJobs ────────────────────────────────
    # mirror: Jobs.cs:797-977
    def auto_rebuild_pieces_from_selected_jobs(self) -> None:
        if self.is_headless_host:
            return

        selected_layers = [l for s in self.job_spec_breakdown for l in s.layers
                           if l.is_pane and l.is_selected]

        if not selected_layers:
            if self.pieces:
                self.pieces.clear()
                self.refresh_derived()
                self.run_message = ("No panes selected — tick pane boxes to "
                                    "populate cutting list.")
            return

        merged: Dict[Tuple[str, int, int, str], dict] = {}
        for layer in selected_layers:
            for (glass, label, w, h, qty) in layer.pieces:
                if w <= 0 or h <= 0 or qty <= 0:            # Jobs.cs:872
                    continue
                norm = composition_key_normalize(glass)
                pos_key = (layer.position or "").strip().lower()
                key = (norm, w, h, pos_key)
                if key in merged:
                    merged[key]["qty"] += qty
                    if label.strip() and label != "—":
                        merged[key]["labels"].add(label.strip())
                else:
                    labels = set()
                    if label.strip() and label != "—":
                        labels.add(label.strip())
                    merged[key] = {"glass": glass, "labels": labels, "w": w,
                                   "h": h, "qty": qty, "pos": layer.position}

        self.pieces.clear()
        for key in sorted(merged, key=lambda k: (k[0].lower(), k[1], k[2])):
            v = merged[key]
            label = ", ".join(sorted(v["labels"])[:8]) if v["labels"] else "—"
            row = PieceEntryVm(glass_ref=v["glass"], piece_label=label,
                               width_text=str(v["w"]), height_text=str(v["h"]),
                               qty_text=str(v["qty"]))
            row.position = v["pos"]
            self.pieces.append(row)

        self.refresh_derived()

        total_pcs = sum(v["qty"] for v in merged.values())
        distinct = len({k[0].lower() for k in merged})
        jobs = sum(1 for j in self.available_jobs if j.is_selected)
        self.run_message = (f"Auto-imported {len(selected_layers)} pane(s) from "
                            f"{jobs} job(s) → {total_pcs} pieces in {distinct} "
                            f"cutting group(s) (merged by spec+glass+size).")

        # RC-009 fallback — Jobs.cs:937-976
        if self._rc009_fallback and not merged and selected_layers:
            self.pieces.clear()
            for layer in selected_layers:
                for (glass, label, w, h, qty) in layer.pieces:
                    if w <= 0 or h <= 0 or qty <= 0:
                        continue
                    row = PieceEntryVm(glass_ref=glass, piece_label=label,
                                       width_text=str(w), height_text=str(h),
                                       qty_text=str(qty))
                    row.position = layer.position
                    self.pieces.append(row)
            self.refresh_derived()

    # ── RefreshDerived ───────────────────────────────────────────────────
    # mirror: Parts.cs:153-215
    def refresh_derived(self) -> None:
        self.rebuild_cutting_groups()

        count = 0
        area = 0.0
        for p in self.pieces:
            parsed = p.try_parse()
            if parsed is None:
                continue
            w, h, q = parsed
            count += q
            area += w / 1000.0 * (h / 1000.0) * q

        self.piece_summary = ("No pieces yet" if count == 0
                              else f"{count} pieces · {area:.2f} m² glass")
        self.has_pieces = count > 0

        self.size_groups = []
        groups: Dict[str, List[PieceEntryVm]] = {}
        for p in self.pieces:
            if p.try_parse() is None:
                continue
            groups.setdefault((p.glass_ref or "").strip(), []).append(p)
        for key in sorted(groups, key=lambda k: k.lower()):
            if not self.selected_group_matches(key):        # Parts.cs:186
                continue
            g_count, g_area = 0, 0.0
            g_sizes = set()
            for p in groups[key]:
                w, h, q = p.try_parse()
                g_count += q
                g_area += w / 1000.0 * (h / 1000.0) * q
                g_sizes.add((w, h))
            self.size_groups.append({
                "composition": key,
                "thickness": parse_thickness_label(key),
                "piece_count": g_count,
                "size_rows": len(g_sizes),
                "area_m2": round(g_area, 2),
            })

        self.rebuild_cut_plan()
        self.refresh_kpis()

    # ── RebuildCuttingGroups ─────────────────────────────────────────────
    # mirror: Sizes.cs:94-119
    def rebuild_cutting_groups(self) -> None:
        previous = self.selected_cutting_group

        comps: List[str] = []
        for p in self.pieces:
            if p.try_parse() is None:
                continue
            c = (p.glass_ref or "").strip()
            if self._fix_unlabelled and c == "":
                c = "(unlabelled glass)"
            if c == "":                                      # Sizes.cs:101
                continue
            if c.lower() not in [x.lower() for x in comps]:
                comps.append(c)
        comps.sort(key=lambda c: c.lower())

        self.cutting_groups = [ALL_CUTTING_GROUPS] + comps

        matched = next((c for c in comps
                        if c.lower() == (previous or "").lower()), None)
        self.selected_cutting_group = matched or ALL_CUTTING_GROUPS

    def selected_group_matches(self, composition: str) -> bool:
        # mirror: Sizes.cs:84-86
        return (self.selected_cutting_group.lower() == ALL_CUTTING_GROUPS.lower()
                or self.selected_cutting_group.lower() == (composition or "").lower())

    # ── RebuildCutPlan ───────────────────────────────────────────────────
    # mirror: Sizes.cs:126-171
    def rebuild_cut_plan(self) -> None:
        self.cut_plan_rows = []
        number = 0
        total = 0
        for p in self.pieces:
            parsed = p.try_parse()
            if parsed is None:
                continue
            w, h, q = parsed
            if self._fix_unlabelled:
                composition = ((p.glass_ref or "").strip()
                               or "(unlabelled glass)")
            else:
                composition = ("—" if (p.glass_ref or "").strip() == ""
                               else p.glass_ref.strip())
            if not self.selected_group_matches(composition):   # Sizes.cs:139
                continue
            total += q
            if number >= CUT_PLAN_DISPLAY_CAP:
                continue
            for i in range(1, q + 1):
                if number >= CUT_PLAN_DISPLAY_CAP:
                    break
                number += 1
                self.cut_plan_rows.append({
                    "number": number,
                    "ref": p.piece_label.strip() or "—",
                    "composition": composition,
                    "size": f"{w} × {h} mm",
                })
        self.has_cut_plan = len(self.cut_plan_rows) > 0

    # ── RefreshKpis ──────────────────────────────────────────────────────
    # mirror: Parts.cs:221-241
    def refresh_kpis(self) -> None:
        pcs = 0
        for p in self.pieces:
            parsed = p.try_parse()
            if parsed:
                pcs += parsed[2]
        self.kpi_pieces = str(pcs)
        self.kpi_groups = str(max(0, len(self.cutting_groups) - 1))

    # ── SelectedCuttingGroup setter ──────────────────────────────────────
    # mirror: Sizes.cs:71-80
    def select_cutting_group(self, value: Optional[str]) -> None:
        if self.selected_cutting_group == value or value is None:
            return
        self.selected_cutting_group = value
        if self._fix_dropdown:
            self.refresh_derived()

    # ── TryBuildInputs expansion (Q6) ────────────────────────────────────
    # mirror: Margins.cs:144-180
    def try_build_inputs(self) -> List[dict]:
        out: List[dict] = []
        for p in self.pieces:
            parsed = p.try_parse()
            if parsed is None:
                return []
            w, h, q = parsed
            for i in range(q):
                out.append({"glass_ref": p.glass_ref, "w": w, "h": h,
                            "position": p.position})
        return out


# ════════════════════════════════════════════════════════════════════════════
#  SCENARIOS
# ════════════════════════════════════════════════════════════════════════════
LOW_E = "6mm Climaguard HP Neutral 70 Low-E FT glass"
CLEAR = "6mm Clear FT glass"
RAMLY = "6mm Obscure Clear Ramly FT glass"


def benchmark_job(job_id: str = "JOB-2026-001") -> Tuple[JobOptionVm, int]:
    """The owner's calibration DGU (Parts.cs history, removed 2026-09-20):
       Spec 1 — low-e outer + 12 ASP + 6mm clear inner, 34 rows, 64 units.
       Spec 2 — low-e outer + 12 ASP + 6mm obscure ramly inner, 12 rows, 12 units.
       OUTER = 76 low-e, INNER = 64 clear + 12 ramly. Q0 = 152."""
    spec1_rows = [
        ("11", 1050, 2127, 2), ("9", 1054, 2130, 2), ("10", 1055, 2130, 2),
        ("12", 800, 1798, 2), ("1", 905, 2536, 2), ("2", 1054, 1986, 2),
        ("4", 854, 1981, 2), ("7", 1003, 1857, 2), ("3", 856, 1970, 2),
        ("8", 844, 1457, 2), ("13", 650, 1382, 2), ("14", 650, 1372, 2),
        ("2b", 605, 1471, 2), ("5", 553, 1448, 2), ("SA", 499, 1430, 2),
        ("S", 509, 1421, 2), ("R17", 1815, 725, 1), ("R8", 1515, 530, 1),
        ("R10", 2013, 828, 1), ("R10b", 2513, 535, 1), ("A2", 1055, 1985, 2),
        ("A11", 1046, 2130, 2), ("A9", 1050, 2130, 2), ("A10", 1055, 2130, 2),
        ("A7", 968, 1889, 2), ("A4", 850, 1970, 2), ("A3", 837, 1984, 2),
        ("A1", 838, 2532, 2), ("A14", 657, 1350, 2), ("A8", 935, 1418, 2),
        ("A13", 652, 1350, 2), ("A5", 654, 1479, 2), ("A6", 655, 1474, 2),
        ("A12", 800, 1805, 2),
    ]
    spec2_rows = [
        ("A7", 525, 380), ("A11", 725, 430), ("A13", 720, 430), ("A14", 720, 430),
        ("A18", 725, 430), ("A19", 725, 430), ("8", 520, 375), ("12", 725, 430),
        ("14", 710, 430), ("15", 730, 430), ("19", 720, 430), ("20", 720, 430),
    ]

    g1 = SpecGroupSnapshot(
        group_number=1,
        selected_spec_name="6mm Climaguard HP Neutral 70 Low-E FT glass + 12mm ASP + 6mm Clear FT glass",
        selected_glass_type="DGU",
        item_rows=[GlassItemRowSnapshot(i + 1, r[0], r[1], r[2], 0, 0, r[3])
                   for i, r in enumerate(spec1_rows)],
        outer_sheet=(3210, 2250), inner_sheet=(3210, 2250))
    g2 = SpecGroupSnapshot(
        group_number=2,
        selected_spec_name="6mm Climaguard HP Neutral 70 Low-E FT glass + 12mm ASP + 6mm Obscure Clear Ramly FT glass",
        selected_glass_type="DGU",
        item_rows=[GlassItemRowSnapshot(i + 1, r[0], r[1], r[2], 0, 0, 1)
                   for i, r in enumerate(spec2_rows)],
        outer_sheet=(3210, 2250), inner_sheet=(2500, 1875))

    build_types = {1: "Dgu", 2: "Dgu"}
    parsed = {1: [LOW_E, CLEAR], 2: [LOW_E, RAMLY]}
    sheets = {1: ((3210, 2250), (3210, 2250)), 2: ((3210, 2250), (2500, 1875))}

    q0 = sum(r.qty for g in (g1, g2) for r in g.item_rows) * 2   # 2 panes per unit
    opt = build_job_option(job_id, [g1, g2], build_types, parsed, sheets)
    assert opt is not None, "benchmark job must be listed in the picker"
    return opt, q0


def simple_job(job_id: str, w: int, h: int, qty: int,
               composition: str = CLEAR, build: str = "Sgu",
               spec_name: str = "6mm Clear FT glass") -> Tuple[JobOptionVm, int]:
    g = SpecGroupSnapshot(
        group_number=1, selected_spec_name=spec_name,
        selected_glass_type=build,
        item_rows=[GlassItemRowSnapshot(1, "R1", w, h, 0, 0, qty)],
        outer_sheet=(3210, 2250), inner_sheet=(0, 0))
    parsed = {1: [composition] if composition else []}
    opt = build_job_option(job_id, [g], {1: build}, parsed,
                           {1: ((3210, 2250), (0, 0))})
    return opt, qty


# ────────────────────────────────────────────────────────────────────────────
#  measurement
# ────────────────────────────────────────────────────────────────────────────
def measure(vm: OptimizationViewModel, jobs: List[JobOptionVm]) -> dict:
    selected = [j for j in jobs if j.is_selected]
    q1 = sum(l.total_pcs for j in selected for s in j.specs for l in s.layers
             if l.is_pane)
    q2 = sum(p[4] for j in selected for s in j.specs for l in s.layers
             if l.is_pane and l.is_selected for p in l.pieces)
    q4 = sum(p.try_parse()[2] for p in vm.pieces if p.try_parse())
    q5_groups = sum(g["piece_count"] for g in vm.size_groups)
    q5_plan = len(vm.cut_plan_rows)
    q6 = len(vm.try_build_inputs())
    return {"Q1_layers": q1, "Q2_selected_layers": q2, "Q3_pieces_rows": len(vm.pieces),
            "Q4_pieces_qty": q4, "Q5_sizegroups_qty": q5_groups,
            "Q5_cutplan_rows": q5_plan, "Q6_engine_parts": q6,
            "HasPieces": vm.has_pieces, "HasCutPlan": vm.has_cut_plan,
            "PieceSummary": vm.piece_summary,
            "CuttingGroups": len(vm.cutting_groups),
            "SelectedCuttingGroup": vm.selected_cutting_group}


def hdr(t: str) -> None:
    print("\n" + "═" * 78)
    print("  " + t)
    print("═" * 78)


def row(label: str, m: dict) -> None:
    print(f"  {label:<34} layers={m['Q1_layers']:>4} sel={m['Q2_selected_layers']:>4} "
          f"rows={m['Q3_pieces_rows']:>3} qty={m['Q4_pieces_qty']:>4} "
          f"SG={m['Q5_sizegroups_qty']:>4} CP={m['Q5_cutplan_rows']:>4} "
          f"ENG={m['Q6_engine_parts']:>4} HasPieces={str(m['HasPieces']):<5}")


PASS, FAIL = [], []


def check(name: str, ok: bool, detail: str = "") -> None:
    (PASS if ok else FAIL).append(name)
    print(f"  [{'PASS' if ok else 'FAIL'}] {name}" + (f" — {detail}" if detail else ""))


# ════════════════════════════════════════════════════════════════════════════
def main() -> int:
    hdr("RC-009 SECOND-PASS FORENSIC HARNESS — JOBS → SIZES quantity conservation")

    # ── Test C / benchmark: two-spec DGU, 152 pieces ─────────────────────
    hdr("TEST C — benchmark job (2 specs, DGU, qty>1) → tick one job")
    job, q0 = benchmark_job()
    vm = OptimizationViewModel()
    vm.available_jobs = [job]
    m0 = measure(vm, [job])
    row("before tick", m0)
    vm.tick_job(job, True)
    m1 = measure(vm, [job])
    row("after tick", m1)
    print(f"  Q0 (Step 5 rows × 2 panes) = {q0}")
    print(f"  RunMessage: {vm.run_message}")
    print(f"  SizeGroups: " + ", ".join(
        f"{g['composition'] or '(blank)'}={g['piece_count']}" for g in vm.size_groups))
    check("C: Q0 == Q1 == Q2 == Q4",
          q0 == m1["Q1_layers"] == m1["Q2_selected_layers"] == m1["Q4_pieces_qty"],
          f"Q0={q0} Q1={m1['Q1_layers']} Q2={m1['Q2_selected_layers']} Q4={m1['Q4_pieces_qty']}")
    check("C: SIZES tab quantity == Pieces quantity (Q5 == Q4)",
          m1["Q5_sizegroups_qty"] == m1["Q4_pieces_qty"],
          f"Q5={m1['Q5_sizegroups_qty']}")
    check("C: CUT PLAN expands every piece (Q5plan == Q4)",
          m1["Q5_cutplan_rows"] == m1["Q4_pieces_qty"])
    check("C: engine receives every piece (Q6 == Q4)",
          m1["Q6_engine_parts"] == m1["Q4_pieces_qty"])
    check("C: HasPieces true → CUTTING LIST panel visible", m1["HasPieces"] is True)
    check("C: 3 cutting groups (low-e / clear / ramly)", m1["CuttingGroups"] == 4,
          f"dropdown entries={m1['CuttingGroups']} (incl. ALL)")

    # ── Test A: one job / one pane / one size ────────────────────────────
    hdr("TEST A — one job, one pane (SGU), one size, qty 1")
    jA, qA = simple_job("JOB-A", 1200, 900, 1)
    vmA = OptimizationViewModel(); vmA.available_jobs = [jA]
    vmA.tick_job(jA, True)
    mA = measure(vmA, [jA])
    row("after tick", mA)
    check("A: 1 piece survives to SIZES",
          qA == mA["Q4_pieces_qty"] == mA["Q5_sizegroups_qty"] == mA["Q6_engine_parts"])

    # ── Test E: qty > 1 ──────────────────────────────────────────────────
    hdr("TEST E — one job, one pane, qty 37")
    jE, qE = simple_job("JOB-E", 1050, 2127, 37)
    vmE = OptimizationViewModel(); vmE.available_jobs = [jE]
    vmE.tick_job(jE, True)
    mE = measure(vmE, [jE])
    row("after tick", mE)
    check("E: qty 37 conserved to SIZES + engine",
          mE["Q4_pieces_qty"] == 37 and mE["Q5_sizegroups_qty"] == 37
          and mE["Q6_engine_parts"] == 37)
    check("E: CUT PLAN expands qty into 37 rows", mE["Q5_cutplan_rows"] == 37)

    # ── Test B: one job, multiple panes (DGU equal-size) ─────────────────
    hdr("TEST B — one job, DGU, equal-size (W2/H2 blank → inherits W1/H1)")
    gB = SpecGroupSnapshot(
        group_number=1,
        selected_spec_name="6mm Clear FT glass + 12mm ASP + 6mm Clear FT glass",
        selected_glass_type="DGU",
        item_rows=[GlassItemRowSnapshot(1, "R1", 1000, 1500, 0, 0, 4)],
        outer_sheet=(3210, 2250), inner_sheet=(3210, 2250))
    jB = build_job_option("JOB-B", [gB], {1: "Dgu"}, {1: [CLEAR, CLEAR]},
                          {1: ((3210, 2250), (3210, 2250))})
    vmB = OptimizationViewModel(); vmB.available_jobs = [jB]
    vmB.tick_job(jB, True)
    mB = measure(vmB, [jB])
    row("after tick", mB)
    print(f"  pane layers: {[(l.position, l.count_label) for s in jB.specs for l in s.layers if l.is_pane]}")
    check("B: outer + inner both imported (8 pieces from 4 units)",
          mB["Q4_pieces_qty"] == 8 and mB["Q5_sizegroups_qty"] == 8,
          "equal-size DGU must NOT collapse outer+inner — the merge key carries position")

    # ── Test D: different W/H combinations ───────────────────────────────
    hdr("TEST D — dimension conservation, source → Step 5 → Pieces → SIZES → engine")
    jD, _ = simple_job("JOB-D", 1234, 2345, 3)
    vmD = OptimizationViewModel(); vmD.available_jobs = [jD]
    vmD.tick_job(jD, True)
    src = jD.specs[0].layers[0].pieces[0]
    piece = vmD.pieces[0]
    plan = vmD.cut_plan_rows[0]
    eng = vmD.try_build_inputs()[0]
    chain = [(src[2], src[3]), (int(piece.width_text), int(piece.height_text)),
             (eng["w"], eng["h"])]
    print(f"  Step5 row W1×H1      = 1234 × 2345")
    print(f"  ProductionWorkItem   = {src[2]} × {src[3]}")
    print(f"  PieceEntryVm         = {piece.width_text} × {piece.height_text}")
    print(f"  SIZES cut-plan row   = {plan['size']}")
    print(f"  TryBuildInputs part  = {eng['w']} × {eng['h']}")
    check("D: dimensions identical at every hop",
          all(c == (1234, 2345) for c in chain) and plan["size"] == "1234 × 2345 mm")

    # ── Test I: change job selection after SIZES populated ───────────────
    hdr("TEST I — change job selection after SIZES has populated (merge across jobs)")
    jI1, _ = simple_job("JOB-I1", 1000, 1000, 5, CLEAR)
    jI2, _ = simple_job("JOB-I2", 1000, 1000, 7, CLEAR)
    vmI = OptimizationViewModel(); vmI.available_jobs = [jI1, jI2]
    vmI.tick_job(jI1, True); row("job 1 ticked", measure(vmI, [jI1, jI2]))
    vmI.tick_job(jI2, True); mI = measure(vmI, [jI1, jI2])
    row("job 2 also ticked", mI)
    print(f"  RunMessage: {vmI.run_message}")
    check("I: identical size+glass across two jobs merges to qty 12",
          mI["Q3_pieces_rows"] == 1 and mI["Q4_pieces_qty"] == 12,
          f"rows={mI['Q3_pieces_rows']} qty={mI['Q4_pieces_qty']}")
    vmI.tick_job(jI2, False); mI2 = measure(vmI, [jI1, jI2])
    row("job 2 unticked", mI2)
    check("I: unticking removes exactly that job's quantity", mI2["Q4_pieces_qty"] == 5)

    # ── Test G: clear selection and select again ─────────────────────────
    hdr("TEST G — 'Clear' pane selection, then re-tick a job")
    jG, _ = simple_job("JOB-G", 800, 1200, 6)
    vmG = OptimizationViewModel(); vmG.available_jobs = [jG]
    vmG.tick_job(jG, True); row("ticked", measure(vmG, [jG]))
    vmG.set_all_panes_selected(False); row("after Clear (panes unticked)", measure(vmG, [jG]))
    check("G: Clear empties the cutting list", vmG.pieces == [] and vmG.has_pieces is False)
    vmG.tick_job(jG, False); vmG.tick_job(jG, True)
    mG = measure(vmG, [jG])
    row("untick + re-tick job", mG)
    check("G: re-ticking the job restores the sizes (RC-013 per-job pane reset)",
          mG["Q4_pieces_qty"] == 6,
          "RC-009 made this work with a blanket force over EVERY selected job's "
          "panes; RC-013 makes it work by resetting only the job being re-selected. "
          "With neither, the job re-ticks with every pane still unticked and SIZES "
          "stays EMPTY")

    # ── Test F: navigate away and return ─────────────────────────────────
    hdr("TEST F — navigate away and return (VM is cached by MainViewModel)")
    print("  MainViewModel.Part1.cs:102  OptimizationVM => _lazyOptimizationVM ??= ResolveOrNew<...>()")
    print("  MainViewModel.Experience.cs:119  ProfessionalContent = _currentViewModel (same instance)")
    jF, _ = simple_job("JOB-F", 900, 900, 2)
    vmF = OptimizationViewModel(); vmF.available_jobs = [jF]
    vmF.tick_job(jF, True)
    before = measure(vmF, [jF])
    # returning to the tab does not reconstruct the VM: no ctor, no LoadJobs,
    # so nothing clears Pieces. Modelled as a no-op re-read.
    after = measure(vmF, [jF])
    check("F: state survives navigation (no re-construction, no clear)", before == after)

    # ── Test J: reload the application ───────────────────────────────────
    hdr("TEST J — reload the application and repeat")
    jJ, _ = simple_job("JOB-J", 1500, 750, 9)
    vmJ = OptimizationViewModel(); vmJ.available_jobs = [jJ]
    vmJ.tick_job(jJ, True); mJ = measure(vmJ, [jJ])
    row("fresh VM, tick", mJ)
    check("J: identical result on a fresh VM", mJ["Q4_pieces_qty"] == 9)

    # ── DEFECT REPRO 1 — RC-009 fallback is dead code ────────────────────
    hdr("DEFECT REPRO 1 — RC-009 fallback (Jobs.cs:937-976) can never add a row")
    print("  The fallback runs only when merged.Count == 0, and it applies the")
    print("  IDENTICAL filter (w<=0||h<=0||qty<=0) to the IDENTICAL source")
    print("  (selectedLayers[*].Pieces). merged==0 therefore implies the")
    print("  fallback also yields 0 rows — it is provably unreachable-as-useful.")
    reached = 0
    for (w, h, q) in [(0, 100, 1), (100, 0, 1), (100, 100, 0), (-5, 100, 1)]:
        layer = BuildUpLayerVm(kind="Pane", position="Outer", is_selected=True,
                               pieces=[("", "R", w, h, q)])
        spec = SpecOptionVm(1, "s", "Sgu", [layer])
        jobX = JobOptionVm("JOB-X", [spec]); jobX.is_selected = True
        vmX = OptimizationViewModel(rc009_fallback=True); vmX.available_jobs = [jobX]
        vmX.job_spec_breakdown = [spec]
        vmX.pane_checklist = [layer]
        vmX.auto_rebuild_pieces_from_selected_jobs()
        if vmX.pieces:
            reached += 1
    check("REPRO1: fallback produced rows in 0 of 4 zero-dimension cases",
          reached == 0, f"rows produced={reached}")

    # ── DEFECT REPRO 2 — unlabelled glass vanishes from SIZES ────────────
    hdr("DEFECT REPRO 2 — a specific cutting-group selection silently drops")
    print("              pieces whose composition is EMPTY (Sizes.cs:101 vs")
    print("              Parts.cs:184 / Sizes.cs:137)")
    gU1 = SpecGroupSnapshot(1, "6mm Clear FT glass", "SGU",
                            [GlassItemRowSnapshot(1, "R1", 1000, 1000, 0, 0, 4)],
                            (3210, 2250), (0, 0))
    # spec 2's name does not parse AND is blank → CompositionKey.Normalize("") == ""
    gU2 = SpecGroupSnapshot(2, "", "SGU",
                            [GlassItemRowSnapshot(1, "R2", 1200, 800, 0, 0, 9)],
                            (3210, 2250), (0, 0))
    jU = build_job_option("JOB-U", [gU1, gU2], {1: "Sgu", 2: "Sgu"},
                          {1: [CLEAR], 2: []},
                          {1: ((3210, 2250), (0, 0)), 2: ((3210, 2250), (0, 0))})
    assert jU is not None
    vmU = OptimizationViewModel(fix_unlabelled_group=False,
                                fix_dropdown_refresh=False)
    vmU.available_jobs = [jU]
    vmU.tick_job(jU, True)
    mU = measure(vmU, [jU])
    row("both specs, dropdown = ALL", mU)
    print(f"  dropdown: {vmU.cutting_groups}")
    labelled = composition_key_normalize(CLEAR)
    vmU.select_cutting_group(labelled)          # operator picks the clear group
    vmU.refresh_derived()                        # next refresh (qty edit, job tick…)
    mU2 = measure(vmU, [jU])
    row("dropdown = '6mm clear ft'", mU2)
    lost = mU["Q4_pieces_qty"] - mU2["Q5_sizegroups_qty"]
    print(f"  Pieces still holds {mU2['Q4_pieces_qty']} pieces; SIZES shows "
          f"{mU2['Q5_sizegroups_qty']} — {lost} VANISHED")
    check("REPRO2 (pre-fix): unlabelled pieces are lost from SIZES",
          mU2["Q4_pieces_qty"] == 13 and mU2["Q5_sizegroups_qty"] == 4,
          "this is the only code-level mechanism that empties/partially empties "
          "SIZES while Pieces is populated")

    # A JobOptionVm/BuildUpLayerVm carries its own IsSelected state, so the
    # fixed variant must run on a FRESH job — re-ticking an already-ticked
    # instance is a no-op (Jobs.cs:88 early-returns on an unchanged value).
    jU2 = build_job_option("JOB-U2", [gU1, gU2], {1: "Sgu", 2: "Sgu"},
                           {1: [CLEAR], 2: []},
                           {1: ((3210, 2250), (0, 0)), 2: ((3210, 2250), (0, 0))})
    vmUfix = OptimizationViewModel(fix_unlabelled_group=True,
                                   fix_dropdown_refresh=True)
    vmUfix.available_jobs = [jU2]
    vmUfix.tick_job(jU2, True)
    vmUfix.select_cutting_group(labelled)
    mUf = measure(vmUfix, [jU])
    row("FIXED: dropdown = '6mm clear ft'", mUf)
    check("REPRO2 (post-fix): dropdown selection re-filters immediately",
          mUf["Q5_sizegroups_qty"] == 4 and mUf["Q5_cutplan_rows"] == 4)
    vmUfix.select_cutting_group(ALL_CUTTING_GROUPS)
    mUf2 = measure(vmUfix, [jU])
    row("FIXED: dropdown = ALL", mUf2)
    check("REPRO2 (post-fix): ALL conserves every piece (13)",
          mUf2["Q5_sizegroups_qty"] == 13 and mUf2["Q5_cutplan_rows"] == 13,
          "unlabelled glass now gets its own group instead of being filtered out")

    # ── DEFECT REPRO 3 — dropdown selection does not refresh SIZES ───────
    hdr("DEFECT REPRO 3 — SelectedCuttingGroup setter never calls RefreshDerived")
    jS, _ = benchmark_job("JOB-S")
    vmS = OptimizationViewModel(fix_dropdown_refresh=False)
    vmS.available_jobs = [jS]
    vmS.tick_job(jS, True)
    before = (len(vmS.size_groups), sum(g["piece_count"] for g in vmS.size_groups))
    vmS.select_cutting_group(composition_key_normalize(LOW_E))
    after = (len(vmS.size_groups), sum(g["piece_count"] for g in vmS.size_groups))
    print(f"  SizeGroups before picking a group: {before}")
    print(f"  SizeGroups after  picking a group: {after}")
    check("REPRO3 (pre-fix): SIZES tab does NOT change when the dropdown changes",
          before == after,
          "Sizes.cs:71-80 raises PropertyChanged only; Run() DOES honour the "
          "selection, so screen and engine disagree")

    jS2, _ = benchmark_job("JOB-S2")          # fresh instance — see REPRO2 note
    vmS2 = OptimizationViewModel(fix_dropdown_refresh=True)
    vmS2.available_jobs = [jS2]
    vmS2.tick_job(jS2, True)
    vmS2.select_cutting_group(composition_key_normalize(LOW_E))
    print(f"  FIXED: SizeGroups after picking low-e = "
          f"{[(g['composition'], g['piece_count']) for g in vmS2.size_groups]}")
    check("REPRO3 (post-fix): SIZES follows the dropdown immediately",
          len(vmS2.size_groups) == 1
          and vmS2.size_groups[0]["piece_count"] == 76)

    # ── DEFECT REPRO 4 — RC-009 force-IsSelected stomps operator intent ──
    hdr("DEFECT REPRO 4 — RC-009's forced layer.IsSelected=true (Jobs.cs:730)")
    jR1, _ = simple_job("JOB-R1", 1000, 1000, 5, CLEAR)
    jR2, _ = simple_job("JOB-R2", 2000, 500, 3, RAMLY, spec_name="6mm Obscure Clear Ramly FT glass")
    vmR = OptimizationViewModel(rc009_force_selected=True, per_job_reset=False)
    vmR.available_jobs = [jR1, jR2]
    vmR.tick_job(jR1, True); vmR.tick_job(jR2, True)
    row("both jobs ticked", measure(vmR, [jR1, jR2]))
    pane = vmR.pane_checklist[0]
    vmR.tick_pane(pane, False)                 # operator removes JOB-R1's pane
    row("operator unticks JOB-R1's pane", measure(vmR, [jR1, jR2]))
    check("REPRO4a: unticking a pane removes its glass", 
          measure(vmR, [jR1, jR2])["Q4_pieces_qty"] == 3)
    vmR.tick_job(jR2, False); vmR.tick_job(jR2, True)   # unrelated job tick
    mR = measure(vmR, [jR1, jR2])
    row("after an unrelated job re-tick", mR)
    check("REPRO4b (pre-fix): the unticked pane is silently RE-ADDED",
          mR["Q4_pieces_qty"] == 8,
          "RC-009 forces every pane of every selected job back to IsSelected=true "
          "on ANY job-selection change — the operator's removal is undone and "
          "quantity is added back without consent")

    # ── ROOT CAUSE A/B 1 — the pre-6c7224f1 build ────────────────────────
    hdr("ROOT CAUSE A/B 1 — build WITHOUT 6c7224f1 (RebuildSelectedJobBreakdown")
    print("              has no AutoRebuildPiecesFromSelectedJobs call)")
    jOld, qOld = benchmark_job("JOB-OLD")
    vmOld = OptimizationViewModel(auto_import_on_job_tick=False,
                                  rc009_force_selected=True,
                                  rc009_fallback=True,
                                  per_job_reset=False,
                                  fix_unlabelled_group=False,
                                  fix_dropdown_refresh=False)
    vmOld.available_jobs = [jOld]
    vmOld.tick_job(jOld, True)
    mOld = measure(vmOld, [jOld])
    row("pre-6c7224f1 build, job ticked", mOld)
    print(f"  PaneChecklist rows   : {len(vmOld.pane_checklist)}   ← 'specifications/middle pane populate'")
    print(f"  pane CountLabels     : {[l.count_label for l in vmOld.pane_checklist]}")
    print(f"  STOCKS rows          : {len(vmOld.stocks)}   ← the Step 4 pool also fills")
    print(f"  Pieces               : {len(vmOld.pieces)}   ← CUTTING LIST EMPTY")
    print(f"  SizeGroups           : {len(vmOld.size_groups)}   ← SIZES TAB EMPTY")
    print(f"  HasPieces            : {vmOld.has_pieces}  → the empty hint is what renders")
    print(f"  PieceSummary         : {vmOld.piece_summary!r}")
    check("AB1: pre-6c7224f1 build reproduces the user's EXACT symptom",
          len(vmOld.pane_checklist) == 4 and mOld["Q1_layers"] == 152
          and len(vmOld.pieces) == 0 and len(vmOld.size_groups) == 0
          and vmOld.has_pieces is False,
          "specs + panes + stocks populate, cutting list / SIZES stay empty")
    # and the operator's only way out was the button:
    vmOld.auto_rebuild_pieces_from_selected_jobs()   # == AddSelectedPanes()
    mOld2 = measure(vmOld, [jOld])
    row("same build, after 'Add selected panes'", mOld2)
    check("AB1: the button was the ONLY path to Pieces before 6c7224f1",
          mOld2["Q4_pieces_qty"] == 152)

    # ── ROOT CAUSE A/B 2 — RC-009's forced IsSelected, on vs off ─────────
    hdr("ROOT CAUSE A/B 2 — is RC-009's forced layer.IsSelected=true load-bearing?")
    for force in (False, True):
        jF2, _ = simple_job("JOB-F2", 800, 1200, 6)
        vmF2 = OptimizationViewModel(rc009_force_selected=force,
                                     per_job_reset=False)
        vmF2.available_jobs = [jF2]
        vmF2.tick_job(jF2, True)
        vmF2.set_all_panes_selected(False)        # operator presses "Clear"
        vmF2.tick_job(jF2, False)                 # untick the job
        vmF2.tick_job(jF2, True)                  # tick it again
        mF2 = measure(vmF2, [jF2])
        row(f"force IsSelected={str(force):<5} re-tick", mF2)
        if not force:
            check("AB2: WITHOUT RC-009 the re-tick leaves SIZES EMPTY",
                  mF2["Q4_pieces_qty"] == 0,
                  "pane layers live on the JobOptionVm, so 'Clear' unticks them "
                  "permanently; re-ticking the job rebuilds the same layers")
        else:
            check("AB2: WITH RC-009 the re-tick restores the sizes",
                  mF2["Q4_pieces_qty"] == 6)
    print("  ⇒ RC-009's force IS load-bearing for exactly one scenario:")
    print("     Clear (or untick a pane) → untick job → re-tick job.")
    print("     It is NOT load-bearing for a first tick: layers are constructed")
    print("     with IsSelected=true at Jobs.cs:417, so the first tick already")
    print("     worked from 6c7224f1 onward. It also stomps operator intent")
    print("     (REPRO4b), which the per-job reset below replaces.")

    # ── the replacement semantics ────────────────────────────────────────
    hdr("FIX SEMANTICS — reset a job's panes when THAT job is (re-)selected")
    jP1, _ = simple_job("JOB-P1", 1000, 1000, 5, CLEAR)
    jP2, _ = simple_job("JOB-P2", 2000, 500, 3, RAMLY,
                        spec_name="6mm Obscure Clear Ramly FT glass")
    vmP = OptimizationViewModel(rc009_force_selected=False, per_job_reset=True)
    vmP.available_jobs = [jP1, jP2]
    vmP.tick_job(jP1, True)
    vmP.tick_job(jP2, True)
    row("both ticked", measure(vmP, [jP1, jP2]))
    vmP.tick_pane(vmP.pane_checklist[0], False)     # operator removes P1's pane
    row("operator unticks JOB-P1 pane", measure(vmP, [jP1, jP2]))
    # The replacement is now INSIDE the harness's tick_job, mirroring
    # OnJobSelectionChanged -> ResetPaneTicksToDefault: re-selecting JOB-P2
    # resets only JOB-P2's panes, with no manual poking from the test.
    vmP.tick_job(jP2, False)
    vmP.tick_job(jP2, True)
    mP = measure(vmP, [jP1, jP2])
    row("JOB-P2 re-ticked (per-job reset)", mP)
    check("FIX: JOB-P1's operator-removed pane stays removed",
          mP["Q4_pieces_qty"] == 3,
          "quantity is never added back without consent")
    vmP.tick_job(jP1, False)
    vmP.tick_job(jP1, True)
    mP2 = measure(vmP, [jP1, jP2])
    row("JOB-P1 re-ticked (per-job reset)", mP2)
    check("FIX: re-selecting JOB-P1 restores its own panes", mP2["Q4_pieces_qty"] == 8)

    # ── summary ──────────────────────────────────────────────────────────
    hdr("SUMMARY")
    print(f"  PASS: {len(PASS)}    FAIL: {len(FAIL)}")
    for f in FAIL:
        print(f"    FAILED → {f}")
    return 1 if FAIL else 0


if __name__ == "__main__":
    sys.exit(main())
