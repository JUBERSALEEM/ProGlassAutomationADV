"use client";

import { useEffect, useMemo, useState } from "react";
import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { formatDateDDMMYYYY } from "@/lib/date-utils";
import { confirmedProformaHrefForJobOrder, deliveryNoteHrefForJobOrder } from "@/lib/job-order-links";
import {
  buildProductionRoutePreview,
  plannedStage,
  PRODUCTION_STAGE_DEFINITIONS,
  type PlannedProductionStage,
  type ProductionStageCode,
} from "@/lib/job-order-production";
import type { JobOrderRow } from "@/types/job-orders";

type Props = {
  row: JobOrderRow | null;
  authToken: string | null;
  productionTrackingAvailable: boolean;
  onClose: () => void;
  onChanged: () => void;
};

type ApiResponse = { ok: true; data: unknown } | { ok: false; error: { message: string } };

function stageBadge(status: string): "success" | "warning" | "neutral" {
  if (status === "Completed") return "success";
  if (status === "In Progress" || status === "In Production") return "warning";
  return "neutral";
}

export function JobOrderDetailModal({ row, authToken, productionTrackingAvailable, onClose, onChanged }: Props) {
  const routePreview = useMemo(
    () => row ? buildProductionRoutePreview(row.source_lines) : null,
    [row],
  );
  const [draftStages, setDraftStages] = useState<PlannedProductionStage[]>(() => routePreview?.stages ?? []);
  const [routeReviewed, setRouteReviewed] = useState(false);
  const [newStage, setNewStage] = useState<ProductionStageCode | "">("");
  const [busy, setBusy] = useState(false);
  const [savingStage, setSavingStage] = useState<string | null>(null);
  const [progressInputs, setProgressInputs] = useState<Record<string, string>>(() => Object.fromEntries(
    (row?.production_job_order?.stages ?? []).map((stage) => [stage.id, String(stage.completed_qty)]),
  ));
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!row) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !busy && !savingStage) onClose();
    };
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [row, busy, savingStage, onClose]);

  if (!row) return null;

  const values = [
    ["Proforma date", formatDateDDMMYYYY(row.order_date) || "—", true],
    ["Customer reference", row.customer_ref || "—", true],
    ["Company", row.company || "Unassigned", false],
    ["Salesman", row.salesman || "—", false],
    ["Prepared by", row.prepared_by || "—", false],
    ["Type of work", row.type_of_work || "—", true],
    ["Confirmed quantity", `${row.ordered_qty.toLocaleString()} finished units`, true],
    ["Confirmed area", `${row.ordered_sqm.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} m²`, true],
    ["Source rows grouped", row.record_count.toLocaleString(), true],
    ["Workbook", row.source_file?.split(/[\\/]/).pop() || "Unlabelled source", true],
  ] as const;
  const production = row.production_job_order;
  const existingCodes = new Set(draftStages.map((stage) => stage.stage_code));
  const requiredStageCodes = new Set(routePreview?.required_stage_codes ?? ["cutting"]);
  const canRelease = Boolean(
    productionTrackingAvailable && authToken && routeReviewed && !busy &&
    [...requiredStageCodes].every((code) => draftStages.some((stage) => stage.stage_code === code && stage.planned_qty > 0)) &&
    draftStages.every((stage) =>
      Number.isFinite(stage.planned_qty) && stage.planned_qty > 0 &&
      (stage.quantity_unit === "linear metres" || Number.isInteger(stage.planned_qty)),
    ),
  );

  const updatePlan = (code: ProductionStageCode, value: string) => {
    const qty = value === "" ? 0 : Number(value);
    setDraftStages((current) => current.map((stage) => stage.stage_code === code
      ? { ...stage, planned_qty: Number.isFinite(qty) && qty >= 0 ? Math.round(qty * 1000) / 1000 : 0 }
      : stage));
    setRouteReviewed(false);
  };

  const addPlanStage = () => {
    if (!newStage || existingCodes.has(newStage)) return;
    const suggested = newStage === "tempering"
      ? draftStages.find((stage) => stage.stage_code === "cutting")?.planned_qty ?? 0
      : newStage === "holes_cutouts" ? row.ordered_qty : 0;
    const next = [...draftStages, plannedStage(newStage, suggested)].sort((a, b) => a.stage_order - b.stage_order);
    setDraftStages(next);
    setNewStage("");
    setRouteReviewed(false);
  };

  const removePlanStage = (code: ProductionStageCode) => {
    if (requiredStageCodes.has(code)) return;
    setDraftStages((current) => current.filter((stage) => stage.stage_code !== code));
    setRouteReviewed(false);
  };

  const releaseJobOrder = async () => {
    if (!authToken || !canRelease) return;
    setBusy(true);
    setError(null);
    try {
      const response = await fetch("/api/job-orders/release", {
        method: "POST",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${authToken}` },
        body: JSON.stringify({
          pi_number: row.pi_number,
          source_file: row.source_file,
          record_ids: row.record_ids.map(String),
          operations: draftStages,
          route_confirmed: true,
        }),
        cache: "no-store",
      });
      const result = await response.json() as ApiResponse;
      if (!response.ok || !result.ok) throw new Error(result.ok ? "The production release failed." : result.error.message);
      onChanged();
    } catch (releaseError) {
      setError(releaseError instanceof Error ? releaseError.message : "Unable to release this PI to production.");
    } finally {
      setBusy(false);
    }
  };

  const saveProgress = async (
    stageId: string,
    operationId: string,
    plannedQty: number,
    quantityUnit: string,
  ) => {
    if (!authToken) return;
    const completedQty = Number(progressInputs[stageId]);
    if (!Number.isFinite(completedQty) || completedQty < 0 || completedQty > plannedQty) {
      setError(`Completed quantity must be between 0 and ${plannedQty.toLocaleString()}.`);
      return;
    }
    if (quantityUnit !== "linear metres" && !Number.isInteger(completedQty)) {
      setError("Pane, piece and finished-unit progress must be a whole number.");
      return;
    }
    if (Math.round(completedQty * 1000) / 1000 !== completedQty) {
      setError("Production progress supports up to three decimal places.");
      return;
    }
    setSavingStage(stageId);
    setError(null);
    try {
      const response = await fetch("/api/job-orders/progress", {
        method: "PATCH",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${authToken}` },
        body: JSON.stringify({
          job_order_id: production?.id,
          operation_id: operationId,
          completed_qty: completedQty,
        }),
        cache: "no-store",
      });
      const result = await response.json() as ApiResponse;
      if (!response.ok || !result.ok) throw new Error(result.ok ? "The progress update failed." : result.error.message);
      onChanged();
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "Unable to save stage progress.");
    } finally {
      setSavingStage(null);
    }
  };

  const stageChoices = PRODUCTION_STAGE_DEFINITIONS.filter((stage) => !existingCodes.has(stage.code));
  const completedStages = production?.stages.filter((stage) => stage.status === "Completed").length ?? 0;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink-950/85 p-3.5 animate-fade-in" onMouseDown={(event) => { if (event.target === event.currentTarget && !busy && !savingStage) onClose(); }}>
      <section role="dialog" aria-modal="true" aria-labelledby="job-order-detail-title" className="relative flex max-h-[92vh] w-full max-w-4xl flex-col overflow-hidden rounded-2xl border border-line-strong/80 bg-ink-900 shadow-raised">
        <div className="flex shrink-0 items-start justify-between gap-3.5 border-b border-line-strong/60 p-4 sm:p-5">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <span className="eyebrow text-brand-300">Production release · confirmed PI</span>
              <Badge tone="success">{row.confirmation_status}</Badge>
              {production && <Badge tone={stageBadge(production.status)}>{production.status}</Badge>}
            </div>
            <h2 id="job-order-detail-title" className="mt-2 truncate font-mono text-lg font-bold text-text-primary sm:text-xl">{row.pi_number}</h2>
            <p className="mt-1 truncate text-xs text-text-muted">{row.company || "Unassigned company"}{row.customer_ref ? ` · ${row.customer_ref}` : ""}</p>
          </div>
          <button type="button" onClick={onClose} disabled={busy || Boolean(savingStage)} aria-label="Close Job Order details" className="shrink-0 rounded-lg border border-line-strong p-1.5 text-text-faint transition-colors hover:bg-ink-850 hover:text-text-primary disabled:opacity-50">✕</button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-5">
          <div className="grid grid-cols-2 gap-x-5 gap-y-4 sm:grid-cols-3 lg:grid-cols-4">
            {values.map(([label, value, mono]) => (
              <div key={label} className="min-w-0">
                <span className="mb-1 block text-3xs font-semibold uppercase tracking-wider text-text-faint">{label}</span>
                <span className={`block break-words text-xs text-text-primary ${mono ? "font-mono" : ""}`}>{value}</span>
              </div>
            ))}
          </div>

          <div className="mt-5 rounded-2xl border border-brand-400/25 bg-brand-400/5 p-3.5">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h3 className="text-sm font-bold text-text-primary">Production routing & progress</h3>
                <p className="mt-1 text-2xs leading-relaxed text-text-muted">
                  Release records a separate website Job Order from this still-Confirmed PI. Stage quantities are reviewed by an operator and progress is saved to Supabase.
                </p>
              </div>
              {production && (
                <div className="text-right">
                  <div className="font-mono text-sm font-bold text-brand-200">{production.job_order_number}</div>
                  <div className="mt-0.5 text-3xs text-text-faint">{completedStages} of {production.stages.length} stages completed</div>
                </div>
              )}
            </div>

            {!production && (
              <>
                <div className="mt-3.5 rounded-xl border border-state-warning/30 bg-state-warning/10 p-3 text-2xs leading-relaxed text-state-warning">
                  <strong>Summary-based route — verify before release.</strong> Supabase public.records does not contain pane treatment or all processing charges. Pane counts are estimated from type and quantity; add missing stages and correct any quantities before confirming.
                  {routePreview?.build_types.length ? <p className="mt-1.5 font-semibold">Detected build families: {routePreview.build_types.join(", ")}</p> : null}
                  {!productionTrackingAvailable && <p className="mt-1.5 font-semibold">Release is unavailable until the live Supabase tracking schema and required source-data scope are ready.</p>}
                  {routePreview?.warnings.map((warning) => <p key={warning} className="mt-1.5">{warning}</p>)}
                </div>
                <div className="mt-3.5 overflow-x-auto rounded-xl border border-line-strong/60">
                  <table className="w-full min-w-[620px] text-left text-xs">
                    <thead className="bg-ink-950/80 text-3xs font-semibold uppercase tracking-wider text-text-faint">
                      <tr><th className="px-3 py-2.5">Stage</th><th className="px-3 py-2.5">Planned quantity</th><th className="px-3 py-2.5">Unit</th><th className="w-20 px-3 py-2.5 text-right">Action</th></tr>
                    </thead>
                    <tbody className="divide-y divide-line/40">
                      {draftStages.map((stage) => (
                        <tr key={stage.stage_code}>
                          <td className="px-3 py-2.5 font-semibold text-text-primary">
                            {stage.stage_order}. {stage.stage_label}
                            {requiredStageCodes.has(stage.stage_code) && <span className="ml-2 text-3xs font-medium uppercase tracking-wider text-brand-300">Required</span>}
                          </td>
                          <td className="px-3 py-2.5">
                            <input aria-label={`${stage.stage_label} planned quantity`} type="number" min="0" step={stage.quantity_unit === "linear metres" ? "0.001" : "1"} value={stage.planned_qty || ""} onChange={(event) => updatePlan(stage.stage_code, event.target.value)} className="w-32 rounded-lg border border-line-strong bg-ink-950 px-2.5 py-1.5 font-mono text-xs text-text-primary focus:border-brand-400 focus:outline-none" />
                          </td>
                          <td className="px-3 py-2.5 text-2xs text-text-muted">{stage.quantity_unit}</td>
                          <td className="px-3 py-2.5 text-right">
                            {!requiredStageCodes.has(stage.stage_code) && <button type="button" onClick={() => removePlanStage(stage.stage_code)} className="text-2xs text-text-faint underline hover:text-state-danger">Remove</button>}
                          </td>
                        </tr>
                      ))}
                      {draftStages.length === 0 && <tr><td colSpan={4} className="px-3 py-5 text-center text-2xs text-text-muted">No route could be inferred. Add Cutting, enter its pane quantity, then add the required downstream stages.</td></tr>}
                    </tbody>
                  </table>
                </div>
                <div className="mt-3 flex flex-wrap items-end gap-2">
                  <label className="text-3xs font-semibold uppercase tracking-wider text-text-faint">
                    Add process stage
                    <select value={newStage} onChange={(event) => setNewStage(event.target.value as ProductionStageCode | "")} className="mt-1 block min-w-52 rounded-lg border border-line-strong bg-ink-950 px-2.5 py-2 text-xs font-normal normal-case tracking-normal text-text-primary focus:border-brand-400 focus:outline-none">
                      <option value="">Choose stage…</option>
                      {stageChoices.map((stage) => <option key={stage.code} value={stage.code}>{stage.label} · {stage.quantityUnit}</option>)}
                    </select>
                  </label>
                  <button type="button" onClick={addPlanStage} disabled={!newStage} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-text-secondary hover:border-brand-400 hover:text-brand-200 disabled:opacity-40">Add stage</button>
                </div>
                <p className="mt-2 text-3xs text-text-faint">Pane, piece and finished-unit counts are whole numbers; linear metres may use 0.001 precision.</p>
                <label className="mt-3.5 flex items-start gap-2.5 rounded-xl border border-line-strong/70 bg-ink-950/50 p-3 text-2xs leading-relaxed text-text-secondary">
                  <input type="checkbox" checked={routeReviewed} onChange={(event) => setRouteReviewed(event.target.checked)} className="mt-0.5 accent-brand-400" />
                  <span>I reviewed the PI summary, build type, pane-based cutting quantity and required processing stages. I confirm this route for production.</span>
                </label>
              </>
            )}

            {production && (
              <div className="mt-3.5 overflow-x-auto rounded-xl border border-line-strong/60">
                <table className="w-full min-w-[680px] text-left text-xs">
                  <thead className="bg-ink-950/80 text-3xs font-semibold uppercase tracking-wider text-text-faint">
                    <tr><th className="px-3 py-2.5">Production stage</th><th className="px-3 py-2.5 text-right">Planned</th><th className="px-3 py-2.5">Completed</th><th className="px-3 py-2.5">Balance</th><th className="px-3 py-2.5">Status</th><th className="px-3 py-2.5 text-right">Save</th></tr>
                  </thead>
                  <tbody className="divide-y divide-line/40">
                    {production.stages.map((stage) => {
                      const current = Number(progressInputs[stage.id] ?? stage.completed_qty);
                      return (
                        <tr key={stage.id}>
                          <td className="px-3 py-2.5 font-semibold text-text-primary">{stage.stage_order}. {stage.stage_label}</td>
                          <td className="px-3 py-2.5 text-right font-mono">{stage.planned_qty.toLocaleString()} <span className="text-3xs text-text-faint">{stage.quantity_unit}</span></td>
                          <td className="px-3 py-2.5">
                            <input aria-label={`${stage.stage_label} completed quantity`} type="number" min="0" max={stage.planned_qty} step={stage.quantity_unit === "linear metres" ? "0.001" : "1"} value={progressInputs[stage.id] ?? String(stage.completed_qty)} onChange={(event) => setProgressInputs((currentInputs) => ({ ...currentInputs, [stage.id]: event.target.value }))} className="w-28 rounded-lg border border-line-strong bg-ink-950 px-2.5 py-1.5 font-mono text-xs text-text-primary focus:border-brand-400 focus:outline-none" />
                          </td>
                          <td className="px-3 py-2.5 font-mono text-text-muted">{Math.max(0, stage.planned_qty - (Number.isFinite(current) ? current : stage.completed_qty)).toLocaleString()} {stage.quantity_unit}</td>
                          <td className="px-3 py-2.5"><Badge tone={stageBadge(stage.status)}>{stage.status}</Badge></td>
                          <td className="px-3 py-2.5 text-right"><button type="button" onClick={() => void saveProgress(stage.id, stage.id, stage.planned_qty, stage.quantity_unit)} disabled={!authToken || Boolean(savingStage)} className="rounded-md border border-line-strong px-2.5 py-1 text-3xs font-semibold text-text-secondary hover:border-brand-400 hover:text-brand-200 disabled:opacity-40">{savingStage === stage.id ? "Saving…" : "Save"}</button></td>
                        </tr>
                      );
                    })}
                    {production.stages.length === 0 && <tr><td colSpan={6} className="px-3 py-5 text-center text-2xs text-text-muted">No production stages are attached to this release.</td></tr>}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {error && <div role="alert" className="mt-3.5 rounded-xl border border-state-danger/50 bg-state-danger/10 px-3.5 py-2.5 text-xs text-state-danger">{error}</div>}

          <div className="mt-4 flex flex-wrap items-center gap-2">
            <LocaleLink href={confirmedProformaHrefForJobOrder(row.pi_number, row.source_file)} className="inline-flex h-9 items-center justify-center rounded-xl border border-line-strong/80 bg-ink-850/80 px-3.5 text-xs font-semibold text-text-muted transition-colors hover:border-brand-400 hover:text-brand-200">
              Open Proforma Records
            </LocaleLink>
            {production?.status === "Completed" ? (
              <LocaleLink href={deliveryNoteHrefForJobOrder(row.pi_number, row.source_file)} className="inline-flex h-9 items-center justify-center rounded-xl border border-brand-400/40 bg-brand-400/10 px-3.5 text-xs font-semibold text-brand-200 transition-colors hover:bg-brand-400/20">
                Next: Delivery Note →
              </LocaleLink>
            ) : (
              <span className="inline-flex h-9 items-center rounded-xl border border-line-strong/50 px-3.5 text-2xs text-text-faint">Delivery hand-off follows production completion</span>
            )}
          </div>
        </div>

        <div className="flex shrink-0 flex-wrap items-center justify-between gap-3 border-t border-line-strong/60 bg-ink-950/50 px-5 py-3.5 sm:px-6">
          <span className="text-3xs text-text-faint">Source PI stays Confirmed and immutable in Excel Sync.</span>
          <div className="flex gap-2">
            {!production && (
              <button type="button" onClick={() => void releaseJobOrder()} disabled={!canRelease} className="inline-flex h-10 items-center justify-center rounded-xl bg-brand-400 px-4 text-xs font-bold text-accent-ink transition-colors hover:bg-brand-300 disabled:cursor-not-allowed disabled:opacity-40">
                {busy ? "Rechecking PI…" : "Review confirmed & release"}
              </button>
            )}
            <button type="button" onClick={onClose} disabled={busy || Boolean(savingStage)} className="inline-flex h-10 items-center justify-center rounded-xl border border-line-strong px-4 text-xs font-semibold text-text-muted hover:text-text-primary disabled:opacity-50">Close</button>
          </div>
        </div>
      </section>
    </div>
  );
}
