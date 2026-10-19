// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import React from "react";

const mocks = vi.hoisted(() => {
  const me = {
    id: "0568fecb-0f8e-4f1d-9d77-845a5be0cbbc",
    email: "up17mix@gmail.com",
    user_metadata: { email_verified: true },
  };
  const messages = [
    {
      id: "ddbe5a13-f575-4ecf-a18b-e7fab498ee9a",
      author_user_id: "ee646814-0d2f-47b0-be96-68fcb51dda54",
      author_display_name: "techwithyassh",
      body: "i am fine you say",
      reply_to_message_id: null,
      deleted_at: null,
      deleted_by: null,
      created_at: "2026-10-19T07:32:00.502035+00:00",
    },
    {
      id: "4059d398-fb14-4a70-b91d-d124c97e4f94",
      author_user_id: me.id,
      author_display_name: "up17mix",
      body: "how are you",
      reply_to_message_id: null,
      deleted_at: null,
      deleted_by: null,
      created_at: "2026-10-19T07:30:36.910950+00:00",
    },
    {
      id: "e1553ac8-5e71-4fd0-b8ff-f6ebb705ffa2",
      author_user_id: "ee646814-0d2f-47b0-be96-68fcb51dda54",
      author_display_name: "techwithyassh",
      body: "hy",
      reply_to_message_id: null,
      deleted_at: null,
      deleted_by: null,
      created_at: "2026-10-19T07:28:50.169885+00:00",
    },
    // Malformed external rows must be dropped or coerced at the Supabase
    // boundary instead of crashing the signed-in render path.
    null as never,
    {
      id: "broken-row",
      author_user_id: "ee646814-0d2f-47b0-be96-68fcb51dda54",
      author_display_name: null as never,
      body: 42 as never,
      reply_to_message_id: 123 as never,
      deleted_at: null,
      deleted_by: null,
      created_at: "not-a-date",
    },
  ];

  const otherId = "ee646814-0d2f-47b0-be96-68fcb51dda54";
  const profiles = [
    {
      user_id: me.id,
      display_name: "up17mix",
      avatar_icon: "star",
      avatar_color: "emerald",
      updated_at: "2026-10-19T07:35:00.000Z",
    },
    {
      user_id: otherId,
      display_name: "Tech With Yassh",
      avatar_icon: "gem",
      avatar_color: "sky",
      updated_at: "2026-10-19T07:35:00.000Z",
    },
  ];
  const receipts = [
    {
      message_id: "4059d398-fb14-4a70-b91d-d124c97e4f94",
      user_id: otherId,
      display_name: "Tech With Yassh",
      delivered_at: "2026-10-19T07:31:00.000Z",
      read_at: "2026-10-19T07:31:30.000Z",
    },
    {
      message_id: "ddbe5a13-f575-4ecf-a18b-e7fab498ee9a",
      user_id: me.id,
      display_name: "up17mix",
      delivered_at: "2026-10-19T07:32:10.000Z",
      read_at: null,
    },
  ];
  const channelTables: string[] = [];
  const rpcCalls: Array<{ name: string; args?: unknown }> = [];

  class Query implements PromiseLike<unknown> {
    private head = false;
    constructor(private table: string) {}
    select(_columns?: string, options?: { head?: boolean }) {
      this.head = Boolean(options?.head);
      return this;
    }
    eq() { return this; }
    neq() { return this; }
    is() { return this; }
    gt() { return this; }
    lt() { return this; }
    in() { return this; }
    order() { return this; }
    limit() { return this; }
    maybeSingle() {
      if (this.table === "global_chat_reads") {
        return Promise.resolve({
          data: {
            user_id: me.id,
            last_read_at: "2026-10-19T07:30:36.910950+00:00",
            updated_at: "2026-10-19T07:30:37.321818+00:00",
          },
          error: null,
        });
      }
      return Promise.resolve({ data: null, error: null });
    }
    private result() {
      if (this.table === "global_chat_messages" && this.head) return { data: null, count: 1, error: null };
      if (this.table === "global_chat_messages") return { data: messages, count: null, error: null };
      if (this.table === "global_chat_receipts") return { data: receipts, count: null, error: null };
      if (this.table === "user_profiles") return { data: profiles, count: null, error: null };
      return { data: [], count: null, error: null };
    }
    then<TResult1 = unknown, TResult2 = never>(
      onfulfilled?: ((value: unknown) => TResult1 | PromiseLike<TResult1>) | null,
      onrejected?: ((reason: unknown) => TResult2 | PromiseLike<TResult2>) | null,
    ): Promise<TResult1 | TResult2> {
      return Promise.resolve(this.result()).then(onfulfilled, onrejected);
    }
  }

  const channel = {
    on: vi.fn(function (this: unknown, _event: string, config?: { table?: string }) {
      if (config?.table) channelTables.push(config.table);
      return this;
    }),
    subscribe: vi.fn((callback?: (status: string) => void) => {
      callback?.("SUBSCRIBED");
      return channel;
    }),
  };

  const state: {
    authCallback: ((event: string, session: { user: typeof me; access_token?: string } | null) => void) | null;
    channelCalls: string[];
    channelTables: string[];
    rpcCalls: Array<{ name: string; args?: unknown }>;
  } = { authCallback: null, channelCalls: [], channelTables, rpcCalls };

  const client = {
    auth: {
      getSession: vi.fn(async () => ({
        data: { session: { user: me, access_token: "test-access-token" } },
        error: null,
      })),
      onAuthStateChange: vi.fn((callback: NonNullable<typeof state.authCallback>) => {
        state.authCallback = callback;
        return { data: { subscription: { unsubscribe: vi.fn() } } };
      }),
    },
    from: vi.fn((table: string) => new Query(table)),
    rpc: vi.fn(async (name: string, args?: { p_last_read_at?: string; p_message_ids?: string[] }) => {
      rpcCalls.push({ name, args });
      if (name === "mark_global_chat_receipts_read") {
        return {
          data: (args?.p_message_ids ?? []).map((message_id) => ({
            message_id,
            user_id: me.id,
            display_name: "up17mix",
            delivered_at: "2026-10-19T07:31:00.000Z",
            read_at: "2026-10-19T07:32:00.000Z",
          })),
          error: null,
        };
      }
      if (name === "mark_global_chat_read") {
        return {
          data: { user_id: me.id, last_read_at: args?.p_last_read_at, updated_at: new Date().toISOString() },
          error: null,
        };
      }
      return { data: [], error: null };
    }),
    realtime: {
      setAuth: vi.fn(async () => undefined),
    },
    channel: vi.fn((name: string) => {
      state.channelCalls.push(name);
      return channel;
    }),
    removeChannel: vi.fn(async () => "ok"),
  };

  return { me, client, state, channel };
});

vi.mock("@/lib/supabase", () => ({
  isSupabaseConfigured: () => true,
  supabaseBrowser: () => mocks.client,
}));

vi.mock("@/i18n/provider", () => ({
  useLocale: () => "en-US",
}));

vi.mock("@/components/ui/LocaleLink", () => ({
  LocaleLink: ({ href, children, ...rest }: { href: string; children: React.ReactNode }) =>
    React.createElement("a", { href, ...rest }, children),
}));

import { GlobalChatWorkspace } from "@/components/chat/GlobalChatWorkspace";

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  mocks.state.channelCalls.length = 0;
  mocks.state.channelTables.length = 0;
  mocks.state.rpcCalls.length = 0;
  mocks.state.authCallback = null;
});

describe("community chat signed-in workspace", () => {
  it("renders one unread safely and does not restart realtime for duplicate auth events", async () => {
    Element.prototype.scrollIntoView = vi.fn();
    HTMLElement.prototype.scrollTo = vi.fn();

    render(<GlobalChatWorkspace />);

    await waitFor(() => expect(screen.getAllByText("i am fine you say").length).toBeGreaterThan(0));
    expect(screen.getAllByText("how are you").length).toBeGreaterThan(0);
    expect(screen.getByText("Unread messages")).not.toBeNull();
    expect(screen.getByLabelText("ProGlass community chat room").className).toContain("max-w-[430px]");
    expect(screen.getByText("Live now")).not.toBeNull();
    expect(screen.getAllByText("Tech With Yassh").length).toBeGreaterThan(0);
    expect(screen.getAllByText("💎").length).toBeGreaterThan(0);
    expect(screen.getAllByText("⭐").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Seen by 1 · Info").length).toBeGreaterThan(0);
    expect(mocks.client.realtime.setAuth).toHaveBeenCalledWith("test-access-token");
    expect(mocks.state.channelTables).toEqual(
      expect.arrayContaining(["global_chat_messages", "global_chat_receipts", "user_profiles"]),
    );
    await waitFor(() =>
      expect(mocks.state.rpcCalls).toEqual(
        expect.arrayContaining([
          expect.objectContaining({
            name: "mark_global_chat_receipts_read",
            args: { p_message_ids: ["e1553ac8-5e71-4fd0-b8ff-f6ebb705ffa2"] },
          }),
        ]),
      ),
    );
    expect(mocks.state.channelCalls).toHaveLength(1);
    expect(mocks.state.channelCalls[0]).toMatch(new RegExp(`^global-community-chat-${mocks.me.id}-`));

    await act(async () => {
      mocks.state.authCallback?.("TOKEN_REFRESHED", {
        user: { ...mocks.me, user_metadata: { ...mocks.me.user_metadata } },
        access_token: "test-access-token",
      });
    });

    await waitFor(() => expect(screen.getAllByText("i am fine you say").length).toBeGreaterThan(0));
    // INITIAL_SESSION/getSession/TOKEN_REFRESHED can describe the same user;
    // none of those duplicates may tear down and recreate the Realtime channel.
    expect(mocks.state.channelCalls).toHaveLength(1);
  });

  it("keeps the loaded room visible when Realtime setup throws", async () => {
    Element.prototype.scrollIntoView = vi.fn();
    HTMLElement.prototype.scrollTo = vi.fn();
    mocks.client.channel.mockImplementationOnce(() => {
      throw new Error("Simulated realtime failure");
    });

    render(<GlobalChatWorkspace />);

    await waitFor(() => expect(screen.getAllByText("i am fine you say").length).toBeGreaterThan(0));
    expect(await screen.findByText("Simulated realtime failure")).not.toBeNull();
    expect(screen.getAllByText("how are you").length).toBeGreaterThan(0);
  });
});
