import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { createOrOpenDn, listDeliveryNotes, listJobOrders } from "../api/endpoints";
import { Card, Empty, ErrorNote, Spinner, StatusBadge, date } from "../components/ui";
import { errorText } from "../auth/AuthContext";

const PAGE_SIZE = 20;

/** Delivery notes — list + the WPF's ONE create-or-open path. */
export default function DeliveryNotesPage() {
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [mastersOnly, setMastersOnly] = useState(false);
  const [includeVoid, setIncludeVoid] = useState(false);
  const [page, setPage] = useState(1);
  const [showCreate, setShowCreate] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: ["delivery-notes", { search, status, mastersOnly, includeVoid, page }],
    queryFn: () =>
      listDeliveryNotes({
        search: search || undefined,
        status: status || undefined,
        mastersOnly,
        includeVoid,
        page,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: (prev) => prev,
  });

  const jobs = useQuery({
    queryKey: ["job-orders", { pageSize: 100 }],
    queryFn: () => listJobOrders({ pageSize: 100 }),
    enabled: showCreate,
  });

  const createOrOpen = useMutation({
    mutationFn: (jobOrderId: string) => createOrOpenDn(jobOrderId),
    onSuccess: async (result) => {
      setError(null);
      setNotice(result.message);
      await queryClient.invalidateQueries({ queryKey: ["delivery-notes"] });
    },
    onError: (err) => {
      setNotice(null);
      setError(errorText(err));
    },
  });

  const totalCount = query.data?.totalCount ?? 0;
  const pageCount = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <Card
      title="Delivery notes"
      actions={
        <div className="filters">
          <input
            placeholder="Search DN / job / customer…"
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
            <option>Dispatching</option>
            <option>Dispatched</option>
            <option>Delivered</option>
            <option>Signed</option>
            <option>Received</option>
            <option>Cancelled</option>
          </select>
          <label className="check">
            <input
              type="checkbox"
              checked={mastersOnly}
              onChange={(e) => {
                setMastersOnly(e.target.checked);
                setPage(1);
              }}
            />
            Masters only
          </label>
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
          <button className="btn primary" onClick={() => setShowCreate((v) => !v)}>
            {showCreate ? "Close" : "Create / open"}
          </button>
        </div>
      }
    >
      {showCreate && (
        <div className="create-form">
          <h3>Create or open dispatching note</h3>
          <p className="sub">
            The WPF's one path: an existing open note is reopened, otherwise the next
            note is created from the master's remaining balance.
          </p>
          {jobs.isLoading ? (
            <Spinner />
          ) : (
            <div className="form-grid">
              <label>
                Job order
                <select
                  value=""
                  disabled={createOrOpen.isPending}
                  onChange={(e) => e.target.value && createOrOpen.mutate(e.target.value)}
                >
                  <option value="">Choose a job order…</option>
                  {(jobs.data?.items ?? []).map((job) => (
                    <option key={job.id} value={job.id}>
                      {job.jobOrderNo} — {job.customerName}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          )}
        </div>
      )}
      {notice && <div className="note ok">{notice}</div>}
      {error && <div className="note bad">{error}</div>}

      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.items.length ?? 0) === 0 ? (
        <Empty label="No delivery notes match." />
      ) : (
        <>
          <table>
            <thead>
              <tr>
                <th>DN number</th>
                <th>Job</th>
                <th>Customer</th>
                <th>Type</th>
                <th>Status</th>
                <th>Delivery date</th>
                <th className="num">Ordered</th>
                <th className="num">Delivered</th>
                <th className="num">Balance</th>
              </tr>
            </thead>
            <tbody>
              {query.data!.items.map((dn) => (
                <tr key={dn.id} className={dn.isVoid ? "void" : undefined}>
                  <td>
                    <Link to={`/delivery-notes/${dn.id}`}>{dn.deliveryNo}</Link>
                    {dn.isMasterDn && <span className="sub"> (master)</span>}
                    {dn.isVoid && <span className="sub"> void</span>}
                  </td>
                  <td>{dn.jobOrderNo}</td>
                  <td>{dn.customerName}</td>
                  <td>{dn.deliveryType}</td>
                  <td>
                    <StatusBadge value={dn.status} />
                  </td>
                  <td>{date(dn.deliveryDate)}</td>
                  <td className="num">{dn.totalOrderedQty}</td>
                  <td className="num">{dn.totalDeliveredQty}</td>
                  <td className="num">{dn.totalBalanceQty}</td>
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
              Page {page} / {pageCount} · {totalCount} note(s)
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
