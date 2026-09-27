import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  applySpecification,
  deleteSpecification,
  getRecentSpecificationUsage,
  getSpecificationUsage,
  listSpecifications,
  reconcileSpecifications,
} from "../api/endpoints";
import type { Specification } from "../api/types";
import { Card, Empty, ErrorNote, Spinner, date, money } from "../components/ui";
import { errorText } from "../auth/AuthContext";

/**
 * The Step 4 glass library — smart-sorted (most-used first), with the
 * APPLY upsert (rate always refreshes; catalogue details only when
 * supplied — preserve-on-blank), the verified usage history, and the
 * ledger reconcile.
 */
export default function SpecificationsPage() {
  const [glassType, setGlassType] = useState("");
  const [showApply, setShowApply] = useState(false);
  const [selected, setSelected] = useState<Specification | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["specifications"] });

  const list = useQuery({
    queryKey: ["specifications", "list", glassType],
    queryFn: () => listSpecifications(glassType || undefined),
  });

  const apply = useMutation({
    mutationFn: (body: Record<string, unknown>) => applySpecification(body),
    onSuccess: async (spec) => {
      setError(null);
      setNotice(`Applied ${spec.glassType} — ${spec.specName} (rate ${spec.ratePerSqm}).`);
      setShowApply(false);
      await invalidate();
    },
    onError: (err) => {
      setNotice(null);
      setError(errorText(err));
    },
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteSpecification(id),
    onSuccess: async () => {
      setError(null);
      setSelected(null);
      await invalidate();
    },
    onError: (err) => setError(errorText(err)),
  });

  const reconcile = useMutation({
    mutationFn: () => reconcileSpecifications(),
    onSuccess: async (result) => {
      setError(null);
      setNotice(
        `Reconciled: ${result.usageRows} usage rows, ${result.purged} purged, ${result.healed} healed.`,
      );
      await invalidate();
    },
    onError: (err) => setError(errorText(err)),
  });

  return (
    <Card
      title="Glass library (Step 4 specifications)"
      actions={
        <div className="filters">
          <input
            placeholder="Filter by glass type…"
            value={glassType}
            onChange={(e) => setGlassType(e.target.value)}
          />
          <button
            className="btn primary"
            disabled={showApply}
            onClick={() => setShowApply(true)}
          >
            Apply spec
          </button>
          <button
            className="btn"
            disabled={reconcile.isPending}
            onClick={() => reconcile.mutate()}
          >
            {reconcile.isPending ? "Reconciling…" : "Reconcile ledger"}
          </button>
        </div>
      }
    >
      <p className="sub">
        Smart-sorted: most-used first, then most recent. APPLY upserts by (glass type,
        spec name) — the rate always refreshes; catalogue details refresh only when
        supplied (a blank field preserves what an earlier save recorded).
      </p>
      {notice && <div className="note ok">{notice}</div>}
      {error && <div className="note bad">{error}</div>}

      {showApply && (
        <ApplyForm
          pending={apply.isPending}
          onCancel={() => setShowApply(false)}
          onSubmit={(body) => apply.mutate(body)}
        />
      )}

      <ErrorNote error={list.error} />
      {list.isLoading ? (
        <Spinner />
      ) : (list.data?.length ?? 0) === 0 ? (
        <Empty label="No specifications yet — they are created from a proforma's Step 4 APPLY." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Spec name</th>
              <th>Glass type</th>
              <th className="num">Rate / sqm</th>
              <th className="num">Cost basis</th>
              <th className="num">Used</th>
              <th>Last used</th>
              <th>Last proforma</th>
              <th className="num">Sheet (mm)</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {list.data!.map((spec) => (
              <tr
                key={spec.id}
                className={selected?.id === spec.id ? "active" : undefined}
                onClick={() => setSelected(spec)}
                style={{ cursor: "pointer" }}
              >
                <td>
                  <strong>{spec.specName}</strong>
                  {spec.color ? <span className="sub"> · {spec.color}</span> : null}
                  {!spec.isActive && <span className="sub"> inactive</span>}
                </td>
                <td>{spec.glassType}</td>
                <td className="num">{money(spec.ratePerSqm)}</td>
                <td className="num">{spec.costBasisPerSqm ? money(spec.costBasisPerSqm) : "—"}</td>
                <td className="num">{spec.usageCount}</td>
                <td>{date(spec.lastUsedAt)}</td>
                <td>{spec.lastProformaNo ?? "—"}</td>
                <td className="num">
                  {spec.sheetWidthMm
                    ? `${spec.sheetWidthMm}×${spec.sheetHeightMm}`
                    : "—"}
                </td>
                <td className="actions">
                  <button
                    className="btn small bad"
                    disabled={remove.isPending}
                    onClick={(e) => {
                      e.stopPropagation();
                      if (
                        window.confirm(
                          `Delete specification "${spec.specName}"?`,
                        )
                      )
                        remove.mutate(spec.id);
                    }}
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {selected && <UsageHistory spec={selected} />}
      {!selected && <RecentUsage />}
    </Card>
  );
}

function ApplyForm(props: {
  pending: boolean;
  onCancel: () => void;
  onSubmit: (body: Record<string, unknown>) => void;
}) {
  const [glassType, setGlassType] = useState("");
  const [specName, setSpecName] = useState("");
  const [ratePerSqm, setRatePerSqm] = useState("");
  const [costBasisPerSqm, setCostBasisPerSqm] = useState("");
  const [category, setCategory] = useState("");
  const [color, setColor] = useState("");
  const [thicknessMm, setThicknessMm] = useState("");
  const [sheetWidthMm, setSheetWidthMm] = useState("");
  const [sheetHeightMm, setSheetHeightMm] = useState("");

  const submit = () => {
    const body: Record<string, unknown> = {
      glassType,
      specName,
      ratePerSqm: Number(ratePerSqm),
    };
    // Preserve-on-blank: only send what was actually supplied.
    if (costBasisPerSqm !== "") body.costBasisPerSqm = Number(costBasisPerSqm);
    if (category !== "") body.category = category;
    if (color !== "") body.color = color;
    if (thicknessMm !== "") body.thicknessMm = Number(thicknessMm);
    if (sheetWidthMm !== "") body.sheetWidthMm = Number(sheetWidthMm);
    if (sheetHeightMm !== "") body.sheetHeightMm = Number(sheetHeightMm);
    props.onSubmit(body);
  };

  return (
    <form
      className="create-form"
      onSubmit={(e) => {
        e.preventDefault();
        submit();
      }}
    >
      <h3>Apply specification (upsert by glass type + spec name)</h3>
      <div className="form-grid">
        <label>
          Glass type *
          <input
            value={glassType}
            onChange={(e) => setGlassType(e.target.value)}
            required
          />
        </label>
        <label>
          Spec name *
          <input
            value={specName}
            onChange={(e) => setSpecName(e.target.value)}
            required
          />
        </label>
        <label>
          Rate / sqm *
          <input
            type="number"
            step="0.01"
            value={ratePerSqm}
            onChange={(e) => setRatePerSqm(e.target.value)}
            required
          />
        </label>
        <label>
          Cost basis / sqm
          <input
            type="number"
            step="0.01"
            value={costBasisPerSqm}
            onChange={(e) => setCostBasisPerSqm(e.target.value)}
          />
        </label>
        <label>
          Category
          <input value={category} onChange={(e) => setCategory(e.target.value)} />
        </label>
        <label>
          Colour
          <input value={color} onChange={(e) => setColor(e.target.value)} />
        </label>
        <label>
          Thickness (mm)
          <input
            type="number"
            value={thicknessMm}
            onChange={(e) => setThicknessMm(e.target.value)}
          />
        </label>
        <label>
          Sheet width (mm)
          <input
            type="number"
            value={sheetWidthMm}
            onChange={(e) => setSheetWidthMm(e.target.value)}
          />
        </label>
        <label>
          Sheet height (mm)
          <input
            type="number"
            value={sheetHeightMm}
            onChange={(e) => setSheetHeightMm(e.target.value)}
          />
        </label>
      </div>
      <p className="sub">
        Blank catalogue fields are NOT sent — the upsert preserves what an earlier save
        recorded. The raw ParamsJson/PanesJson fields stay API-only.
      </p>
      <div className="form-actions">
        <button type="button" className="btn ghost" onClick={props.onCancel}>
          Cancel
        </button>
        <button type="submit" className="btn primary" disabled={props.pending}>
          {props.pending ? "Applying…" : "Apply"}
        </button>
      </div>
    </form>
  );
}

function UsageHistory(props: { spec: Specification }) {
  const query = useQuery({
    queryKey: ["specifications", "usage", props.spec.id],
    queryFn: () => getSpecificationUsage(props.spec.id),
  });
  return (
    <>
      <h3 style={{ margin: "14px 0 6px" }}>
        Verified uses — {props.spec.glassType} / {props.spec.specName}
      </h3>
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.length ?? 0) === 0 ? (
        <Empty label="No verified uses yet (uses come from SAVED invoices)." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Proforma</th>
              <th>Customer</th>
              <th>Project</th>
              <th className="num">Price / sqm</th>
              <th className="num">Sheet (mm)</th>
              <th>Used at</th>
            </tr>
          </thead>
          <tbody>
            {query.data!.map((usage) => (
              <tr key={usage.id}>
                <td>{usage.proformaNo}</td>
                <td>{usage.customerName}</td>
                <td>{usage.projectName}</td>
                <td className="num">{money(usage.pricePerSqm)}</td>
                <td className="num">
                  {usage.sheetWidthMm}×{usage.sheetHeightMm}
                </td>
                <td>{date(usage.usedAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

function RecentUsage() {
  const query = useQuery({
    queryKey: ["specifications", "recent-usage"],
    queryFn: () => getRecentSpecificationUsage(20),
  });
  if (query.isLoading || query.error) {
    return query.isLoading ? null : <ErrorNote error={query.error} />;
  }
  if ((query.data?.length ?? 0) === 0) return null;
  return (
    <>
      <h3 style={{ margin: "14px 0 6px" }}>Recent activity (last 20)</h3>
      <table>
        <thead>
          <tr>
            <th>Proforma</th>
            <th>Spec</th>
            <th>Customer</th>
            <th className="num">Price / sqm</th>
            <th>Used at</th>
          </tr>
        </thead>
        <tbody>
          {query.data!.map((usage) => (
            <tr key={usage.id}>
              <td>{usage.proformaNo}</td>
              <td>
                {usage.glassType} / {usage.specName}
              </td>
              <td>{usage.customerName}</td>
              <td className="num">{money(usage.pricePerSqm)}</td>
              <td>{date(usage.usedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
