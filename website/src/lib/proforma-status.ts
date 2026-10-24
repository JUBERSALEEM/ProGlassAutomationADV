/**
 * The approved PI status vocabulary, and the transitions that are actually
 * implemented.
 *
 * The words are the business words: a PI is being prepared (Draft), has been sent
 * to the customer (Sent), has been accepted through the existing business process
 * (Approved), has gone through the verified issuance workflow (Issued), or is no
 * longer live (Cancelled). "Void" is not a business status; the database migration
 * converted every legacy Void row to Cancelled, and this module normalises any
 * value still arriving from an older database so a page never shows the old word.
 *
 * What is implemented today is only what the website can honestly perform. There is
 * no send operation, no acceptance operation and no issuance operation on the
 * website, so no code here moves a document into Sent, Approved or Issued. Listing
 * them as allowed targets without an operation behind them would be a lie in a
 * type, so `IMPLEMENTED_TRANSITIONS` is deliberately empty and the vocabulary
 * exists for reading and for the operations that will be added with their contracts.
 */

export type ProformaStatus = "Draft" | "Sent" | "Approved" | "Issued" | "Cancelled";

export const PROFORMA_STATUSES: readonly ProformaStatus[] = [
  "Draft",
  "Sent",
  "Approved",
  "Issued",
  "Cancelled",
] as const;

/** How each stored value is shown. `Void` survives only as an alias. */
const LABELS: Record<string, ProformaStatus> = {
  Draft: "Draft",
  Sent: "Sent",
  Approved: "Approved",
  Issued: "Issued",
  Cancelled: "Cancelled",
  Void: "Cancelled",
};

/** A stored status, normalised to the approved vocabulary. */
export function normaliseProformaStatus(value: string | null | undefined): ProformaStatus {
  if (!value) return "Draft";
  return LABELS[value] ?? "Draft";
}

/** True when the stored value is the legacy word the database no longer allows. */
export function isLegacyStatus(value: string | null | undefined): boolean {
  return value === "Void";
}

/**
 * Transitions the website can perform today, keyed by the operation that performs
 * them. Empty on purpose: a transition is added here only together with the
 * operation that causes it, so nothing can mark a document as sent when it was
 * never sent.
 */
export const IMPLEMENTED_TRANSITIONS: ReadonlyArray<{ from: ProformaStatus; to: ProformaStatus; operation: string }> = [];

/**
 * Transitions that belong to operations the website does not have yet. Recorded so
 * the gap is explicit rather than implied by a missing button.
 */
export const PENDING_TRANSITIONS: ReadonlyArray<{ from: ProformaStatus; to: ProformaStatus; operation: string }> = [
  { from: "Draft", to: "Sent", operation: "send the PI to the customer" },
  { from: "Sent", to: "Approved", operation: "record customer acceptance" },
  { from: "Sent", to: "Issued", operation: "the verified issuance workflow" },
  { from: "Draft", to: "Cancelled", operation: "cancel the PI" },
  { from: "Sent", to: "Cancelled", operation: "cancel the PI" },
];
