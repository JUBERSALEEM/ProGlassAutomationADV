"use client";

/**
 * The single website session used by Account, community chat, private
 * inquiry chat and the support bubble.
 *
 * Before this module, each surface owned a slightly different copy of the
 * same getSession/onAuthStateChange logic. That made a valid saved login
 * look different from page to page: one surface could still hold the saved
 * Supabase session while another had locally discarded its token after a
 * single API 401. The store below keeps one browser-tab snapshot, starts
 * one auth listener, recovers/refreshes the persisted Supabase session
 * before declaring the customer signed out, and synchronizes sign-in/out
 * across tabs through the shared Supabase storage key.
 *
 * The persisted credential itself remains Supabase's: `supabaseBrowser()`
 * uses the explicit shared `sb-<project>-auth-token` local-storage key. This
 * module never invents or mirrors a second credential format.
 */

import { useCallback, useEffect, useSyncExternalStore } from "react";
import type { Session, User } from "@supabase/supabase-js";
import {
  hasStoredSupabaseSession,
  isSupabaseConfigured,
  isSupabaseUnavailableError,
  supabaseAuthStorageKey,
  supabaseBrowser,
} from "@/lib/supabase";

export type WebsiteSessionState = {
  checking: boolean;
  session: Session | null;
  user: User | null;
  accessToken: string | null;
  userEmail: string | null;
  /** True only after recovery proves there is no usable saved session. */
  isAuthRequired: boolean;
  /** True when auth could not be reached; this is not proof of sign-out. */
  unavailable: boolean;
  error: string | null;
};

const INITIAL_STATE: WebsiteSessionState = {
  checking: true,
  session: null,
  user: null,
  accessToken: null,
  userEmail: null,
  isAuthRequired: false,
  unavailable: false,
  error: null,
};

let state: WebsiteSessionState = INITIAL_STATE;
let started = false;
let authSubscription: { unsubscribe: () => void } | null = null;
let browserListenersAttached = false;
let recoveryPromise: Promise<Session | null> | null = null;
let lastForcedRecoveryAt = 0;
let lastForcedRecoveryToken = "";

const listeners = new Set<() => void>();

function userSignature(user: User | null): string {
  if (!user) return "";
  try {
    return `${user.id}\n${user.email ?? ""}\n${JSON.stringify(user.user_metadata ?? {})}`;
  } catch {
    return `${user.id}\n${user.email ?? ""}`;
  }
}

function sameState(left: WebsiteSessionState, right: WebsiteSessionState): boolean {
  return (
    left.checking === right.checking &&
    left.accessToken === right.accessToken &&
    userSignature(left.user) === userSignature(right.user) &&
    left.userEmail === right.userEmail &&
    left.isAuthRequired === right.isAuthRequired &&
    left.unavailable === right.unavailable &&
    left.error === right.error
  );
}

function emit(next: WebsiteSessionState) {
  if (sameState(state, next)) {
    // Keep the freshest Session object even when its visible identity is the
    // same; later expiry checks read expires_at from it.
    state = { ...state, session: next.session };
    return;
  }
  state = next;
  for (const listener of listeners) listener();
}

function errorMessage(error: unknown): string {
  if (error instanceof Error && error.message.trim()) return error.message;
  if (typeof error === "object" && error !== null && "message" in error) {
    const message = (error as { message?: unknown }).message;
    if (typeof message === "string" && message.trim()) return message;
  }
  return "Unable to confirm your saved sign-in.";
}

function looksUnavailable(error: unknown): boolean {
  if (!error) return false;
  const record =
    typeof error === "object"
      ? (error as { name?: string; message?: string; status?: number; code?: string; details?: string })
      : undefined;
  if (record && isSupabaseUnavailableError(record)) return true;
  const text = `${record?.name ?? ""} ${errorMessage(error)}`;
  return /failed to fetch|fetch failed|network|load failed|networkrequestfailed|timed out|timeout/i.test(text);
}

function applySession(session: Session | null) {
  if (!session?.user || !session.access_token) {
    applySignedOut();
    return;
  }
  emit({
    checking: false,
    session,
    user: session.user,
    accessToken: session.access_token,
    userEmail: session.user.email ?? null,
    isAuthRequired: false,
    unavailable: false,
    error: null,
  });
}

function applySignedOut() {
  emit({
    checking: false,
    session: null,
    user: null,
    accessToken: null,
    userEmail: null,
    isAuthRequired: true,
    unavailable: false,
    error: null,
  });
}

function applyUnavailable(error: unknown) {
  emit({
    ...state,
    checking: false,
    // A transport failure is not an auth answer. Keep any session we already
    // know about and, crucially, do not convert it into a sign-in prompt.
    isAuthRequired: false,
    unavailable: true,
    error: `Your saved sign-in could not be confirmed because the authentication service is temporarily unreachable. ${errorMessage(error)}`,
  });
}

type RefreshResult = {
  session: Session | null;
  error: unknown;
  unavailable: boolean;
};

async function refreshSavedSession(): Promise<RefreshResult> {
  try {
    const auth = supabaseBrowser().auth;
    if (typeof auth.refreshSession !== "function") {
      return { session: null, error: new Error("Session refresh is unavailable."), unavailable: true };
    }
    const { data, error } = await auth.refreshSession();
    if (error) return { session: null, error, unavailable: looksUnavailable(error) };
    return { session: data.session ?? null, error: null, unavailable: false };
  } catch (error) {
    return { session: null, error, unavailable: looksUnavailable(error) };
  }
}

/**
 * Recover the persisted session once, deduplicating concurrent callers.
 *
 * `getSession()` already refreshes an expired access token. The explicit
 * refresh below is the second chance for the case that produced the login
 * loop: a page/API believed the browser token was dead while the refresh
 * token in shared storage was still usable.
 */
export async function recoverWebsiteSession(): Promise<Session | null> {
  if (!isSupabaseConfigured()) {
    emit({ ...INITIAL_STATE, checking: false, unavailable: true, error: "Sign-in is not configured in this deployment." });
    return null;
  }
  if (recoveryPromise) return recoveryPromise;

  recoveryPromise = (async () => {
    const hadStoredSession = hasStoredSupabaseSession();
    try {
      const { data, error } = await supabaseBrowser().auth.getSession();
      if (data.session?.user && data.session.access_token) {
        applySession(data.session);
        return data.session;
      }

      if (error || hadStoredSession || state.session) {
        const refreshed = await refreshSavedSession();
        if (refreshed.session?.user && refreshed.session.access_token) {
          applySession(refreshed.session);
          return refreshed.session;
        }
        if (refreshed.unavailable || looksUnavailable(error)) {
          applyUnavailable(refreshed.error ?? error);
          return null;
        }
      }

      applySignedOut();
      return null;
    } catch (error) {
      if (looksUnavailable(error)) {
        const refreshed = await refreshSavedSession();
        if (refreshed.session?.user && refreshed.session.access_token) {
          applySession(refreshed.session);
          return refreshed.session;
        }
        applyUnavailable(refreshed.error ?? error);
        return null;
      }
      if (hadStoredSession || state.session) {
        const refreshed = await refreshSavedSession();
        if (refreshed.session?.user && refreshed.session.access_token) {
          applySession(refreshed.session);
          return refreshed.session;
        }
        if (refreshed.unavailable) {
          applyUnavailable(refreshed.error ?? error);
          return null;
        }
      }
      applySignedOut();
      return null;
    }
  })().finally(() => {
    recoveryPromise = null;
  });

  return recoveryPromise;
}

/**
 * Recover after an API route returns 401.
 *
 * A 401 is treated as "prove the saved session is really dead" rather than
 * as permission to erase the page's token. If recovery produces a different
 * access token, subscribed pages rerender and their token-bound effects run
 * again with the fresh token. A short cooldown prevents a permanently
 * rejected token from causing a refresh loop.
 */
export async function recoverWebsiteSessionAfterUnauthorized(
  previousToken?: string | null,
): Promise<Session | null> {
  const first = await recoverWebsiteSession();
  if (first?.access_token && first.access_token !== previousToken) return first;
  if (!first) return null;

  const now = Date.now();
  if (lastForcedRecoveryToken === first.access_token || now - lastForcedRecoveryAt < 15_000) {
    applySignedOut();
    return null;
  }
  lastForcedRecoveryAt = now;
  lastForcedRecoveryToken = first.access_token;

  const refreshed = await refreshSavedSession();
  if (refreshed.session?.user && refreshed.session.access_token) {
    applySession(refreshed.session);
    return refreshed.session;
  }
  if (refreshed.unavailable) {
    applyUnavailable(refreshed.error);
    return null;
  }
  applySignedOut();
  return null;
}

export async function signOutWebsiteSession(): Promise<void> {
  try {
    if (isSupabaseConfigured()) await supabaseBrowser().auth.signOut();
  } finally {
    applySignedOut();
  }
}

function recoverIfNeeded() {
  if (!started || typeof document !== "undefined" && document.visibilityState === "hidden") return;
  if (!state.session) {
    if (hasStoredSupabaseSession()) void recoverWebsiteSession();
    return;
  }
  const expiresAt = state.session.expires_at;
  if (typeof expiresAt === "number" && expiresAt * 1000 - Date.now() < 120_000) {
    void recoverWebsiteSession();
  }
}

function attachBrowserListeners() {
  if (browserListenersAttached || typeof window === "undefined") return;
  browserListenersAttached = true;

  window.addEventListener("storage", (event) => {
    if (event.key !== null && event.key !== supabaseAuthStorageKey()) return;
    // Another tab signed in, refreshed or signed out. Re-read the shared
    // Supabase storage instead of keeping this tab's stale answer.
    window.setTimeout(() => void recoverWebsiteSession(), 0);
  });
  window.addEventListener("focus", recoverIfNeeded);
  document.addEventListener("visibilitychange", recoverIfNeeded);
  window.addEventListener("online", recoverIfNeeded);
}

function ensureWebsiteSessionStarted() {
  if (started || typeof window === "undefined") return;
  started = true;

  if (!isSupabaseConfigured()) {
    emit({ ...INITIAL_STATE, checking: false, unavailable: true, error: "Sign-in is not configured in this deployment." });
    return;
  }

  try {
    const { data } = supabaseBrowser().auth.onAuthStateChange((event, session) => {
      if (session?.user && session.access_token) {
        applySession(session);
        return;
      }
      if (event === "SIGNED_OUT") {
        applySignedOut();
        return;
      }
      // Do not call another Supabase auth method synchronously inside the
      // auth callback. Schedule recovery so an INITIAL_SESSION/null race
      // cannot erase a session that shared storage can still prove.
      window.setTimeout(() => void recoverWebsiteSession(), 0);
    });
    authSubscription = data.subscription;
  } catch {
    // Recovery below still gives the page one authoritative session read.
  }

  attachBrowserListeners();
  void recoverWebsiteSession();
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function getSnapshot(): WebsiteSessionState {
  return state;
}

function getServerSnapshot(): WebsiteSessionState {
  return INITIAL_STATE;
}

export type WebsiteSession = WebsiteSessionState & {
  refreshSession: () => Promise<Session | null>;
  recoverAfterUnauthorized: () => Promise<Session | null>;
  requireAuth: () => void;
  signOut: () => Promise<void>;
};

export function useWebsiteSession({ enabled = true }: { enabled?: boolean } = {}): WebsiteSession {
  const snapshot = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);

  useEffect(() => {
    if (enabled) ensureWebsiteSessionStarted();
  }, [enabled]);

  const refreshSession = useCallback(() => recoverWebsiteSession(), []);
  const recoverAfterUnauthorized = useCallback(
    () => recoverWebsiteSessionAfterUnauthorized(snapshot.accessToken),
    [snapshot.accessToken],
  );
  const requireAuth = useCallback(() => {
    void recoverAfterUnauthorized();
  }, [recoverAfterUnauthorized]);
  const signOut = useCallback(() => signOutWebsiteSession(), []);

  return { ...snapshot, refreshSession, recoverAfterUnauthorized, requireAuth, signOut };
}

/** Test-only isolation for the tab-lifetime store. */
export function resetWebsiteSessionForTests() {
  try {
    authSubscription?.unsubscribe();
  } catch {
    // Test doubles do not need real teardown semantics.
  }
  authSubscription = null;
  started = false;
  recoveryPromise = null;
  lastForcedRecoveryAt = 0;
  lastForcedRecoveryToken = "";
  state = INITIAL_STATE;
  for (const listener of listeners) listener();
}
