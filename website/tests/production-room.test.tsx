import { describe, expect, it } from "vitest";
import {
  PRODUCTION_ROOM_CATEGORY_META,
  PRODUCTION_ROOM_EDIT_WINDOW_MS,
  canDeleteProductionRoomMessage,
  canEditProductionRoomMessage,
  canPinProductionRoomMessage,
  countUnreadProductionRoomMessages,
  filterProductionRoomThreads,
  firstUnreadProductionRoomMessageId,
  groupProductionRoomThreads,
  isProductionRoomCategory,
  isProductionRoomDraftValid,
  mergeProductionRoomMessages,
  normalizeProductionRoomDraft,
  normalizeProductionRoomMember,
  normalizeProductionRoomMessage,
  normalizeProductionRoomRead,
  pinnedProductionRoomDecisions,
  productionRoomCategoryMeta,
  productionRoomStats,
  validateProductionRoomMemberInput,
} from "@/lib/production-room";
import type { ProductionRoomMessage } from "@/types/production-room";

const AUTHOR = "11111111-1111-4111-8111-111111111111";
const OTHER = "22222222-2222-4222-8222-222222222222";

function message(overrides: Partial<Record<keyof ProductionRoomMessage, unknown>> = {}) {
  return {
    id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
    parent_id: null,
    author_user_id: AUTHOR,
    category: "general_production",
    body: "Body",
    is_pinned: false,
    pinned_by: null,
    pinned_at: null,
    created_at: "2026-10-22T08:00:00.000Z",
    edited_at: null,
    edit_count: 0,
    deleted_by: null,
    deleted_at: null,
    ...overrides,
  };
}

const NOW = new Date("2026-10-22T08:10:00.000Z").getTime();

describe("production room categories", () => {
  it("mirrors the nine database categories", () => {
    expect(PRODUCTION_ROOM_CATEGORY_META.map((meta) => meta.value).sort()).toEqual(
      [
        "action_item",
        "decision",
        "dispatch_dependency",
        "general_production",
        "machine_issue",
        "material_shortage",
        "quality_issue",
        "schedule_change",
        "urgent_blocker",
      ].sort(),
    );
  });

  it("rejects a category the table would refuse", () => {
    expect(isProductionRoomCategory("decision")).toBe(true);
    expect(isProductionRoomCategory("gossip")).toBe(false);
    expect(productionRoomCategoryMeta("gossip").value).toBe("general_production");
  });

  it("gives every category a description, so the taxonomy is usable at speed", () => {
    for (const meta of PRODUCTION_ROOM_CATEGORY_META) {
      expect(meta.description.length).toBeGreaterThan(20);
      expect(meta.short.length).toBeGreaterThan(2);
    }
  });
});

describe("production room normalisation", () => {
  it("drops rows that cannot be rendered", () => {
    expect(normalizeProductionRoomMessage(null)).toBeNull();
    expect(normalizeProductionRoomMessage({ id: "x" })).toBeNull();
    expect(normalizeProductionRoomMessage({ id: "x", author_user_id: AUTHOR })).toBeNull();
  });

  it("falls back to the general category rather than trusting an unknown one", () => {
    const row = normalizeProductionRoomMessage(message({ category: "not_a_category" }));
    expect(row?.category).toBe("general_production");
  });

  it("coerces a negative edit count and a non-boolean pin", () => {
    const row = normalizeProductionRoomMessage(message({ edit_count: -4, is_pinned: "yes" }));
    expect(row?.edit_count).toBe(0);
    expect(row?.is_pinned).toBe(false);
  });

  it("keeps a member active unless the row says otherwise", () => {
    expect(normalizeProductionRoomMember({ user_id: AUTHOR, display_name: "A" })?.active).toBe(true);
    expect(
      normalizeProductionRoomMember({ user_id: AUTHOR, display_name: "A", active: false })?.active,
    ).toBe(false);
  });

  it("reads a read position from either a single row or an array", () => {
    const row = { user_id: AUTHOR, last_read_at: "2026-10-22T08:00:00.000Z", updated_at: "x" };
    expect(normalizeProductionRoomRead(row)?.last_read_at).toBe("2026-10-22T08:00:00.000Z");
    expect(normalizeProductionRoomRead([row])?.user_id).toBe(AUTHOR);
    expect(normalizeProductionRoomRead(null)).toBeNull();
  });

  it("merges by id and orders by time", () => {
    const a = message({ id: "a", created_at: "2026-10-22T08:00:00.000Z" });
    const b = message({ id: "b", created_at: "2026-10-22T07:00:00.000Z" });
    const merged = mergeProductionRoomMessages(
      [normalizeProductionRoomMessage(a)!],
      [normalizeProductionRoomMessage(b)!, normalizeProductionRoomMessage({ ...a, body: "Edited" })!],
    );
    expect(merged.map((row) => row.id)).toEqual(["b", "a"]);
    expect(merged[1]?.body).toBe("Edited");
  });
});

describe("production room threads", () => {
  it("groups one level of replies under their root", () => {
    const root = normalizeProductionRoomMessage(message({ id: "root" }))!;
    const reply = normalizeProductionRoomMessage(message({ id: "reply", parent_id: "root" }))!;
    const threads = groupProductionRoomThreads([root, reply]);
    expect(threads).toHaveLength(1);
    expect(threads[0]?.replies.map((row) => row.id)).toEqual(["reply"]);
  });

  it("shows a reply whose root is outside the loaded window instead of losing it", () => {
    const orphan = normalizeProductionRoomMessage(message({ id: "orphan", parent_id: "missing" }))!;
    const threads = groupProductionRoomThreads([orphan]);
    expect(threads).toHaveLength(1);
    expect(threads[0]?.root.id).toBe("orphan");
  });

  it("filters on the root category, keeping replies attached", () => {
    const root = normalizeProductionRoomMessage(message({ id: "root", category: "machine_issue" }))!;
    const reply = normalizeProductionRoomMessage(message({ id: "reply", parent_id: "root" }))!;
    const threads = groupProductionRoomThreads([root, reply]);
    expect(filterProductionRoomThreads(threads, { category: "machine_issue" })).toHaveLength(1);
    expect(filterProductionRoomThreads(threads, { category: "decision" })).toHaveLength(0);
  });

  it("keeps a root visible when only one of its replies matches the search", () => {
    const root = normalizeProductionRoomMessage(message({ id: "root", body: "Frame is ready" }))!;
    const reply = normalizeProductionRoomMessage(
      message({ id: "reply", parent_id: "root", body: "Need laminate glue" }),
    )!;
    const threads = groupProductionRoomThreads([root, reply]);
    expect(filterProductionRoomThreads(threads, { query: "laminate" })).toHaveLength(1);
    expect(filterProductionRoomThreads(threads, { query: "toughened" })).toHaveLength(0);
  });
});

describe("production room unread arithmetic", () => {
  it("counts another author's unseen message and ignores your own", () => {
    const own = normalizeProductionRoomMessage(message({ id: "own" }))!;
    const other = normalizeProductionRoomMessage(
      message({ id: "other", author_user_id: OTHER, created_at: "2026-10-22T09:00:00.000Z" }),
    )!;
    expect(countUnreadProductionRoomMessages([own, other], AUTHOR, "2026-10-22T08:30:00.000Z")).toBe(1);
    expect(countUnreadProductionRoomMessages([own, other], AUTHOR, "2026-10-22T09:30:00.000Z")).toBe(0);
  });

  it("points at the first unread message", () => {
    const rows = [
      normalizeProductionRoomMessage(message({ id: "a", created_at: "2026-10-22T07:00:00.000Z" }))!,
      normalizeProductionRoomMessage(
        message({ id: "b", author_user_id: OTHER, created_at: "2026-10-22T08:00:00.000Z" }),
      )!,
      normalizeProductionRoomMessage(
        message({ id: "c", author_user_id: OTHER, created_at: "2026-10-22T09:00:00.000Z" }),
      )!,
    ];
    expect(firstUnreadProductionRoomMessageId(rows, AUTHOR, "2026-10-22T07:30:00.000Z")).toBe("b");
  });

  it("does not call a deleted message unread", () => {
    const deleted = normalizeProductionRoomMessage(
      message({ id: "gone", author_user_id: OTHER, deleted_at: "2026-10-22T09:00:00.000Z" }),
    )!;
    expect(countUnreadProductionRoomMessages([deleted], AUTHOR, null)).toBe(0);
  });
});

describe("production room permissions mirror the database", () => {
  it("lets an author edit only inside thirty minutes", () => {
    const row = normalizeProductionRoomMessage(message())!;
    expect(canEditProductionRoomMessage(row, AUTHOR, NOW)).toBe(true);
    expect(canEditProductionRoomMessage(row, AUTHOR, NOW + PRODUCTION_ROOM_EDIT_WINDOW_MS)).toBe(false);
    expect(canEditProductionRoomMessage(row, OTHER, NOW)).toBe(false);
  });

  it("refuses an edit on a deleted message", () => {
    const row = normalizeProductionRoomMessage(message({ deleted_at: "2026-10-22T08:05:00.000Z" }))!;
    expect(canEditProductionRoomMessage(row, AUTHOR, NOW)).toBe(false);
  });

  it("lets an admin delete anything but an author only inside the window", () => {
    const old = normalizeProductionRoomMessage(message({ created_at: "2026-10-22T06:00:00.000Z" }))!;
    expect(canDeleteProductionRoomMessage(old, AUTHOR, false, NOW)).toBe(false);
    expect(canDeleteProductionRoomMessage(old, AUTHOR, true, NOW)).toBe(true);
    expect(canDeleteProductionRoomMessage(old, OTHER, false, NOW)).toBe(false);
  });

  it("pins only a top-level decision, and only for an admin", () => {
    const decision = normalizeProductionRoomMessage(message({ category: "decision" }))!;
    const reply = normalizeProductionRoomMessage(
      message({ id: "r", parent_id: "root", category: "decision" }),
    )!;
    const blocker = normalizeProductionRoomMessage(message({ id: "u", category: "urgent_blocker" }))!;
    expect(canPinProductionRoomMessage(decision, true)).toBe(true);
    expect(canPinProductionRoomMessage(decision, false)).toBe(false);
    expect(canPinProductionRoomMessage(reply, true)).toBe(false);
    expect(canPinProductionRoomMessage(blocker, true)).toBe(false);
  });

  it("lists only pinned, live decisions", () => {
    const rows = [
      normalizeProductionRoomMessage(message({ id: "p", category: "decision", is_pinned: true }))!,
      normalizeProductionRoomMessage(message({ id: "n", category: "decision" }))!,
      normalizeProductionRoomMessage(
        message({ id: "d", category: "decision", is_pinned: true, deleted_at: "2026-10-22T09:00:00.000Z" }),
      )!,
    ];
    expect(pinnedProductionRoomDecisions(rows).map((row) => row.id)).toEqual(["p"]);
  });
});

describe("production room stats and validation", () => {
  it("counts threads, replies and categories without counting deleted rows", () => {
    const rows = [
      normalizeProductionRoomMessage(message({ id: "root" }))!,
      normalizeProductionRoomMessage(message({ id: "reply", parent_id: "root" }))!,
      normalizeProductionRoomMessage(message({ id: "gone", deleted_at: "2026-10-22T09:00:00.000Z" }))!,
    ];
    const stats = productionRoomStats(rows);
    expect(stats.total).toBe(2);
    expect(stats.threads).toBe(1);
    expect(stats.replies).toBe(1);
    expect(stats.byCategory.general_production).toBe(1);
  });

  it("normalises a draft and enforces the 4,000 character boundary", () => {
    expect(normalizeProductionRoomDraft("  a \r\n b  ")).toBe("a\nb");
    expect(isProductionRoomDraftValid("")).toBe(false);
    expect(isProductionRoomDraftValid("x".repeat(4000))).toBe(true);
    expect(isProductionRoomDraftValid("x".repeat(4001))).toBe(false);
  });

  it("validates a member registration before the write", () => {
    const valid = { email: "person@example.com", displayName: "Person", department: "Cutting" };
    expect(validateProductionRoomMemberInput(valid)).toBeNull();
    expect(validateProductionRoomMemberInput({ ...valid, email: "nope" })).toContain("email");
    expect(validateProductionRoomMemberInput({ ...valid, displayName: "" })).toContain("Display name");
    expect(validateProductionRoomMemberInput({ ...valid, department: "" })).toContain("Department");
  });
});
