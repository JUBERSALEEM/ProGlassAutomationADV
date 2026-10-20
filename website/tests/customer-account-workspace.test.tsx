// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import React from "react";

const mocks = vi.hoisted(() => {
  const me = {
    id: "ee646814-0d2f-47b0-be96-68fcb51dda54",
    email: "techwithyassh@example.com",
    user_metadata: { full_name: "Tech With Yassh" },
  };
  const state: {
    sessionUser: typeof me | null;
    profile: Record<string, string> | null;
    upserts: Array<Record<string, string>>;
    search: string;
    router: { push: ReturnType<typeof vi.fn>; replace: ReturnType<typeof vi.fn>; refresh: ReturnType<typeof vi.fn> };
  } = {
    sessionUser: me,
    profile: {
      user_id: me.id,
      display_name: "Tech With Yassh",
      avatar_icon: "initials",
      avatar_color: "brand",
      updated_at: "2026-10-19T07:35:00.000Z",
    },
    upserts: [],
    search: "",
    router: { push: vi.fn(), replace: vi.fn(), refresh: vi.fn() },
  };

  class Query {
    select() {
      return this;
    }
    eq() {
      return this;
    }
    maybeSingle() {
      return Promise.resolve({ data: state.profile, error: null });
    }
    upsert(payload: Record<string, string>) {
      state.upserts.push(payload);
      state.profile = payload;
      return this;
    }
    single() {
      return Promise.resolve({ data: state.profile, error: null });
    }
  }

  const client = {
    auth: {
      getSession: vi.fn(async () => ({
        data: { session: state.sessionUser ? { user: state.sessionUser, access_token: "test-access-token", expires_at: 4102444800 } : null },
        error: null,
      })),
      onAuthStateChange: vi.fn(() => ({ data: { subscription: { unsubscribe: vi.fn() } } })),
      updateUser: vi.fn(async (input: { data?: Record<string, unknown>; password?: string }) => ({
        data: {
          user: state.sessionUser
            ? { ...state.sessionUser, user_metadata: { ...state.sessionUser.user_metadata, ...(input.data ?? {}) } }
            : null,
        },
        error: null,
      })),
      signOut: vi.fn(async () => ({ error: null })),
    },
    from: vi.fn(() => new Query()),
  };

  return { me, state, client };
});

vi.mock("@/lib/supabase", () => ({
  isSupabaseConfigured: () => true,
  supabaseBrowser: () => mocks.client,
  hasStoredSupabaseSession: () => Boolean(mocks.state.sessionUser),
  isSupabaseUnavailableError: () => false,
  supabaseAuthStorageKey: () => "sb-test-auth-token",
}));

vi.mock("next/navigation", () => ({
  useRouter: () => mocks.state.router,
  useSearchParams: () => new URLSearchParams(mocks.state.search),
}));

vi.mock("@/components/reports/ReportsAuthForm", () => ({
  ReportsAuthForm: () => React.createElement("div", null, "Customer sign-in form"),
  safeAuthRedirect: (value: string | null, fallback: string) =>
    value && value.startsWith("/") && !value.startsWith("//") ? value : fallback,
}));

vi.mock("@/components/ui/LocaleLink", () => ({
  LocaleLink: ({ href, children, ...rest }: { href: string; children: React.ReactNode }) =>
    React.createElement("a", { href, ...rest }, children),
}));

import { CustomerAccountWorkspace } from "@/components/account/CustomerAccountWorkspace";
import { resetWebsiteSessionForTests } from "@/lib/website-session";

afterEach(() => {
  cleanup();
  resetWebsiteSessionForTests();
  vi.clearAllMocks();
  mocks.state.sessionUser = mocks.me;
  mocks.state.profile = {
    user_id: mocks.me.id,
    display_name: "Tech With Yassh",
    avatar_icon: "initials",
    avatar_color: "brand",
    updated_at: "2026-10-19T07:35:00.000Z",
  };
  mocks.state.upserts.length = 0;
  mocks.state.search = "";
});

describe("customer account workspace", () => {
  it("recognizes the saved session and saves a selected chat icon", async () => {
    render(<CustomerAccountWorkspace />);

    expect(await screen.findByRole("heading", { name: "Tech With Yassh" })).not.toBeNull();
    expect(screen.queryByText("Customer sign-in form")).toBeNull();
    expect(screen.getAllByText("techwithyassh@example.com").length).toBeGreaterThan(0);
    expect(screen.getByText("✓ Saved sign-in active")).not.toBeNull();

    fireEvent.change(screen.getByLabelText("Display name"), { target: { value: "Yassh from ProGlass" } });
    fireEvent.click(screen.getByRole("button", { name: /Crystal/ }));
    fireEvent.click(screen.getByRole("button", { name: /Sky/ }));
    fireEvent.click(screen.getByRole("button", { name: "Save profile" }));

    await waitFor(() => expect(mocks.state.upserts).toHaveLength(1));
    expect(mocks.state.upserts[0]).toMatchObject({
      user_id: mocks.me.id,
      display_name: "Yassh from ProGlass",
      avatar_icon: "gem",
      avatar_color: "sky",
    });
    expect(mocks.client.auth.updateUser).toHaveBeenCalledWith(
      expect.objectContaining({
        data: expect.objectContaining({
          full_name: "Yassh from ProGlass",
          avatar_icon: "gem",
          avatar_color: "sky",
        }),
      }),
    );
    expect(await screen.findByText("Profile saved. Your name and icon now update live in community chat.")).not.toBeNull();
  });

  it("automatically continues an explicit redirect when the saved session exists", async () => {
    mocks.state.search = "redirect=%2Fchat";

    render(<CustomerAccountWorkspace />);

    expect(await screen.findByText("Saved sign-in found")).not.toBeNull();
    await waitFor(() => expect(mocks.state.router.replace).toHaveBeenCalledWith("/chat"), { timeout: 2000 });
    expect(screen.queryByText("Customer sign-in form")).toBeNull();
  });

  it("shows the sign-in form and saved-session explanation only when signed out", async () => {
    mocks.state.sessionUser = null;

    render(<CustomerAccountWorkspace />);

    expect(await screen.findByText("Customer sign-in form")).not.toBeNull();
    expect(screen.getByText("One saved sign-in for your projects and chats.")).not.toBeNull();
    expect(screen.getByText("Saved secure session")).not.toBeNull();
    expect(screen.queryByText("Your WhatsApp-style chat profile")).toBeNull();
  });
});
