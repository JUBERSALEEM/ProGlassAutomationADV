"use client";

/**
 * The two pieces of delivery vocabulary every deliveries component speaks:
 * the lifecycle badge and the progress bar.
 *
 * Both are derived from the ledger's own lifecycle value rather than from the
 * raw status string, because "part delivered" is not something the workbook
 * has a column for — it is what the movements add up to. Keeping the mapping
 * here means a part-delivered PI cannot be coloured green on one screen and
 * amber on another.
 */

import { Badge } from "@/components/ui/Badge";
import { lifecycleLabel } from "@/lib/delivery-ledger";
import type { DeliveryLifecycle, DeliveryMovement } from "@/types/deliveries";

const TONE: Record<DeliveryLifecycle, "neutral" | "accent" | "success" | "warning" | "danger"> = {
  awaiting_dispatch: "neutral",
  partial: "warning",
  complete: "success",
  over_delivered: "danger",
};

export function DeliveryStatusBadge({
  lifecycle,
  className,
}: {
  lifecycle: DeliveryLifecycle;
  className?: string;
}) {
  return (
    <Badge tone={TONE[lifecycle]} className={className}>
      {lifecycleLabel(lifecycle)}
    </Badge>
  );
}

/**
 * How far along a PI is, by quantity.
 *
 * The bar caps at 100% but the number beside it does not: a PI at 118% is a
 * fact the reader needs, not a rendering problem to hide. Over-delivery is
 * drawn in the danger tone so it cannot be mistaken for completion.
 */
export function DeliveryProgress({
  progressPct,
  lifecycle,
  compact = false,
}: {
  progressPct: number;
  lifecycle: DeliveryLifecycle;
  compact?: boolean;
}) {
  const width = Math.min(100, Math.max(0, progressPct));
  const barTone =
    lifecycle === "over_delivered"
      ? "bg-state-danger"
      : lifecycle === "complete"
        ? "bg-state-success"
        : lifecycle === "partial"
          ? "bg-state-warning"
          : "bg-line-strong";

  return (
    <div className={compact ? "w-24" : "w-full"}>
      <div className="flex items-center justify-between gap-2">
        <span className="font-mono text-2xs font-semibold text-text-primary">
          {progressPct.toFixed(0)}%
        </span>
        {!compact && (
          <span className="text-3xs text-text-faint">{lifecycleLabel(lifecycle)}</span>
        )}
      </div>
      <div className="mt-1 h-1.5 w-full overflow-hidden rounded-full bg-ink-950">
        <div
          className={`h-full rounded-full ${barTone} transition-all duration-300`}
          style={{ width: `${Math.max(width, width > 0 ? 3 : 0)}%` }}
        />
      </div>
    </div>
  );
}

/**
 * A movement's own state, in three words or fewer.
 *
 * `is_void` and the drafting statuses are shown as distinct tones because the
 * reader must be able to tell "this note never left" from "this note counted":
 * a draft and a dispatch look identical in the database if you only read the
 * quantity column.
 */
export function MovementStatusChip({ movement }: { movement: DeliveryMovement }) {
  if (movement.is_void) {
    return (
      <span className="rounded border border-state-danger/30 bg-state-danger/10 px-1.5 py-0.5 font-mono text-3xs text-state-danger">
        Void
      </span>
    );
  }
  if (!movement.counts_toward_balance) {
    return (
      <span className="rounded border border-line-strong bg-ink-850 px-1.5 py-0.5 font-mono text-3xs text-text-faint">
        {movement.status || "Draft"} · not dispatched
      </span>
    );
  }
  if (movement.signed_off) {
    return (
      <span className="rounded border border-state-success/30 bg-state-success/10 px-1.5 py-0.5 font-mono text-3xs text-state-success">
        {movement.status}
      </span>
    );
  }
  return (
    <span className="rounded border border-brand-400/30 bg-brand-400/10 px-1.5 py-0.5 font-mono text-3xs text-brand-200">
      {movement.status || "Dispatched"}
    </span>
  );
}

export function MovementTypeChip({ movement }: { movement: DeliveryMovement }) {
  const type = movement.delivery_type || "Original";
  const normalized = type.toLowerCase();
  const tone =
    normalized === "return"
      ? "border-state-danger/30 bg-state-danger/10 text-state-danger"
      : normalized === "rework" || normalized === "replacement" || normalized === "remake"
        ? "border-state-warning/30 bg-state-warning/10 text-state-warning"
        : "border-line-strong/70 bg-ink-850/80 text-text-muted";

  return (
    <span className={`rounded border px-1.5 py-0.5 font-mono text-3xs ${tone}`}>{type}</span>
  );
}
