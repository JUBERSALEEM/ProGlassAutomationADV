import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams, Link } from "react-router-dom";
import {
  cancelPiece,
  getJobOrder,
  getProductionPlan,
  listPiecesForJob,
  recordBreakage,
  recordQc,
  recordStage,
  releaseProduction,
} from "../api/endpoints";
import {
  Card,
  Empty,
  ErrorNote,
  Spinner,
  StatusBadge,
  money,
  sqm,
} from "../components/ui";
import { errorText } from "../auth/AuthContext";

/**
 * The job-order workbench: header → production plan (pre-release) →
 * released pieces with the floor actions (stage / QC / breakage /
 * cancel). Every action maps 1:1 to an api/v1/production endpoint and
 * surfaces the server's own message — the client never invents rules.
 */
export default function JobOrderDetailPage() {
  const { id = "" } = useParams();
  const queryClient = useQueryClient();
  const [actionError, setActionError] = useState<string | null>(null);
  const [releaseNote, setReleaseNote] = useState<string | null>(null);

  const job = useQuery({
    queryKey: ["job-order", id],
    queryFn: () => getJobOrder(id),
    enabled: id !== "",
  });

  // Pieces: empty until released (404/empty list is the pre-release
  // state — the plan endpoint still renders the deterministic preview).
  const pieces = useQuery({
    queryKey: ["pieces", id],
    queryFn: () => listPiecesForJob(id),
    enabled: id !== "",
  });
  const plan = useQuery({
    queryKey: ["plan", id],
    queryFn: () => getProductionPlan(id),
    enabled: id !== "",
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ["pieces", id] });
    void queryClient.invalidateQueries({ queryKey: ["plan", id] });
  };

  const release = useMutation({
    mutationFn: () => releaseProduction(id),
    onSuccess: (result) => {
      setReleaseNote(
        `Released ${result.insertedItems} item(s)` +
          (result.existingItems > 0
            ? `, ${result.existingItems} already present (idempotent)`
            : "") +
          `, ${result.projectedExecutions} process execution(s) projected.`,
      );
      setActionError(null);
      invalidate();
    },
    onError: (error) => setActionError(errorText(error)),
  });

  const stage = useMutation({
    mutationFn: (args: { pieceId: string; qtyDone: number }) =>
      recordStage(args.pieceId, args.qtyDone),
    onSuccess: () => {
      setActionError(null);
      setReleaseNote(null);
      invalidate();
    },
    onError: (error) => setActionError(errorText(error)),
  });

  const qc = useMutation({
    mutationFn: (args: { pieceId: string; result: "Passed" | "Failed" | "Rework" }) =>
      recordQc(args.pieceId, args.result),
    onSuccess: () => {
      setActionError(null);
      invalidate();
    },
    onError: (error) => setActionError(errorText(error)),
  });

  const breakage = useMutation({
    mutationFn: (args: { pieceId: string; qty: number; reason: string }) =>
      recordBreakage(args.pieceId, args.qty, args.reason),
    onSuccess: () => {
      setActionError(null);
      invalidate();
    },
    onError: (error) => setActionError(errorText(error)),
  });

  const cancel = useMutation({
    mutationFn: (args: { pieceId: string; reason?: string }) =>
      cancelPiece(args.pieceId, args.reason),
    onSuccess: () => {
      setActionError(null);
      invalidate();
    },
    onError: (error) => setActionError(errorText(error)),
  });

  if (job.isLoading) return <Spinner label="Loading job order…" />;
  if (job.error) return <ErrorNote error={job.error} />;
  const order = job.data!;

  const released = (pieces.data?.length ?? 0) > 0;
  const items = released ? pieces.data! : (plan.data?.items ?? []);

  return (
    <div className="stack">
      <Card
        title={`${order.jobOrderNo} — ${order.customerName}`}
        actions={
          <Link className="btn ghost" to="/job-orders">
            ← All job orders
          </Link>
        }
      >
        <div className="kv-grid">
          <div>
            <span className="kv-label">Status</span>
            <StatusBadge value={order.status} />
          </div>
          <div>
            <span className="kv-label">Priority</span>
            <StatusBadge value={order.priority} />
          </div>
          <div>
            <span className="kv-label">Project</span>
            {order.projectName || "—"}
          </div>
          <div>
            <span className="kv-label">Source PI</span>
            {order.sourceProformaNo}
          </div>
          <div>
            <span className="kv-label">Grand total (VAT incl.)</span>
            {money(order.grandTotalVatInclusive)}
          </div>
          <div>
            <span className="kv-label">Assigned to</span>
            {order.assignedTo || "—"}
          </div>
        </div>
        {order.cuttingInstructions && (
          <p className="note">
            <strong>Cutting:</strong> {order.cuttingInstructions}
          </p>
        )}
      </Card>

      <Card
        title={released ? "Production pieces" : "Production plan (not released yet)"}
        actions={
          <button
            className="btn primary"
            disabled={release.isPending}
            onClick={() => release.mutate()}
          >
            {release.isPending
              ? "Releasing…"
              : released
                ? "Re-release (idempotent)"
                : "Release to production"}
          </button>
        }
      >
        <ErrorNote error={plan.error} />
        {actionError && <div className="note bad">{actionError}</div>}
        {releaseNote && <div className="note ok">{releaseNote}</div>}
        {plan.data?.warnings.map((warning, index) => (
          <div key={index} className="note warn">
            {warning}
          </div>
        ))}

        {items.length === 0 ? (
          plan.isLoading ? (
            <Spinner label="Building the plan…" />
          ) : (
            <Empty label="The distribution engine produced no work items for this job." />
          )
        ) : (
          <table>
            <thead>
              <tr>
                <th>Piece</th>
                <th>Stage</th>
                <th>Status</th>
                <th className="num">Size (mm)</th>
                <th className="num">Qty</th>
                <th className="num">Done</th>
                <th className="num">m²</th>
                <th>Floor actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map((piece) => (
                <tr key={piece.id} className={piece.isCancelled ? "void" : undefined}>
                  <td>
                    <div>{piece.glassRef || piece.uniqueKey}</div>
                    <div className="sub">
                      {piece.specName} · {piece.buildType} · S{piece.specGroupNumber}#{piece.serialNumber}
                      {piece.position ? ` · ${piece.position}` : ""}
                      {piece.isFoc ? " · FOC" : ""}
                    </div>
                  </td>
                  <td>{piece.stageLabel || piece.stage}</td>
                  <td>
                    <StatusBadge value={piece.status} />
                  </td>
                  <td className="num">
                    {piece.widthMm}×{piece.heightMm}
                  </td>
                  <td className="num">{piece.qty}</td>
                  <td className="num">{piece.qtyDone}</td>
                  <td className="num">{sqm(piece.totalSqm)}</td>
                  <td>
                    <FloorActions
                      pieceId={piece.id}
                      qty={piece.qty}
                      qtyDone={piece.qtyDone}
                      cancelled={piece.isCancelled}
                      stagePending={stage.isPending}
                      qcPending={qc.isPending}
                      onStage={(qtyDone) =>
                        stage.mutate({ pieceId: piece.id, qtyDone })
                      }
                      onQc={(result) => qc.mutate({ pieceId: piece.id, result })}
                      onBreakage={(qty, reason) =>
                        breakage.mutate({ pieceId: piece.id, qty, reason })
                      }
                      onCancel={() => cancel.mutate({ pieceId: piece.id })}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </div>
  );
}

function FloorActions(props: {
  pieceId: string;
  qty: number;
  qtyDone: number;
  cancelled: boolean;
  stagePending: boolean;
  qcPending: boolean;
  onStage: (qtyDone: number) => void;
  onQc: (result: "Passed" | "Failed" | "Rework") => void;
  onBreakage: (qty: number, reason: string) => void;
  onCancel: () => void;
}) {
  const [breakageOpen, setBreakageOpen] = useState(false);
  const [breakageQty, setBreakageQty] = useState(1);
  const [breakageReason, setBreakageReason] = useState("");

  if (props.cancelled) return <span className="sub">cancelled</span>;
  const done = props.qtyDone >= props.qty;

  return (
    <div className="actions">
      <button
        className="btn small"
        disabled={props.stagePending || done}
        title={done ? "Already complete" : "Record one unit done (auto-status)"}
        onClick={() => props.onStage(props.qtyDone + 1)}
      >
        +1 done
      </button>
      <button
        className="btn small ok"
        disabled={props.qcPending}
        onClick={() => props.onQc("Passed")}
        title="QC passed"
      >
        QC ✓
      </button>
      <button
        className="btn small warn"
        disabled={props.qcPending}
        onClick={() => props.onQc("Rework")}
        title="QC → rework"
      >
        Rework
      </button>
      <button
        className="btn small bad"
        onClick={() => setBreakageOpen((open) => !open)}
        title="Record breakage"
      >
        Breakage
      </button>
      <button
        className="btn small ghost"
        onClick={() => props.onCancel()}
        title="Cancel this piece (idempotent)"
      >
        Cancel
      </button>

      {breakageOpen && (
        <div className="breakage-form">
          <input
            type="number"
            min={1}
            max={props.qty}
            value={breakageQty}
            onChange={(e) => setBreakageQty(Number(e.target.value))}
          />
          <input
            placeholder="Reason (required)"
            value={breakageReason}
            onChange={(e) => setBreakageReason(e.target.value)}
          />
          <button
            className="btn small bad"
            disabled={breakageQty < 1 || breakageReason.trim() === ""}
            onClick={() => {
              props.onBreakage(breakageQty, breakageReason.trim());
              setBreakageOpen(false);
              setBreakageReason("");
            }}
          >
            Record
          </button>
        </div>
      )}
    </div>
  );
}
