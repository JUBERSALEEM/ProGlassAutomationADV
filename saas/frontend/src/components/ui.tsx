/** Small shared UI atoms (no UI kit — one stylesheet, §Phase 28 scope). */
import type { ReactNode } from "react";

const STATUS_TONE: Record<string, string> = {
  // pieces / jobs share the SaaS status vocabulary
  Pending: "idle",
  InProgress: "busy",
  Completed: "ok",
  Paused: "idle",
  Rework: "warn",
  Rejected: "bad",
  Cancelled: "bad",
  QcPending: "busy",
  QcFailed: "bad",
  Draft: "idle",
  InProduction: "busy",
  ReadyForDelivery: "ok",
  Delivered: "ok",
  Closed: "ok",
  High: "warn",
  Normal: "idle",
  Low: "idle",
};

export function StatusBadge({ value }: { value: string }) {
  const tone = STATUS_TONE[value] ?? "idle";
  return <span className={`badge ${tone}`}>{value}</span>;
}

export function Card({
  title,
  children,
  actions,
}: {
  title?: string;
  children: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <section className="card">
      {(title || actions) && (
        <header>
          {title && <h2>{title}</h2>}
          {actions}
        </header>
      )}
      {children}
    </section>
  );
}

export function ErrorNote({ error }: { error: unknown }) {
  if (!error) return null;
  const text =
    error instanceof Error ? error.message : String(error);
  return <div className="note bad">{text}</div>;
}

export function Spinner({ label = "Loading…" }: { label?: string }) {
  return <div className="spinner">{label}</div>;
}

export function Empty({ label }: { label: string }) {
  return <div className="empty">{label}</div>;
}

export function money(value: number | null | undefined): string {
  if (value === null || value === undefined) return "—";
  return value.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

export function sqm(value: number | null | undefined): string {
  if (value === null || value === undefined) return "—";
  return `${value.toLocaleString(undefined, { maximumFractionDigits: 3 })} m²`;
}

export function date(value: string | null | undefined): string {
  if (!value) return "—";
  return new Date(value).toLocaleDateString();
}
