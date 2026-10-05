/**
 * The Job Order release gate is deliberately stricter than a display label:
 * source rows must be active and their status must be the exact Confirmed
 * value emitted by Excel Sync.
 */
export function isActiveConfirmedProforma(status: unknown, isActive: unknown): boolean {
  return status === "Confirmed" && isActive === true;
}
