// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import React from "react";

const mocks = vi.hoisted(() => {
  const me = {
    id: "11111111-1111-4111-8111-111111111111",
    email: "lead@proglass.test",
    user_metadata: { full_name: "Production Lead", inquiry_admin: true },
  };
  const other = "22222222-2222-4222-8222-222222222222";
  const state: {
    memberRows: Array<Record<string, unknown>>;
    messageRows: Array<Record<string, unknown>>;
    rpcCalls: Array<{ name: string; args?: unknown }>;
  } = { memberRows: [], messageRows: [], rpcCalls: [] };

  class Query implements PromiseLike<unknown> {
    constructor(private table: string) {}
    select() {
      return this;
    }
    order() {
      return this;
    }
    limit() {
      return this;
    }
    eq() {
      return this;
    }
    maybeSingle() {
      if (this.table === "production_room_reads") {
        return Promise.resolve({ data: null, error: null });
      }
      return Promise.resolve({ data: null, error: null });
    }
    private result() {
      if (this.table === "production_room_members") return { data: state.memberRows, error: null };
      if (this.table === "production_room_messages") return { data: state.messageRows, error: null };
      return { data: null, error: null };
    }
    then<TResult1 = unknown, TResult2 = never>(
      onfulfilled?: ((value: unknown) => TResult1 | PromiseLike<TResult1>) | null,
      onrejected?: ((reason: unknown) => TResult2 | PromiseLike<TResult2>) | null,
    ): Promise<TResult1 | TResult2> {
      return Promise.resolve(this.result()).then(onfulfilled, onrejected);
    }
  }

  const channel = {
    on: vi.fn(function (this: unknown) {
      return this;
    }),
    subscribe: vi.fn(function (this: unknown, callback?: (status: string) => void) {
      callback?.("SUBSCRIBED");
      return this;
    }),
  };

  const client = {
    auth: {
      getSession: vi.fn(async () => ({ data: { session: { user: me, access_token: "token" } }, error: null })),
      onAuthStateChange: vi.fn(() => ({ data: { subscription: { unsubscribe: vi.fn() } } })),
      signOut: vi.fn(async () => ({ error: null })),
      refreshSession: vi.fn(async () => ({ data: { session: null }, error: null })),
    },
    from: vi.fn((table: string) => new Query(table)),
    rpc: vi.fn(async (name: string, args?: unknown) => {
      state.rpcCalls.push({ name, args });
      if (name === "post_production_room_message") {
        const params = args as { p_body: string; p_category: string; p_parent_id: string | null };
        return {
          data: {
            id: "99999999-9999-4999-8999-999999999999",
            parent_id: params.p_parent_id,
            author_user_id: me.id,
            category: params.p_category,
            body: params.p_body,
            is_pinned: false,
            pinned_by: null,
            pinned_at: null,
            created_at: new Date().toISOString(),
            edited_at: null,
            edit_count: 0,
            deleted_by: null,
            deleted_at: null,
          },
          error: null,
        };
      }
      if (name === "mark_production_room_read") {
        return {
          data: { user_id: me.id, last_read_at: new Date().toISOString(), last_read_message_id: null, updated_at: "" },
          error: null,
        };
      }
      return { data: null, error: null };
    }),
    realtime: { setAuth: vi.fn(async () => undefined) },
    channel: vi.fn(() => channel),
    removeChannel: vi.fn(async () => "ok"),
  };

  return { me, other, client, state };
});

vi.mock("@/lib/supabase", () => ({
  isSupabaseConfigured: () => true,
  supabaseBrowser: () => mocks.client,
  hasStoredSupabaseSession: () => true,
  isSupabaseUnavailableError: () => false,
  supabaseAuthStorageKey: () => "sb-test-auth-token",
}));

vi.mock("@/i18n/provider", () => ({
  useLocale: () => "en-US",
}));

vi.mock("@/components/ui/LocaleLink", () => ({
  LocaleLink: ({ href, children, ...rest }: { href: string; children: React.ReactNode }) =>
    React.createElement("a", { href, ...rest }, children),
}));

import { ProductionRoomWorkspace } from "@/components/production/ProductionRoomWorkspace";
import { resetWebsiteSessionForTests } from "@/lib/website-session";

const memberRow = (overrides: Record<string, unknown> = {}) => ({
  user_id: mocks.me.id,
  display_name: "Production Lead",
  department: "Cutting",
  is_room_admin: true,
  active: true,
  created_at: "2026-10-20T08:00:00.000Z",
  updated_at: "2026-10-20T08:00:00.000Z",
  ...overrides,
});

const messageRow = (overrides: Record<string, unknown> = {}) => ({
  id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
  parent_id: null,
  author_user_id: mocks.other,
  category: "machine_issue",
  body: "The laminator is down",
  is_pinned: false,
  pinned_by: null,
  pinned_at: null,
  created_at: new Date().toISOString(),
  edited_at: null,
  edit_count: 0,
  deleted_by: null,
  deleted_at: null,
  action_owner_user_id: null,
  action_due_at: null,
  action_status: null,
  ...overrides,
});

afterEach(() => {
  cleanup();
  resetWebsiteSessionForTests();
  vi.clearAllMocks();
  mocks.state.memberRows = [];
  mocks.state.messageRows = [];
  mocks.state.rpcCalls = [];
});

describe("production room workspace", () => {
  it("tells a signed-in non-member that the room is empty on purpose", async () => {
    mocks.state.memberRows = [];
    mocks.state.messageRows = [];

    render(<ProductionRoomWorkspace />);

    await waitFor(() =>
      expect(screen.getByText("Your account is not on the production team yet")).not.toBeNull(),
    );
    // No composer, no messages, and nothing that pretends the room is empty.
    expect(screen.queryByLabelText("Message the production room")).toBeNull();
    expect(screen.queryByText("No posts yet")).toBeNull();
  });

  it("shows the room to a member and posts with the selected category", async () => {
    mocks.state.memberRows = [memberRow()];
    mocks.state.messageRows = [messageRow()];

    render(<ProductionRoomWorkspace />);

    await waitFor(() => expect(screen.getByText("The laminator is down")).not.toBeNull());

    const composer = screen.getByLabelText("Message the production room") as HTMLTextAreaElement;
    fireEvent.change(composer, { target: { value: "Rack three is clear" } });

    fireEvent.change(screen.getByLabelText("Category"), { target: { value: "dispatch_dependency" } });
    fireEvent.click(screen.getByRole("button", { name: "Post" }));

    await waitFor(() =>
      expect(mocks.state.rpcCalls).toEqual([
        {
          name: "post_production_room_message",
          args: { p_body: "Rack three is clear", p_category: "dispatch_dependency", p_parent_id: null },
        },
      ]),
    );
    expect(await screen.findByText("Rack three is clear")).not.toBeNull();
  });

  it("inherits the parent category when replying, because the table does", async () => {
    mocks.state.memberRows = [memberRow()];
    mocks.state.messageRows = [messageRow()];

    render(<ProductionRoomWorkspace />);

    await waitFor(() => expect(screen.getByText("The laminator is down")).not.toBeNull());
    fireEvent.click(screen.getByRole("button", { name: "Reply" }));

    const composer = screen.getByLabelText("Message the production room") as HTMLTextAreaElement;
    fireEvent.change(composer, { target: { value: "Engineer is on the way" } });
    fireEvent.click(screen.getByRole("button", { name: "Send reply" }));

    await waitFor(() =>
      expect(mocks.state.rpcCalls[0]).toEqual({
        name: "post_production_room_message",
        args: {
          p_body: "Engineer is on the way",
          p_category: "machine_issue",
          p_parent_id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
        },
      }),
    );
  });

  it("offers a pin only on a top-level decision and only to a room admin", async () => {
    mocks.state.memberRows = [memberRow()];
    mocks.state.messageRows = [
      messageRow({ id: "decision-1", category: "decision", body: "We cut laminate first" }),
      messageRow({ id: "blocker-1", category: "urgent_blocker", body: "Line two is blocked" }),
    ];

    render(<ProductionRoomWorkspace />);

    await waitFor(() => expect(screen.getByText("We cut laminate first")).not.toBeNull());
    expect(screen.getAllByRole("button", { name: "Pin decision" })).toHaveLength(1);
  });

  it("hides edit and delete once the author's thirty minutes have closed", async () => {
    const stale = new Date(Date.now() - 40 * 60 * 1000).toISOString();
    mocks.state.memberRows = [memberRow({ is_room_admin: false })];
    mocks.state.messageRows = [
      messageRow({
        id: "stale-1",
        author_user_id: mocks.me.id,
        body: "An old note of mine",
        created_at: stale,
      }),
      messageRow({ id: "fresh-1", author_user_id: mocks.me.id, body: "A new note of mine" }),
    ];

    render(<ProductionRoomWorkspace />);

    await waitFor(() => expect(screen.getByText("An old note of mine")).not.toBeNull());
    expect(screen.getAllByRole("button", { name: "Edit" })).toHaveLength(1);
    expect(screen.getAllByRole("button", { name: "Delete" })).toHaveLength(1);
  });
});

describe("production room action items in the workspace", () => {
  it("posts an action item with its owner and due time in one write", async () => {
    mocks.state.memberRows = [
      memberRow(),
      memberRow({ user_id: mocks.other, display_name: "Site Engineer", department: "Install" }),
    ];
    mocks.state.messageRows = [];

    render(<ProductionRoomWorkspace />);

    await waitFor(() => expect(screen.getByLabelText("Message the production room")).not.toBeNull());
    fireEvent.change(screen.getByLabelText("Category"), { target: { value: "action_item" } });

    const owner = await screen.findByLabelText("Action owner");
    fireEvent.change(owner, { target: { value: mocks.other } });
    fireEvent.change(screen.getByLabelText("Action due date"), { target: { value: "2026-10-30T09:00" } });
    fireEvent.change(screen.getByLabelText("Message the production room"), {
      target: { value: "Chase the anodised trim" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Post" }));

    await waitFor(() => expect(mocks.state.rpcCalls).toHaveLength(1));
    const call = mocks.state.rpcCalls[0]!;
    expect(call.name).toBe("post_production_room_action_item");
    expect(call.args).toMatchObject({ p_body: "Chase the anodised trim", p_owner_user_id: mocks.other });
    expect(new Date((call.args as { p_due_at: string }).p_due_at).getTime()).toBe(
      new Date("2026-10-30T09:00").getTime(),
    );
  });

  it("keeps the owner and due fields out of a plain message", async () => {
    mocks.state.memberRows = [memberRow()];
    mocks.state.messageRows = [];

    render(<ProductionRoomWorkspace />);

    await waitFor(() => expect(screen.getByLabelText("Message the production room")).not.toBeNull());
    expect(screen.queryByLabelText("Action owner")).toBeNull();
    expect(screen.queryByLabelText("Action due date")).toBeNull();
    fireEvent.change(screen.getByLabelText("Category"), { target: { value: "action_item" } });
    expect(await screen.findByLabelText("Action owner")).not.toBeNull();
  });

  it("shows an overdue action and lets its owner close it", async () => {
    const overdue = new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString();
    mocks.state.memberRows = [memberRow({ is_room_admin: false })];
    mocks.state.messageRows = [
      messageRow({
        id: "action-1",
        author_user_id: mocks.other,
        category: "action_item",
        body: "Book the crane",
        action_owner_user_id: mocks.me.id,
        action_due_at: overdue,
        action_status: "open",
      }),
    ];

    render(<ProductionRoomWorkspace />);

    await waitFor(() => expect(screen.getByText("Book the crane")).not.toBeNull());
    // Both the item chip and the filter chip can say "Overdue".
    expect(screen.getAllByText("Overdue").length).toBeGreaterThan(0);
    expect(screen.getByText(/Owner: Production Lead/)).not.toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Mark done" }));

    await waitFor(() =>
      expect(mocks.state.rpcCalls[0]).toEqual({
        name: "set_production_room_action",
        args: {
          p_message_id: "action-1",
          p_owner_user_id: mocks.me.id,
          p_due_at: overdue,
          p_status: "done",
        },
      }),
    );
  });

  it("does not offer close or assign on somebody else's action", async () => {
    mocks.state.memberRows = [memberRow({ is_room_admin: false })];
    mocks.state.messageRows = [
      messageRow({
        id: "action-2",
        author_user_id: mocks.other,
        category: "action_item",
        body: "Order the gaskets",
        action_owner_user_id: mocks.other,
        action_status: "open",
      }),
    ];

    render(<ProductionRoomWorkspace />);

    await waitFor(() => expect(screen.getByText("Order the gaskets")).not.toBeNull());
    expect(screen.queryByRole("button", { name: "Mark done" })).toBeNull();
    expect(screen.queryByText("Assign")).toBeNull();
  });
});
