import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createWorkstation,
  getDashboardKpis,
  getJobProgress,
  getProductionQueue,
  getQueuePressure,
  getWorkstationLoad,
  listWorkstations,
  seedWorkstations,
} from "../api/endpoints";
import { Card, Empty, ErrorNote, Spinner, StatusBadge, date } from "../components/ui";
import { errorText } from "../auth/AuthContext";

type Tab = "dashboard" | "queue" | "progress" | "workstations";

/**
 * The production control room (Phase 26a surfaces): factory KPIs, queue
 * pressure, workstation load, the filtered work queue with readiness and
 * block reasons, job-level progress and the workstation master.
 */
export default function ProductionControlPage() {
  const [tab, setTab] = useState<Tab>("dashboard");
  return (
    <Card
      title="Production control room"
      actions={
        <div className="tabs">
          {(
            [
              ["dashboard", "Dashboard"],
              ["queue", "Queue"],
              ["progress", "Progress"],
              ["workstations", "Workstations"],
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
      {tab === "dashboard" && <DashboardTab />}
      {tab === "queue" && <QueueTab />}
      {tab === "progress" && <ProgressTab />}
      {tab === "workstations" && <WorkstationsTab />}
    </Card>
  );
}

function DashboardTab() {
  const kpis = useQuery({ queryKey: ["prod-kpis"], queryFn: getDashboardKpis });
  const pressure = useQuery({ queryKey: ["prod-pressure"], queryFn: getQueuePressure });
  const load = useQuery({ queryKey: ["prod-ws-load"], queryFn: getWorkstationLoad });

  if (kpis.isLoading) return <Spinner />;
  if (kpis.error) return <ErrorNote error={kpis.error} />;
  const k = kpis.data!;

  return (
    <>
      <div className="stats">
        <div className="stat">
          <span className="stat-label">Jobs in production</span>
          <span className="stat-value">{k.jobsInProduction}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Ready to start</span>
          <span className="stat-value">{k.readyToStart}</span>
        </div>
        <div className="stat">
          <span className="stat-label">In progress</span>
          <span className="stat-value">{k.inProgress}</span>
        </div>
        <div className="stat">
          <span className="stat-label">On hold</span>
          <span className="stat-value">{k.onHold}</span>
        </div>
        <div className={`stat ${k.blocked > 0 ? "loss" : ""}`}>
          <span className="stat-label">Blocked</span>
          <span className="stat-value">{k.blocked}</span>
        </div>
        <div className="stat">
          <span className="stat-label">QC pending / failed</span>
          <span className="stat-value">
            {k.qcPending} / {k.qcFailed}
          </span>
        </div>
        <div className="stat">
          <span className="stat-label">Rework</span>
          <span className="stat-value">{k.rework}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Completed today</span>
          <span className="stat-value">{k.completedToday}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Ready for delivery</span>
          <span className="stat-value">{k.readyForDelivery}</span>
        </div>
        <div className={`stat ${k.overdueJobs > 0 ? "loss" : ""}`}>
          <span className="stat-label">Overdue / due soon</span>
          <span className="stat-value">
            {k.overdueJobs} / {k.dueSoon}
          </span>
        </div>
        <div className={`stat ${k.materialShortages > 0 ? "loss" : ""}`}>
          <span className="stat-label">Material shortages</span>
          <span className="stat-value">{k.materialShortages}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Bottleneck</span>
          <span className="stat-value" style={{ fontSize: 18 }}>
            {k.bottleneckProcess || "—"} ({k.bottleneckCount})
          </span>
        </div>
      </div>

      <ErrorNote error={pressure.error} />
      {pressure.data && pressure.data.length > 0 && (
        <>
          <h3 style={{ margin: "14px 0 6px" }}>Queue pressure by process</h3>
          <table>
            <thead>
              <tr>
                <th>Process</th>
                <th>Pressure</th>
                <th className="num">Ready</th>
                <th className="num">Blocked</th>
                <th className="num">Overdue</th>
                <th className="num">QC backlog</th>
                <th className="num">Rework</th>
                <th className="num">Avg wait (h)</th>
                <th className="num">Total</th>
              </tr>
            </thead>
            <tbody>
              {pressure.data.map((row) => (
                <tr key={row.processType}>
                  <td>{row.processName}</td>
                  <td>
                    <StatusBadge value={row.pressureLabel} />
                  </td>
                  <td className="num">{row.readyQueue}</td>
                  <td className="num">{row.blockedQueue}</td>
                  <td className="num">{row.overdueQueue}</td>
                  <td className="num">{row.qcBacklog}</td>
                  <td className="num">{row.reworkBacklog}</td>
                  <td className="num">{row.avgWaitHours.toFixed(1)}</td>
                  <td className="num">{row.total}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}

      <ErrorNote error={load.error} />
      {load.data && load.data.length > 0 && (
        <>
          <h3 style={{ margin: "14px 0 6px" }}>Workstation load</h3>
          <table>
            <thead>
              <tr>
                <th>Station</th>
                <th>Process</th>
                <th className="num">Assigned</th>
                <th className="num">Ready</th>
                <th className="num">In progress</th>
                <th className="num">Blocked</th>
              </tr>
            </thead>
            <tbody>
              {load.data.map((row) => (
                <tr key={row.workstationId}>
                  <td>
                    {row.code} — {row.name}
                  </td>
                  <td>{row.processName}</td>
                  <td className="num">{row.assignedCount}</td>
                  <td className="num">{row.readyCount}</td>
                  <td className="num">{row.inProgressCount}</td>
                  <td className="num">{row.blockedCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </>
  );
}

function QueueTab() {
  const [stage, setStage] = useState("");
  const [readiness, setReadiness] = useState("");
  const [search, setSearch] = useState("");
  const [includeCompleted, setIncludeCompleted] = useState(false);
  const [includeCancelled, setIncludeCancelled] = useState(false);

  const query = useQuery({
    queryKey: ["prod-queue", { stage, readiness, search, includeCompleted, includeCancelled }],
    queryFn: () =>
      getProductionQueue({
        stage: stage || undefined,
        readiness: readiness || undefined,
        search: search || undefined,
        includeCompleted,
        includeCancelled,
      }),
    placeholderData: (prev) => prev,
  });

  return (
    <>
      <div className="filters">
        <input
          placeholder="Search job / spec / key…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <input
          placeholder="Stage…"
          value={stage}
          onChange={(e) => setStage(e.target.value)}
        />
        <select value={readiness} onChange={(e) => setReadiness(e.target.value)}>
          <option value="">All readiness</option>
          <option>Ready</option>
          <option>Blocked</option>
          <option>InProgress</option>
          <option>Completed</option>
        </select>
        <label className="check">
          <input
            type="checkbox"
            checked={includeCompleted}
            onChange={(e) => setIncludeCompleted(e.target.checked)}
          />
          Show completed
        </label>
        <label className="check">
          <input
            type="checkbox"
            checked={includeCancelled}
            onChange={(e) => setIncludeCancelled(e.target.checked)}
          />
          Show cancelled
        </label>
      </div>
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.items.length ?? 0) === 0 ? (
        <Empty label="Queue is empty for these filters." />
      ) : (
        <>
          <p className="sub">{query.data!.total} work item(s).</p>
          <table>
            <thead>
              <tr>
                <th>Job / piece</th>
                <th>Customer</th>
                <th>Stage</th>
                <th>Readiness</th>
                <th className="num">Qty done / total</th>
                <th>Priority / due</th>
                <th>Station / operator</th>
                <th>Flags</th>
              </tr>
            </thead>
            <tbody>
              {query.data!.items.map((item) => (
                <tr
                  key={item.workItemId}
                  className={
                    item.isCancelled || item.isOnHold || item.hasMaterialShortage
                      ? "void"
                      : undefined
                  }
                >
                  <td>
                    <strong>{item.jobRef}</strong>
                    <span className="sub">
                      {" "}
                      #{item.specGroupNumber}/S{item.serialNumber} {item.position}
                    </span>
                  </td>
                  <td>{item.customerName}</td>
                  <td>
                    <StatusBadge value={item.stageLabel || item.stage} />
                  </td>
                  <td>
                    {item.readiness}
                    {item.blockReason ? (
                      <span className="sub"> — {item.blockReason}</span>
                    ) : null}
                  </td>
                  <td className="num">
                    {item.qtyDone}/{item.qty}
                  </td>
                  <td>
                    {item.priority}
                    {item.dueDate ? ` · ${date(item.dueDate)}` : ""}
                    {item.dueRisk ? <span className="sub"> ({item.dueRisk})</span> : null}
                  </td>
                  <td>
                    {item.workstationName || "—"}
                    {item.operator ? ` · ${item.operator}` : ""}
                  </td>
                  <td>
                    {item.isOnHold ? `HOLD (${item.holdReason}) ` : ""}
                    {item.isRework ? "rework " : ""}
                    {item.isFoc ? "FOC " : ""}
                    {item.hasMaterialShortage
                      ? `shortage ${item.materialShortage}`
                      : ""}
                    {item.isCancelled ? "cancelled" : ""}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </>
  );
}

function ProgressTab() {
  const query = useQuery({
    queryKey: ["prod-progress"],
    queryFn: () => getJobProgress(),
  });
  return (
    <>
      <p className="sub">
        Job-level progress — physical pieces; DGU components are never double-counted.
      </p>
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.length ?? 0) === 0 ? (
        <Empty label="No released production yet." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Job / spec</th>
              <th className="num">Required</th>
              <th className="num">Completed</th>
              <th className="num">In progress</th>
              <th className="num">Blocked</th>
              <th className="num">Rework / QC</th>
              <th className="num">On hold</th>
              <th className="num">Remaining</th>
              <th className="num">Progress</th>
            </tr>
          </thead>
          <tbody>
            {query.data!.map((row) => (
              <tr key={`${row.jobRef}-${row.specGroupNumber}-${row.serialNumber}`}>
                <td>
                  <strong>{row.jobRef}</strong>
                  <span className="sub">
                    {" "}
                    #{row.specGroupNumber}/S{row.serialNumber} {row.specName}
                  </span>
                </td>
                <td className="num">{row.required}</td>
                <td className="num">{row.completed}</td>
                <td className="num">{row.inProgress}</td>
                <td className="num">{row.blocked}</td>
                <td className="num">
                  {row.rework} / {row.qcPending}
                </td>
                <td className="num">{row.onHold}</td>
                <td className="num">{row.remaining}</td>
                <td className="num">{row.progressPct.toFixed(1)}%</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

function WorkstationsTab() {
  const [showCreate, setShowCreate] = useState(false);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [processType, setProcessType] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const list = useQuery({
    queryKey: ["prod-workstations"],
    queryFn: () => listWorkstations(),
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["prod-workstations"] });

  const seed = useMutation({
    mutationFn: seedWorkstations,
    onSuccess: async () => {
      setError(null);
      setNotice("Default stations seeded (only when none existed).");
      await invalidate();
    },
    onError: (err) => setError(errorText(err)),
  });

  const create = useMutation({
    mutationFn: () =>
      createWorkstation({
        code,
        name: name || undefined,
        processType: Number(processType),
        description: description || undefined,
      }),
    onSuccess: async (station) => {
      setError(null);
      setNotice(`Station ${station.code} saved.`);
      setShowCreate(false);
      setCode("");
      setName("");
      setProcessType("");
      setDescription("");
      await invalidate();
    },
    onError: (err) => setError(errorText(err)),
  });

  return (
    <>
      <div className="filters">
        <button
          className="btn primary"
          disabled={showCreate}
          onClick={() => setShowCreate(true)}
        >
          New station
        </button>
        <button className="btn" disabled={seed.isPending} onClick={() => seed.mutate()}>
          {seed.isPending ? "Seeding…" : "Seed the ten defaults"}
        </button>
      </div>
      {notice && <div className="note ok">{notice}</div>}
      {error && <div className="note bad">{error}</div>}
      {showCreate && (
        <form
          className="create-form"
          onSubmit={(e) => {
            e.preventDefault();
            create.mutate();
          }}
        >
          <h3>New workstation (idempotent on the code)</h3>
          <div className="form-grid">
            <label>
              Code *
              <input value={code} onChange={(e) => setCode(e.target.value)} required />
            </label>
            <label>
              Name
              <input value={name} onChange={(e) => setName(e.target.value)} />
            </label>
            <label>
              Process type (int) *
              <input
                type="number"
                value={processType}
                onChange={(e) => setProcessType(e.target.value)}
                required
              />
            </label>
            <label>
              Description
              <input
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />
            </label>
          </div>
          <div className="form-actions">
            <button type="button" className="btn ghost" onClick={() => setShowCreate(false)}>
              Cancel
            </button>
            <button type="submit" className="btn primary" disabled={create.isPending}>
              {create.isPending ? "Saving…" : "Save"}
            </button>
          </div>
        </form>
      )}
      <ErrorNote error={list.error} />
      {list.isLoading ? (
        <Spinner />
      ) : (list.data?.length ?? 0) === 0 ? (
        <Empty label="No workstations — seed the ten defaults." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Process</th>
              <th>Active</th>
              <th>Description</th>
            </tr>
          </thead>
          <tbody>
            {list.data!.map((station) => (
              <tr key={station.id} className={!station.isActive ? "void" : undefined}>
                <td>
                  <strong>{station.code}</strong>
                </td>
                <td>{station.name}</td>
                <td>
                  {station.processName} ({station.processType})
                </td>
                <td>{station.isActive ? "yes" : "no"}</td>
                <td>{station.description || "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
