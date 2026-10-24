/**
 * The desktop inbox reports where an inquiry has reached through
 * `update_customer_inquiry_status_v1`, which writes four `pipeline_*` columns on
 * `inquiry_customer_submissions`. Those columns are the shop's workflow state,
 * not the customer's `status`, and this module is the only place that turns them
 * into words.
 *
 * The status list has to match the check inside the function body. A value the
 * function would refuse is not a value this module should render confidently, so
 * `inquiryPipelineLabel` falls back to the raw string rather than guessing.
 */
export const INQUIRY_PIPELINE_STATUSES = ["prepared", "sent", "confirmed", "hold", "cancelled"] as const;

export type InquiryPipelineStatus = (typeof INQUIRY_PIPELINE_STATUSES)[number];

const LABELS: Record<InquiryPipelineStatus, string> = {
  prepared: "Prepared",
  sent: "Sent",
  confirmed: "Confirmed",
  hold: "On hold",
  cancelled: "Cancelled",
};

export function isInquiryPipelineStatus(value: unknown): value is InquiryPipelineStatus {
  return typeof value === "string" && (INQUIRY_PIPELINE_STATUSES as readonly string[]).includes(value);
}

/** A label for any value the database allowed to be written. */
export function inquiryPipelineLabel(value: string | null | undefined): string {
  if (!value) return "";
  return isInquiryPipelineStatus(value) ? LABELS[value] : value;
}

/** Empty string, never a placeholder glyph, so callers decide what to show. */
export function formatInquiryPipelineTime(
  value: string | null | undefined,
  locale?: string,
  timeZone = "Asia/Dubai",
): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short", timeZone }).format(date);
}

export type InquiryPipelineFields = {
  pipeline_status?: string | null;
  pipeline_note?: string | null;
  pipeline_updated_at?: string | null;
};

/** The desktop has never reported on this inquiry: render nothing at all. */
export function hasInquiryPipelineStatus(row: InquiryPipelineFields | null | undefined): boolean {
  return Boolean(row?.pipeline_status);
}

/** One line for a list row: status, then the note when there is one. */
export function inquiryPipelineSummary(row: InquiryPipelineFields, locale?: string): string {
  const status = inquiryPipelineLabel(row.pipeline_status);
  if (!status) return "";
  const parts = [status];
  const note = row.pipeline_note?.trim();
  if (note) parts.push(note);
  const at = formatInquiryPipelineTime(row.pipeline_updated_at, locale);
  if (at) parts.push(`updated ${at}`);
  return parts.join(", ");
}

/** Removes the internal workflow columns before a customer-facing response. */
export function withoutPipelineFields<T extends object>(row: T): T {
  const rest = { ...(row as Record<string, unknown>) };
  delete rest.pipeline_status;
  delete rest.pipeline_note;
  delete rest.pipeline_updated_at;
  delete rest.pipeline_updated_by;
  return rest as T;
}

/**
 * Where the desktop handoff stands, derived from the columns the acknowledgement
 * already writes: an accepted submission is eligible for the desktop queue, and
 * a delivered one has moved to `downloaded` with a timestamp. Nothing here reads
 * the delivery ledger, which stays closed to the browser on purpose.
 */
export type InquiryHandoffFields = {
  status?: string | null;
  downloaded_at?: string | null;
};

const HANDOFF_READY = "Ready for desktop";
const HANDOFF_DONE = "Handed to desktop";

export function inquiryHandoffLabel(row: InquiryHandoffFields | null | undefined): string {
  if (!row?.status) return "";
  if (row.status === "downloaded") return HANDOFF_DONE;
  if (row.status === "accepted") return HANDOFF_READY;
  return "";
}

export function isInquiryHandedOff(row: InquiryHandoffFields | null | undefined): boolean {
  return inquiryHandoffLabel(row) === HANDOFF_DONE;
}

/** One line for the review screen, empty when there is nothing to say yet. */
export function inquiryHandoffSummary(
  row: InquiryHandoffFields,
  locale?: string,
  timeZone = "Asia/Dubai",
): string {
  const label = inquiryHandoffLabel(row);
  if (!label) return "";
  const at = formatInquiryPipelineTime(row.downloaded_at, locale, timeZone);
  return at ? `${label}, ${at}` : label;
}
