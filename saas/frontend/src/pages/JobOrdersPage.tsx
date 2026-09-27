import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { listJobOrders } from "../api/endpoints";
import { Card, Empty, ErrorNote, Spinner, StatusBadge, money, date } from "../components/ui";

const PAGE_SIZE = 20;

/** Job orders — GET /api/v1/job-orders (search + status filter + paging). */
export default function JobOrdersPage() {
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [includeVoid, setIncludeVoid] = useState(false);
  const [page, setPage] = useState(1);

  const query = useQuery({
    queryKey: ["job-orders", { search, status, includeVoid, page }],
    queryFn: () =>
      listJobOrders({
        search: search || undefined,
        status: status || undefined,
        includeVoid,
        page,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: (prev) => prev,
  });

  const totalCount = query.data?.totalCount ?? 0;
  const pageCount = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <Card
      title="Job orders"
      actions={
        <div className="filters">
          <input
            placeholder="Search job / customer / project…"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
          <select
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All statuses</option>
            <option>Draft</option>
            <option>InProduction</option>
            <option>ReadyForDelivery</option>
            <option>Delivered</option>
            <option>Closed</option>
          </select>
          <label className="check">
            <input
              type="checkbox"
              checked={includeVoid}
              onChange={(e) => {
                setIncludeVoid(e.target.checked);
                setPage(1);
              }}
            />
            Show void
          </label>
        </div>
      }
    >
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.items.length ?? 0) === 0 ? (
        <Empty label="No job orders match." />
      ) : (
        <>
          <table>
            <thead>
              <tr>
                <th>Job</th>
                <th>PI</th>
                <th>Customer / Project</th>
                <th>Status</th>
                <th>Priority</th>
                <th>Delivery</th>
                <th className="num">Grand total</th>
              </tr>
            </thead>
            <tbody>
              {query.data!.items.map((job) => (
                <tr key={job.id} className={job.isVoid ? "void" : undefined}>
                  <td>
                    <Link to={`/job-orders/${job.id}`}>{job.jobOrderNo}</Link>
                    {job.isVoid && <span className="sub"> (void)</span>}
                  </td>
                  <td>{job.sourceProformaNo}</td>
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
          <div className="pager">
            <button
              className="btn ghost"
              disabled={page <= 1 || query.isFetching}
              onClick={() => setPage((p) => p - 1)}
            >
              ← Previous
            </button>
            <span>
              Page {page} / {pageCount} · {totalCount} job order(s)
            </span>
            <button
              className="btn ghost"
              disabled={page >= pageCount || query.isFetching}
              onClick={() => setPage((p) => p + 1)}
            >
              Next →
            </button>
          </div>
        </>
      )}
    </Card>
  );
}
