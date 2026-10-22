import {
  PRODUCTION_ROOM_ACTION_STATUSES,
  PRODUCTION_ROOM_CATEGORIES,
  type ProductionRoomActionStatus,
  type ProductionRoomCategory,
  type ProductionRoomMember,
  type ProductionRoomMessage,
  type ProductionRoomRead,
} from "@/types/production-room";

export const PRODUCTION_ROOM_PATH = "/production";
export const PRODUCTION_ROOM_MAX_BODY = 4000;
export const PRODUCTION_ROOM_PAGE_SIZE = 200;
export const PRODUCTION_ROOM_EDIT_WINDOW_MS = 30 * 60 * 1000;
export const PRODUCTION_ROOM_MEMBER_MAX_NAME = 120;
export const PRODUCTION_ROOM_MEMBER_MAX_DEPARTMENT = 80;

export type ProductionRoomCategoryMeta = {
  value: ProductionRoomCategory;
  label: string;
  short: string;
  /** Token class used for the category chip. Colour is never the only signal. */
  tone: string;
  description: string;
};

/**
 * Nine categories, each with a sentence that says what belongs in it. A room
 * that only offers labels invites the wrong post in the wrong thread; the
 * description is what makes the taxonomy usable at speed on a shop floor.
 */
export const PRODUCTION_ROOM_CATEGORY_META: readonly ProductionRoomCategoryMeta[] = [
  {
    value: "general_production",
    label: "General production",
    short: "General",
    tone: "border-line-strong text-text-secondary",
    description: "Routine shop-floor coordination that has no sharper category.",
  },
  {
    value: "urgent_blocker",
    label: "Urgent blocker",
    short: "Blocker",
    tone: "border-state-danger/50 text-state-danger",
    description: "Work that cannot proceed until someone acts today.",
  },
  {
    value: "material_shortage",
    label: "Material shortage",
    short: "Material",
    tone: "border-state-warning/50 text-state-warning",
    description: "Glass, hardware or consumables missing or short of the required quantity.",
  },
  {
    value: "machine_issue",
    label: "Machine issue",
    short: "Machine",
    tone: "border-state-warning/50 text-state-warning",
    description: "A machine down, degraded, or producing out of tolerance.",
  },
  {
    value: "quality_issue",
    label: "Quality issue",
    short: "Quality",
    tone: "border-state-warning/50 text-state-warning",
    description: "Scratch, chip, wrong size, wrong treatment or failed inspection.",
  },
  {
    value: "schedule_change",
    label: "Schedule change",
    short: "Schedule",
    tone: "border-brand-300/50 text-brand-300",
    description: "A delivery date, sequence or priority that has moved.",
  },
  {
    value: "dispatch_dependency",
    label: "Dispatch dependency",
    short: "Dispatch",
    tone: "border-brand-300/50 text-brand-300",
    description: "Waiting on transport, crating, a rack, or a customer collection slot.",
  },
  {
    value: "decision",
    label: "Decision",
    short: "Decision",
    tone: "border-emerald-300/50 text-emerald-300",
    description: "A ruling the team must be able to find later. Only top-level decisions can be pinned.",
  },
  {
    value: "action_item",
    label: "Action item",
    short: "Action",
    tone: "border-brand-200/50 text-brand-200",
    description: "One owner, one task, one due moment. Say who in the message.",
  },
];

export function productionRoomCategoryMeta(category: string): ProductionRoomCategoryMeta {
  return (
    PRODUCTION_ROOM_CATEGORY_META.find((meta) => meta.value === category) ??
    PRODUCTION_ROOM_CATEGORY_META[0]!
  );
}

export function isProductionRoomCategory(value: unknown): value is ProductionRoomCategory {
  return (
    typeof value === "string" &&
    (PRODUCTION_ROOM_CATEGORIES as readonly string[]).includes(value)
  );
}

type UnknownRecord = Record<string, unknown>;

function recordFrom(value: unknown): UnknownRecord | null {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as UnknownRecord)
    : null;
}

function text(record: UnknownRecord, key: string, fallback = ""): string {
  const value = record[key];
  return typeof value === "string" ? value : fallback;
}

function nullableText(record: UnknownRecord, key: string): string | null {
  const value = record[key];
  return typeof value === "string" && value ? value : null;
}

function numberOr(record: UnknownRecord, key: string, fallback: number): number {
  const value = record[key];
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

/**
 * Realtime payloads, PostgREST rows and RPC returns all arrive here. A row that
 * cannot be rendered safely is dropped rather than crashing the room, and a
 * category outside the database allowlist is normalised to the general
 * category instead of being trusted.
 */
export function normalizeProductionRoomMessage(value: unknown): ProductionRoomMessage | null {
  const record = recordFrom(value);
  if (!record) return null;
  const id = text(record, "id");
  const authorUserId = text(record, "author_user_id");
  const createdAt = text(record, "created_at");
  if (!id || !authorUserId || !createdAt) return null;
  const category = isProductionRoomCategory(record.category) ? record.category : "general_production";
  return {
    id,
    parent_id: nullableText(record, "parent_id"),
    author_user_id: authorUserId,
    category,
    body: text(record, "body"),
    is_pinned: record.is_pinned === true,
    pinned_by: nullableText(record, "pinned_by"),
    pinned_at: nullableText(record, "pinned_at"),
    created_at: createdAt,
    edited_at: nullableText(record, "edited_at"),
    edit_count: Math.max(0, Math.trunc(numberOr(record, "edit_count", 0))),
    deleted_by: nullableText(record, "deleted_by"),
    deleted_at: nullableText(record, "deleted_at"),
    action_owner_user_id: nullableText(record, "action_owner_user_id"),
    action_due_at: nullableText(record, "action_due_at"),
    action_status: isProductionRoomActionStatus(record.action_status) ? record.action_status : null,
  };
}

export function isProductionRoomActionStatus(value: unknown): value is ProductionRoomActionStatus {
  return (
    typeof value === "string" &&
    (PRODUCTION_ROOM_ACTION_STATUSES as readonly string[]).includes(value)
  );
}

export function normalizeProductionRoomMessages(value: unknown): ProductionRoomMessage[] {
  if (!Array.isArray(value)) return [];
  return value
    .map(normalizeProductionRoomMessage)
    .filter((message): message is ProductionRoomMessage => Boolean(message));
}

export function normalizeProductionRoomMember(value: unknown): ProductionRoomMember | null {
  const record = recordFrom(value);
  if (!record) return null;
  const userId = text(record, "user_id");
  const displayName = text(record, "display_name");
  if (!userId || !displayName) return null;
  return {
    user_id: userId,
    display_name: displayName,
    department: text(record, "department"),
    is_room_admin: record.is_room_admin === true,
    active: record.active !== false,
    created_at: text(record, "created_at"),
    updated_at: text(record, "updated_at"),
  };
}

export function normalizeProductionRoomMembers(value: unknown): ProductionRoomMember[] {
  if (!Array.isArray(value)) return [];
  return value
    .map(normalizeProductionRoomMember)
    .filter((member): member is ProductionRoomMember => Boolean(member));
}

export function normalizeProductionRoomRead(value: unknown): ProductionRoomRead | null {
  const record = Array.isArray(value) ? recordFrom(value[0]) : recordFrom(value);
  if (!record) return null;
  const userId = text(record, "user_id");
  const lastReadAt = text(record, "last_read_at");
  if (!userId || !lastReadAt) return null;
  return {
    user_id: userId,
    last_read_at: lastReadAt,
    last_read_message_id: nullableText(record, "last_read_message_id"),
    updated_at: text(record, "updated_at", lastReadAt),
  };
}

function timeOf(message: ProductionRoomMessage): number {
  const value = new Date(message.created_at).getTime();
  return Number.isFinite(value) ? value : 0;
}

export function mergeProductionRoomMessages(
  existing: readonly ProductionRoomMessage[],
  incoming: readonly ProductionRoomMessage[],
): ProductionRoomMessage[] {
  const byId = new Map<string, ProductionRoomMessage>();
  for (const message of normalizeProductionRoomMessages(existing)) byId.set(message.id, message);
  for (const message of normalizeProductionRoomMessages(incoming)) byId.set(message.id, message);
  return [...byId.values()].sort((a, b) => {
    const byTime = timeOf(a) - timeOf(b);
    return byTime !== 0 ? byTime : a.id.localeCompare(b.id);
  });
}

export type ProductionRoomThread = {
  root: ProductionRoomMessage;
  replies: ProductionRoomMessage[];
};

/**
 * One level of threading, mirroring the database: a reply cannot itself be
 * replied to, and a reply inherits its parent's category. Grouping keeps that
 * shape instead of inventing depth the table cannot store.
 */
export function groupProductionRoomThreads(
  messages: readonly ProductionRoomMessage[],
): ProductionRoomThread[] {
  const sorted = mergeProductionRoomMessages([], messages);
  const roots = new Map<string, ProductionRoomThread>();
  const orphans: ProductionRoomMessage[] = [];
  for (const message of sorted) {
    if (message.parent_id === null) roots.set(message.id, { root: message, replies: [] });
  }
  for (const message of sorted) {
    if (message.parent_id === null) continue;
    const thread = roots.get(message.parent_id);
    if (thread) thread.replies.push(message);
    else orphans.push(message);
  }
  // A reply whose parent is older than the loaded window still deserves to be
  // read, so it is shown as its own row rather than silently dropped.
  for (const orphan of orphans) roots.set(orphan.id, { root: orphan, replies: [] });
  return [...roots.values()].sort((a, b) => {
    const byTime = timeOf(a.root) - timeOf(b.root);
    return byTime !== 0 ? byTime : a.root.id.localeCompare(b.root.id);
  });
}

export function isProductionRoomDeleted(message: ProductionRoomMessage): boolean {
  return Boolean(message.deleted_at);
}

/** Mirrors `delete_production_room_message`: admin, or author inside 30 minutes. */
export function canDeleteProductionRoomMessage(
  message: ProductionRoomMessage,
  userId: string | null,
  isRoomAdmin: boolean,
  now: number = Date.now(),
): boolean {
  if (!userId || isProductionRoomDeleted(message)) return false;
  if (isRoomAdmin) return true;
  if (message.author_user_id !== userId) return false;
  return now - timeOf(message) < PRODUCTION_ROOM_EDIT_WINDOW_MS;
}

/** Mirrors `edit_production_room_message`: author only, inside 30 minutes. */
export function canEditProductionRoomMessage(
  message: ProductionRoomMessage,
  userId: string | null,
  now: number = Date.now(),
): boolean {
  if (!userId || isProductionRoomDeleted(message)) return false;
  if (message.author_user_id !== userId) return false;
  return now - timeOf(message) < PRODUCTION_ROOM_EDIT_WINDOW_MS;
}

/** Mirrors the pin check constraint: admin, top-level, decision, not deleted. */
export function canPinProductionRoomMessage(
  message: ProductionRoomMessage,
  isRoomAdmin: boolean,
): boolean {
  if (!isRoomAdmin || isProductionRoomDeleted(message)) return false;
  return message.parent_id === null && message.category === "decision";
}

export function pinnedProductionRoomDecisions(
  messages: readonly ProductionRoomMessage[],
): ProductionRoomMessage[] {
  return mergeProductionRoomMessages([], messages)
    .filter(
      (message) =>
        message.is_pinned &&
        message.parent_id === null &&
        message.category === "decision" &&
        !isProductionRoomDeleted(message),
    )
    .sort((a, b) => (b.pinned_at ?? "").localeCompare(a.pinned_at ?? ""));
}

function isUnread(message: ProductionRoomMessage, userId: string | null, lastReadAt: string | null): boolean {
  if (!userId || message.author_user_id === userId || isProductionRoomDeleted(message)) return false;
  if (!lastReadAt) return true;
  const readTime = new Date(lastReadAt).getTime();
  return Number.isFinite(readTime) ? timeOf(message) > readTime : true;
}

export function countUnreadProductionRoomMessages(
  messages: readonly ProductionRoomMessage[],
  userId: string | null,
  lastReadAt: string | null,
): number {
  return mergeProductionRoomMessages([], messages).filter((message) =>
    isUnread(message, userId, lastReadAt),
  ).length;
}

export function firstUnreadProductionRoomMessageId(
  messages: readonly ProductionRoomMessage[],
  userId: string | null,
  lastReadAt: string | null,
): string | null {
  return (
    mergeProductionRoomMessages([], messages).find((message) =>
      isUnread(message, userId, lastReadAt),
    )?.id ?? null
  );
}

export type ProductionRoomFilter = {
  category?: ProductionRoomCategory | "all";
  query?: string;
  unreadOnly?: boolean;
  /** Only open action items, whether or not they have been assigned. */
  openActionsOnly?: boolean;
  /** Only action items owned by the reader. */
  mineOnly?: boolean;
  /** Only action items past their due moment and not yet closed. */
  overdueOnly?: boolean;
};

/**
 * Filtering happens on threads, not rows: hiding a root must hide its replies,
 * and a reply that matches must keep its root visible with the reply shown.
 */
export function filterProductionRoomThreads(
  threads: readonly ProductionRoomThread[],
  filter: ProductionRoomFilter,
  options: { userId?: string | null; lastReadAt?: string | null } = {},
): ProductionRoomThread[] {
  const category = filter.category ?? "all";
  const query = (filter.query ?? "").replace(/\s+/g, " ").trim().toLocaleLowerCase();
  const unreadOnly = filter.unreadOnly === true;
  const openActionsOnly = filter.openActionsOnly === true;
  const mineOnly = filter.mineOnly === true;
  const overdueOnly = filter.overdueOnly === true;
  const userId = options.userId ?? null;
  const lastReadAt = options.lastReadAt ?? null;

  const matches = (message: ProductionRoomMessage) => {
    if (query && !message.body.toLocaleLowerCase().includes(query)) return false;
    return true;
  };
  const unreadIn = (thread: ProductionRoomThread) =>
    [thread.root, ...thread.replies].some((message) => isUnread(message, userId, lastReadAt));

  return threads.filter((thread) => {
    if (category !== "all" && thread.root.category !== category) return false;
    if (unreadOnly && !unreadIn(thread)) return false;
    if (openActionsOnly && !isActionItem(thread.root)) return false;
    if (openActionsOnly && isActionClosed(thread.root)) return false;
    if (mineOnly && (!isActionItem(thread.root) || thread.root.action_owner_user_id !== userId)) return false;
    if (overdueOnly && !isActionOverdue(thread.root)) return false;
    if (!query) return true;
    if (matches(thread.root)) return true;
    return thread.replies.some(matches);
  });
}

export type ProductionRoomStats = {
  total: number;
  threads: number;
  replies: number;
  pinnedDecisions: number;
  openActions: number;
  overdueActions: number;
  myOpenActions: number;
  byCategory: Record<ProductionRoomCategory, number>;
};

export function productionRoomStats(
  messages: readonly ProductionRoomMessage[],
  options: { userId?: string | null; now?: number } = {},
): ProductionRoomStats {
  const byCategory = Object.fromEntries(
    PRODUCTION_ROOM_CATEGORIES.map((category) => [category, 0]),
  ) as Record<ProductionRoomCategory, number>;
  const userId = options.userId ?? null;
  const now = options.now ?? Date.now();
  let total = 0;
  let replies = 0;
  let pinnedDecisions = 0;
  let openActions = 0;
  let overdueActions = 0;
  let myOpenActions = 0;
  for (const message of normalizeProductionRoomMessages(messages)) {
    if (isProductionRoomDeleted(message)) continue;
    total += 1;
    if (message.parent_id !== null) {
      replies += 1;
      continue;
    }
    byCategory[message.category] += 1;
    if (message.is_pinned && message.category === "decision") pinnedDecisions += 1;
    if (isActionItem(message) && !isActionClosed(message)) {
      openActions += 1;
      if (isActionOverdue(message, now)) overdueActions += 1;
      if (userId && message.action_owner_user_id === userId) myOpenActions += 1;
    }
  }
  return { total, threads: total - replies, replies, pinnedDecisions, openActions, overdueActions, myOpenActions, byCategory };
}

/** True only for a top-level action item, which is the only row a filter may read. */
export function isActionItem(message: ProductionRoomMessage): boolean {
  return message.category === "action_item" && message.parent_id === null;
}

/** A null status means open, exactly as the writer treats it. */
export function productionRoomActionStatus(
  message: ProductionRoomMessage,
): ProductionRoomActionStatus | null {
  if (!isActionItem(message)) return null;
  return message.action_status ?? "open";
}

export function isActionClosed(message: ProductionRoomMessage): boolean {
  const status = productionRoomActionStatus(message);
  return status === "done" || status === "cancelled";
}

/**
 * Overdue is a fact about the due moment, not about the status: a closed item
 * is never overdue however old it is, and an item with no date can be late in
 * spirit but not on the clock, so it is not counted here.
 */
export function isActionOverdue(
  message: ProductionRoomMessage,
  now: number = Date.now(),
): boolean {
  if (!isActionItem(message) || isActionClosed(message)) return false;
  if (!message.action_due_at) return false;
  const due = new Date(message.action_due_at).getTime();
  return Number.isFinite(due) && due < now;
}

export function isActionDueToday(
  message: ProductionRoomMessage,
  now: number = Date.now(),
): boolean {
  if (!isActionItem(message) || isActionClosed(message) || !message.action_due_at) return false;
  const due = new Date(message.action_due_at);
  const today = new Date(now);
  return (
    due.getFullYear() === today.getFullYear() &&
    due.getMonth() === today.getMonth() &&
    due.getDate() === today.getDate()
  );
}

export function productionRoomActionStatusLabel(
  message: ProductionRoomMessage,
  now: number = Date.now(),
): string {
  const status = productionRoomActionStatus(message);
  if (status === "done") return "Done";
  if (status === "cancelled") return "Cancelled";
  if (isActionOverdue(message, now)) return "Overdue";
  if (isActionDueToday(message, now)) return "Due today";
  if (!message.action_due_at) return "No due date";
  return "Open";
}

/** Mirrors `set_production_room_action`: admin or author reassigns, owner closes. */
export function canManageActionItem(
  message: ProductionRoomMessage,
  userId: string | null,
  isRoomAdmin: boolean,
): boolean {
  if (!userId || !isActionItem(message) || isProductionRoomDeleted(message)) return false;
  return isRoomAdmin || message.author_user_id === userId || message.action_owner_user_id === userId;
}

/** An owner who is neither the author nor an admin may only close or reopen. */
export function canReassignActionItem(
  message: ProductionRoomMessage,
  userId: string | null,
  isRoomAdmin: boolean,
): boolean {
  if (!userId || !isActionItem(message) || isProductionRoomDeleted(message)) return false;
  return isRoomAdmin || message.author_user_id === userId;
}

/** `datetime-local` gives "YYYY-MM-DDTHH:mm"; the API needs a full instant. */
export function actionDueInputToIso(value: string): string | null {
  const text = typeof value === "string" ? value.trim() : "";
  if (!text) return null;
  const parsed = new Date(text);
  return Number.isFinite(parsed.getTime()) ? parsed.toISOString() : null;
}

export function actionDueIsoToInput(value: string | null): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const pad = (part: number) => String(part).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function formatProductionRoomDueDate(
  value: string | null | undefined,
  locale?: string,
): string {
  if (!value) return "No due date";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "No due date";
  return formatter(locale, { dateStyle: "medium", timeStyle: "short" }).format(date);
}

export function normalizeProductionRoomDraft(value: string): string {
  const safe = typeof value === "string" ? value : "";
  // Trailing space at the end of a pasted line is invisible in the composer and
  // produces ragged indentation in the room, so each line is trimmed while
  // blank lines are kept as the author wrote them.
  return safe
    .normalize("NFKC")
    .replace(/\r\n?/g, "\n")
    .split("\n")
    .map((line) => line.trim())
    .join("\n")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

export function isProductionRoomDraftValid(value: string): boolean {
  const body = normalizeProductionRoomDraft(value);
  return body.length >= 1 && body.length <= PRODUCTION_ROOM_MAX_BODY;
}

export function productionRoomExcerpt(body: string, maxLength = 180): string {
  const value = (typeof body === "string" ? body : "").replace(/\s+/g, " ").trim();
  const safeMax = Number.isFinite(maxLength) ? Math.max(1, Math.trunc(maxLength)) : 180;
  if (value.length <= safeMax) return value;
  return `${value.slice(0, Math.max(1, safeMax - 1)).trimEnd()}…`;
}

function formatter(locale: string | undefined, options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat {
  try {
    return new Intl.DateTimeFormat(locale, options);
  } catch {
    return new Intl.DateTimeFormat("en-US", options);
  }
}

export function formatProductionRoomTime(value: string | null | undefined, locale?: string): string {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "-";
  return formatter(locale, { hour: "numeric", minute: "2-digit" }).format(date);
}

export function formatProductionRoomDateTime(value: string | null | undefined, locale?: string): string {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "-";
  return formatter(locale, { dateStyle: "medium", timeStyle: "short" }).format(date);
}

export function productionRoomEditRemaining(
  message: ProductionRoomMessage,
  now: number = Date.now(),
): number {
  return Math.max(0, PRODUCTION_ROOM_EDIT_WINDOW_MS - (now - timeOf(message)));
}

export function validateProductionRoomMemberInput(input: {
  email: string;
  displayName: string;
  department: string;
}): string | null {
  const email = (input.email ?? "").trim().toLowerCase();
  const displayName = (input.displayName ?? "").trim();
  const department = (input.department ?? "").trim();
  if (email.length < 3 || email.length > 254 || email.indexOf("@") < 1) {
    return "Enter the exact email address of an existing ProGlass sign-in.";
  }
  if (displayName.length < 1 || displayName.length > PRODUCTION_ROOM_MEMBER_MAX_NAME) {
    return "Display name must be 1 to 120 characters.";
  }
  if (department.length < 1 || department.length > PRODUCTION_ROOM_MEMBER_MAX_DEPARTMENT) {
    return "Department must be 1 to 80 characters.";
  }
  return null;
}
