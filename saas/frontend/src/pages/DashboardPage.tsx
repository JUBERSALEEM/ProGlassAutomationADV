import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import {
  listJobOrders,
  listLowStockSheets,
  listSheets,
} from "../api/endpoints";
import { Card, Empty, ErrorNote, Spinner, StatusBadge, money, sqm, date } from "../components/ui";

/**
 * Dashboard — real numbers only: the first page of job orders, the
 * low-stock list, and the stock summary (the same endpoints the module
 * pages use; nothing is invented or aggregated beyond the DTOs).
 */
export default function DashboardPage() {
  const jobs = useQuery({
    queryKey: ["job-orders", { pageSize: 8 }],
    queryFn: () => listJobOrders({ pageSize: 8 }),
  });
  const lowStock = useQuery({
    queryKey: ["low-stock"],
    queryFn: listLowStockSheets,
  });
  const stock = useQuery({
    queryKey: ["sheets", { pageSize: 1 }],
    queryFn: () => listSheets({ pageSize: 1 }),
  });

  const activeJobs = jobs.data?.items.filter((j) => !j.isVoid) ?? [];
  const inProduction = activeJobs.filter((j) => j.status === "InProduction");

  return (
    <div className="stack">
      <div className="stats">
        <div className="stat">
          <div className="stat-value">{jobs.data?.totalCount ?? "—"}</div>
          <div className="stat-label">Job orders</div>
        </div>
        <div className="stat">
          <div className="stat-value">{inProduction.length}</div>
          <div className="stat-label">In production (first page)</div>
        </div>
        <div className="stat">
          <div className="stat-value">{lowStock.data?.length ?? "—"}</div>
          <div className="stat-label">Low-stock sheets</div>
        </div>
        <div className="stat">
          <div className="stat-value">{stock.data?.totalCount ?? "—"}</div>
          <div className="stat-label">Glass sheet types</div>
        </div>
      </div>

      <Card
        title="Recent job orders"
        actions={
          <Link className="btn ghost" to="/job-orders">
            All job orders →
          </Link>
        }
      >
        <ErrorNote error={jobs.error} />
        {jobs.isLoading ? (
          <Spinner />
        ) : (jobs.data?.items.length ?? 0) === 0 ? (
          <Empty label="No job orders yet — convert a proforma invoice first." />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Job</th>
                <th>Customer / Project</th>
                <th>Status</th>
                <th>Priority</th>
                <th>Delivery</th>
                <th className="num">Grand total</th>
              </tr>
            </thead>
            <tbody>
              {jobs.data!.items.map((job) => (
                <tr key={job.id}>
                  <td>
                    <Link to={`/job-orders/${job.id}`}>{job.jobOrderNo}</Link>
                  </td>
                  <td>
                    <div>{job.customerName}</div>
                    <div className="sub">{job.projectName}</div>
                  </td>
                  <td>
                    <StatusBadge value={job.status} />
                  </td>
                  <td>
                    <StatusBadge value={job.priority} />
                  </td>
                  <td>{date(job.deliveryDate)}</td>
                  <td className="num">{money(job.grandTotalVatInclusive)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      <Card title="Low stock">
        <ErrorNote error={lowStock.error} />
        {lowStock.isLoading ? (
          <Spinner />
        ) : (lowStock.data?.length ?? 0) === 0 ? (
          <Empty label="No sheet types below the low-stock threshold." />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Sheet</th>
                <th className="num">Balance (sheets)</th>
                <th className="num">Balance (m²)</th>
              </tr>
            </thead>
            <tbody>
              {lowStock.data!.map((sheet) => (
                <tr key={sheet.id}>
                  <td>
                    {sheet.color} {sheet.thicknessMm}mm {sheet.category} —{" "}
                    {sheet.widthMm}×{sheet.heightMm}
                  </td>
                  <td className="num">{sheet.balance}</td>
                  <td className="num">{sqm(sheet.totalSqmBalance)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </div>
  );
}
