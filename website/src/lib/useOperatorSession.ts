"use client";

/**
 * One operator session, two dashboards.
 *
 * The Reports dashboard and the Deliveries Tracker stand behind the same door:
 * the same Supabase project, the same Row Level Security policies, the same
 * operator account. Before this hook existed, the session logic lived inside
 * `RecordsDashboard` — and the second page that needed it would either have
 * copied it (two behaviours to keep in step) or silently skipped it (one page
 * protected, one page open). Both are the kind of bug that shows up after the
 * release, in production, on the page nobody re-tested.
 *
 * So the logic lives here: check the session once, subscribe to auth changes,
 * expose the access token so the API route can forward it to PostgREST as the
 * `authenticated` role, and sign out through both the client library and the
 * server's cookie route.
 */

import { useCallback, useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { isSupabaseConfigured, supabaseBrowser } from "@/lib/supabase";

export type OperatorSession = {
  /** True while the first session check is in flight. */
  checking: boolean;
  userEmail: string | null;
  /** The JWT forwarded to `/api/…` so PostgREST runs as this operator. */
  authToken: string | null;
  /** True when the page must show the sign-in gate instead of data. */
  isAuthRequired: boolean;
  signOut: () => Promise<void>;
  /**
   * Close the gate because the server said the session is no longer valid.
   *
   * A 401 from `/api/…` is the authoritative answer — the token the browser
   * still believes in has been revoked or expired — so the page stops asking
   * and shows the sign-in panel rather than looping on failed requests.
   */
  requireAuth: () => void;
};

/** Where a signed-out operator is sent, and where they return afterwards. */
const SIGN_IN_PATH = "/reports/login";

export function useOperatorSession(): OperatorSession {
  const router = useRouter();
  const pathname = usePathname();

  const [checking, setChecking] = useState(() => isSupabaseConfigured());
  const [userEmail, setUserEmail] = useState<string | null>(null);
  const [authToken, setAuthToken] = useState<string | null>(null);
  const [isAuthRequired, setIsAuthRequired] = useState(false);

  useEffect(() => {
    if (!isSupabaseConfigured()) return;

    let isSubscribed = true;

    async function checkSession() {
      try {
        const supabase = supabaseBrowser();
        const {
          data: { session },
        } = await supabase.auth.getSession();
        if (!isSubscribed) return;

        if (session?.user) {
          setUserEmail(session.user.email ?? null);
          setAuthToken(session.access_token);
          setIsAuthRequired(false);
        } else if (process.env.NODE_ENV === "production") {
          setIsAuthRequired(true);
        }
      } catch (error) {
        console.warn("[operator-session] Session check failed:", error);
      } finally {
        if (isSubscribed) setChecking(false);
      }
    }

    checkSession();

    let unsubscribe = () => {};
    try {
      const {
        data: { subscription },
      } = supabaseBrowser().auth.onAuthStateChange((_event, session) => {
        if (!isSubscribed) return;
        if (session?.user) {
          setUserEmail(session.user.email ?? null);
          setAuthToken(session.access_token);
          setIsAuthRequired(false);
        } else {
          setUserEmail(null);
          setAuthToken(null);
          if (process.env.NODE_ENV === "production") setIsAuthRequired(true);
        }
      });
      unsubscribe = () => subscription.unsubscribe();
    } catch {
      // No auth event stream available — the initial check above is enough.
    }

    return () => {
      isSubscribed = false;
      unsubscribe();
    };
  }, []);

  const signOut = useCallback(async () => {
    try {
      if (isSupabaseConfigured()) {
        await supabaseBrowser().auth.signOut();
      }
      try {
        await fetch("/api/auth/signout", { method: "POST" });
      } catch {
        // The client library already cleared the local session; the server
        // cookie route is a courtesy, not a dependency.
      }
    } finally {
      setUserEmail(null);
      setAuthToken(null);
      setIsAuthRequired(true);
      router.push(`${SIGN_IN_PATH}?redirect=${encodeURIComponent(pathname)}`);
    }
  }, [pathname, router]);

  const requireAuth = useCallback(() => {
    setAuthToken(null);
    setIsAuthRequired(true);
  }, []);

  return { checking, userEmail, authToken, isAuthRequired, signOut, requireAuth };
}
