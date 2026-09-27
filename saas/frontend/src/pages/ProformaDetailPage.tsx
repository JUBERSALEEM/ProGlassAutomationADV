import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import {
  createProformaRevision,
  duplicateProforma,
  getProforma,
  getProformaRevisions,
  getSpecGroups,
  getStep6,
  getStep7,
  getStep9,
  listClients,
  listCompanies,
  listProjects,
  listSalesmen,
  transitionProforma,
  voidProforma,
} from "../api/endpoints";
import {
  Card,
  Empty,
  ErrorNote,
  Spinner,
  StatusBadge,
  money,
  sqm,
  date,
} from "../components/ui";
import { errorText } from "../auth/AuthContext";

type Tab = "step5" | "step6" | "step7" | "step9" | "chain";

/**
 * The proforma workbench (Phase 29): header + lifecycle actions and the
 * wizard's review tabs. Step 5/6/7/9 are READ views over the persisted
 * sections — the whole-grid PUT editors are a later slice (documented in
 * mapping §1.15); lifecycle (transition/void/duplicate/revision) and
 * draft creation are the writes.
 */
export default function ProformaDetailPage() {
  const { id = "" } = useParams();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<Tab>("step5");
  const [actionError, setActionError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const pi = useQuery({
    queryKey: ["proforma", id],
    queryFn: () => getProforma(id),
    enabled: id !== "",
  });

  // Master data for id → name resolution (cached across pages).
  const companies = useQuery({ queryKey: ["pickers", "companies"], queryFn: () => listCompanies() });
  const clients = useQuery({ queryKey: ["pickers", "clients"], queryFn: () => listClients() });
  const projects = useQuery({ queryKey: ["pickers", "projects"], queryFn: () => listProjects() });
  const salesmen = useQuery({ queryKey: ["pickers", "salesmen"], queryFn: () => listSalesmen() });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["proforma", id] });

  const transition = useMutation({
    mutationFn: (args: { to: string; reason?: string }) =>
      transitionProforma(id, args.to, args.reason),
    onSuccess: async (next) => {
      setActionError(null);
      setNotice(`Status → ${next.status}.`);
      await invalidate();
    },
    onError: (err) => setActionError(errorText(err)),
  });

  const voidIt = useMutation({
    mutationFn: (reason?: string) => voidProforma(id, reason),
    onSuccess: async () => {
      setActionError(null);
      setNotice("Invoice voided (soft-close — the number and audit trail are kept).");
      await invalidate();
    },
    onError: (err) => setActionError(errorText(err)),
  });

  const duplicate = useMutation({
    mutationFn: () => duplicateProforma(id),
    onSuccess: async (copy) => {
      setActionError(null);
      setNotice(`Duplicated as ${copy.proformaNo}.`);
      await queryClient.invalidateQueries({ queryKey: ["proformas"] });
    },
    onError: (err) => setActionError(errorText(err)),
  });

  const revision = useMutation({
    mutationFn: () => createProformaRevision(id),
    onSuccess: async (rev) => {
      setActionError(null);
      setNotice(`Revision ${rev.proformaNo} created (original stays byte-identical).`);
      await queryClient.invalidateQueries({ queryKey: ["proformas"] });
    },
    onError: (err) => setActionError(errorText(err)),
  });

  if (pi.isLoading) return <Spinner label="Loading invoice…" />;
  if (pi.error) return <ErrorNote error={pi.error} />;
  const invoice = pi.data!;

  const companyName =
    companies.data?.items.find((c) => c.id === invoice.companyId)?.name ?? "—";
  const clientName = clients.data?.items.find((c) => c.id === invoice.clientId)?.customerName ?? "—";
  const projectName =
    projects.data?.items.find((p) => p.id === invoice.projectId)?.identifierName ?? "—";
  const salesmanName =
    salesmen.data?.items.find((s) => s.id === invoice.salesmanId)?.name ?? "—";

  return (
    <div className="stack">
      <Card
        title={`${invoice.proformaNo} — ${clientName}`}
        actions={
          <Link className="btn ghost" to="/proformas">
            ← All proformas
          </Link>
        }
      >
        <div className="kv-grid">
          <div>
            <span className="kv-label">Status</span>
            <StatusBadge value={invoice.status} />
            {invoice.isVoid && <span className="sub"> void</span>}
          </div>
          <div>
            <span className="kv-label">Revision</span>R{invoice.revisionNumber}
          </div>
          <div>
            <span className="kv-label">Company</span>
            {companyName}
          </div>
          <div>
            <span className="kv-label">Project</span>
            {projectName}
          </div>
          <div>
            <span className="kv-label">Salesman</span>
            {salesmanName}
          </div>
          <div>
            <span className="kv-label">Issued / valid until</span>
            {date(invoice.issueDate)} → {date(invoice.validUntil)}
          </div>
        </div>

        <div className="totals-strip">
          <div>
            <span className="kv-label">Glass materials</span>
            {money(invoice.glassMaterialsSubtotal)}
          </div>
          <div>
            <span className="kv-label">Other charges</span>
            {money(invoice.otherChargesSubtotal)}
          </div>
          <div>
            <span className="kv-label">Discount</span>
            {money(invoice.discountAmount)}
          </div>
          <div>
            <span className="kv-label">VAT</span>
            {money(invoice.vatAmount)}
          </div>
          <div>
            <span className="kv-label">Grand total</span>
            <strong>{money(invoice.grandTotalVatInclusive)}</strong>
          </div>
        </div>

        <div className="actions bar">
          {invoice.validTransitions.length > 0 ? (
            <TransitionControl
              transitions={invoice.validTransitions}
              disabled={transition.isPending}
              onTransition={(to, reason) => transition.mutate({ to, reason })}
            />
          ) : (
            <span className="sub">No further transitions from {invoice.status}.</span>
          )}
          <button
            className="btn small"
            disabled={duplicate.isPending}
            onClick={() => duplicate.mutate()}
          >
            Duplicate
          </button>
          <button
            className="btn small"
            disabled={revision.isPending}
            onClick={() => revision.mutate()}
            title="New draft sharing this root (PI-…-Rn)"
          >
            New revision
          </button>
          <button
            className="btn small bad"
            disabled={voidIt.isPending || invoice.isVoid}
            onClick={() => {
              const reason = window.prompt("Void reason (optional):") ?? "";
              voidIt.mutate(reason || undefined);
            }}
          >
            {invoice.isVoid ? "Voided" : "Void"}
          </button>
        </div>

        {actionError && <div className="note bad">{actionError}</div>}
        {notice && <div className="note ok">{notice}</div>}
      </Card>

      <div className="tabs">
        {(
          [
            ["step5", "Step 5 — Glass grid"],
            ["step6", "Step 6 — Charges & wastage"],
            ["step7", "Step 7 — Pricing"],
            ["step9", "Step 9 — P&L"],
            ["chain", "Revisions"],
          ] as [Tab, string][]
        ).map(([key, label]) => (
          <button
            key={key}
            className={`tab ${tab === key ? "active" : ""}`}
            onClick={() => setTab(key)}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "step5" && <Step5Tab id={id} />}
      {tab === "step6" && <Step6Tab id={id} />}
      {tab === "step7" && <Step7Tab id={id} />}
      {tab === "step9" && <Step9Tab id={id} />}
      {tab === "chain" && <ChainTab id={id} />}
    </div>
  );
}

function TransitionControl(props: {
  transitions: string[];
  disabled: boolean;
  onTransition: (to: string, reason?: string) => void;
}) {
  const [to, setTo] = useState(props.transitions[0] ?? "");
  const [reason, setReason] = useState("");
  return (
    <span className="actions">
      <select value={to} onChange={(e) => setTo(e.target.value)}>
        {props.transitions.map((t) => (
          <option key={t}>{t}</option>
        ))}
      </select>
      <input
        placeholder="Reason (optional)"
        value={reason}
        onChange={(e) => setReason(e.target.value)}
      />
      <button
        className="btn small primary"
        disabled={props.disabled || !to}
        onClick={() => props.onTransition(to, reason || undefined)}
      >
        Transition
      </button>
    </span>
  );
}

function Step5Tab({ id }: { id: string }) {
  const groups = useQuery({
    queryKey: ["spec-groups", id],
    queryFn: () => getSpecGroups(id),
  });
  if (groups.isLoading) return <Spinner />;
  if (groups.error) return <ErrorNote error={groups.error} />;
  if ((groups.data?.length ?? 0) === 0)
    return <Empty label="No glass groups — the Step 5 grid is empty (or not filled yet)." />;

  return (
    <div className="stack">
      {groups.data!.map((group) => (
        <Card
          key={group.id}
          title={`Group ${group.groupNumber} — ${group.specName} (${group.glassType})`}
          actions={
            <span className="sub">
              {money(group.ratePerSqm)}/m² · {group.priceSource} · {group.totalQty} pcs ·{" "}
              {sqm(group.totalSqm)}
            </span>
          }
        >
          <div className="sub" style={{ marginBottom: 8 }}>
            Sheets — outer {group.outerGlassColor || "—"} {group.outerSheetWidthMm}×
            {group.outerSheetHeightMm}mm
            {group.innerGlassColor
              ? ` · inner ${group.innerGlassColor} ${group.innerSheetWidthMm}×${group.innerSheetHeightMm}mm`
              : ""}
            {group.thirdGlassColor ? ` · third ${group.thirdGlassColor}` : ""}
          </div>
          <table>
            <thead>
              <tr>
                <th>Serial</th>
                <th>Position</th>
                <th className="num">W1×H1 (mm)</th>
                <th className="num">W2×H2 (mm)</th>
                <th className="num">Qty</th>
                <th className="num">SQM</th>
                <th className="num">Total SQM</th>
                <th className="num">Rate</th>
                <th className="num">Total</th>
                <th>Notes</th>
              </tr>
            </thead>
            <tbody>
              {group.items.map((item) => (
                <tr key={item.id}>
                  <td>{item.rowSerial}</td>
                  <td>{item.position || "—"}</td>
                  <td className="num">
                    {item.w1}×{item.h1}
                  </td>
                  <td className="num">
                    {item.w2 || item.h2 ? `${item.w2}×${item.h2}` : "—"}
                  </td>
                  <td className="num">{item.qty}</td>
                  <td className="num">{item.sqm.toFixed(3)}</td>
                  <td className="num">{item.totalSqm.toFixed(3)}</td>
                  <td className="num">{money(item.unitPrice)}</td>
                  <td className="num">{money(item.total)}</td>
                  <td>{item.notes || ""}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      ))}
    </div>
  );
}

function Step6Tab({ id }: { id: string }) {
  const step6 = useQuery({ queryKey: ["step6", id], queryFn: () => getStep6(id) });
  if (step6.isLoading) return <Spinner />;
  if (step6.error) return <ErrorNote error={step6.error} />;
  const data = step6.data!;

  return (
    <div className="stack">
      <Card title={`Predefined charges`} actions={<span className="sub">Subtotal {money(data.subtotal)}</span>}>
        {data.charges.length === 0 ? (
          <Empty label="No charge rows." />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Charge</th>
                <th>Metric</th>
                <th>Specs</th>
                <th className="num">Qty</th>
                <th className="num">Rate</th>
                <th className="num">Amount</th>
              </tr>
            </thead>
            <tbody>
              {data.charges.map((c) => (
                <tr key={c.id} className={c.isActive ? undefined : "void"}>
                  <td>
                    {c.chargeName || c.templateKey}
                    {c.polishSubType ? <span className="sub"> · {c.polishSubType}</span> : null}
                    {c.description ? <div className="sub">{c.description}</div> : null}
                  </td>
                  <td>
                    {c.metricKey} ({c.unit})
                  </td>
                  <td className="sub">
                    {c.checkedSpecGroupNumbers.length === 0
                      ? "all"
                      : c.checkedSpecGroupNumbers.join(", ")}
                  </td>
                  <td className="num">{c.metricQty}</td>
                  <td className="num">{money(c.rate)}</td>
                  <td className="num">{money(c.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      <Card title="Wastage">
        {data.wastages.length === 0 ? (
          <Empty label="No wastage rows." />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Color</th>
                <th>Formula</th>
                <th className="num">SQM</th>
                <th className="num">Sheets</th>
                <th className="num">Rate</th>
                <th className="num">Amount</th>
              </tr>
            </thead>
            <tbody>
              {data.wastages.map((w) => (
                <tr key={w.id} className={w.isActive ? undefined : "void"}>
                  <td>{w.colorName}</td>
                  <td>
                    {w.useOptimizationFormula
                      ? `Optimization ${w.optimizationPercent}%`
                      : `Manual ${money(w.formulaPricePerSqm)}/m²`}
                    {w.isManuallyAdded ? <span className="sub"> · manual</span> : null}
                  </td>
                  <td className="num">{w.totalSqm.toFixed(3)}</td>
                  <td className="num">{w.sheetQuantity}</td>
                  <td className="num">{money(w.rate)}</td>
                  <td className="num">{money(w.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      <Card title="Custom charges">
        {data.customs.length === 0 ? (
          <Empty label="No custom rows." />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Charge</th>
                <th className="num">Qty</th>
                <th>Unit</th>
                <th className="num">Rate</th>
                <th className="num">Amount</th>
              </tr>
            </thead>
            <tbody>
              {data.customs.map((c) => (
                <tr key={c.id} className={c.isActive ? undefined : "void"}>
                  <td>{c.chargeName}</td>
                  <td className="num">{c.qty}</td>
                  <td>{c.unit}</td>
                  <td className="num">{money(c.rate)}</td>
                  <td className="num">{money(c.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </div>
  );
}

function Step7Tab({ id }: { id: string }) {
  const step7 = useQuery({ queryKey: ["step7", id], queryFn: () => getStep7(id) });
  if (step7.isLoading) return <Spinner />;
  if (step7.error) return <ErrorNote error={step7.error} />;
  const s = step7.data!;
  return (
    <Card title="Step 7 — pricing waterfall">
      <table className="waterfall">
        <tbody>
          <tr>
            <td>Glass materials subtotal</td>
            <td className="num">{money(s.glassMaterialsSubtotal)}</td>
          </tr>
          <tr>
            <td>Other charges subtotal</td>
            <td className="num">{money(s.otherChargesSubtotal)}</td>
          </tr>
          <tr>
            <td>Net subtotal</td>
            <td className="num">{money(s.netSubtotal)}</td>
          </tr>
          <tr>
            <td>
              Discount ({s.discountType || "None"}, {s.discountValue})
            </td>
            <td className="num">−{money(s.discountAmount)}</td>
          </tr>
          <tr>
            <td>Subtotal before VAT</td>
            <td className="num">{money(s.subtotalBeforeVat)}</td>
          </tr>
          <tr>
            <td>VAT {s.isVatApplicable ? `(${s.vatPercent}%)` : "(not applicable)"}</td>
            <td className="num">{money(s.vatAmount)}</td>
          </tr>
          <tr className="grand">
            <td>
              <strong>Grand total (VAT inclusive)</strong>
            </td>
            <td className="num">
              <strong>{money(s.grandTotalVatInclusive)}</strong>
            </td>
          </tr>
        </tbody>
      </table>
    </Card>
  );
}

function Step9Tab({ id }: { id: string }) {
  const step9 = useQuery({ queryKey: ["step9", id], queryFn: () => getStep9(id) });
  if (step9.isLoading) return <Spinner />;
  if (step9.error) return <ErrorNote error={step9.error} />;
  const s = step9.data!;

  if (!s.hasBeenCalculated)
    return (
      <Card title="Step 9 — P&L">
        <Empty label="Not calculated yet — the P&L is computed when the section is saved." />
      </Card>
    );

  const kpis = s.kpis;
  return (
    <div className="stack">
      {kpis && (
        <div className="stats">
          <div className="stat">
            <div className="stat-value">{money(kpis.grossVatInclusive)}</div>
            <div className="stat-label">Gross (VAT incl.)</div>
          </div>
          <div className="stat">
            <div className="stat-value">{money(kpis.netExVat)}</div>
            <div className="stat-label">Net ex-VAT</div>
          </div>
          <div className="stat">
            <div className="stat-value">{money(kpis.totalGlassCost)}</div>
            <div className="stat-label">Glass cost</div>
          </div>
          <div className="stat">
            <div className="stat-value">{money(kpis.totalProductionCost)}</div>
            <div className="stat-label">Production cost</div>
          </div>
          <div className={`stat ${kpis.isLoss ? "loss" : ""}`}>
            <div className="stat-value">{money(kpis.profit)}</div>
            <div className="stat-label">{kpis.isLoss ? "LOSS" : "Profit"}</div>
          </div>
          <div className="stat">
            <div className="stat-value">{kpis.overallMarginPercent.toFixed(1)}%</div>
            <div className="stat-label">Overall margin</div>
          </div>
        </div>
      )}
      <Card title="Cost rows (WPF build order)">
        <table>
          <thead>
            <tr>
              <th>Kind</th>
              <th>Spec</th>
              <th>Description</th>
              <th className="num">Sheet</th>
              <th className="num">Qty</th>
              <th className="num">Rate</th>
              <th className="num">Amount</th>
            </tr>
          </thead>
          <tbody>
            {s.rows.map((row) => (
              <tr key={row.id}>
                <td>{row.kind}</td>
                <td>
                  {row.specNumber > 0 ? `#${row.specNumber} ` : ""}
                  {row.specName || "—"}
                </td>
                <td>{row.description}</td>
                <td className="num sub">
                  {row.sheetWidthMm > 0 ? `${row.sheetWidthMm}×${row.sheetHeightMm}` : "—"}
                </td>
                <td className="num">
                  {row.quantity} {row.unitLabel}
                </td>
                <td className="num">{money(row.pricePerUnit)}</td>
                <td className="num">{money(row.amount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </div>
  );
}

function ChainTab({ id }: { id: string }) {
  const chain = useQuery({
    queryKey: ["proforma-revisions", id],
    queryFn: () => getProformaRevisions(id),
  });
  if (chain.isLoading) return <Spinner />;
  if (chain.error) return <ErrorNote error={chain.error} />;
  return (
    <Card title="Revision chain">
      <table>
        <thead>
          <tr>
            <th>PI number</th>
            <th>Rev</th>
            <th>Status</th>
            <th>Created</th>
            <th className="num">Grand total</th>
          </tr>
        </thead>
        <tbody>
          {chain.data!.map((pi) => (
            <tr key={pi.id}>
              <td>
                <Link to={`/proformas/${pi.id}`}>{pi.proformaNo}</Link>
              </td>
              <td>R{pi.revisionNumber}</td>
              <td>
                <StatusBadge value={pi.status} />
                {pi.isVoid && <span className="sub"> void</span>}
              </td>
              <td>{date(pi.createdAt)}</td>
              <td className="num">{money(pi.grandTotalVatInclusive)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </Card>
  );
}
