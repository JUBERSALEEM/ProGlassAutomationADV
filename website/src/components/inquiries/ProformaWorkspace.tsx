"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { InquiryAccessGate } from "@/components/inquiries/InquiryAccessGate";
import { ProformaBuilder } from "@/components/inquiries/proforma/ProformaBuilder";
import { ProformaSpecification } from "@/components/inquiries/proforma/ProformaSpecification";
import { useOperatorSession } from "@/lib/useOperatorSession";
import {
  draftIssues,
  draftTotals,
  emptyLine,
  formatMoney,
  formatSqm,
  fromSavedLine,
  groupTotals,
  groupsOf,
  newIdempotencyKey,
  nextGroupNo,
  priceDraft,
  removeGroup,
  toLineInputs,
  type DraftLine,
} from "@/lib/proforma-draft";
import type { ProformaResponse, ProformaWorkspaceData } from "@/types/proforma";

/**
 * The proforma workspace for one inquiry.
 *
 * Left: the specification, numbered by group, every figure derived from the
 * panel dimensions and the approved rate. Right: the Step 4 builder and the
 * specifications the last save recorded. No cost, sheet purchase rate, margin,
 * profit figure or optimiser control is imported anywhere in this tree.
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
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [issued, setIssues] = useState<string[]>([]);
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
          return;
        }
        setData(body.data);
        const restored = body.data.lines.map(fromSavedLine);
        setLines(restored.length ? restored : [emptyLine(1)]);
        setActiveGroup(restored.length ? Math.min(...restored.map((line) => line.groupNo)) : 1);
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
  const currency = data?.proforma?.currency || "AED";
  const revision = data?.proforma?.revision ?? 0;

  const edit = (key: string, patch: Partial<DraftLine>) => {
    setNotice(null);
    setLines((current) => current.map((line) => (line.key === key ? { ...line, ...patch } : line)));
  };
  const addLine = (groupNo: number) => {
    setLines((current) => [...current, emptyLine(groupNo)]);
    setActiveGroup(groupNo);
  };
  const removeLine = (key: string) => {
    setLines((current) => {
      const next = current.filter((line) => line.key !== key);
      return next.length ? next : [emptyLine(1)];
    });
  };
  const addGroup = () => {
    const groupNo = nextGroupNo(lines);
    setLines((current) => [...current, emptyLine(groupNo)]);
    setActiveGroup(groupNo);
  };
  const dropGroup = (groupNo: number) => {
    const next = removeGroup(lines, groupNo);
    setLines(next.length ? next : [emptyLine(1)]);
    setActiveGroup(groupsOf(next.length ? next : [emptyLine(1)])[0] || 1);
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
    try {
      const response = await fetch(`/api/inquiries/${inquiryId}/proforma`, {
        method: "POST",
        headers: { Authorization: `Bearer ${authToken}`, "Content-Type": "application/json", "Idempotency-Key": key },
        body: JSON.stringify({ expected_revision: revision, currency, lines: toLineInputs(lines) }),
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
      setData(body.data);
      setLines(body.data.lines.map(fromSavedLine));
      setNotice(`Saved revision ${body.data.proforma?.revision ?? 1} of ${body.data.proforma?.proforma_number ?? "the draft"}.`);
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
  if (isAuthRequired) return <InquiryAccessGate redirectPath={`/inquiries/${inquiryId}/proforma`} />;

  return (
    <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <header className="border-b border-line-strong/60 bg-ink-950/80">
        <div className="shell flex flex-wrap items-center justify-between gap-x-6 gap-y-3 py-4">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2 text-3xs text-text-faint">
              <LocaleLink href="/inquiries" className="hover:text-brand-200 hover:underline">Inquiry inbox</LocaleLink>
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

        {data && !data.installed ? (
          <section className="rounded-2xl border border-state-warning/40 bg-state-warning/10 p-4">
            <h2 className="text-sm font-bold text-state-warning">Proforma storage is not installed here</h2>
            <p className="mt-1 text-xs leading-relaxed text-text-secondary">
              This deployment has no proforma tables, so nothing can be resumed or saved. The Phase 15 migration must be applied before this workspace can hold a draft.
            </p>
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

        {data && data.installed ? (
          <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_22rem] xl:grid-cols-[minmax(0,1fr)_26rem]">
            <ProformaSpecification
              priced={priced}
              groups={groups}
              perGroup={perGroup}
              activeGroup={activeGroup}
              currency={currency}
              totals={totals}
              onEdit={edit}
              onAddLine={addLine}
              onRemoveLine={removeLine}
              onAddGroup={addGroup}
              onRemoveGroup={dropGroup}
              onFocusGroup={setActiveGroup}
            />
            <ProformaBuilder
              activeGroup={activeGroup}
              groups={groups}
              currency={currency}
              saved={{
                revision,
                groups: data.lines.length
                  ? groupTotals(priceDraft(data.lines.map(fromSavedLine)))
                  : [],
              }}
              issues={issued}
              onAssign={setActiveGroup}
              onAddLine={(groupNo, line) => {
                setLines((current) => [...current, { ...line, groupNo }]);
                setActiveGroup(groupNo);
                setNotice(`Added a line to specification ${groupNo}.`);
              }}
              onReuse={(savedGroup) => {
                const source = data.lines.filter((line) => line.group_no === savedGroup);
                const target = nextGroupNo(lines);
                setLines((current) => [
                  ...current,
                  ...source.map((line) => ({ ...fromSavedLine(line), groupNo: target })),
                ]);
                setActiveGroup(target);
                setNotice(`Reused specification ${savedGroup} as specification ${target}.`);
              }}
              onReplace={(savedGroup, target) => {
                const source = data.lines.filter((line) => line.group_no === savedGroup);
                setLines((current) => [
                  ...current.filter((line) => line.groupNo !== target),
                  ...source.map((line) => ({ ...fromSavedLine(line), groupNo: target })),
                ]);
                setActiveGroup(target);
                setNotice(`Specification ${target} now holds the saved rows of specification ${savedGroup}.`);
              }}
            />
          </div>
        ) : null}

        {data ? (
          <section className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-line-strong/60 bg-ink-900/70 px-4 py-3">
            <p className="text-3xs text-text-faint">
              {totals.lines} line{totals.lines === 1 ? "" : "s"}, {formatSqm(totals.sqm)} sqm, {formatMoney(totals.value, currency)}
              {revision ? `, saved revision ${revision} at ${data.proforma?.updated_at ? new Date(data.proforma.updated_at).toLocaleString() : "an unknown time"}` : ", not saved yet"}
            </p>
            <p className="text-3xs text-text-faint">Values are recomputed by the database on save.</p>
          </section>
        ) : null}
      </div>
    </main>
  );
}
