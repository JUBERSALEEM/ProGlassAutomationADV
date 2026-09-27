import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  getDeliveryAnalytics,
  getExecutiveSummary,
  getJoAnalytics,
  getProfitability,
  getProfitabilitySummary,
  traceRow,
} from "../api/endpoints";
import type { MixRow, ReportingFilterQuery } from "../api/types";
import { Card, Empty, ErrorNote, Spinner, date, money } from "../components/ui";

type Tab = "exec" | "profit" | "jo" | "delivery" | "trace";

/** Reports — the shared-filter read models (WPF Views/Reports family). */
export default function ReportsPage() {
  const [tab, setTab] = useState<Tab>("exec");

  // ONE shared filter (the WPF ReportingFilter): dates + text axes + search.
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [customer, setCustomer] = useState("");
  const [company, setCompany] = useState("");
  const [salesman, setSalesman] = useState("");
  const [project, setProject] = useState("");
  const [searchText, setSearchText] = useState("");

  const filter: ReportingFilterQuery = {
    dateFrom: dateFrom || undefined,
    dateTo: dateTo || undefined,
    customer: customer || undefined,
    company: company || undefined,
    salesman: salesman || undefined,
    project: project || undefined,
    searchText: searchText || undefined,
  };

  return (
    <Card
      title="Reports"
      actions={
        <div className="tabs">
          {(
            [
              ["exec", "Executive"],
              ["profit", "Profitability"],
              ["jo", "Job orders"],
              ["delivery", "Delivery"],
              ["trace", "Row trace"],
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
      {tab !== "trace" && (
        <div className="filters" style={{ marginBottom: 12 }}>
          <label>
            From
            <input type="date" value={dateFrom} onChange={(e) => setDateFrom(e.target.value)} />
          </label>
          <label>
            To
            <input type="date" value={dateTo} onChange={(e) => setDateTo(e.target.value)} />
          </label>
          <input
            placeholder="Customer"
            value={customer}
            onChange={(e) => setCustomer(e.target.value)}
          />
          <input
            placeholder="Company"
            value={company}
            onChange={(e) => setCompany(e.target.value)}
          />
          <input
            placeholder="Salesman"
            value={salesman}
            onChange={(e) => setSalesman(e.target.value)}
          />
          <input
            placeholder="Project"
            value={project}
            onChange={(e) => setProject(e.target.value)}
          />
          <input
            placeholder="Search text…"
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
          />
        </div>
      )}
      {tab === "exec" && <Executive filter={filter} />}
      {tab === "profit" && <Profitability filter={filter} />}
      {tab === "jo" && <JoAnalytics filter={filter} />}
      {tab === "delivery" && <DeliveryAnalyticsView filter={filter} />}
      {tab === "trace" && <Trace />}
    </Card>
  );
}

function Executive(props: { filter: ReportingFilterQuery }) {
  const query = useQuery({
    queryKey: ["report-exec", props.filter],
    queryFn: () => getExecutiveSummary(props.filter),
  });
  if (query.isLoading) return <Spinner />;
  if (query.error) return <ErrorNote error={query.error} />;
  const data = query.data!;
  if (!data.hasData) return <Empty label="No documents in this period." />;

  return (
    <>
      <p className="sub">
        {data.periodDescription} · generated {date(data.generatedAt)} — the order-to-cash
        cohort funnel with headline money, operational health and the mix.
      </p>
      <table>
        <thead>
          <tr>
            <th>Funnel stage</th>
            <th className="num">Count</th>
            <th className="num">Value</th>
            <th className="num">Share of quoted</th>
            <th className="num">Step conversion</th>
            <th>Flag</th>
          </tr>
        </thead>
        <tbody>
          {data.funnel.map((stage) => (
            <tr key={stage.label} className={stage.isBottleneck ? "void" : undefined}>
              <td>{stage.label}</td>
              <td className="num">{stage.count}</td>
              <td className="num">{money(stage.value)}</td>
              <td className="num">{stage.sharePct.toFixed(1)}%</td>
              <td className="num">{stage.stepConversionPct.toFixed(1)}%</td>
              <td>{stage.isBottleneck ? "⚠ bottleneck (<60%)" : ""}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="stats">
        <div className="stat">
          <span className="stat-label">Quoted</span>
          <span className="stat-value">{money(data.quotedValue)}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Invoiced</span>
          <span className="stat-value">{money(data.invoicedValue)}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Outstanding</span>
          <span className="stat-value">{money(data.outstandingValue)}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Revenue</span>
          <span className="stat-value">{money(data.revenue)}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Cost</span>
          <span className="stat-value">{money(data.cost)}</span>
        </div>
        <div className={`stat ${data.profit < 0 ? "loss" : ""}`}>
          <span className="stat-label">Profit (margin {data.marginPct.toFixed(1)}%)</span>
          <span className="stat-value">{money(data.profit)}</span>
        </div>
      </div>
      <p className="sub" style={{ marginTop: 10 }}>
        Health: {data.unconvertedQuotes} unconverted quotes · {data.uninvoicedConfirmed}{" "}
        confirmed-not-invoiced · {data.overdueInvoices} overdue invoices · delivery{" "}
        {data.deliveryCompletionPct.toFixed(1)}% complete · {data.criticalPendingQty} critical
        pending qty
        {data.hasCostGap
          ? ` · costing gap: ${data.invoicesWithoutCost} invoice(s) without Step 9`
          : ""}
        .
      </p>
      <MixTable title="Top customers" rows={data.topCustomers} />
      <MixTable title="Top specifications" rows={data.topSpecifications} />
    </>
  );
}

function MixTable(props: { title: string; rows: MixRow[] }) {
  if (props.rows.length === 0) return null;
  return (
    <>
      <h3 style={{ margin: "14px 0 6px" }}>{props.title}</h3>
      <table>
        <thead>
          <tr>
            <th>#</th>
            <th>Label</th>
            <th className="num">Count</th>
            <th className="num">Qty</th>
            <th className="num">Sqm</th>
            <th className="num">Value</th>
            <th className="num">Share</th>
          </tr>
        </thead>
        <tbody>
          {props.rows.map((row) => (
            <tr key={`${row.rank}-${row.label}`} className={row.isOtherBucket ? "void" : undefined}>
              <td>{row.rank}</td>
              <td>{row.label}</td>
              <td className="num">{row.count}</td>
              <td className="num">{row.qty}</td>
              <td className="num">{row.sqm.toFixed(2)}</td>
              <td className="num">{money(row.value)}</td>
              <td className="num">{row.sharePct.toFixed(1)}%</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}

function Profitability(props: { filter: ReportingFilterQuery }) {
  const summary = useQuery({
    queryKey: ["report-profit-summary", props.filter],
    queryFn: () => getProfitabilitySummary(props.filter),
  });
  const rows = useQuery({
    queryKey: ["report-profit", props.filter],
    queryFn: () => getProfitability({ ...props.filter, pageSize: 50 }),
  });
  if (rows.isLoading || summary.isLoading) return <Spinner />;
  if (rows.error) return <ErrorNote error={rows.error} />;
  const s = summary.data;

  return (
    <>
      {s && (
        <div className="stats">
          <div className="stat">
            <span className="stat-label">Revenue</span>
            <span className="stat-value">{money(s.totalRevenue)}</span>
          </div>
          <div className="stat">
            <span className="stat-label">Cost</span>
            <span className="stat-value">{money(s.totalCost)}</span>
          </div>
          <div className="stat">
            <span className="stat-label">Profit</span>
            <span className="stat-value">{money(s.totalProfit)}</span>
          </div>
          <div className="stat">
            <span className="stat-label">Avg margin</span>
            <span className="stat-value">{s.avgMargin.toFixed(1)}%</span>
          </div>
          <div className="stat">
            <span className="stat-label">Costed / missing</span>
            <span className="stat-value">
              {s.invoicesWithCost} / {s.invoicesWithoutCost}
            </span>
          </div>
        </div>
      )}
      {s && s.invoicesWithoutCost > 0 && (
        <p className="sub" style={{ marginTop: 8 }}>
          Costing gap — {s.invoicesWithoutCost} invoice(s) without a Step 9 calculation.
        </p>
      )}
      {(rows.data?.items.length ?? 0) === 0 ? (
        <Empty label="No proformas in this period." />
      ) : (
        <table style={{ marginTop: 12 }}>
          <thead>
            <tr>
              <th>Proforma</th>
              <th>Customer</th>
              <th>Status</th>
              <th className="num">Revenue</th>
              <th className="num">Cost</th>
              <th className="num">Glass</th>
              <th className="num">Production</th>
              <th className="num">Profit</th>
              <th className="num">Margin</th>
              <th className="num">Qty / Sqm</th>
              <th>Cost source</th>
            </tr>
          </thead>
          <tbody>
            {rows.data!.items.map((row) => (
              <tr key={row.key} className={!row.hasCostData ? "void" : undefined}>
                <td>{row.key}</td>
                <td>{row.name}</td>
                <td>{row.category}</td>
                <td className="num">{money(row.revenue)}</td>
                <td className="num">{row.hasCostData ? money(row.cost) : "—"}</td>
                <td className="num">{row.hasCostData ? money(row.glassCost) : "—"}</td>
                <td className="num">{row.hasCostData ? money(row.productionCost) : "—"}</td>
                <td className="num">{row.hasCostData ? money(row.profit) : "—"}</td>
                <td className="num">{row.hasCostData ? `${row.margin.toFixed(1)}%` : "—"}</td>
                <td className="num">
                  {row.qty} / {row.sqm.toFixed(2)}
                </td>
                <td>{row.costSource}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

function JoAnalytics(props: { filter: ReportingFilterQuery }) {
  const query = useQuery({
    queryKey: ["report-jo", props.filter],
    queryFn: () => getJoAnalytics(props.filter),
  });
  if (query.isLoading) return <Spinner />;
  if (query.error) return <ErrorNote error={query.error} />;
  const data = query.data!;
  return (
    <>
      <div className="stats">
        <div className="stat">
          <span className="stat-label">Job orders</span>
          <span className="stat-value">{data.totalJobOrders}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Qty</span>
          <span className="stat-value">{data.totalQty}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Sqm</span>
          <span className="stat-value">{data.totalSqm.toFixed(2)}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Value</span>
          <span className="stat-value">{money(data.totalValue)}</span>
        </div>
      </div>
      <p className="sub" style={{ marginTop: 8 }}>
        Dominant specification {data.dominantSpecification} ({data.topSpecSharePct.toFixed(1)}%) ·
        dominant colour {data.dominantColour}.
      </p>
      <MixTable title="Top customers" rows={data.topCustomers} />
      <MixTable title="Top specifications" rows={data.topSpecifications} />
      <MixTable title="Top colours" rows={data.topColours} />
      <MixTable title="Glass type mix" rows={data.glassTypeMix} />
      {data.throughput.length > 0 && (
        <>
          <h3 style={{ margin: "14px 0 6px" }}>Weekly throughput</h3>
          <table>
            <thead>
              <tr>
                <th>Week of</th>
                <th className="num">Job orders</th>
                <th className="num">Sqm</th>
              </tr>
            </thead>
            <tbody>
              {data.throughput.map((point) => (
                <tr key={point.weekStart}>
                  <td>{date(point.weekStart)}</td>
                  <td className="num">{point.count}</td>
                  <td className="num">{point.sqm.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </>
  );
}

function DeliveryAnalyticsView(props: { filter: ReportingFilterQuery }) {
  const query = useQuery({
    queryKey: ["report-delivery", props.filter],
    queryFn: () => getDeliveryAnalytics(props.filter),
  });
  if (query.isLoading) return <Spinner />;
  if (query.error) return <ErrorNote error={query.error} />;
  const data = query.data!;
  return (
    <>
      <div className="stats">
        <div className="stat">
          <span className="stat-label">Rows</span>
          <span className="stat-value">{data.totalRows}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Ordered qty</span>
          <span className="stat-value">{data.orderedQty}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Delivered qty</span>
          <span className="stat-value">{data.deliveredQty}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Pending qty</span>
          <span className="stat-value">{data.pendingQty}</span>
        </div>
        <div className={`stat ${data.criticalPendingQty > 0 ? "loss" : ""}`}>
          <span className="stat-label">Critical pending</span>
          <span className="stat-value">{data.criticalPendingQty}</span>
        </div>
      </div>
      {data.pendingAgeing.length > 0 && (
        <>
          <h3 style={{ margin: "14px 0 6px" }}>Pending ageing</h3>
          <table>
            <thead>
              <tr>
                <th>Bucket</th>
                <th>Days</th>
                <th className="num">Rows</th>
                <th className="num">Qty</th>
                <th className="num">Sqm</th>
                <th className="num">Share</th>
              </tr>
            </thead>
            <tbody>
              {data.pendingAgeing.map((bucket) => (
                <tr key={bucket.label} className={bucket.isCritical ? "void" : undefined}>
                  <td>{bucket.isCritical ? `⚠ ${bucket.label}` : bucket.label}</td>
                  <td>
                    {bucket.minDays}
                    {bucket.maxDays > 0 ? `–${bucket.maxDays}` : "+"}
                  </td>
                  <td className="num">{bucket.count}</td>
                  <td className="num">{bucket.qty}</td>
                  <td className="num">{bucket.sqm.toFixed(2)}</td>
                  <td className="num">{bucket.sharePct.toFixed(1)}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
      <MixTable title="Pending by customer" rows={data.pendingByCustomer} />
      <MixTable title="Pending by specification" rows={data.pendingBySpecification} />
      <MixTable title="Delivered by specification" rows={data.deliveredBySpecification} />
    </>
  );
}

function Trace() {
  const [rowId, setRowId] = useState("");
  const [submitted, setSubmitted] = useState("");

  const query = useQuery({
    queryKey: ["report-trace", submitted],
    queryFn: () => traceRow(submitted),
    enabled: submitted !== "",
    retry: false,
  });
  const trace = query.data;

  return (
    <>
      <p className="sub">
        The complete audit trail for one glass row — enter the composite row id
        ("PI-2026-0842-G1-S1"); a bare proforma number traces the first row.
      </p>
      <form
        className="filters"
        onSubmit={(e) => {
          e.preventDefault();
          setSubmitted(rowId.trim());
        }}
      >
        <input
          placeholder="Row id or proforma number…"
          value={rowId}
          onChange={(e) => setRowId(e.target.value)}
        />
        <button className="btn primary" type="submit">
          Trace
        </button>
      </form>
      {submitted && query.isLoading && <Spinner />}
      {query.error && <ErrorNote error={query.error} />}
      {trace && (
        <>
          <h3 style={{ margin: "12px 0 6px" }}>
            {trace.sourceRow.rowId} — {trace.sourceRow.specification}
          </h3>
          <p className="sub">
            {trace.sourceRow.glassRef} · {trace.sourceRow.w1}×{trace.sourceRow.h1}
            {trace.sourceRow.w2 || trace.sourceRow.h2
              ? ` / ${trace.sourceRow.w2}×${trace.sourceRow.h2}`
              : ""}{" "}
            · qty {trace.sourceRow.qty} · {money(trace.sourceRow.amount)}
          </p>
          <table>
            <thead>
              <tr>
                <th>Stage</th>
                <th>Document</th>
                <th>Status</th>
                <th className="num">Qty</th>
                <th className="num">Value</th>
                <th>Date</th>
                <th>Notes</th>
              </tr>
            </thead>
            <tbody>
              {trace.steps.map((step, index) => (
                <tr key={index}>
                  <td>{step.stage}</td>
                  <td>{step.documentNumber}</td>
                  <td>{step.status}</td>
                  <td className="num">{step.qty}</td>
                  <td className="num">{step.value ? money(step.value) : "—"}</td>
                  <td>{date(step.date)}</td>
                  <td>{step.notes}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="sub" style={{ marginTop: 10 }}>
            Reconciliation — ordered {trace.reconciliation.orderedQty} · job orders{" "}
            {trace.reconciliation.jobOrderQty} · delivered {trace.reconciliation.deliveredQty} ·
            remaining {trace.reconciliation.remainingQty} · invoiced{" "}
            {trace.reconciliation.invoicedQty} → {trace.reconciliation.deliveryState} /{" "}
            {trace.reconciliation.invoiceState}.
          </p>
        </>
      )}
    </>
  );
}
