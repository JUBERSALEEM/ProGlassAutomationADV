"use client";

/**
 * Operator/customer API session adapter.
 *
 * The actual saved-login state now lives in `useWebsiteSession`, the one
 * shared browser session store used by Account and both chats. This hook
 * retains the older operator-facing shape (email + bearer token + reports
 * sign-out redirect) without owning a second, divergent copy of Supabase
 * session state. In particular, `requireAuth()` no longer erases the token
 * locally on the first API 401: it asks the shared store to recover or
 * refresh the persisted Supabase session and only gates the page if that
 * recovery proves the session is really gone.
 */

import { useCallback } from "react";
import { usePathname, useRouter } from "next/navigation";
import type { Session } from "@supabase/supabase-js";
import { useWebsiteSession } from "@/lib/website-session";

export type OperatorSession = {
  /** True while the shared saved-session check/recovery is in flight. */
  checking: boolean;
  userEmail: string | null;
  /** The JWT forwarded to `/api/…` so PostgREST runs as this user. */
  authToken: string | null;
  /** True when recovery proves the page must show the sign-in gate. */
  isAuthRequired: boolean;
  /** True when auth was unreachable; callers should offer retry, not login. */
  unavailable: boolean;
  error: string | null;
  refreshSession: () => Promise<Session | null>;
  signOut: () => Promise<void>;
  /**
   * Recover after an API 401. A refreshed token updates every subscribed
   * chat/page at once; a genuinely revoked/expired session then gates.
   */
  requireAuth: () => void;
};

/** Where a signed-out operator is sent, and where they return afterwards. */
const SIGN_IN_PATH = "/reports/login";

export function useOperatorSession(): OperatorSession {
  const router = useRouter();
  const pathname = usePathname();
  const session = useWebsiteSession();
  const { signOut: signOutSharedSession } = session;

  const signOut = useCallback(async () => {
    try {
      await signOutSharedSession();
      try {
        await fetch("/api/auth/signout", { method: "POST" });
      } catch {
        // The shared Supabase session is already cleared; the server cookie
        // route remains a courtesy for legacy operator surfaces.
      }
    } finally {
      const redirect = pathname || "/reports";
      router.push(`${SIGN_IN_PATH}?redirect=${encodeURIComponent(redirect)}`);
    }
  }, [pathname, router, signOutSharedSession]);

  return {
    checking: session.checking,
    userEmail: session.userEmail,
    authToken: session.accessToken,
    // Preserve the historical local-development behaviour for internal
    // operator workspaces; production uses the shared store's authoritative
    // signed-out answer. Customer chat also checks for a token directly.
    isAuthRequired: session.isAuthRequired && process.env.NODE_ENV === "production",
    unavailable: session.unavailable,
    error: session.error,
    refreshSession: session.refreshSession,
    signOut,
    requireAuth: session.requireAuth,
  };
}
