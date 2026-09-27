import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  deleteClient,
  deleteCompany,
  deleteProject,
  deleteSalesman,
  listClients,
  listCompanies,
  listProjects,
  listSalesmen,
  saveClient,
  saveCompany,
  saveProject,
  saveSalesman,
  updateClient,
  updateCompany,
  updateProject,
  updateSalesman,
} from "../api/endpoints";
import type { Client, Company, Project, Salesman } from "../api/types";
import { Card, Empty, ErrorNote, Spinner, date } from "../components/ui";
import { errorText } from "../auth/AuthContext";

/**
 * Master data — companies, clients, projects, salesmen. The list/editor
 * mirrors the WPF master-data views: POST is the natural-key upsert (a
 * different key = a different row), PUT edits by id, DELETE is the
 * audited delete. Usage counts show what the documents reference.
 */
export default function MasterDataPage() {
  const [kind, setKind] = useState<"company" | "client" | "project" | "salesman">("company");
  return (
    <Card
      title="Master data"
      actions={
        <div className="tabs">
          {(
            [
              ["company", "Companies"],
              ["client", "Clients"],
              ["project", "Projects"],
              ["salesman", "Salesmen"],
            ] as ["company" | "client" | "project" | "salesman", string][]
          ).map(([key, label]) => (
            <button
              key={key}
              className={`tab ${kind === key ? "active" : ""}`}
              onClick={() => setKind(key)}
            >
              {label}
            </button>
          ))}
        </div>
      }
    >
      {kind === "company" && <CompanyTab />}
      {kind === "client" && <ClientTab />}
      {kind === "project" && <ProjectTab />}
      {kind === "salesman" && <SalesmanTab />}
    </Card>
  );
}

function useInvalidate(kind: string) {
  const queryClient = useQueryClient();
  return () =>
    queryClient.invalidateQueries({ queryKey: ["master-data", kind] });
}

function ClientPicker(props: { value: string; onChange: (id: string) => void }) {
  const clients = useQuery({
    queryKey: ["master-data", "client", { pageSize: 100 }],
    queryFn: () => listClients({ pageSize: 100 }),
  });
  return (
    <select value={props.value} onChange={(e) => props.onChange(e.target.value)}>
      <option value="">— none —</option>
      {(clients.data?.items ?? []).map((client) => (
        <option key={client.id} value={client.id}>
          {client.customerName}
        </option>
      ))}
    </select>
  );
}

// ── shared editor plumbing ───────────────────────────────────────────

function useForm<T extends Record<string, unknown>>(initial: T) {
  const [values, setValues] = useState<T>(initial);
  const set = (key: keyof T, value: unknown) =>
    setValues((prev) => ({ ...prev, [key]: value }));
  return { values, set, reset: (next: T) => setValues(next) };
}

function EditorShell(props: {
  title: string;
  error: string | null;
  pending: boolean;
  onClose: () => void;
  onSubmit: () => void;
  children: React.ReactNode;
}) {
  return (
    <form
      className="create-form"
      onSubmit={(e) => {
        e.preventDefault();
        props.onSubmit();
      }}
    >
      <h3>{props.title}</h3>
      <div className="form-grid">{props.children}</div>
      {props.error && <div className="note bad">{props.error}</div>}
      <div className="form-actions">
        <button type="button" className="btn ghost" onClick={props.onClose}>
          Cancel
        </button>
        <button type="submit" className="btn primary" disabled={props.pending}>
          {props.pending ? "Saving…" : "Save"}
        </button>
      </div>
    </form>
  );
}

function Field(props: { label: string; children: React.ReactNode }) {
  return (
    <label>
      {props.label}
      {props.children}
    </label>
  );
}

// ── companies ────────────────────────────────────────────────────────

const LOGO_LAYOUTS = ["None", "LeftPortrait", "TopPanel"];

function CompanyTab() {
  const [search, setSearch] = useState("");
  const query = useQuery({
    queryKey: ["master-data", "company", { search }],
    queryFn: () => listCompanies({ search: search || undefined }),
  });
  const invalidate = useInvalidate("company");
  const [editing, setEditing] = useState<Company | null>(null);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const empty = {
    name: "",
    trn: "",
    location: "",
    representative: "",
    representativeMobile: "",
    email: "",
    accountNumber: "",
    bankName: "",
    logoStorageKey: "",
    logoLayout: "None",
    logoWidthMm: 0,
    logoHeightMm: 0,
    isDefault: false,
  };
  const form = useForm<Record<string, unknown>>(
    editing ? { ...empty, ...stripDto(editing) } : empty,
  );

  const save = useMutation({
    mutationFn: () => {
      const body = { ...form.values };
      if (editing) return updateCompany(editing.id, body);
      return saveCompany(body);
    },
    onSuccess: async () => {
      setError(null);
      setEditing(null);
      setCreating(false);
      await invalidate();
    },
    onError: (err) => setError(errorText(err)),
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteCompany(id),
    onSuccess: invalidate,
    onError: (err) => setError(errorText(err)),
  });

  return (
    <>
      <div className="filters">
        <input
          placeholder="Search name / TRN…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <button
          className="btn primary"
          disabled={creating || editing !== null}
          onClick={() => {
            setCreating(true);
            setEditing(null);
            form.reset(empty);
          }}
        >
          New company
        </button>
      </div>
      {(creating || editing !== null) && (
        <EditorShell
          title={editing ? `Edit ${editing.name}` : "New company (natural-key upsert: same name + TRN reuses the row)"}
          error={error}
          pending={save.isPending}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          onSubmit={() => save.mutate()}
        >
          <Field label="Name">
            <input
              value={String(form.values.name ?? "")}
              onChange={(e) => form.set("name", e.target.value)}
              required
            />
          </Field>
          <Field label="TRN">
            <input
              value={String(form.values.trn ?? "")}
              onChange={(e) => form.set("trn", e.target.value)}
            />
          </Field>
          <Field label="Location">
            <input
              value={String(form.values.location ?? "")}
              onChange={(e) => form.set("location", e.target.value)}
            />
          </Field>
          <Field label="Representative">
            <input
              value={String(form.values.representative ?? "")}
              onChange={(e) => form.set("representative", e.target.value)}
            />
          </Field>
          <Field label="Representative mobile">
            <input
              value={String(form.values.representativeMobile ?? "")}
              onChange={(e) => form.set("representativeMobile", e.target.value)}
            />
          </Field>
          <Field label="Email">
            <input
              type="email"
              value={String(form.values.email ?? "")}
              onChange={(e) => form.set("email", e.target.value)}
            />
          </Field>
          <Field label="Account number">
            <input
              value={String(form.values.accountNumber ?? "")}
              onChange={(e) => form.set("accountNumber", e.target.value)}
            />
          </Field>
          <Field label="Bank name">
            <input
              value={String(form.values.bankName ?? "")}
              onChange={(e) => form.set("bankName", e.target.value)}
            />
          </Field>
          <Field label="Logo layout">
            <select
              value={String(form.values.logoLayout ?? "None")}
              onChange={(e) => form.set("logoLayout", e.target.value)}
            >
              {LOGO_LAYOUTS.map((layout) => (
                <option key={layout}>{layout}</option>
              ))}
            </select>
          </Field>
          <Field label="Logo width (mm)">
            <input
              type="number"
              step="0.1"
              value={Number(form.values.logoWidthMm ?? 0)}
              onChange={(e) => form.set("logoWidthMm", Number(e.target.value))}
            />
          </Field>
          <Field label="Logo height (mm)">
            <input
              type="number"
              step="0.1"
              value={Number(form.values.logoHeightMm ?? 0)}
              onChange={(e) => form.set("logoHeightMm", Number(e.target.value))}
            />
          </Field>
          <label className="check" style={{ alignSelf: "end" }}>
            <input
              type="checkbox"
              checked={Boolean(form.values.isDefault)}
              onChange={(e) => form.set("isDefault", e.target.checked)}
            />
            Default company
          </label>
        </EditorShell>
      )}
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.items.length ?? 0) === 0 ? (
        <Empty label="No companies yet." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>TRN</th>
              <th>Location</th>
              <th>Representative</th>
              <th>Bank</th>
              <th className="num">Used</th>
              <th>Created</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {query.data!.items.map((row) => (
              <tr key={row.id}>
                <td>
                  <strong>{row.name}</strong>
                  {row.isDefault && <span className="sub"> · default</span>}
                </td>
                <td>{row.trn || "—"}</td>
                <td>{row.location || "—"}</td>
                <td>
                  {row.representative || "—"}
                  {row.representativeMobile ? ` · ${row.representativeMobile}` : ""}
                </td>
                <td>{row.bankName || "—"}</td>
                <td className="num">{row.usageCount}</td>
                <td>{date(row.createdAtUtc)}</td>
                <td className="actions">
                  <button
                    className="btn small"
                    onClick={() => {
                      setEditing(row);
                      setCreating(false);
                      form.reset({ ...empty, ...stripDto(row) });
                    }}
                  >
                    Edit
                  </button>
                  <button
                    className="btn small bad"
                    disabled={remove.isPending}
                    onClick={() => {
                      if (window.confirm(`Delete company "${row.name}"?`))
                        remove.mutate(row.id);
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
      {error && !creating && editing === null && <div className="note bad">{error}</div>}
    </>
  );
}

/** DTO → editable fields (drops id/usage/audit columns). */
function stripDto(row: object): Record<string, unknown> {
  const {
    id: _id,
    usageCount: _usage,
    lastUsedAtUtc: _last,
    createdAtUtc: _created,
    ...rest
  } = row as Record<string, unknown>;
  return rest;
}

// ── clients ──────────────────────────────────────────────────────────

function ClientTab() {
  const [search, setSearch] = useState("");
  const query = useQuery({
    queryKey: ["master-data", "client", { search }],
    queryFn: () => listClients({ search: search || undefined }),
  });
  const invalidate = useInvalidate("client");
  const [editing, setEditing] = useState<Client | null>(null);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const empty = {
    customerName: "",
    customerTrn: "",
    referenceCode: "",
    salesRepresentative: "",
    documentPreparedBy: "",
    billingAddress: "",
    attentionExecutiveName: "",
    phone: "",
  };
  const form = useForm<Record<string, unknown>>(
    editing ? { ...empty, ...stripDto(editing) } : empty,
  );

  const save = useMutation({
    mutationFn: () =>
      editing ? updateClient(editing.id, form.values) : saveClient(form.values),
    onSuccess: async () => {
      setError(null);
      setEditing(null);
      setCreating(false);
      await invalidate();
    },
    onError: (err) => setError(errorText(err)),
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteClient(id),
    onSuccess: invalidate,
    onError: (err) => setError(errorText(err)),
  });

  const fields: [keyof typeof empty, string][] = [
    ["customerName", "Customer name"],
    ["customerTrn", "Customer TRN"],
    ["referenceCode", "Reference code"],
    ["salesRepresentative", "Sales representative"],
    ["documentPreparedBy", "Document prepared by"],
    ["attentionExecutiveName", "Attention (executive)"],
    ["phone", "Phone"],
  ];

  return (
    <>
      <div className="filters">
        <input
          placeholder="Search name / TRN…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <button
          className="btn primary"
          disabled={creating || editing !== null}
          onClick={() => {
            setCreating(true);
            setEditing(null);
            form.reset(empty);
          }}
        >
          New client
        </button>
      </div>
      {(creating || editing !== null) && (
        <EditorShell
          title={editing ? `Edit ${editing.customerName}` : "New client (name + TRN = the key; a different TRN is a different client)"}
          error={error}
          pending={save.isPending}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          onSubmit={() => save.mutate()}
        >
          {fields.map(([key, label]) => (
            <Field key={key} label={label}>
              <input
                value={String(form.values[key] ?? "")}
                onChange={(e) => form.set(key, e.target.value)}
                required={key === "customerName"}
              />
            </Field>
          ))}
          <Field label="Billing address">
            <input
              value={String(form.values.billingAddress ?? "")}
              onChange={(e) => form.set("billingAddress", e.target.value)}
            />
          </Field>
        </EditorShell>
      )}
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.items.length ?? 0) === 0 ? (
        <Empty label="No clients yet." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Customer</th>
              <th>TRN</th>
              <th>Reference</th>
              <th>Sales rep</th>
              <th>Phone</th>
              <th className="num">Used</th>
              <th>Created</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {query.data!.items.map((row) => (
              <tr key={row.id}>
                <td>
                  <strong>{row.customerName}</strong>
                </td>
                <td>{row.customerTrn || "—"}</td>
                <td>{row.referenceCode || "—"}</td>
                <td>{row.salesRepresentative || "—"}</td>
                <td>{row.phone || "—"}</td>
                <td className="num">{row.usageCount}</td>
                <td>{date(row.createdAtUtc)}</td>
                <td className="actions">
                  <button
                    className="btn small"
                    onClick={() => {
                      setEditing(row);
                      setCreating(false);
                      form.reset({ ...empty, ...stripDto(row) });
                    }}
                  >
                    Edit
                  </button>
                  <button
                    className="btn small bad"
                    disabled={remove.isPending}
                    onClick={() => {
                      if (window.confirm(`Delete client "${row.customerName}"?`))
                        remove.mutate(row.id);
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
      {error && !creating && editing === null && <div className="note bad">{error}</div>}
    </>
  );
}

// ── projects ─────────────────────────────────────────────────────────

function ProjectTab() {
  const [search, setSearch] = useState("");
  const query = useQuery({
    queryKey: ["master-data", "project", { search }],
    queryFn: () => listProjects({ search: search || undefined }),
  });
  const invalidate = useInvalidate("project");
  const [editing, setEditing] = useState<Project | null>(null);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const empty = {
    identifierName: "",
    contractNo: "",
    reference: "",
    siteDeliveryLocation: "",
    lpoNumber: "",
    customerId: "",
  };
  const form = useForm<Record<string, unknown>>(
    editing ? { ...empty, ...stripDto(editing) } : empty,
  );

  const save = useMutation({
    mutationFn: () =>
      editing
        ? updateProject(editing.id, form.values)
        : saveProject(form.values),
    onSuccess: async () => {
      setError(null);
      setEditing(null);
      setCreating(false);
      await invalidate();
    },
    onError: (err) => setError(errorText(err)),
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteProject(id),
    onSuccess: invalidate,
    onError: (err) => setError(errorText(err)),
  });

  const fields: [keyof typeof empty, string][] = [
    ["contractNo", "Contract no"],
    ["reference", "Reference"],
    ["siteDeliveryLocation", "Site delivery location"],
    ["lpoNumber", "LPO number"],
  ];

  return (
    <>
      <div className="filters">
        <input
          placeholder="Search…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <button
          className="btn primary"
          disabled={creating || editing !== null}
          onClick={() => {
            setCreating(true);
            setEditing(null);
            form.reset(empty);
          }}
        >
          New project
        </button>
      </div>
      {(creating || editing !== null) && (
        <EditorShell
          title={editing ? `Edit ${editing.identifierName}` : "New project"}
          error={error}
          pending={save.isPending}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          onSubmit={() => save.mutate()}
        >
          <Field label="Identifier name">
            <input
              value={String(form.values.identifierName ?? "")}
              onChange={(e) => form.set("identifierName", e.target.value)}
              required
            />
          </Field>
          {fields.map(([key, label]) => (
            <Field key={key} label={label}>
              <input
                value={String(form.values[key] ?? "")}
                onChange={(e) => form.set(key, e.target.value)}
              />
            </Field>
          ))}
          <Field label="Customer">
            <ClientPicker
              value={String(form.values.customerId ?? "")}
              onChange={(id) => form.set("customerId", id || null)}
            />
          </Field>
        </EditorShell>
      )}
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.items.length ?? 0) === 0 ? (
        <Empty label="No projects yet." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Project</th>
              <th>Contract</th>
              <th>LPO</th>
              <th>Site</th>
              <th className="num">Used</th>
              <th>Created</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {query.data!.items.map((row) => (
              <tr key={row.id}>
                <td>
                  <strong>{row.identifierName}</strong>
                </td>
                <td>{row.contractNo || "—"}</td>
                <td>{row.lpoNumber || "—"}</td>
                <td>{row.siteDeliveryLocation || "—"}</td>
                <td className="num">{row.usageCount}</td>
                <td>{date(row.createdAtUtc)}</td>
                <td className="actions">
                  <button
                    className="btn small"
                    onClick={() => {
                      setEditing(row);
                      setCreating(false);
                      form.reset({ ...empty, ...stripDto(row) });
                    }}
                  >
                    Edit
                  </button>
                  <button
                    className="btn small bad"
                    disabled={remove.isPending}
                    onClick={() => {
                      if (window.confirm(`Delete project "${row.identifierName}"?`))
                        remove.mutate(row.id);
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
      {error && !creating && editing === null && <div className="note bad">{error}</div>}
    </>
  );
}

// ── salesmen ─────────────────────────────────────────────────────────

function SalesmanTab() {
  const [search, setSearch] = useState("");
  const query = useQuery({
    queryKey: ["master-data", "salesman", { search }],
    queryFn: () => listSalesmen({ search: search || undefined }),
  });
  const invalidate = useInvalidate("salesman");
  const [editing, setEditing] = useState<Salesman | null>(null);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const empty = { name: "", department: "" };
  const form = useForm<Record<string, unknown>>(
    editing ? { ...empty, ...stripDto(editing) } : empty,
  );

  const save = useMutation({
    mutationFn: () =>
      editing ? updateSalesman(editing.id, form.values) : saveSalesman(form.values),
    onSuccess: async () => {
      setError(null);
      setEditing(null);
      setCreating(false);
      await invalidate();
    },
    onError: (err) => setError(errorText(err)),
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteSalesman(id),
    onSuccess: invalidate,
    onError: (err) => setError(errorText(err)),
  });

  return (
    <>
      <div className="filters">
        <input
          placeholder="Search…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <button
          className="btn primary"
          disabled={creating || editing !== null}
          onClick={() => {
            setCreating(true);
            setEditing(null);
            form.reset(empty);
          }}
        >
          New salesman
        </button>
      </div>
      {(creating || editing !== null) && (
        <EditorShell
          title={editing ? `Edit ${editing.name}` : "New salesman"}
          error={error}
          pending={save.isPending}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          onSubmit={() => save.mutate()}
        >
          <Field label="Name">
            <input
              value={String(form.values.name ?? "")}
              onChange={(e) => form.set("name", e.target.value)}
              required
            />
          </Field>
          <Field label="Department">
            <input
              value={String(form.values.department ?? "")}
              onChange={(e) => form.set("department", e.target.value)}
            />
          </Field>
        </EditorShell>
      )}
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.items.length ?? 0) === 0 ? (
        <Empty label="No salesmen yet." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Department</th>
              <th className="num">Used</th>
              <th>Created</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {query.data!.items.map((row) => (
              <tr key={row.id}>
                <td>
                  <strong>{row.name}</strong>
                </td>
                <td>{row.department || "—"}</td>
                <td className="num">{row.usageCount}</td>
                <td>{date(row.createdAtUtc)}</td>
                <td className="actions">
                  <button
                    className="btn small"
                    onClick={() => {
                      setEditing(row);
                      setCreating(false);
                      form.reset({ ...empty, ...stripDto(row) });
                    }}
                  >
                    Edit
                  </button>
                  <button
                    className="btn small bad"
                    disabled={remove.isPending}
                    onClick={() => {
                      if (window.confirm(`Delete salesman "${row.name}"?`))
                        remove.mutate(row.id);
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
      {error && !creating && editing === null && <div className="note bad">{error}</div>}
    </>
  );
}
