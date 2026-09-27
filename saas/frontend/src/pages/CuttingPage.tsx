import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  completeCuttingPiece,
  completeCuttingSession,
  cuttingBreakage,
  getCuttingProgress,
  getCuttingRequirements,
  listCuttingSessions,
  listJobOrders,
  runCutting,
} from "../api/endpoints";
import { Card, Empty, ErrorNote, Spinner, StatusBadge, date } from "../components/ui";
import { errorText } from "../auth/AuthContext";

/**
 * The cutting floor (Phase 26b surfaces): per-job progress, the
 * requirement build (material groups), RUN (reserve → pack → verify →
 * persist → allocate, with per-group compensation), and the sessions
 * with their sheets/pieces — complete piece/session, breakage with the
 * replacement flow.
 */
export default function CuttingPage() {
  const [jobOrderId, setJobOrderId] = useState("");
  const [showRun, setShowRun] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const jobs = useQuery({
    queryKey: ["job-orders", { pageSize: 100 }],
    queryFn: () => listJobOrders({ pageSize: 100 }),
  });

  const progress = useQuery({
    queryKey: ["cutting-progress", jobOrderId],
    queryFn: () => getCuttingProgress(jobOrderId),
    enabled: jobOrderId !== "",
  });

  const requirements = useQuery({
    queryKey: ["cutting-requirements", jobOrderId],
    queryFn: () => getCuttingRequirements(jobOrderId),
    enabled: jobOrderId !== "",
  });

  const sessions = useQuery({
    queryKey: ["cutting-sessions", jobOrderId],
    queryFn: () => listCuttingSessions(jobOrderId),
    enabled: jobOrderId !== "",
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ["cutting-progress", jobOrderId] });
    await queryClient.invalidateQueries({ queryKey: ["cutting-sessions", jobOrderId] });
    await queryClient.invalidateQueries({ queryKey: ["cutting-requirements", jobOrderId] });
  };

  const run = useMutation({
    mutationFn: (options: { rotation?: string; split?: string; effortLevel?: number; notes?: string }) =>
      runCutting(jobOrderId, options),
    onSuccess: async (result) => {
      setError(null);
      setShowRun(false);
      const lines = [
        `${result.message}`,
        `${result.sessions} session(s), ${result.sheets} sheet(s), ${result.pieces} piece(s), ${result.reservations} reservation(s), ${result.allocations} allocation(s).`,
        ...result.groups
          .filter((group) => !group.ok)
          .map((group) => `Group ${group.groupId}: ${group.message}`),
        ...result.warnings,
        ...result.errors,
      ];
      setNotice(lines.join(" · "));
      await invalidate();
    },
    onError: (err) => {
      setNotice(null);
      setError(errorText(err));
    },
  });

  return (
    <div className="stack">
      <Card
        title="Cutting floor"
        actions={
          <div className="filters">
            <select
              value={jobOrderId}
              onChange={(e) => {
                setJobOrderId(e.target.value);
                setNotice(null);
                setError(null);
              }}
            >
              <option value="">Choose a job order…</option>
              {(jobs.data?.items ?? []).map((job) => (
                <option key={job.id} value={job.id}>
                  {job.jobOrderNo} — {job.customerName}
                </option>
              ))}
            </select>
            <button
              className="btn primary"
              disabled={jobOrderId === "" || showRun}
              onClick={() => setShowRun(true)}
            >
              Run cutting
            </button>
          </div>
        }
      >
        {jobOrderId === "" ? (
          <Empty label="Pick a job order to see its cutting floor." />
        ) : (
          <>
            {progress.data && (
              <p className="sub">
                Progress: {progress.data.completed}/{progress.data.required} completed ·{" "}
                {progress.data.remaining} remaining · {progress.data.breakage} breakage ·{" "}
                {progress.data.foc} FOC · {progress.data.rework} rework ·{" "}
                {progress.data.qcPending} QC pending.
              </p>
            )}
            {showRun && (
              <RunForm
                pending={run.isPending}
                onCancel={() => setShowRun(false)}
                onSubmit={(options) => run.mutate(options)}
              />
            )}
            {notice && <div className="note ok">{notice}</div>}
            {error && <div className="note bad">{error}</div>}

            <ErrorNote error={requirements.error} />
            {requirements.isLoading ? (
              <Spinner />
            ) : (requirements.data?.length ?? 0) === 0 ? (
              <Empty label="No cutting requirements (no released production pieces)." />
            ) : (
              <details>
                <summary style={{ cursor: "pointer", margin: "8px 0" }}>
                  Requirement build — {requirements.data!.length} material group(s)
                </summary>
                {requirements.data!.map((group) => (
                  <div key={group.groupId} style={{ margin: "10px 0" }}>
                    <h4 style={{ margin: "0 0 4px" }}>{group.displayLabel}</h4>
                    <p className="sub" style={{ margin: "0 0 4px" }}>
                      {group.totalPieces} piece(s) · {group.totalAreaSqm.toFixed(2)} sqm
                      {group.coating ? ` · ${group.coating}` : ""}
                      {group.treatment ? ` · ${group.treatment}` : ""}
                    </p>
                    <table>
                      <thead>
                        <tr>
                          <th>Piece</th>
                          <th>Spec</th>
                          <th className="num">Size (mm)</th>
                          <th>Flags</th>
                          <th>Material key</th>
                        </tr>
                      </thead>
                      <tbody>
                        {group.requirements.map((req) => (
                          <tr key={req.pieceId}>
                            <td>{req.pieceLabel}</td>
                            <td>
                              #{req.specGroupNumber}/S{req.serialNumber} {req.specName}
                            </td>
                            <td className="num">
                              {req.widthMm}×{req.heightMm}
                            </td>
                            <td>
                              {req.requiresTempering ? "temper " : ""}
                              {req.isFoc ? "FOC " : ""}
                              {req.replacementSequence > 0 ? `RPL:${req.replacementSequence}` : ""}
                            </td>
                            <td className="sub">{req.materialKey}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ))}
              </details>
            )}
          </>
        )}
      </Card>

      {jobOrderId !== "" && (
        <Card title="Cutting sessions">
          <ErrorNote error={sessions.error} />
          {sessions.isLoading ? (
            <Spinner />
          ) : (sessions.data?.length ?? 0) === 0 ? (
            <Empty label="No cutting sessions yet — run cutting to create them." />
          ) : (
            sessions.data!.map((session) => (
              <SessionCard
                key={session.id}
                session={session}
                onChanged={invalidate}
                onError={(msg) => {
                  setNotice(null);
                  setError(msg);
                }}
              />
            ))
          )}
        </Card>
      )}
    </div>
  );
}

function RunForm(props: {
  pending: boolean;
  onCancel: () => void;
  onSubmit: (options: {
    rotation?: string;
    split?: string;
    effortLevel?: number;
    notes?: string;
  }) => void;
}) {
  const [rotation, setRotation] = useState("");
  const [split, setSplit] = useState("");
  const [effort, setEffort] = useState("");
  const [notes, setNotes] = useState("");
  return (
    <form
      className="create-form"
      onSubmit={(e) => {
        e.preventDefault();
        props.onSubmit({
          rotation: rotation || undefined,
          split: split || undefined,
          effortLevel: effort ? Number(effort) : undefined,
          notes: notes || undefined,
        });
      }}
    >
      <h3>Run the cutting workflow</h3>
      <p className="sub">
        Per material group: reserve stock → pack (optimization) → verify → persist →
        allocate. Failing groups are skipped with their reservations released.
      </p>
      <div className="form-grid">
        <label>
          Rotation policy
          <input
            placeholder="server default"
            value={rotation}
            onChange={(e) => setRotation(e.target.value)}
          />
        </label>
        <label>
          Split mode
          <input
            placeholder="server default"
            value={split}
            onChange={(e) => setSplit(e.target.value)}
          />
        </label>
        <label>
          Effort level (1–5)
          <input
            type="number"
            min={1}
            max={5}
            placeholder="server default"
            value={effort}
            onChange={(e) => setEffort(e.target.value)}
          />
        </label>
        <label>
          Notes
          <input value={notes} onChange={(e) => setNotes(e.target.value)} />
        </label>
      </div>
      <div className="form-actions">
        <button type="button" className="btn ghost" onClick={props.onCancel}>
          Cancel
        </button>
        <button type="submit" className="btn primary" disabled={props.pending}>
          {props.pending ? "Running…" : "Run"}
        </button>
      </div>
    </form>
  );
}

function SessionCard(props: {
  session: import("../api/types").CuttingSession;
  onChanged: () => Promise<void>;
  onError: (msg: string) => void;
}) {
  const { session } = props;
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const guard = async (fn: () => Promise<unknown>) => {
    try {
      await fn();
      setError(null);
      await props.onChanged();
    } catch (err) {
      setError(errorText(err));
      props.onError(errorText(err));
    }
  };

  const sheets = useQuery({
    queryKey: ["cutting-sheets", session.cuttingSessionId],
    queryFn: () => import("../api/endpoints").then((m) => m.listCuttingSheets(session.cuttingSessionId)),
    enabled: open,
  });
  const pieces = useQuery({
    queryKey: ["cutting-pieces", session.cuttingSessionId],
    queryFn: () => import("../api/endpoints").then((m) => m.listCuttingPieces(session.cuttingSessionId)),
    enabled: open,
  });

  return (
    <div style={{ border: "1px solid var(--line)", borderRadius: 10, padding: "8px 12px", marginBottom: 10 }}>
      <div
        style={{ display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap", cursor: "pointer" }}
        onClick={() => setOpen((v) => !v)}
      >
        <strong>{session.cuttingSessionId}</strong>
        <StatusBadge value={session.status} />
        <span className="sub">
          {session.totalPieces} piece(s) · {session.sheetsUsed} sheet(s) · util{" "}
          {session.utilizationPct.toFixed(1)}% · waste {session.wastagePct.toFixed(1)}%
          {session.isVerified ? " · verified" : ""}
        </span>
        <span className="sub">{date(session.createdAt)}</span>
        {session.status !== "Completed" && (
          <button
            className="btn small primary"
            onClick={(e) => {
              e.stopPropagation();
              guard(() => completeCuttingSession(session.cuttingSessionId));
            }}
          >
            Complete session
          </button>
        )}
      </div>
      {error && <div className="note bad">{error}</div>}
      {open && (
        <>
          {sheets.isLoading || pieces.isLoading ? (
            <Spinner />
          ) : (
            <>
              <h4 style={{ margin: "10px 0 4px" }}>Sheets</h4>
              <table>
                <thead>
                  <tr>
                    <th>Sheet</th>
                    <th>Stock</th>
                    <th className="num">Usable (mm)</th>
                    <th className="num">Used / stock sqm</th>
                    <th className="num">Utilization</th>
                    <th className="num">Pieces</th>
                  </tr>
                </thead>
                <tbody>
                  {(sheets.data ?? []).map((sheet) => (
                    <tr key={sheet.id}>
                      <td>#{sheet.sheetNo}</td>
                      <td>
                        {sheet.stockSizeLabel}
                        {sheet.color ? ` · ${sheet.color}` : ""}
                      </td>
                      <td className="num">
                        {sheet.usableWidthMm}×{sheet.usableHeightMm}
                      </td>
                      <td className="num">
                        {sheet.usedSqm.toFixed(2)} / {sheet.stockSqm.toFixed(2)}
                      </td>
                      <td className="num">{sheet.utilizationPct.toFixed(1)}%</td>
                      <td className="num">{sheet.placedPieceCount}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <h4 style={{ margin: "10px 0 4px" }}>Placed pieces</h4>
              <table>
                <thead>
                  <tr>
                    <th>Piece</th>
                    <th>Spec</th>
                    <th className="num">Size (mm)</th>
                    <th className="num">Pos</th>
                    <th className="num">Cut #</th>
                    <th>Status</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {(pieces.data ?? []).map((piece) => (
                    <tr key={piece.id}>
                      <td>
                        {piece.pieceLabel}
                        {piece.isRotated ? <span className="sub"> ⟳</span> : null}
                      </td>
                      <td>
                        #{piece.specGroupNumber}/S{piece.serialNumber} {piece.glassRef}
                      </td>
                      <td className="num">
                        {piece.widthMm}×{piece.heightMm}
                      </td>
                      <td className="num">
                        {piece.x},{piece.y}
                      </td>
                      <td className="num">{piece.cutSequence}</td>
                      <td>
                        <StatusBadge value={piece.status} />
                        {piece.isFoc ? <span className="sub"> FOC</span> : null}
                      </td>
                      <td className="actions">
                        {piece.status !== "Completed" && piece.status !== "Breakage" && (
                          <>
                            <button
                              className="btn small"
                              onClick={() =>
                                guard(() => completeCuttingPiece(piece.placedPieceId))
                              }
                            >
                              Complete
                            </button>
                            <button
                              className="btn small bad"
                              onClick={() => {
                                const reason = window.prompt("Breakage reason:");
                                if (!reason) return;
                                const isFoc = window.confirm("FOC breakage?");
                                guard(() =>
                                  cuttingBreakage(piece.placedPieceId, reason, isFoc),
                                );
                              }}
                            >
                              Breakage
                            </button>
                          </>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
        </>
      )}
    </div>
  );
}
