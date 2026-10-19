import type { SupabaseClient } from "@supabase/supabase-js";

import { isSupabaseConfigured, supabaseServerWithToken } from "@/lib/supabase";
import {
  PRODUCTION_ROOM_CATEGORIES,
  type ProductionRoomAccess,
  type ProductionRoomCategory,
  type ProductionRoomData,
  type ProductionRoomFilters,
  type ProductionRoomMember,
  type ProductionRoomMessage,
  type ProductionRoomRead,
} from "@/types/production-room";

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/;
const MEMBER_FIELDS =
  "user_id,display_name,department,is_room_admin,active,created_by,created_at,updated_by,updated_at";
const MESSAGE_FIELDS =
  "id,parent_id,author_user_id,category,body,is_pinned,pinned_by,pinned_at,created_at,edited_at,edit_count,deleted_by,deleted_at";
const READ_FIELDS = "user_id,last_read_at,last_read_message_id,updated_at";

export type ProductionRoomFailureCode =
  | "UNAUTHORIZED"
  | "FORBIDDEN"
  | "INVALID"
  | "NOT_FOUND"
  | "NOT_CONFIGURED"
  | "TEMPORARY_FAILURE";

export class ProductionRoomFailure extends Error {
  constructor(
    public code: ProductionRoomFailureCode,
    message: string,
  ) {
    super(message);
  }
}

export type ProductionRoomFilterInput = {
  category?: string;
  query?: string;
  from?: string;
  to?: string;
  pinnedOnly?: boolean;
};

export type ProductionRoomPostInput = {
  body: string;
  category?: string;
  parentId?: string | null;
};

export type ProductionRoomMemberInput = {
  email: string;
  displayName: string;
  department: string;
  isRoomAdmin: boolean;
  active: boolean;
};

function fail(error: { code?: string; message?: string }): never {
  const value = `${error.code || ""} ${error.message || ""}`;
  if (/42P01|PGRST202|PGRST205|schema cache|does not exist/i.test(value)) {
    throw new ProductionRoomFailure(
      "NOT_CONFIGURED",
      "Production coordination room is not installed in this deployment.",
    );
  }
  if (/jwt|42501|permission|unauthorized/i.test(value)) {
    throw new ProductionRoomFailure(
      "FORBIDDEN",
      error.message || "This account cannot perform that production room action.",
    );
  }
  if (/22023|23514|21000|23505/i.test(value)) {
    throw new ProductionRoomFailure("INVALID", error.message || "Invalid production room request.");
  }
  if (error.code === "P0002") {
    throw new ProductionRoomFailure("NOT_FOUND", error.message || "Production room record not found.");
  }
  console.error("[production-room]", { code: error.code, message: error.message });
  throw new ProductionRoomFailure(
    "TEMPORARY_FAILURE",
    "Production coordination room could not complete the request.",
  );
}

async function access(token: string): Promise<{ client: SupabaseClient; userId: string }> {
  if (!token) throw new ProductionRoomFailure("UNAUTHORIZED", "Sign in to continue.");
  if (!isSupabaseConfigured()) {
    throw new ProductionRoomFailure("NOT_CONFIGURED", "Supabase is not configured.");
  }
  const client = supabaseServerWithToken(token);
  const auth = await client.auth.getUser(token);
  if (auth.error || !auth.data.user) {
    throw new ProductionRoomFailure("UNAUTHORIZED", "Session expired or invalid.");
  }
  return { client, userId: auth.data.user.id };
}

function normalizedText(value: unknown, minimum: number, maximum: number, label: string): string {
  const text = typeof value === "string" ? value.normalize("NFKC").trim() : "";
  if (text.length < minimum || text.length > maximum) {
    throw new ProductionRoomFailure(
      "INVALID",
      `${label} must contain ${minimum} to ${maximum} characters.`,
    );
  }
  return text;
}

function requireUuid(value: string, label: string): string {
  if (!UUID.test(value || "")) {
    throw new ProductionRoomFailure("NOT_FOUND", `${label} not found.`);
  }
  return value;
}

function parseCategory(value: string | undefined, fallback: ProductionRoomCategory): ProductionRoomCategory {
  const candidate = (value || fallback) as ProductionRoomCategory;
  if (!PRODUCTION_ROOM_CATEGORIES.includes(candidate)) {
    throw new ProductionRoomFailure("INVALID", "Invalid production room category.");
  }
  return candidate;
}

function validDateOnly(value: string | undefined, label: string): string | undefined {
  if (!value) return undefined;
  if (!DATE_ONLY.test(value)) {
    throw new ProductionRoomFailure("INVALID", `${label} must use YYYY-MM-DD.`);
  }
  const parsed = new Date(`${value}T00:00:00.000Z`);
  if (Number.isNaN(parsed.getTime()) || parsed.toISOString().slice(0, 10) !== value) {
    throw new ProductionRoomFailure("INVALID", `${label} is not a valid date.`);
  }
  return value;
}

function normalizeFilters(input: ProductionRoomFilterInput): ProductionRoomFilters {
  const category = input.category ? parseCategory(input.category, "general_production") : undefined;
  const query = input.query ? normalizedText(input.query, 1, 100, "Search") : undefined;
  const from = validDateOnly(input.from, "From date");
  const to = validDateOnly(input.to, "To date");
  if (from && to && from > to) {
    throw new ProductionRoomFailure("INVALID", "From date must not be after To date.");
  }
  return { category, query, from, to, pinnedOnly: input.pinnedOnly === true };
}

function escapeLike(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/%/g, "\\%").replace(/_/g, "\\_");
}

async function accessState(client: SupabaseClient, userId: string): Promise<ProductionRoomAccess> {
  const memberResult = await client
    .from("production_room_members")
    .select(MEMBER_FIELDS)
    .eq("user_id", userId)
    .maybeSingle();
  if (memberResult.error) fail(memberResult.error);

  const roleResult = await client.rpc("has_inquiry_role", { p_roles: ["inquiry_admin"] });
  if (roleResult.error) fail(roleResult.error);

  const member = (memberResult.data || null) as ProductionRoomMember | null;
  const isMember = Boolean(member?.active);
  const isRoomAdmin = Boolean(isMember && member?.is_room_admin);
  return {
    userId,
    member,
    isMember,
    isRoomAdmin,
    canManageMembers: roleResult.data === true || isRoomAdmin,
  };
}

export async function getProductionRoomAccess(token: string): Promise<ProductionRoomAccess> {
  const { client, userId } = await access(token);
  return accessState(client, userId);
}

async function listMembers(client: SupabaseClient): Promise<ProductionRoomMember[]> {
  const result = await client
    .from("production_room_members")
    .select(MEMBER_FIELDS)
    .order("display_name")
    .order("user_id")
    .limit(200);
  if (result.error) fail(result.error);
  return (result.data || []) as ProductionRoomMember[];
}

export async function getProductionRoom(
  token: string,
  filterInput: ProductionRoomFilterInput,
): Promise<ProductionRoomData> {
  const filters = normalizeFilters(filterInput);
  const { client, userId } = await access(token);
  const roomAccess = await accessState(client, userId);

  if (!roomAccess.isMember) {
    return {
      access: roomAccess,
      members: roomAccess.canManageMembers ? await listMembers(client) : [],
      messages: [],
      pinned: [],
      readPosition: null,
      unreadCount: 0,
    };
  }

  const members = await listMembers(client);

  const readResult = await client
    .from("production_room_reads")
    .select(READ_FIELDS)
    .eq("user_id", userId)
    .maybeSingle();
  if (readResult.error) fail(readResult.error);
  const readPosition = (readResult.data || null) as ProductionRoomRead | null;

  let matchedQuery = client.from("production_room_messages").select(MESSAGE_FIELDS);
  if (filters.category) matchedQuery = matchedQuery.eq("category", filters.category);
  if (filters.query) matchedQuery = matchedQuery.ilike("body", `%${escapeLike(filters.query)}%`);
  if (filters.from) matchedQuery = matchedQuery.gte("created_at", `${filters.from}T00:00:00.000Z`);
  if (filters.to) matchedQuery = matchedQuery.lte("created_at", `${filters.to}T23:59:59.999Z`);
  if (filters.pinnedOnly) matchedQuery = matchedQuery.eq("is_pinned", true);
  const matchedResult = await matchedQuery
    .order("created_at", { ascending: false })
    .order("id", { ascending: false })
    .limit(500);
  if (matchedResult.error) fail(matchedResult.error);
  const matched = (matchedResult.data || []) as ProductionRoomMessage[];

  const threadIds: string[] = [];
  const seenThreadIds = new Set<string>();
  for (const message of matched) {
    const threadId = message.parent_id || message.id;
    if (!seenThreadIds.has(threadId)) {
      seenThreadIds.add(threadId);
      threadIds.push(threadId);
      if (threadIds.length >= 100) break;
    }
  }

  const byId = new Map<string, ProductionRoomMessage>();
  for (const message of matched) byId.set(message.id, message);

  if (threadIds.length) {
    const parentsResult = await client
      .from("production_room_messages")
      .select(MESSAGE_FIELDS)
      .in("id", threadIds)
      .limit(100);
    if (parentsResult.error) fail(parentsResult.error);
    for (const message of (parentsResult.data || []) as ProductionRoomMessage[]) {
      byId.set(message.id, message);
    }

    const repliesResult = await client
      .from("production_room_messages")
      .select(MESSAGE_FIELDS)
      .in("parent_id", threadIds)
      .order("created_at")
      .order("id")
      .limit(1000);
    if (repliesResult.error) fail(repliesResult.error);
    for (const message of (repliesResult.data || []) as ProductionRoomMessage[]) {
      byId.set(message.id, message);
    }
  }

  const messages = [...byId.values()].sort((a, b) => {
    const byTime = new Date(a.created_at).getTime() - new Date(b.created_at).getTime();
    return byTime || a.id.localeCompare(b.id);
  });

  const pinnedResult = await client
    .from("production_room_messages")
    .select(MESSAGE_FIELDS)
    .eq("is_pinned", true)
    .is("parent_id", null)
    .order("pinned_at", { ascending: false })
    .order("id", { ascending: false })
    .limit(50);
  if (pinnedResult.error) fail(pinnedResult.error);

  let unreadQuery = client
    .from("production_room_messages")
    .select("id", { count: "exact", head: true })
    .neq("author_user_id", userId)
    .is("deleted_at", null);
  if (readPosition?.last_read_at) {
    unreadQuery = unreadQuery.gt("created_at", readPosition.last_read_at);
  }
  const unreadResult = await unreadQuery;
  if (unreadResult.error) fail(unreadResult.error);

  return {
    access: roomAccess,
    members,
    messages,
    pinned: (pinnedResult.data || []) as ProductionRoomMessage[],
    readPosition,
    unreadCount: unreadResult.count || 0,
  };
}

export async function postProductionRoomMessage(
  token: string,
  input: ProductionRoomPostInput,
): Promise<ProductionRoomMessage> {
  const body = normalizedText(input.body, 1, 4000, "Message");
  const parentId = input.parentId ? requireUuid(input.parentId, "Production room thread") : null;
  const category = parentId ? null : parseCategory(input.category, "general_production");
  const { client } = await access(token);
  const result = await client.rpc("post_production_room_message", {
    p_body: body,
    p_category: category,
    p_parent_id: parentId,
  });
  if (result.error) fail(result.error);
  return result.data as ProductionRoomMessage;
}

export async function editProductionRoomMessage(
  token: string,
  messageId: string,
  bodyValue: string,
): Promise<ProductionRoomMessage> {
  requireUuid(messageId, "Production room message");
  const body = normalizedText(bodyValue, 1, 4000, "Message");
  const { client } = await access(token);
  const result = await client.rpc("edit_production_room_message", {
    p_message_id: messageId,
    p_body: body,
  });
  if (result.error) fail(result.error);
  return result.data as ProductionRoomMessage;
}

export async function deleteProductionRoomMessage(
  token: string,
  messageId: string,
): Promise<ProductionRoomMessage> {
  requireUuid(messageId, "Production room message");
  const { client } = await access(token);
  const result = await client.rpc("delete_production_room_message", {
    p_message_id: messageId,
  });
  if (result.error) fail(result.error);
  return result.data as ProductionRoomMessage;
}

export async function setProductionRoomMessagePinned(
  token: string,
  messageId: string,
  pinned: boolean,
): Promise<ProductionRoomMessage> {
  requireUuid(messageId, "Production room message");
  if (typeof pinned !== "boolean") {
    throw new ProductionRoomFailure("INVALID", "Pin state must be true or false.");
  }
  const { client } = await access(token);
  const result = await client.rpc("set_production_room_message_pinned", {
    p_message_id: messageId,
    p_pinned: pinned,
  });
  if (result.error) fail(result.error);
  return result.data as ProductionRoomMessage;
}

export async function markProductionRoomRead(
  token: string,
  throughValue?: string,
): Promise<ProductionRoomRead> {
  let through = new Date().toISOString();
  if (throughValue) {
    const parsed = new Date(throughValue);
    if (Number.isNaN(parsed.getTime())) {
      throw new ProductionRoomFailure("INVALID", "Read position must be a valid timestamp.");
    }
    through = parsed.toISOString();
  }
  const { client } = await access(token);
  const result = await client.rpc("mark_production_room_read", { p_through: through });
  if (result.error) fail(result.error);
  return result.data as ProductionRoomRead;
}

export async function registerProductionRoomMember(
  token: string,
  input: ProductionRoomMemberInput,
): Promise<ProductionRoomMember> {
  const email = normalizedText(input.email, 3, 254, "Email").toLowerCase();
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    throw new ProductionRoomFailure("INVALID", "Enter a valid Auth email address.");
  }
  const displayName = normalizedText(input.displayName, 1, 120, "Display name");
  const department = normalizedText(input.department, 1, 80, "Department");
  if (typeof input.isRoomAdmin !== "boolean" || typeof input.active !== "boolean") {
    throw new ProductionRoomFailure("INVALID", "Member admin and active states must be true or false.");
  }
  const { client } = await access(token);
  const result = await client.rpc("register_production_room_member", {
    p_email: email,
    p_display_name: displayName,
    p_department: department,
    p_is_room_admin: input.isRoomAdmin,
    p_active: input.active,
  });
  if (result.error) fail(result.error);
  return result.data as ProductionRoomMember;
}

export async function setProductionRoomMemberActive(
  token: string,
  userId: string,
  active: boolean,
): Promise<ProductionRoomMember> {
  requireUuid(userId, "Production room member");
  if (typeof active !== "boolean") {
    throw new ProductionRoomFailure("INVALID", "Member active state must be true or false.");
  }
  const { client } = await access(token);
  const result = await client.rpc("set_production_room_member_active", {
    p_user_id: userId,
    p_active: active,
  });
  if (result.error) fail(result.error);
  return result.data as ProductionRoomMember;
}
