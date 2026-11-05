"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { InquiryAccessGate } from "@/components/inquiries/InquiryAccessGate";
import { ProformaBuilder } from "@/components/inquiries/proforma/ProformaBuilder";
import { ProformaOtherCharges } from "@/components/inquiries/proforma/ProformaOtherCharges";
import { ProformaDocument } from "@/components/inquiries/proforma/ProformaDocument";
import { ProformaSharePanel } from "@/components/inquiries/proforma/ProformaSharePanel";
import { draftDiffersFromSaved } from "@/lib/proforma-document";
import { EMPTY_RATE_SETTING, applyRatePlan, formatRate, planRates, settingsFromServer, settingsPayload, type RateSetting } from "@/lib/proforma-rates";
import { ProformaSpecification } from "@/components/inquiries/proforma/ProformaSpecification";
import { useOperatorSession } from "@/lib/useOperatorSession";
import {
  draftIssues,
  draftTotals,
  duplicateLine,
  emptyLine,
  formatMoney,
  formatSqm,
  fromSavedLine,
  groupTotals,
  groupsOf,
  moveLine,
  newIdempotencyKey,
  nextGroupNo,
  priceDraft,
  removeGroup,
  toLineInputs,
  type DraftLine,
} from "@/lib/proforma-draft";
import type { ProformaResponse, ProformaWorkflowSnapshot, ProformaWorkspaceData } from "@/types/proforma";
import { applyAdvancedStep6Suggestions, applyStep4BuildToStep5, defaultStep4Build, emptyStep6, estimateStep4Rate, generatedStep4SpecName, restoreStep5Ownership, snapshotStep5Ownership, totalStep6, type Step4Build, type Step6Draft } from "@/lib/proforma-workflow";

function stableJson(value: unknown): string {
  const normalize = (item: unknown): unknown => {
    if (Array.isArray(item)) return item.map(normalize);
    if (item && typeof item === "object") {
      return Object.fromEntries(Object.entries(item).sort(([left], [right]) => left.localeCompare(right)).map(([key, nested]) => [key, normalize(nested)]));
    }
    return item;
  };
  return JSON.stringify(normalize(value));
}

/**
 * The proforma workspace for one inquiry.
 *
 * The staff-only workflow mirrors WPF Steps 4–6: build/estimate, cut list, and
 * Other Charges. Step 4 cost and margin inputs plus Step 6 charges are stored in
 * the staff revision snapshot and are not part of customer technical intake.
 *
 * Saving is a revision, not an overwrite. The client sends the revision it
 * loaded plus a fresh idempotency key, and the database refuses a stale or
 * duplicated write.
 */
export function ProformaWorkspace({ inquiryId }: { inquiryId: string }) {
  const { checking, userEmail, authToken, isAuthRequired, requireAuth } = useOperatorSession();
  const [data, setData] = useState<ProformaWorkspaceData | null>(null);
  const [lines, setLines] = useState<DraftLine[]>([]);
  const [activeGroup, setActiveGroup] = useState(1);
  const [step4Builds, setStep4Builds] = useState<Record<number, Step4Build>>({});
  const [step6, setStep6] = useState<Step6Draft>(emptyStep6());
  const [workflowStep, setWorkflowStep] = useState<4 | 5 | 6>(4);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  /** Only the load failure decides what the page is: a save failure must not replace it. */
  const [loadFailure, setLoadFailure] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [issued, setIssues] = useState<string[]>([]);
  /** Which half of the workspace is on screen: the working grid or the saved document. */
  const [view, setView] = useState<"specification" | "document">("specification");
  /**
   * Step 5's per specification rate entry. The two inputs are stored with the
   * revision that saved them and loaded back with it, so closing and reopening the
   * workspace shows the figures the revision was priced with. The money that gets
   * charged is still the rate each line carries.
   */
  const [rateSettings, setRateSettings] = useState<Record<number, RateSetting>>({});
  const saveKey = useRef<string | null>(null);

  const [refresh, setRefresh] = useState(0);

  /** Reload the workspace from the server, keeping the button and the effect in step. */
  const reload = () => {
    setLoading(true);
    setRefresh((value) => value + 1);
  };

  useEffect(() => {
    if (checking || (isAuthRequired && !authToken)) return;
    const controller = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/inquiries/${inquiryId}/proforma`, {
          headers: { Authorization: `Bearer ${authToken}` },
          cache: "no-store",
          signal: controller.signal,
        });
        if (response.status === 401) {
          requireAuth();
          return;
        }
        const body = (await response.json().catch(() => null)) as ProformaResponse | null;
        if (!body || !body.ok) {
          setError(body && !body.ok ? body.error.message : "The proforma service is not available in this deployment.");
        setLoadFailure(body && !body.ok ? body.error.code : "INTERNAL_ERROR");
          return;
        }
        setData(body.data);
        setRateSettings(settingsFromServer(body.data.groups));
        const restored = restoreStep5Ownership(body.data.lines.map(fromSavedLine), body.data.workflow_snapshot?.step5_ownership);
        setLines(restored.length ? restored : [emptyLine(1)]);
        const snapshot = body.data.workflow_snapshot;
        setStep4Builds(snapshot?.step4_groups ?? {});
        setStep6(snapshot?.step6 ?? emptyStep6());
        setActiveGroup(restored.length ? Math.min(...restored.map((line) => line.groupNo)) : 1);
        setWorkflowStep(restored.length ? 5 : 4);
      } catch (reason) {
        if (reason instanceof Error && reason.name === "AbortError") return;
        setError("The proforma service could not be reached. Check the connection and try again.");
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    })();
    return () => controller.abort();
  }, [authToken, checking, inquiryId, isAuthRequired, refresh, requireAuth]);

  const priced = useMemo(() => priceDraft(lines), [lines]);
  const totals = useMemo(() => draftTotals(priced), [priced]);
  const perGroup = useMemo(() => groupTotals(priced), [priced]);
  const groups = useMemo(() => groupsOf(lines), [lines]);
  const otherChargesTotal = useMemo(() => totalStep6(step6, lines, step4Builds), [step6, lines, step4Builds]);
  const workflowSnapshotNow: ProformaWorkflowSnapshot = useMemo(() => ({ step4_groups: step4Builds, step6, step5_ownership: snapshotStep5Ownership(lines) }), [step4Builds, step6, lines]);
  const workflowDirty = data?.workflow_snapshot
    ? stableJson(workflowSnapshotNow) !== stableJson(data.workflow_snapshot)
    : Object.keys(step4Builds).length > 0 || stableJson(step6) !== stableJson(emptyStep6());
  const rateSettingsDirty = stableJson(settingsPayload(rateSettings)) !== stableJson(settingsPayload(settingsFromServer(data?.groups ?? [])));
  const currency = data?.proforma?.currency || "AED";
  const revision = data?.proforma?.revision ?? 0;

  const lineForGroup = (groupNo: number): DraftLine => {
    const line = emptyLine(groupNo);
    const build = step4Builds[groupNo];
    if (!build) return line;
    const rate = estimateStep4Rate(build).rate;
    return { ...line, glassRef: generatedStep4SpecName(build), rate: rate > 0 ? formatRate(rate) : "", glassRefOwner: "step4", rateOwner: "step4" };
  };

  const edit = (key: string, patch: Partial<DraftLine>) => {
    setNotice(null);
    setIssues([]);
    setLines((current) => {
      let changedGroup: number | null = null;
      const updated = current.map((line) => {
        if (line.key !== key) return line;
        if (["w1", "h1", "w2", "h2", "qty"].some((field) => Object.prototype.hasOwnProperty.call(patch, field))) changedGroup = line.groupNo;
        return {
          ...line,
          ...patch,
          ...(Object.prototype.hasOwnProperty.call(patch, "glassRef") ? { glassRefOwner: "manual" as const } : {}),
          ...(Object.prototype.hasOwnProperty.call(patch, "rate") ? { rateOwner: "manual" as const } : {}),
        };
      });
      if (changedGroup === null) return updated;
      const setting = rateSettings[changedGroup] ?? EMPTY_RATE_SETTING;
      const plan = planRates(priceDraft(updated), changedGroup, setting.base, setting.percent);
      return plan ? applyRatePlan(updated, plan) : updated;
    });
  };
  const addLine = (groupNo: number) => {
    setIssues([]);
    setLines((current) => [...current, lineForGroup(groupNo)]);
    setActiveGroup(groupNo);
  };
  const removeLine = (key: string) => {
    setIssues([]);
    setLines((current) => {
      const next = current.filter((line) => line.key !== key);
      return next.length ? next : [emptyLine(1)];
    });
  };
  const duplicateRow = (key: string) => {
    setIssues([]);
    setLines((current) => duplicateLine(current, key));
    setNotice("Copied the row into the same specification.");
  };
  const moveRow = (key: string, direction: -1 | 1) => {
    setIssues([]);
    setLines((current) => moveLine(current, key, direction));
  };

  const addGroup = () => {
    setIssues([]);
    const groupNo = nextGroupNo(lines);
    setLines((current) => [...current, emptyLine(groupNo)]);
    setActiveGroup(groupNo);
  };
  const applyStep4Build = (groupNo: number, build: Step4Build) => {
    setIssues([]);
    const builds = { ...step4Builds, [groupNo]: build };
    const estimate = estimateStep4Rate(build);
    const currentSetting = rateSettings[groupNo] ?? EMPTY_RATE_SETTING;
    const handoff = applyStep4BuildToStep5(lines, groupNo, build, currentSetting.percent);
    setStep4Builds(builds);
    setRateSettings((current) => ({ ...current, [groupNo]: { ...currentSetting, base: handoff.baseRate } }));
    setLines(handoff.lines);
    setStep6((current) => applyAdvancedStep6Suggestions(current, builds));
    setNotice(`Applied Step 4 specification “${handoff.specName}” and estimated ${formatMoney(estimate.rate, currency)} / m² to the Step 5 group. Existing manual references and rate overrides were retained.`);
  };
  const dropGroup = (groupNo: number) => {
    const next = removeGroup(lines, groupNo);
    setLines(next.length ? next : [emptyLine(1)]);
    setActiveGroup(groupsOf(next.length ? next : [emptyLine(1)])[0] || 1);
    const shiftMap = <T,>(current: Record<number, T>): Record<number, T> => Object.fromEntries(Object.entries(current).flatMap(([key, value]) => {
      const source = Number(key);
      if (source === groupNo) return [];
      return [[source > groupNo ? source - 1 : source, value]];
    }));
    setStep4Builds((current) => shiftMap(current));
    setRateSettings((current) => shiftMap(current));
    const shiftAutoWastageId = (id: string): string | null => {
      const match = /^auto-w-(\d+)-(outer|inner|third)$/.exec(id);
      if (!match) return id;
      const source = Number(match[1]);
      return source === groupNo ? null : `auto-w-${source > groupNo ? source - 1 : source}-${match[2]}`;
    };
    setStep6((current) => ({
      ...current,
      charges: current.charges.map((charge) => ({ ...charge, groupNos: charge.groupNos.filter((no) => no !== groupNo).map((no) => no > groupNo ? no - 1 : no) })),
      wastages: current.wastages.flatMap((row) => {
        const id = shiftAutoWastageId(row.id);
        if (!id) return [];
        return [{ ...row, id, groupNos: row.groupNos.filter((no) => no !== groupNo).map((no) => no > groupNo ? no - 1 : no) }];
      }),
      manualWastageIds: (current.manualWastageIds ?? []).flatMap((id) => { const shifted = shiftAutoWastageId(id); return shifted ? [shifted] : []; }),
    }));
  };

  const changeRate = (groupNo: number, patch: Partial<RateSetting>) => {
    setNotice(null);
    setRateSettings((current) => ({
      ...current,
      [groupNo]: { ...(current[groupNo] ?? EMPTY_RATE_SETTING), ...patch },
    }));
  };

  /**
   * Write a specification's base and oversize rates into its rows.
   *
   * This is the Step 5 behaviour the desktop applies when a row's area changes:
   * every row takes the base rate, and the rows at or above the threshold take the
   * surcharged rate. The figures the operator can see are the figures saved.
   */
  const applyRates = (groupNo: number) => {
    const setting = rateSettings[groupNo] ?? EMPTY_RATE_SETTING;
    const plan = planRates(priced, groupNo, setting.base, setting.percent);
    if (!plan) {
      setIssues([`Specification ${groupNo}: enter a base rate before applying rates.`]);
      return;
    }
    setIssues([]);
    setLines((current) => applyRatePlan(current, plan));
    const surcharged = plan.surchargedCount;
    setNotice(
      `Applied ${formatMoney(plan.baseRate, currency)} per m² to specification ${groupNo}` +
        (plan.percent > 0
          ? `, with ${formatMoney(plan.surchargeRate, currency)} on ${surcharged} oversize row${surcharged === 1 ? "" : "s"}.`
          : "."),
    );
  };

  const save = async () => {
    if (!authToken || !data) return;
    const problems = draftIssues(lines);
    setIssues(problems);
    if (problems.length) return;
    setSaving(true);
    setError(null);
    setNotice(null);
    const key = saveKey.current || newIdempotencyKey();
    saveKey.current = key;
    const workflowSnapshot: ProformaWorkflowSnapshot = {
      step4_groups: step4Builds,
      step6,
      step5_ownership: snapshotStep5Ownership(lines),
    };
    try {
      const response = await fetch(`/api/inquiries/${inquiryId}/proforma`, {
        method: "POST",
        headers: { Authorization: `Bearer ${authToken}`, "Content-Type": "application/json", "Idempotency-Key": key },
        body: JSON.stringify({
          expected_revision: revision,
          currency,
          lines: toLineInputs(lines),
          groups: settingsPayload(rateSettings),
          workflow_snapshot: workflowSnapshot,
        }),
      });
      const body = (await response.json().catch(() => null)) as ProformaResponse | null;
      if (response.status === 401) {
        requireAuth();
        return;
      }
      if (response.status === 409) {
        saveKey.current = null;
        setError("This proforma changed since it was loaded. Reload to see the saved revision, then apply your change again.");
        return;
      }
      if (!body || !body.ok) {
        setError(body && !body.ok ? body.error.message : "The proforma could not be saved.");
        return;
      }
      saveKey.current = null;
      setIssues([]);
      setData(body.data);
      setRateSettings(settingsFromServer(body.data.groups));
      setLines(restoreStep5Ownership(body.data.lines.map(fromSavedLine), workflowSnapshot.step5_ownership));
      if (body.data.workflow_snapshot) {
        setStep4Builds(body.data.workflow_snapshot.step4_groups);
        setStep6(body.data.workflow_snapshot.step6);
      }
      setNotice(
        `Saved revision ${body.data.proforma?.revision ?? 1} of ${body.data.proforma?.proforma_number ?? "the draft"}.` +
          (body.data.groups_persisted
            ? " Specification base rates and surcharges are stored with this revision."
            : " This deployment has no specification rate storage, so the base rates and surcharges were not saved.") +
          (body.data.workflow_snapshot_persisted
            ? " Step 4 builds, Step 5 ownership and Step 6 charges are stored with this revision."
            : " Step 4/6 snapshot storage is not available: apply website Phase C12 before relying on save/reopen fidelity."),
      );
    } catch {
      setError("The save did not reach the server. Try again, the same save will not be duplicated.");
    } finally {
      setSaving(false);
    }
  };

  if (checking || loading) {
    return (
      <main className="min-h-[70vh] bg-ink-950 px-4 py-16 text-text-muted">
        <div role="status" className="mx-auto max-w-md text-center text-xs">Loading the proforma workspace</div>
      </main>
    );
  }
  if (isAuthRequired || loadFailure === "FORBIDDEN") {
    return <InquiryAccessGate redirectPath={`/inquiries/${inquiryId}/proforma`} />;
  }

  if (!data && loadFailure === "NOT_FOUND") {
    return (
      <main className="min-h-[70vh] bg-ink-950 px-4 py-16 text-text-primary">
        <section className="mx-auto w-full max-w-lg rounded-panel border border-line-strong/80 bg-ink-900 card-pad" aria-labelledby="proforma-not-found">
          <h1 id="proforma-not-found" className="text-xl font-bold tracking-tight">Inquiry not found</h1>
          <p className="mt-2 text-xs leading-relaxed text-text-muted">
            No published inquiry has this reference. It may have been removed by a later desktop publication, or the
            link may be from an old page. A proforma always belongs to an inquiry, so start from the register.
          </p>
          <div className="mt-5 flex flex-wrap gap-2">
            <LocaleLink href="/inquiries/register" className="rounded-xl bg-brand-400 px-4 py-2 text-sm font-semibold text-accent-ink">
              Back to the register
            </LocaleLink>
            <LocaleLink href="/inquiries/customer-submissions" className="rounded-xl border border-line-strong px-4 py-2 text-sm font-semibold text-text-muted">
              Customer submissions
            </LocaleLink>
          </div>
        </section>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <header className="border-b border-line-strong/60 bg-ink-950/80">
        <div className="shell flex flex-wrap items-center justify-between gap-x-6 gap-y-3 py-4">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2 text-3xs text-text-faint">
              <LocaleLink href="/inquiries/register" className="hover:text-brand-200 hover:underline">Inquiry register</LocaleLink>
              <span aria-hidden="true">/</span>
              <LocaleLink href={`/inquiries/${inquiryId}`} className="hover:text-brand-200 hover:underline">
                {data?.inquiry.inquiry_number || "Inquiry"}
              </LocaleLink>
              <span aria-hidden="true">/</span>
              <span>Proforma invoice</span>
            </div>
            <div className="mt-1 flex flex-wrap items-baseline gap-x-3 gap-y-1">
              <h1 className="text-xl font-bold tracking-tight sm:text-2xl">
                {data?.proforma?.proforma_number || "New proforma draft"}
              </h1>
              {data?.proforma ? (
                <span className="rounded-full border border-line-strong/70 px-2 py-0.5 text-3xs font-semibold uppercase tracking-wider text-text-muted">
                  {data.proforma.status}, revision {data.proforma.revision}
                </span>
              ) : null}
            </div>
            <p className="mt-1 max-w-2xl text-xs leading-relaxed text-text-muted">
              {data?.inquiry.customer_name || "Customer not recorded"}
              {data?.inquiry.project_name ? `, ${data.inquiry.project_name}` : ""}. Priced from panel dimensions and the approved rate, with the half square metre floor and the quantity applied per line.
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {userEmail ? <span className="hidden max-w-[14rem] truncate text-3xs text-text-faint sm:inline">{userEmail}</span> : null}
            <div role="group" aria-label="Proforma view" className="flex overflow-hidden rounded-xl border border-line-strong">
              <button
                type="button"
                aria-pressed={view === "specification"}
                onClick={() => setView("specification")}
                className={`px-3 py-2 text-xs font-semibold ${view === "specification" ? "bg-ink-800 text-text-primary" : "text-text-muted hover:text-text-primary"}`}
              >
                Specification
              </button>
              <button
                type="button"
                aria-pressed={view === "document"}
                onClick={() => setView("document")}
                disabled={!data?.proforma}
                title={data?.proforma ? `Open the saved revision ${data.proforma.revision}` : "Save a revision before a document exists"}
                className={`px-3 py-2 text-xs font-semibold disabled:cursor-not-allowed disabled:opacity-50 ${view === "document" ? "bg-ink-800 text-text-primary" : "text-text-muted hover:text-text-primary"}`}
              >
                Document
              </button>
            </div>
            <button
              type="button"
              onClick={() => void save()}
              disabled={saving || !data?.installed}
              className="rounded-xl bg-brand-400 px-4 py-2 text-xs font-bold text-accent-ink hover:bg-brand-300 disabled:opacity-60"
            >
              {saving ? "Saving" : revision ? `Save revision ${revision + 1}` : "Save draft"}
            </button>
            <LocaleLink
              href={`/inquiries/${inquiryId}`}
              className="rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted hover:text-text-primary"
            >
              Inquiry details
            </LocaleLink>
          </div>
        </div>
      </header>

      <div className="shell mt-4 space-y-3">
        <ProformaSharePanel inquiryId={inquiryId} authToken={authToken} />
        {error ? (
          <section role="alert" className="rounded-2xl border border-state-danger/45 bg-state-danger/10 p-3">
            <p className="text-xs text-state-danger">{error}</p>
            <div className="mt-2 flex gap-2">
              <button type="button" onClick={reload} className="rounded-lg border border-state-danger/40 px-3 py-1.5 text-xs font-semibold text-state-danger">
                Reload
              </button>
              <button type="button" onClick={() => setError(null)} className="rounded-lg border border-line-strong px-3 py-1.5 text-xs font-semibold text-text-muted">
                Dismiss
              </button>
            </div>
          </section>
        ) : null}

        {notice ? (
          <p role="status" className="rounded-2xl border border-state-success/40 bg-state-success/10 px-3 py-2 text-xs text-state-success">{notice}</p>
        ) : null}
        {issued.length > 0 ? <section role="alert" className="rounded-2xl border border-state-warning/40 bg-state-warning/10 p-3"><h2 className="text-xs font-bold text-state-warning">The proforma needs attention before it can be saved</h2><ul className="mt-1 list-disc space-y-1 pl-5 text-xs text-text-secondary">{issued.map((issue) => <li key={issue}>{issue}</li>)}</ul></section> : null}

        {data && !data.installed ? (
          <section className="rounded-2xl border border-state-warning/40 bg-state-warning/10 p-4">
            <h2 className="text-sm font-bold text-state-warning">Proforma storage is not installed here</h2>
            <p className="mt-1 text-xs leading-relaxed text-text-secondary">
              This deployment has no proforma tables, so nothing can be resumed or saved. The Phase 15 migration must be applied before this workspace can hold a draft.
            </p>
          </section>
        ) : null}

        {data?.proforma && !data.workflow_snapshot ? (
          <section role="status" className="rounded-2xl border border-state-warning/40 bg-state-warning/10 p-3">
            <h2 className="text-xs font-bold text-state-warning">This saved revision has no Step 4/6 snapshot</h2>
            <p className="mt-1 text-xs leading-relaxed text-text-secondary">Its Step 5 lines remain intact, but this revision predates structured builder/Other Charges persistence or Phase C12 is not installed. Existing references and rates are treated as manual; do not apply a default build to an old group unless you intend to replace its generated data.</p>
          </section>
        ) : null}

        {data && data.installed && data.inquiry.proforma_number && !data.proforma ? (
          <section className="rounded-2xl border border-line-strong/70 bg-ink-900/80 p-3">
            <h2 className="text-xs font-bold text-text-primary">The desktop holds a proforma for this inquiry</h2>
            <p className="mt-1 text-xs leading-relaxed text-text-secondary">
              {data.inquiry.proforma_number} is recorded by the desktop and is not published to this website, so it cannot be opened or edited here. A draft saved on this page is a separate website document with its own PI-WEB number, and the two are never merged automatically.
            </p>
          </section>
        ) : null}

        {data && data.installed && view === "document" && data.proforma ? (
          <ProformaDocument
            proforma={data.proforma}
            inquiry={data.inquiry}
            lines={data.lines}
            otherChargesTotal={data.workflow_snapshot ? totalStep6(data.workflow_snapshot.step6, data.lines.map(fromSavedLine), data.workflow_snapshot.step4_groups) : undefined}
            hasUnsavedChanges={draftDiffersFromSaved(toLineInputs(lines), data.lines) || workflowDirty || rateSettingsDirty}
            onClose={() => setView("specification")}
          />
        ) : null}

        {data && data.installed && view === "specification" ? (
          <div className="space-y-3">
            <nav aria-label="Proforma workflow steps" className="flex flex-wrap gap-2 rounded-2xl border border-line-strong/60 bg-ink-900/70 p-2">
              {([[4, "Step 4 · Build & estimate"], [5, "Step 5 · Cut list"], [6, "Step 6 · Other Charges"]] as const).map(([step, label]) => <button key={step} type="button" aria-current={workflowStep === step ? "step" : undefined} onClick={() => setWorkflowStep(step)} className={`rounded-xl px-3 py-2 text-xs font-semibold ${workflowStep === step ? "bg-brand-400 text-accent-ink" : "border border-line-strong text-text-muted hover:text-text-primary"}`}>{label}</button>)}
            </nav>

            {workflowStep === 4 ? <ProformaBuilder
              groupNo={activeGroup}
              build={step4Builds[activeGroup] ?? defaultStep4Build()}
              onBuildChange={(build) => setStep4Builds((current) => ({ ...current, [activeGroup]: build }))}
              onApply={applyStep4Build}
              currency={currency}
              saved={{ revision, groups: data.lines.length ? groupTotals(priceDraft(data.lines.map(fromSavedLine))) : [] }}
              onReuse={(savedGroup) => {
                const source = data.lines.filter((line) => line.group_no === savedGroup);
                const target = nextGroupNo(lines);
                setLines((current) => [...current, ...source.map((line) => ({ ...fromSavedLine(line), groupNo: target }))]);
                const savedBuild = data.workflow_snapshot?.step4_groups[savedGroup];
                const savedSetting = settingsFromServer(data.groups)[savedGroup];
                if (savedBuild) {
                  const nextBuilds = { ...step4Builds, [target]: savedBuild };
                  setStep4Builds(nextBuilds);
                  setStep6((charges) => applyAdvancedStep6Suggestions(charges, nextBuilds));
                }
                if (savedSetting) setRateSettings((current) => ({ ...current, [target]: { base: String(savedSetting.base), percent: String(savedSetting.percent) } }));
                setActiveGroup(target);
                setNotice(`Reused specification ${savedGroup} as specification ${target}.`);
              }}
              onReplace={(savedGroup, target) => {
                const source = data.lines.filter((line) => line.group_no === savedGroup);
                setLines((current) => [...current.filter((line) => line.groupNo !== target), ...source.map((line) => ({ ...fromSavedLine(line), groupNo: target }))]);
                const savedBuild = data.workflow_snapshot?.step4_groups[savedGroup];
                const nextBuilds = savedBuild ? { ...step4Builds, [target]: savedBuild } : Object.fromEntries(Object.entries(step4Builds).filter(([key]) => Number(key) !== target));
                setStep4Builds(nextBuilds);
                if (savedBuild) setStep6((charges) => applyAdvancedStep6Suggestions(charges, nextBuilds));
                const savedSetting = settingsFromServer(data.groups)[savedGroup];
                setRateSettings((current) => {
                  const next = { ...current };
                  if (savedSetting) next[target] = { base: String(savedSetting.base), percent: String(savedSetting.percent) };
                  else delete next[target];
                  return next;
                });
                setActiveGroup(target);
                setNotice(`Specification ${target} now holds the saved rows of specification ${savedGroup}.`);
              }}
            /> : null}

            {workflowStep === 5 ? <ProformaSpecification
              priced={priced}
              groups={groups}
              perGroup={perGroup}
              activeGroup={activeGroup}
              currency={currency}
              totals={totals}
              rateSettings={rateSettings}
              builds={step4Builds}
              onRateChange={changeRate}
              onApplyRates={applyRates}
              onEdit={edit}
              onAddLine={addLine}
              onRemoveLine={removeLine}
              onDuplicateLine={duplicateRow}
              onMoveLine={moveRow}
              onAddGroup={addGroup}
              onRemoveGroup={dropGroup}
              onFocusGroup={setActiveGroup}
            /> : null}

            {workflowStep === 6 ? <ProformaOtherCharges value={step6} lines={lines} builds={step4Builds} groupNos={groups} currency={currency} onChange={(next) => setStep6(next)} /> : null}
          </div>
        ) : null}

        {data ? (
          <section className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-line-strong/60 bg-ink-900/70 px-4 py-3">
            <p className="text-3xs text-text-faint">
              {totals.lines} line{totals.lines === 1 ? "" : "s"}, {formatSqm(totals.sqm)} sqm, glass subtotal {formatMoney(totals.value, currency)}, Other Charges {formatMoney(otherChargesTotal, currency)} (separate)
              {revision ? `, saved revision ${revision} at ${data.proforma?.updated_at ? new Date(data.proforma.updated_at).toLocaleString() : "an unknown time"}` : ", not saved yet"}
            </p>
            <p className="text-3xs text-text-faint">Values are recomputed by the database on save.</p>
          </section>
        ) : null}
      </div>
    </main>
  );
}
