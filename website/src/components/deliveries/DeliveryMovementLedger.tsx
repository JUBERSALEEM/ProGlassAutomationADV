"use client";

import type { DeliveryRow } from "@/types/deliveries";
import { MovementStatusChip, MovementTypeChip } from "./DeliveryAtoms";

/**
 * A PI's movements, in order, with the arithmetic shown rather than implied.
 *
 * This is the component that answers the question the workbook cannot: "which
 * loads made up this balance?" Every movement is listed with its own quantity,
 * and the footer restates the identity the whole page is built on:
 *
 *     ordered − delivered + returned = balance
 *
 * The excluded movements — draft notes that never left, voided notes that were
 * cancelled — are listed rather than hidden, greyed and marked, because a
 * reader who can see a cancelled note and no longer counts it trusts the total
 * more than one who never knew it existed.
 */
export function MovementLedger({ row, compact = false }: { row: DeliveryRow; compact?: boolean }) {
  if (row.movements.length === 0) {
    return (
      <div className="rounded-xl border border-line-strong/50 bg-ink-900/70 px-4 py-3 text-xs text-text-muted">
        <span className="font-semibold text-text-primary">No deliveries recorded against this PI.</span>{" "}
        {row.ordered_qty > 0 ? (
          <>
            The full ordered quantity —{" "}
            <span className="font-mono text-brand-200">{row.ordered_qty.toLocaleString()} pcs</span> /{" "}
            <span className="font-mono text-brand-200">{row.ordered_sqm.toFixed(2)} m²</span> — is
            still owed. Record movements in
            <code className="mx-1 font-mono text-2xs text-brand-300">public.delivery_entries</code>
            to move this PI off awaiting dispatch.
          </>
        ) : (
          <>This PI carries movements but no confirmed quantity in the workbook.</>
        )}
      </div>
    );
  }

  const counted = row.movements.filter((movement) => movement.counts_toward_balance);
  const excluded = row.movements.filter((movement) => !movement.counts_toward_balance);
  const returnedQty = counted
    .filter((movement) => movement.delivery_type.trim().toLowerCase() === "return")
    .reduce((sum, movement) => sum + movement.delivered_qty, 0);

  return (
    <div className="rounded-xl border border-line-strong/50 bg-ink-900/70">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line/50 px-4 py-2">
        <span className="text-2xs font-semibold uppercase tracking-wider text-text-faint">
          Delivery ledger · {row.pi_number}
        </span>
        <span className="font-mono text-3xs text-text-faint">
          {row.delivery_count} counted
          {excluded.length > 0 ? ` · ${excluded.length} excluded` : ""}
        </span>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-left text-2xs">
          <thead>
            <tr className="border-b border-line/40 text-3xs uppercase tracking-wider text-text-faint">
              <th className="px-4 py-2 font-semibold">Delivery Note</th>
              <th className="px-3 py-2 font-semibold">Date</th>
              <th className="px-3 py-2 font-semibold">Type</th>
              <th className="px-3 py-2 font-semibold">State</th>
              <th className="px-3 py-2 text-right font-semibold">Qty</th>
              <th className="px-3 py-2 text-right font-semibold">Area</th>
              {!compact && <th className="px-3 py-2 font-semibold">Driver · Vehicle</th>}
              <th className="px-3 py-2 font-semibold">Note</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line/30">
            {row.movements.map((movement) => (
              <tr
                key={String(movement.id)}
                className={movement.counts_toward_balance ? "" : "opacity-60"}
              >
                <td className="px-4 py-2 font-mono text-3xs text-brand-200">
                  {movement.delivery_note_ref ?? "—"}
                  {movement.sequence !== null && (
                    <span className="ml-1 text-text-faint">#{movement.sequence}</span>
                  )}
                </td>
                <td className="px-3 py-2 font-mono text-3xs text-text-muted">
                  {movement.delivery_date || "—"}
                  {movement.delivery_time ? (
                    <span className="ml-1 text-text-faint">{movement.delivery_time}</span>
                  ) : null}
                </td>
                <td className="px-3 py-2">
                  <MovementTypeChip movement={movement} />
                </td>
                <td className="px-3 py-2">
                  <MovementStatusChip movement={movement} />
                </td>
                <td
                  className={`px-3 py-2 text-right font-mono text-xs ${
                    movement.delivery_type.trim().toLowerCase() === "return"
                      ? "text-state-danger"
                      : "text-text-primary"
                  }`}
                >
                  {movement.delivery_type.trim().toLowerCase() === "return" ? "−" : "+"}
                  {Math.abs(movement.delivered_qty).toLocaleString()}
                </td>
                <td className="px-3 py-2 text-right font-mono text-3xs text-text-muted">
                  {Math.abs(movement.delivered_sqm).toFixed(2)} m²
                </td>
                {!compact && (
                  <td className="px-3 py-2 text-3xs text-text-muted">
                    {[movement.driver_name, movement.vehicle_number].filter(Boolean).join(" · ") || "—"}
                  </td>
                )}
                <td className="max-w-[220px] px-3 py-2 text-3xs text-text-faint">
                  {movement.receiver_name ? (
                    <span className="text-text-muted">Received by {movement.receiver_name}. </span>
                  ) : null}
                  {movement.notes ?? ""}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-line/50 px-4 py-2 font-mono text-3xs text-text-faint">
        <span>
          Ordered {row.ordered_qty.toLocaleString()} pcs · Delivered {row.delivered_qty.toLocaleString()}{" "}
          pcs
          {returnedQty > 0 ? ` · Returned ${Math.abs(returnedQty).toLocaleString()} pcs` : ""} · Balance{" "}
          <strong className="text-text-primary">{row.balance_qty.toLocaleString()} pcs</strong>
        </span>
        <span>
          {row.balance_sqm.toFixed(2)} m² outstanding of {row.ordered_sqm.toFixed(2)} m² ordered
        </span>
      </div>
    </div>
  );
}
