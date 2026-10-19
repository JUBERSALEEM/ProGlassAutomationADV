import type { GlobalChatMessage, GlobalChatReceipt } from "@/types/global-chat";

export const GLOBAL_CHAT_PATH = "/chat";
export const GLOBAL_CHAT_MAX_BODY = 1000;
export const GLOBAL_CHAT_PAGE_SIZE = 100;

export function normalizeGlobalChatDraft(value: string): string {
  return value.normalize("NFKC").replace(/\r\n?/g, "\n").trim();
}

export function isGlobalChatDraftValid(value: string): boolean {
  const body = normalizeGlobalChatDraft(value);
  return body.length >= 1 && body.length <= GLOBAL_CHAT_MAX_BODY;
}

function messageTime(message: GlobalChatMessage): number {
  const time = new Date(message.created_at).getTime();
  return Number.isFinite(time) ? time : 0;
}

export function mergeGlobalChatMessages(
  existing: readonly GlobalChatMessage[],
  incoming: readonly GlobalChatMessage[],
): GlobalChatMessage[] {
  const byId = new Map<string, GlobalChatMessage>();
  for (const message of existing) byId.set(message.id, message);
  for (const message of incoming) {
    if (message?.id) byId.set(message.id, message);
  }
  return [...byId.values()].sort((a, b) => {
    const byTime = messageTime(a) - messageTime(b);
    return byTime !== 0 ? byTime : a.id.localeCompare(b.id);
  });
}

function isUnread(message: GlobalChatMessage, userId: string | null, lastReadAt: string | null): boolean {
  if (!userId || message.author_user_id === userId || message.deleted_at) return false;
  if (!lastReadAt) return true;
  return messageTime(message) > new Date(lastReadAt).getTime();
}

export function countUnreadGlobalChatMessages(
  messages: readonly GlobalChatMessage[],
  userId: string | null,
  lastReadAt: string | null,
): number {
  return messages.filter((message) => isUnread(message, userId, lastReadAt)).length;
}

export function firstUnreadGlobalChatMessageId(
  messages: readonly GlobalChatMessage[],
  userId: string | null,
  lastReadAt: string | null,
): string | null {
  return messages.find((message) => isUnread(message, userId, lastReadAt))?.id ?? null;
}

export function globalChatDisplayName(input: {
  email?: string | null;
  metadata?: Record<string, unknown> | null;
}): string {
  const fullName = typeof input.metadata?.full_name === "string" ? input.metadata.full_name.trim() : "";
  const name = typeof input.metadata?.name === "string" ? input.metadata.name.trim() : "";
  const emailName = input.email?.split("@")[0]?.trim() ?? "";
  return (fullName || name || emailName || "ProGlass user").replace(/\s+/g, " ").slice(0, 60);
}

export function authorInitials(displayName: string): string {
  const initials = displayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");
  return initials || "PG";
}

export function formatGlobalChatTime(value: string | null | undefined, locale?: string): string {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat(locale, { hour: "numeric", minute: "2-digit" }).format(date);
}

function sameLocalDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

export function formatGlobalChatDay(
  value: string | null | undefined,
  locale?: string,
  now: Date = new Date(),
): string {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  if (sameLocalDay(date, now)) return "Today";
  const yesterday = new Date(now);
  yesterday.setDate(now.getDate() - 1);
  if (sameLocalDay(date, yesterday)) return "Yesterday";
  return new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(date);
}

export function formatGlobalChatDateTime(value: string | null | undefined, locale?: string): string {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  const day = formatGlobalChatDay(value, locale);
  return `${day} · ${formatGlobalChatTime(value, locale)}`;
}

export function globalChatMessageExcerpt(body: string, maxLength = 180): string {
  const text = body.replace(/\s+/g, " ").trim();
  if (text.length <= maxLength) return text;
  return `${text.slice(0, Math.max(1, maxLength - 1)).trimEnd()}…`;
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
  for (const receipt of receipts) {
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
  for (const receipt of receipts) {
    if (!receipt?.message_id) continue;
    const rows = grouped.get(receipt.message_id) ?? [];
    const index = rows.findIndex((row) => row.user_id === receipt.user_id);
    if (index >= 0) rows[index] = receipt;
    else rows.push(receipt);
    grouped.set(receipt.message_id, rows);
  }
  return grouped;
}
