import type { GlobalChatMessage, GlobalChatReadState, GlobalChatReceipt } from "@/types/global-chat";

export const GLOBAL_CHAT_PATH = "/chat";
export const GLOBAL_CHAT_MAX_BODY = 1000;
export const GLOBAL_CHAT_PAGE_SIZE = 100;

type UnknownRecord = Record<string, unknown>;

function recordFrom(value: unknown): UnknownRecord | null {
  return typeof value === "object" && value !== null && !Array.isArray(value) ? (value as UnknownRecord) : null;
}

function firstRecordFrom(value: unknown): UnknownRecord | null {
  return Array.isArray(value) ? (value.map(recordFrom).find(Boolean) ?? null) : recordFrom(value);
}

function stringField(record: UnknownRecord, key: string, fallback = ""): string {
  const value = record[key];
  return typeof value === "string" ? value : fallback;
}

function requiredStringField(record: UnknownRecord, key: string): string | null {
  const value = stringField(record, key).trim();
  return value ? value : null;
}

function nullableStringField(record: UnknownRecord, key: string): string | null {
  const value = record[key];
  return typeof value === "string" && value ? value : null;
}

/**
 * Supabase REST rows, composite RPC rows, and Realtime payloads should all use
 * the database column names, but the browser must not turn an unexpected or
 * partial payload into a route crash. Normalize once at every boundary and
 * drop rows that lack the identifiers/timestamps needed to render safely.
 */
export function normalizeGlobalChatMessage(value: unknown): GlobalChatMessage | null {
  const record = recordFrom(value);
  if (!record) return null;
  const id = requiredStringField(record, "id");
  const authorUserId = requiredStringField(record, "author_user_id");
  const createdAt = requiredStringField(record, "created_at");
  if (!id || !authorUserId || !createdAt) return null;

  const deletedAt = nullableStringField(record, "deleted_at");
  return {
    id,
    author_user_id: authorUserId,
    author_display_name: stringField(record, "author_display_name", "ProGlass user").trim() || "ProGlass user",
    body: deletedAt ? "" : stringField(record, "body"),
    reply_to_message_id: nullableStringField(record, "reply_to_message_id"),
    deleted_at: deletedAt,
    deleted_by: nullableStringField(record, "deleted_by"),
    created_at: createdAt,
  };
}

export function normalizeGlobalChatMessages(value: unknown): GlobalChatMessage[] {
  if (!Array.isArray(value)) return [];
  return value.map(normalizeGlobalChatMessage).filter((message): message is GlobalChatMessage => Boolean(message));
}

export function normalizeGlobalChatReceipt(value: unknown): GlobalChatReceipt | null {
  const record = recordFrom(value);
  if (!record) return null;
  const messageId = requiredStringField(record, "message_id");
  const userId = requiredStringField(record, "user_id");
  if (!messageId || !userId) return null;
  return {
    message_id: messageId,
    user_id: userId,
    display_name: stringField(record, "display_name", "ProGlass user").trim() || "ProGlass user",
    delivered_at: nullableStringField(record, "delivered_at"),
    read_at: nullableStringField(record, "read_at"),
  };
}

export function normalizeGlobalChatReceipts(value: unknown): GlobalChatReceipt[] {
  if (!Array.isArray(value)) return [];
  return value.map(normalizeGlobalChatReceipt).filter((receipt): receipt is GlobalChatReceipt => Boolean(receipt));
}

export function normalizeGlobalChatReadState(value: unknown): GlobalChatReadState | null {
  const record = firstRecordFrom(value);
  if (!record) return null;
  const userId = requiredStringField(record, "user_id");
  const lastReadAt = requiredStringField(record, "last_read_at");
  if (!userId || !lastReadAt) return null;
  return {
    user_id: userId,
    last_read_at: lastReadAt,
    updated_at: stringField(record, "updated_at", lastReadAt),
  };
}

export function normalizeGlobalChatDraft(value: string): string {
  const safeValue = typeof value === "string" ? value : "";
  return safeValue.normalize("NFKC").replace(/\r\n?/g, "\n").trim();
}

export function isGlobalChatDraftValid(value: string): boolean {
  const body = normalizeGlobalChatDraft(value);
  return body.length >= 1 && body.length <= GLOBAL_CHAT_MAX_BODY;
}

function messageTime(message: GlobalChatMessage): number {
  const time = new Date(typeof message?.created_at === "string" ? message.created_at : "").getTime();
  return Number.isFinite(time) ? time : 0;
}

export function mergeGlobalChatMessages(
  existing: readonly GlobalChatMessage[],
  incoming: readonly GlobalChatMessage[],
): GlobalChatMessage[] {
  const byId = new Map<string, GlobalChatMessage>();
  for (const message of normalizeGlobalChatMessages(existing)) byId.set(message.id, message);
  for (const message of normalizeGlobalChatMessages(incoming)) byId.set(message.id, message);
  return [...byId.values()].sort((a, b) => {
    const byTime = messageTime(a) - messageTime(b);
    return byTime !== 0 ? byTime : a.id.localeCompare(b.id);
  });
}

function isUnread(message: GlobalChatMessage, userId: string | null, lastReadAt: string | null): boolean {
  if (!message || !userId || message.author_user_id === userId || message.deleted_at) return false;
  if (!lastReadAt) return true;
  const readTime = new Date(lastReadAt).getTime();
  return Number.isFinite(readTime) ? messageTime(message) > readTime : true;
}

export function countUnreadGlobalChatMessages(
  messages: readonly GlobalChatMessage[],
  userId: string | null,
  lastReadAt: string | null,
): number {
  return normalizeGlobalChatMessages(messages).filter((message) => isUnread(message, userId, lastReadAt)).length;
}

export function firstUnreadGlobalChatMessageId(
  messages: readonly GlobalChatMessage[],
  userId: string | null,
  lastReadAt: string | null,
): string | null {
  return (
    normalizeGlobalChatMessages(messages).find((message) => isUnread(message, userId, lastReadAt))?.id ?? null
  );
}

export function globalChatDisplayName(input: {
  email?: string | null;
  metadata?: Record<string, unknown> | null;
}): string {
  const metadata = recordFrom(input?.metadata);
  const fullName = typeof metadata?.full_name === "string" ? metadata.full_name.trim() : "";
  const name = typeof metadata?.name === "string" ? metadata.name.trim() : "";
  const email = typeof input?.email === "string" ? input.email : "";
  const emailName = email.split("@")[0]?.trim() ?? "";
  return (fullName || name || emailName || "ProGlass user").replace(/\s+/g, " ").slice(0, 60);
}

export function authorInitials(displayName: string): string {
  const initials = (typeof displayName === "string" ? displayName : "")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");
  return initials || "PG";
}

function dateFormatter(locale: string | undefined, options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat {
  try {
    return new Intl.DateTimeFormat(locale, options);
  } catch {
    return new Intl.DateTimeFormat("en-US", options);
  }
}

export function formatGlobalChatTime(value: string | null | undefined, locale?: string): string {
  if (typeof value !== "string" || !value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return dateFormatter(locale, { hour: "numeric", minute: "2-digit" }).format(date);
}

function sameLocalDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

export function formatGlobalChatDay(
  value: string | null | undefined,
  locale?: string,
  now: Date = new Date(),
): string {
  if (typeof value !== "string" || !value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  const safeNow = now instanceof Date && !Number.isNaN(now.getTime()) ? now : new Date();
  if (sameLocalDay(date, safeNow)) return "Today";
  const yesterday = new Date(safeNow);
  yesterday.setDate(safeNow.getDate() - 1);
  if (sameLocalDay(date, yesterday)) return "Yesterday";
  return dateFormatter(locale, { dateStyle: "medium" }).format(date);
}

export function formatGlobalChatDateTime(value: string | null | undefined, locale?: string): string {
  if (typeof value !== "string" || !value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  const day = formatGlobalChatDay(value, locale);
  return `${day} · ${formatGlobalChatTime(value, locale)}`;
}

export function globalChatMessageExcerpt(body: string, maxLength = 180): string {
  const text = (typeof body === "string" ? body : "").replace(/\s+/g, " ").trim();
  const safeMaxLength = Number.isFinite(maxLength) ? Math.max(1, Math.trunc(maxLength)) : 180;
  if (text.length <= safeMaxLength) return text;
  return `${text.slice(0, Math.max(1, safeMaxLength - 1)).trimEnd()}…`;
}

export type GlobalChatReceiptState = "sent" | "delivered" | "read";

export type GlobalChatReceiptSummary = {
  deliveredCount: number;
  readCount: number;
  state: GlobalChatReceiptState;
};

export function summarizeGlobalChatReceipts(
  receipts: readonly GlobalChatReceipt[],
): GlobalChatReceiptSummary {
  let deliveredCount = 0;
  let readCount = 0;
  for (const receipt of normalizeGlobalChatReceipts(receipts)) {
    if (receipt.read_at) readCount += 1;
    else if (receipt.delivered_at) deliveredCount += 1;
  }
  return {
    deliveredCount,
    readCount,
    state: readCount > 0 ? "read" : deliveredCount > 0 ? "delivered" : "sent",
  };
}

export function groupGlobalChatReceiptsByMessage(
  receipts: readonly GlobalChatReceipt[],
): Map<string, GlobalChatReceipt[]> {
  const grouped = new Map<string, GlobalChatReceipt[]>();
  for (const receipt of normalizeGlobalChatReceipts(receipts)) {
    const rows = grouped.get(receipt.message_id) ?? [];
    const index = rows.findIndex((row) => row?.user_id === receipt.user_id);
    if (index >= 0) rows[index] = receipt;
    else rows.push(receipt);
    grouped.set(receipt.message_id, rows);
  }
  return grouped;
}
