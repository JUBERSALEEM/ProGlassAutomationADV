// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";

const mocks = vi.hoisted(() => {
  const user = {
    id: "0568fecb-0f8e-4f1d-9d77-845a5be0cbbc",
    email: "customer@example.com",
    user_metadata: { full_name: "Customer" },
  };
  const oldSession = { user, access_token: "old-access-token", refresh_token: "saved-refresh-token", expires_at: 4102444800 };
  const newSession = { user, access_token: "new-access-token", refresh_token: "rotated-refresh-token", expires_at: 4102444800 };
  const state: {
    getSessionResult: { data: { session: typeof oldSession | null }; error: Error | null };
    refreshResult: { data: { session: typeof newSession | null }; error: Error | null };
    stored: boolean;
    unavailable: boolean;
  } = {
    getSessionResult: { data: { session: oldSession }, error: null },
    refreshResult: { data: { session: newSession }, error: null },
    stored: true,
    unavailable: false,
  };
  const client = {
    auth: {
      getSession: vi.fn(async () => state.getSessionResult),
      refreshSession: vi.fn(async () => state.refreshResult),
      onAuthStateChange: vi.fn(() => ({ data: { subscription: { unsubscribe: vi.fn() } } })),
      signOut: vi.fn(async () => ({ error: null })),
    },
  };
  return { user, oldSession, newSession, state, client };
});

vi.mock("@/lib/supabase", () => ({
  isSupabaseConfigured: () => true,
  supabaseBrowser: () => mocks.client,
  hasStoredSupabaseSession: () => mocks.state.stored,
  isSupabaseUnavailableError: () => mocks.state.unavailable,
  supabaseAuthStorageKey: () => "sb-test-auth-token",
}));

import {
  recoverWebsiteSessionAfterUnauthorized,
  resetWebsiteSessionForTests,
  useWebsiteSession,
} from "@/lib/website-session";

afterEach(() => {
  cleanup();
  resetWebsiteSessionForTests();
  vi.clearAllMocks();
  mocks.state.getSessionResult = { data: { session: mocks.oldSession }, error: null };
  mocks.state.refreshResult = { data: { session: mocks.newSession }, error: null };
  mocks.state.stored = true;
  mocks.state.unavailable = false;
});

describe("shared website session", () => {
  it("refreshes a stored session when the first session read looks invalid", async () => {
    mocks.state.getSessionResult = {
      data: { session: null },
      error: new Error("Invalid Refresh Token: Refresh Token Not Found"),
    };

    const { result } = renderHook(() => useWebsiteSession());

    await waitFor(() => expect(result.current.accessToken).toBe("new-access-token"));
    expect(result.current.userEmail).toBe("customer@example.com");
    expect(result.current.isAuthRequired).toBe(false);
    expect(mocks.client.auth.refreshSession).toHaveBeenCalledTimes(1);
  });

  it("does not declare sign-out when authentication is temporarily unreachable", async () => {
    mocks.state.unavailable = true;
    mocks.state.getSessionResult = { data: { session: null }, error: new Error("Failed to fetch") };
    mocks.state.refreshResult = { data: { session: null }, error: new Error("Failed to fetch") };

    const { result } = renderHook(() => useWebsiteSession());

    await waitFor(() => expect(result.current.checking).toBe(false));
    expect(result.current.unavailable).toBe(true);
    expect(result.current.isAuthRequired).toBe(false);
    expect(result.current.error).toMatch(/saved sign-in could not be confirmed/i);
  });

  it("force-refreshes once after an API 401 instead of discarding the saved login", async () => {
    const { result } = renderHook(() => useWebsiteSession());
    await waitFor(() => expect(result.current.accessToken).toBe("old-access-token"));

    let recovered: Awaited<ReturnType<typeof recoverWebsiteSessionAfterUnauthorized>>;
    await act(async () => {
      recovered = await recoverWebsiteSessionAfterUnauthorized("old-access-token");
    });

    expect(recovered!.access_token).toBe("new-access-token");
    await waitFor(() => expect(result.current.accessToken).toBe("new-access-token"));
    expect(result.current.isAuthRequired).toBe(false);
  });
});
