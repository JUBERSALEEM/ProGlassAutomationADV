import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import {
  createProformaDraft,
  listClients,
  listCompanies,
  listProformas,
  listProjects,
  listSalesmen,
} from "../api/endpoints";
import { Card, Empty, ErrorNote, Spinner, StatusBadge, money, date } from "../components/ui";
import { errorText } from "../auth/AuthContext";

const PAGE_SIZE = 20;

/** Proforma invoices — GET /api/v1/proforma-invoices + create draft. */
export default function ProformaInvoicesPage() {
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [showCreate, setShowCreate] = useState(false);

  const query = useQuery({
    queryKey: ["proformas", { search, status, page }],
    queryFn: () =>
      listProformas({
        search: search || undefined,
        status: status || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: (prev) => prev,
  });

  const totalCount = query.data?.totalCount ?? 0;
  const pageCount = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <Card
      title="Proforma invoices"
      actions={
        <div className="filters">
          <input
            placeholder="Search PI no / client…"
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
            <option>PendingApproval</option>
            <option>Approved</option>
            <option>Sent</option>
            <option>Confirmed</option>
            <option>Rejected</option>
          </select>
          <button className="btn primary" onClick={() => setShowCreate((v) => !v)}>
            {showCreate ? "Close" : "+ New draft"}
          </button>
        </div>
      }
    >
      {showCreate && <CreateDraftForm onDone={() => setShowCreate(false)} />}

      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.items.length ?? 0) === 0 ? (
        <Empty label="No proforma invoices match." />
      ) : (
        <>
          <table>
            <thead>
              <tr>
                <th>PI number</th>
                <th>Rev</th>
                <th>Status</th>
                <th>Issued</th>
                <th>Valid until</th>
                <th className="num">Glass</th>
                <th className="num">Other</th>
                <th className="num">Grand (VAT incl.)</th>
              </tr>
            </thead>
            <tbody>
              {query.data!.items.map((pi) => (
                <tr key={pi.id} className={pi.isVoid ? "void" : undefined}>
                  <td>
                    <Link to={`/proformas/${pi.id}`}>{pi.proformaNo}</Link>
                    {pi.isVoid && <span className="sub"> (void)</span>}
                  </td>
                  <td>R{pi.revisionNumber}</td>
                  <td>
                    <StatusBadge value={pi.status} />
                  </td>
                  <td>{date(pi.issueDate)}</td>
                  <td>{date(pi.validUntil)}</td>
                  <td className="num">{money(pi.glassMaterialsSubtotal)}</td>
                  <td className="num">{money(pi.otherChargesSubtotal)}</td>
                  <td className="num">{money(pi.grandTotalVatInclusive)}</td>
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
              Page {page} / {pageCount} · {totalCount} invoice(s)
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

function CreateDraftForm({ onDone }: { onDone: () => void }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const companies = useQuery({ queryKey: ["pickers", "companies"], queryFn: listCompanies });
  const clients = useQuery({ queryKey: ["pickers", "clients"], queryFn: listClients });
  const projects = useQuery({ queryKey: ["pickers", "projects"], queryFn: listProjects });
  const salesmen = useQuery({ queryKey: ["pickers", "salesmen"], queryFn: listSalesmen });

  const [companyId, setCompanyId] = useState("");
  const [clientId, setClientId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [salesmanId, setSalesmanId] = useState("");
  const [issueDate, setIssueDate] = useState("");
  const [validUntil, setValidUntil] = useState("");
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () =>
      createProformaDraft({
        companyId: companyId || undefined,
        clientId: clientId || undefined,
        projectId: projectId || undefined,
        salesmanId: salesmanId || undefined,
        issueDate: issueDate || undefined,
        validUntil: validUntil || undefined,
      }),
    onSuccess: async (pi) => {
      await queryClient.invalidateQueries({ queryKey: ["proformas"] });
      onDone();
      navigate(`/proformas/${pi.id}`);
    },
    onError: (err) => setError(errorText(err)),
  });

  return (
    <form
      className="create-form"
      onSubmit={(e) => {
        e.preventDefault();
        create.mutate();
      }}
    >
      <h3>New draft</h3>
      <div className="form-grid">
        <label>
          Company
          <select value={companyId} onChange={(e) => setCompanyId(e.target.value)}>
            <option value="">—</option>
            {(companies.data?.items ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Client
          <select value={clientId} onChange={(e) => setClientId(e.target.value)}>
            <option value="">—</option>
            {(clients.data?.items ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                {c.customerName}
              </option>
            ))}
          </select>
        </label>
        <label>
          Project
          <select value={projectId} onChange={(e) => setProjectId(e.target.value)}>
            <option value="">—</option>
            {(projects.data?.items ?? []).map((p) => (
              <option key={p.id} value={p.id}>
                {p.identifierName}
              </option>
            ))}
          </select>
        </label>
        <label>
          Salesman
          <select value={salesmanId} onChange={(e) => setSalesmanId(e.target.value)}>
            <option value="">—</option>
            {(salesmen.data?.items ?? []).map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Issue date
          <input type="date" value={issueDate} onChange={(e) => setIssueDate(e.target.value)} />
        </label>
        <label>
          Valid until
          <input
            type="date"
            value={validUntil}
            onChange={(e) => setValidUntil(e.target.value)}
          />
        </label>
      </div>
      {error && <div className="note bad">{error}</div>}
      <div className="form-actions">
        <button type="button" className="btn ghost" onClick={onDone}>
          Cancel
        </button>
        <button type="submit" className="btn primary" disabled={create.isPending}>
          {create.isPending ? "Creating…" : "Create draft"}
        </button>
      </div>
    </form>
  );
}
