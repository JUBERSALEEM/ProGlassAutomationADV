import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import {
  generateStatement,
  getVatReport,
  listCreditNotes,
  listTaxInvoices,
  recordPayment,
  transitionTaxInvoice,
} from "../api/endpoints";
import { Card, Empty, ErrorNote, Spinner, StatusBadge, date, money } from "../components/ui";
import { errorText } from "../auth/AuthContext";

type Tab = "tax" | "credit" | "vat" | "statement";

/** Fiscal — tax invoices, credit notes, the UAE VAT return, statements. */
export default function FiscalPage() {
  const [tab, setTab] = useState<Tab>("tax");
  return (
    <Card
      title="Fiscal"
      actions={
        <div className="tabs">
          {(
            [
              ["tax", "Tax invoices"],
              ["credit", "Credit notes"],
              ["vat", "VAT report"],
              ["statement", "Statement"],
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
      {tab === "tax" && <TaxInvoices />}
      {tab === "credit" && <CreditNotes />}
      {tab === "vat" && <VatReportView />}
      {tab === "statement" && <StatementView />}
    </Card>
  );
}

function TaxInvoices() {
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [includeVoid, setIncludeVoid] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ["tax-invoices", { search, status, includeVoid }],
    queryFn: () =>
      listTaxInvoices({
        search: search || undefined,
        status: status || undefined,
        includeVoid,
      }),
  });

  const after = (fn: () => Promise<unknown>) => async () => {
    try {
      await fn();
      setError(null);
    } catch (err) {
      setNotice(null);
      setError(errorText(err));
    }
  };

  return (
    <>
      <div className="filters">
        <input
          placeholder="Search invoice / customer / TRN…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="">All statuses</option>
          <option>Draft</option>
          <option>Issued</option>
          <option>PartiallyPaid</option>
          <option>Paid</option>
          <option>Overdue</option>
          <option>Cancelled</option>
        </select>
        <label className="check">
          <input
            type="checkbox"
            checked={includeVoid}
            onChange={(e) => setIncludeVoid(e.target.checked)}
          />
          Show void
        </label>
      </div>
      {notice && <div className="note ok">{notice}</div>}
      {error && <div className="note bad">{error}</div>}
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.length ?? 0) === 0 ? (
        <Empty label="No tax invoices match." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Invoice</th>
              <th>Customer</th>
              <th>Status</th>
              <th>Issued / due</th>
              <th className="num">VAT</th>
              <th className="num">Grand total</th>
              <th className="num">Paid</th>
              <th className="num">Balance</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {query.data!.map((inv) => (
              <tr key={inv.id} className={inv.isVoid ? "void" : undefined}>
                <td>
                  {inv.invoiceNo}
                  {inv.isVoid && <span className="sub"> void</span>}
                  {inv.deliveryNoteReference && (
                    <span className="sub"> · DN {inv.deliveryNoteReference}</span>
                  )}
                </td>
                <td>{inv.customerName}</td>
                <td>
                  <StatusBadge value={inv.status} />
                </td>
                <td>
                  {date(inv.issueDate)} → {date(inv.dueDate)}
                </td>
                <td className="num">{money(inv.vatAmount)}</td>
                <td className="num">
                  <strong>{money(inv.grandTotal)}</strong>
                </td>
                <td className="num">{money(inv.paidAmount)}</td>
                <td className="num">{money(inv.balanceAmount)}</td>
                <td className="actions">
                  {inv.status === "Draft" && !inv.isVoid && (
                    <button
                      className="btn small primary"
                      onClick={after(() => transitionTaxInvoice(inv.id, "Issued"))}
                    >
                      Issue
                    </button>
                  )}
                  {!inv.isVoid && inv.balanceAmount > 0 && (
                    <button
                      className="btn small"
                      onClick={after(async () => {
                        const raw = window.prompt("Payment amount:");
                        const amount = Number(raw);
                        if (!raw || !Number.isFinite(amount) || amount <= 0) return;
                        await recordPayment(inv.id, amount);
                        setNotice(`Payment recorded on ${inv.invoiceNo}.`);
                      })}
                    >
                      Record payment
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

function CreditNotes() {
  const query = useQuery({
    queryKey: ["credit-notes"],
    queryFn: () => listCreditNotes(),
  });
  return (
    <>
      <p className="sub">
        A credit note never exists on its own — it always corrects a tax invoice. It is
        created from the invoice's correction flow in the WPF; this view lists what was
        issued.
      </p>
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.length ?? 0) === 0 ? (
        <Empty label="No credit notes issued." />
      ) : (
        <table>
          <thead>
            <tr>
              <th>Credit note</th>
              <th>Corrects invoice</th>
              <th>Reason</th>
              <th>Status</th>
              <th className="num">Subtotal</th>
              <th className="num">VAT</th>
              <th className="num">Grand total</th>
              <th>Issued</th>
            </tr>
          </thead>
          <tbody>
            {query.data!.map((cn) => (
              <tr key={cn.id}>
                <td>{cn.creditNoteNo}</td>
                <td>{cn.taxInvoiceNo}</td>
                <td>{cn.reason || "—"}</td>
                <td>
                  <StatusBadge value={cn.status} />
                </td>
                <td className="num">{money(cn.subtotal)}</td>
                <td className="num">{money(cn.vatAmount)}</td>
                <td className="num">{money(cn.grandTotal)}</td>
                <td>{date(cn.issuedAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

function VatReportView() {
  const now = new Date();
  const defaultFrom = `${now.getFullYear()}-01-01`;
  const [from, setFrom] = useState(defaultFrom);
  const [to, setTo] = useState("");

  const query = useQuery({
    queryKey: ["vat-report", from, to],
    queryFn: () => getVatReport(from || undefined, to || undefined),
  });
  const report = query.data;

  return (
    <>
      <div className="filters">
        <label>
          From
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </label>
        <label>
          To
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        </label>
        <span className="sub" style={{ alignSelf: "center" }}>
          Defaults to the year to date (server default).
        </span>
      </div>
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : report ? (
        <>
          <p className="sub">
            UAE VAT return {date(report.from)} → {date(report.to)}: standard-rated output
            VAT minus credit-note adjustments (WPF VatSubmissionView core).
          </p>
          <div className="stats">
            <div className="stat">
              <span className="stat-label">Invoices</span>
              <span className="stat-value">{report.invoiceCount}</span>
            </div>
            <div className="stat">
              <span className="stat-label">Standard-rated supplies</span>
              <span className="stat-value">{money(report.standardRatedSupplies)}</span>
            </div>
            <div className="stat">
              <span className="stat-label">Output VAT</span>
              <span className="stat-value">{money(report.outputVat)}</span>
            </div>
            <div className="stat">
              <span className="stat-label">Credit notes</span>
              <span className="stat-value">{report.creditNoteCount}</span>
            </div>
            <div className="stat">
              <span className="stat-label">Credit-note VAT</span>
              <span className="stat-value">{money(report.creditNoteVat)}</span>
            </div>
            <div className="stat">
              <span className="stat-label">Net VAT payable</span>
              <span className="stat-value">{money(report.netVatPayable)}</span>
            </div>
          </div>
        </>
      ) : null}
    </>
  );
}

function StatementView() {
  const [customer, setCustomer] = useState("");
  const [company, setCompany] = useState("");
  const [project, setProject] = useState("");
  const [salesman, setSalesman] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [error, setError] = useState<string | null>(null);

  const generate = useMutation({
    mutationFn: () =>
      generateStatement({
        customer: customer || undefined,
        company: company || undefined,
        project: project || undefined,
        salesman: salesman || undefined,
        from: from || undefined,
        to: to || undefined,
      }),
    onError: (err) => setError(errorText(err)),
  });

  const statement = generate.data;

  return (
    <>
      <p className="sub">
        The customer statement — the ledger of invoices (debits) and
        payments/applied credit notes (credits) with a running balance. At least one
        filter axis is required.
      </p>
      <form
        className="create-form"
        onSubmit={(e) => {
          e.preventDefault();
          setError(null);
          generate.mutate();
        }}
      >
        <div className="form-grid">
          <label>
            Customer
            <input value={customer} onChange={(e) => setCustomer(e.target.value)} />
          </label>
          <label>
            Company
            <input value={company} onChange={(e) => setCompany(e.target.value)} />
          </label>
          <label>
            Project
            <input value={project} onChange={(e) => setProject(e.target.value)} />
          </label>
          <label>
            Salesman
            <input value={salesman} onChange={(e) => setSalesman(e.target.value)} />
          </label>
          <label>
            From
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </label>
          <label>
            To
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </label>
        </div>
        {error && <div className="note bad">{error}</div>}
        <div className="form-actions">
          <button className="btn primary" disabled={generate.isPending}>
            {generate.isPending ? "Generating…" : "Generate statement"}
          </button>
        </div>
      </form>

      {statement && (
        <>
          <h3 style={{ margin: "12px 0 4px" }}>
            {statement.statementNo} — {statement.customer}
          </h3>
          <p className="sub">
            {statement.title} · {date(statement.from)} → {date(statement.to)}
            {statement.company ? ` · ${statement.company}` : ""}
          </p>
          <table>
            <thead>
              <tr>
                <th>Document</th>
                <th>Date</th>
                <th>Description</th>
                <th className="num">Debit</th>
                <th className="num">Credit</th>
                <th className="num">Balance</th>
              </tr>
            </thead>
            <tbody>
              {statement.lines.map((line, index) => (
                <tr key={index}>
                  <td>{line.documentNo}</td>
                  <td>{date(line.documentDate)}</td>
                  <td>{line.description}</td>
                  <td className="num">{line.debit ? money(line.debit) : "—"}</td>
                  <td className="num">{line.credit ? money(line.credit) : "—"}</td>
                  <td className="num">{money(line.runningBalance)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={3}>
                  <strong>Totals</strong>
                </td>
                <td className="num">{money(statement.totalDebit)}</td>
                <td className="num">{money(statement.totalCredit)}</td>
                <td className="num">
                  <strong>{money(statement.balance)}</strong>
                </td>
              </tr>
            </tfoot>
          </table>
        </>
      )}
    </>
  );
}
