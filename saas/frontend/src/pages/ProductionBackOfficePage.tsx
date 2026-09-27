import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  checkSnapshotIntegrity,
  consumeAllocation,
  deriveCosts,
  getCostSummary,
  getPairingAssemblyReport,
  getProcessStatusReport,
  getQcQueueReport,
  getRouteReport,
  getReworkQueueReport,
  listActualCosts,
  listAllocations,
  listJobOrders,
  listReplacements,
  runConsistencyScan,
} from "../api/endpoints";
import { Card, Empty, ErrorNote, Spinner, StatusBadge, date, money } from "../components/ui";
import { errorText } from "../auth/AuthContext";

type Tab = "costs" | "allocations" | "replacements" | "integrity" | "reports";

/**
 * The production back-office (Phase 26b remaining surfaces): the
 * allocation ledger, actual costs + the per-bucket summary, replacements/
 * FOC lineage, the read-only integrity family (consistency scan + snapshot
 * verdicts), and the WPF production reports.
 */
export default function ProductionBackOfficePage() {
  const [tab, setTab] = useState<Tab>("costs");
  const [jobOrderId, setJobOrderId] = useState("");

  const jobs = useQuery({
    queryKey: ["job-orders", { pageSize: 100 }],
    queryFn: () => listJobOrders({ pageSize: 100 }),
  });

  const needsJob = tab !== "integrity" && !(tab === "reports" && false);

  return (
    <Card
      title="Production back-office"
      actions={
        <div className="tabs">
          {(
            [
              ["costs", "Costs"],
              ["allocations", "Allocations"],
              ["replacements", "Replacements"],
              ["integrity", "Integrity"],
              ["reports", "Reports"],
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
      }
    >
      <div className="filters" style={{ marginBottom: 12 }}>
        <select value={jobOrderId} onChange={(e) => setJobOrderId(e.target.value)}>
          <option value="">Choose a job order…</option>
          {(jobs.data?.items ?? []).map((job) => (
            <option key={job.id} value={job.id}>
              {job.jobOrderNo} — {job.customerName}
            </option>
          ))}
        </select>
        {needsJob && <span className="sub">Job-scoped views need a job order.</span>}
      </div>

      {jobOrderId === "" && needsJob ? (
        <Empty label="Pick a job order above." />
      ) : (
        <>
          {tab === "costs" && jobOrderId !== "" && <CostsTab jobOrderId={jobOrderId} />}
          {tab === "allocations" && jobOrderId !== "" && (
            <AllocationsTab jobOrderId={jobOrderId} />
          )}
          {tab === "replacements" && jobOrderId !== "" && (
            <ReplacementsTab jobOrderId={jobOrderId} />
          )}
          {tab === "integrity" && <IntegrityTab jobOrderId={jobOrderId} />}
          {tab === "reports" && jobOrderId !== "" && (
            <ReportsTab jobOrderId={jobOrderId} />
          )}
        </>
      )}
    </Card>
  );
}

function CostsTab(props: { jobOrderId: string }) {
  const queryClient = useQueryClient();
  const summary = useQuery({
    queryKey: ["cost-summary", props.jobOrderId],
    queryFn: () => getCostSummary(props.jobOrderId),
  });
  const costs = useQuery({
    queryKey: ["actual-costs", props.jobOrderId],
    queryFn: () => listActualCosts(props.jobOrderId),
  });
  const [error, setError] = useState<string | null>(null);

  const derive = useMutation({
    mutationFn: () => deriveCosts(props.jobOrderId),
    onSuccess: async () => {
      setError(null);
      await queryClient.invalidateQueries({ queryKey: ["actual-costs", props.jobOrderId] });
      await queryClient.invalidateQueries({ queryKey: ["cost-summary", props.jobOrderId] });
    },
    onError: (err) => setError(errorText(err)),
  });

  return (
    <>
      {summary.data && (
        <div className="stats">
          <div className="stat">
            <span className="stat-label">Estimated</span>
            <span className="stat-value">{money(summary.data.estimated)}</span>
          </div>
          <div className="stat">
            <span className="stat-label">Actual material</span>
            <span className="stat-value">{money(summary.data.actualMaterial)}</span>
          </div>
          <div className="stat">
            <span className="stat-label">Production</span>
            <span className="stat-value">{money(summary.data.production)}</span>
          </div>
          <div className="stat">
            <span className="stat-label">Wastage</span>
            <span className="stat-value">{money(summary.data.wastage)}</span>
          </div>
          <div className="stat">
            <span className="stat-label">Processing</span>
            <span className="stat-value">{money(summary.data.processing)}</span>
          </div>
          <div className="stat">
            <span className="stat-label">Total actual</span>
            <span className="stat-value">{money(summary.data.totalActual)}</span>
          </div>
          <div className={`stat ${summary.data.focImpact > 0 ? "loss" : ""}`}>
            <span className="stat-label">FOC impact</span>
            <span className="stat-value">{money(summary.data.focImpact)}</span>
          </div>
        </div>
      )}
      <div className="actions bar">
        <button
          className="btn"
          disabled={derive.isPending}
          onClick={() => derive.mutate()}
        >
          {derive.isPending
            ? "Deriving…"
            : "Derive material costs from allocations"}
        </button>
        <span className="sub">piece sqm × the rack's purchase price per sqm</span>
      </div>
      {error && <div className="note bad">{error}</div>}
      <ErrorNote error={costs.error} />
      {costs.isLoading ? (
        <Spinner />
      ) : (costs.data?.length ?? 0) === 0 ? (
        <Empty label="No actual costs recorded." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Cost</th>
              <th>Type</th>
              <th>Spec</th>
              <th className="num">Qty</th>
              <th className="num">Unit</th>
              <th className="num">Total</th>
              <th className="num">Selling</th>
              <th>FOC</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            {costs.data!.map((cost) => (
              <tr key={cost.id}>
                <td>{cost.costId}</td>
                <td>{cost.costType}</td>
                <td>
                  #{cost.specGroupNumber}/S{cost.serialNumber} {cost.glassRef}
                </td>
                <td className="num">{cost.quantity}</td>
                <td className="num">{money(cost.unitCost)}</td>
                <td className="num">
                  <strong>{money(cost.totalCost)}</strong>
                </td>
                <td className="num">{cost.sellingValue ? money(cost.sellingValue) : "—"}</td>
                <td>{cost.isFoc ? "FOC" : ""}</td>
                <td>{date(cost.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

function AllocationsTab(props: { jobOrderId: string }) {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const query = useQuery({
    queryKey: ["allocations", props.jobOrderId],
    queryFn: () => listAllocations(props.jobOrderId),
  });

  const consume = useMutation({
    mutationFn: (allocationId: string) => consumeAllocation(allocationId),
    onSuccess: async () => {
      setError(null);
      await queryClient.invalidateQueries({ queryKey: ["allocations", props.jobOrderId] });
    },
    onError: (err) => setError(errorText(err)),
  });

  return (
    <>
      <p className="sub">
        The allocation ledger in WPF order (sheet, then piece label). Consume flips the
        reservation to Consumed only when ALL its allocations are consumed — planning
        consume, idempotent.
      </p>
      {error && <div className="note bad">{error}</div>}
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.length ?? 0) === 0 ? (
        <Empty label="No allocations — they are created by the cutting workflow." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Allocation</th>
              <th>Reservation key</th>
              <th>Sheet #</th>
              <th>Piece</th>
              <th className="num">Size (mm)</th>
              <th className="num">Qty</th>
              <th className="num">Utilization</th>
              <th>Status</th>
              <th>Consumed</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {query.data!.map((row) => (
              <tr key={row.id}>
                <td>{row.allocationId}</td>
                <td className="sub">{row.reservationKey}</td>
                <td className="num">#{row.sheetNo}</td>
                <td>
                  #{row.specGroupNumber}/S{row.serialNumber} {row.placedPieceLabel}
                </td>
                <td className="num">
                  {row.widthMm}×{row.heightMm}
                </td>
                <td className="num">{row.quantity}</td>
                <td className="num">{row.utilizationPct.toFixed(1)}%</td>
                <td>
                  <StatusBadge value={row.status} />
                </td>
                <td>{date(row.consumedAt)}</td>
                <td className="actions">
                  {row.status !== "Consumed" && (
                    <button
                      className="btn small"
                      disabled={consume.isPending}
                      onClick={() => consume.mutate(row.allocationId)}
                    >
                      Consume
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

function ReplacementsTab(props: { jobOrderId: string }) {
  const query = useQuery({
    queryKey: ["replacements", props.jobOrderId],
    queryFn: () => listReplacements(props.jobOrderId),
  });
  return (
    <>
      <p className="sub">
        RPL:{'{n}'} lineage — the breakage event lands on the ORIGINAL and a traceable
        replacement is created; the original is never silently modified.
      </p>
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.length ?? 0) === 0 ? (
        <Empty label="No replacements / FOC events." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Replacement</th>
              <th>Spec</th>
              <th>Stage</th>
              <th>Entry</th>
              <th>Reason</th>
              <th className="num">Qty</th>
              <th>FOC</th>
              <th className="num">RPL</th>
              <th>Status</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            {query.data!.map((row) => (
              <tr key={row.id}>
                <td>{row.replacementId}</td>
                <td>
                  #{row.specGroupNumber}/S{row.serialNumber} {row.glassRef}
                </td>
                <td>{row.stage}</td>
                <td>{row.entryType}</td>
                <td>{row.reason}</td>
                <td className="num">{row.quantity}</td>
                <td>{row.isFoc ? "FOC" : ""}</td>
                <td className="num">RPL:{row.replacementSequence}</td>
                <td>
                  <StatusBadge value={row.status} />
                </td>
                <td>{date(row.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

function IntegrityTab(props: { jobOrderId: string }) {
  const scan = useQuery({
    queryKey: ["consistency-scan"],
    queryFn: runConsistencyScan,
  });
  const snapshot = useQuery({
    queryKey: ["snapshot-integrity", props.jobOrderId],
    queryFn: () => checkSnapshotIntegrity(props.jobOrderId),
    enabled: props.jobOrderId !== "",
  });

  return (
    <>
      <p className="sub">
        Read-only scans — they never write, never release holds, and never treat
        Consumed-without-Issue as a defect (planning consume ≠ physical issue is the
        product contract).
      </p>
      <ErrorNote error={scan.error} />
      {scan.isLoading ? (
        <Spinner />
      ) : scan.data ? (
        <>
          <div className="stats">
            <div className="stat">
              <span className="stat-label">Reservation rows</span>
              <span className="stat-value">{scan.data.reservationRows}</span>
            </div>
            <div className="stat">
              <span className="stat-label">Allocation rows</span>
              <span className="stat-value">{scan.data.allocationRows}</span>
            </div>
            <div className="stat">
              <span className="stat-label">Glass sheet rows</span>
              <span className="stat-value">{scan.data.glassSheetRows}</span>
            </div>
            <div className="stat">
              <span className="stat-label">Active hold qty</span>
              <span className="stat-value">{scan.data.activeHoldQuantity}</span>
            </div>
            <div className="stat">
              <span className="stat-label">Consumed qty</span>
              <span className="stat-value">{scan.data.consumedQuantity}</span>
            </div>
            <div className={`stat ${(scan.data.findings.length ?? 0) > 0 ? "loss" : ""}`}>
              <span className="stat-label">Findings</span>
              <span className="stat-value">{scan.data.findings.length}</span>
            </div>
          </div>
          {scan.data.findings.length > 0 && (
            <table style={{ marginTop: 12 }}>
              <thead>
                <tr>
                  <th>Kind</th>
                  <th>Code</th>
                  <th>Message</th>
                  <th>Job</th>
                  <th className="num">Qty</th>
                </tr>
              </thead>
              <tbody>
                {scan.data.findings.map((finding, index) => (
                  <tr key={index}>
                    <td>{finding.kind}</td>
                    <td>{finding.code}</td>
                    <td>{finding.message}</td>
                    <td>{finding.jobRef ?? "—"}</td>
                    <td className="num">{finding.quantity}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {scan.data.limitations.length > 0 && (
            <p className="sub" style={{ marginTop: 8 }}>
              Limitations: {scan.data.limitations.join(" · ")}
            </p>
          )}
        </>
      ) : null}

      {props.jobOrderId !== "" && (
        <>
          <h3 style={{ margin: "14px 0 6px" }}>Snapshot integrity</h3>
          <ErrorNote error={snapshot.error} />
          {snapshot.data && (
            <div className="note">
              <strong>{snapshot.data.kind}</strong> — {snapshot.data.message}
              {snapshot.data.hasCutRows ? " (has cut rows)" : ""}
            </div>
          )}
        </>
      )}
    </>
  );
}

function ReportsTab(props: { jobOrderId: string }) {
  const process = useQuery({
    queryKey: ["report-process-status", props.jobOrderId],
    queryFn: () => getProcessStatusReport(props.jobOrderId),
  });
  const pairing = useQuery({
    queryKey: ["report-pairing", props.jobOrderId],
    queryFn: () => getPairingAssemblyReport(props.jobOrderId),
  });
  const route = useQuery({
    queryKey: ["report-route", props.jobOrderId],
    queryFn: () => getRouteReport(props.jobOrderId),
  });
  const rework = useQuery({
    queryKey: ["report-rework-queue"],
    queryFn: getReworkQueueReport,
  });
  const qc = useQuery({
    queryKey: ["report-qc-queue"],
    queryFn: getQcQueueReport,
  });

  return (
    <>
      <ErrorNote error={process.error} />
      {process.isLoading ? (
        <Spinner />
      ) : (process.data?.length ?? 0) === 0 ? (
        <Empty label="No process executions for this job." />
      ) : (
        <details open>
          <summary style={{ cursor: "pointer", margin: "8px 0" }}>
            Process status — {process.data!.length} execution(s)
          </summary>
          <table>
            <thead>
              <tr>
                <th>Job / piece</th>
                <th>Process</th>
                <th>Status</th>
                <th className="num">Qty done / req</th>
                <th>QC</th>
                <th>Operator</th>
                <th>Started / completed</th>
              </tr>
            </thead>
            <tbody>
              {process.data!.map((row, index) => (
                <tr key={index}>
                  <td>
                    <strong>{row.jobRef}</strong>
                    <span className="sub"> {row.pieceId}</span>
                  </td>
                  <td>
                    {row.processType}
                    {row.nextStage ? ` → ${row.nextStage}` : ""}
                  </td>
                  <td>
                    <StatusBadge value={row.status} />
                  </td>
                  <td className="num">
                    {row.qtyDone}/{row.qtyRequired}
                  </td>
                  <td>{row.qcStatus || "—"}</td>
                  <td>{row.operatorName || "—"}</td>
                  <td>
                    {date(row.startedAt)} → {date(row.completedAt)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </details>
      )}

      {(rework.data?.length ?? 0) > 0 && (
        <details>
          <summary style={{ cursor: "pointer", margin: "8px 0" }}>
            Rework queue (all jobs) — {rework.data!.length} row(s)
          </summary>
          <table>
            <thead>
              <tr>
                <th>Job / piece</th>
                <th>Stage</th>
                <th>Reason</th>
                <th className="num">Remaining</th>
              </tr>
            </thead>
            <tbody>
              {rework.data!.map((item) => (
                <tr key={item.workItemId}>
                  <td>
                    {item.jobRef} #{item.specGroupNumber}/S{item.serialNumber}{" "}
                    {item.position}
                  </td>
                  <td>{item.stageLabel || item.stage}</td>
                  <td>{item.notes || item.blockReason || "—"}</td>
                  <td className="num">{item.remaining}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </details>
      )}

      {(qc.data?.length ?? 0) > 0 && (
        <details>
          <summary style={{ cursor: "pointer", margin: "8px 0" }}>
            QC queue (all jobs) — {qc.data!.length} row(s)
          </summary>
          <table>
            <thead>
              <tr>
                <th>Job / piece</th>
                <th>Stage</th>
                <th>QC result</th>
                <th className="num">Qty done / total</th>
              </tr>
            </thead>
            <tbody>
              {qc.data!.map((item) => (
                <tr key={item.workItemId}>
                  <td>
                    {item.jobRef} #{item.specGroupNumber}/S{item.serialNumber}{" "}
                    {item.position}
                  </td>
                  <td>{item.stageLabel || item.stage}</td>
                  <td>{item.qcResult || "pending"}</td>
                  <td className="num">
                    {item.qtyDone}/{item.qty}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </details>
      )}

      {(pairing.data?.length ?? 0) > 0 && (
        <details>
          <summary style={{ cursor: "pointer", margin: "8px 0" }}>
            Pairing / assembly traceability — {pairing.data!.length} row(s)
          </summary>
          <table>
            <thead>
              <tr>
                <th>Job</th>
                <th>Pair</th>
                <th>Assembly</th>
                <th>Type</th>
                <th>Components</th>
              </tr>
            </thead>
            <tbody>
              {pairing.data!.map((row, index) => (
                <tr key={index}>
                  <td>{row.jobRef}</td>
                  <td className="sub">{row.pairId}</td>
                  <td>{row.assemblyLabel}</td>
                  <td>{row.assemblyType}</td>
                  <td className="sub">{row.components}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </details>
      )}

      {(route.data?.length ?? 0) > 0 && (
        <details>
          <summary style={{ cursor: "pointer", margin: "8px 0" }}>
            Route report — {route.data!.length} row(s)
          </summary>
          <table>
            <thead>
              <tr>
                <th>Job / piece</th>
                <th>Build</th>
                <th>Route</th>
                <th>Current stage</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {route.data!.map((row, index) => (
                <tr key={index}>
                  <td>
                    {row.jobRef} {row.pieceId}
                  </td>
                  <td>{row.buildType}</td>
                  <td>{row.route}</td>
                  <td>{row.currentStage}</td>
                  <td>
                    <StatusBadge value={row.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </details>
      )}
    </>
  );
}
