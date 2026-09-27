/**
 * Auth context — the session lives in the API client (localStorage +
 * Bearer token); this context just exposes it to React and invalidates
 * the query cache on login/logout (no tenant data from a previous
 * session may survive).
 */
import { useQueryClient } from "@tanstack/react-query";
import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { ApiError, currentSession, login as apiLogin, logout as apiLogout, type AuthSession } from "../api/client";

interface AuthState {
  session: AuthSession | null;
  busy: boolean;
  login: (tenantSlug: string, username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(currentSession());
  const [busy, setBusy] = useState(false);
  const queryClient = useQueryClient();

  const login = useCallback(
    async (tenantSlug: string, username: string, password: string) => {
      setBusy(true);
      try {
        const next = await apiLogin(tenantSlug, username, password);
        queryClient.clear();
        setSession(next);
      } finally {
        setBusy(false);
      }
    },
    [queryClient],
  );

  const logout = useCallback(async () => {
    await apiLogout();
    queryClient.clear();
    setSession(null);
  }, [queryClient]);

  const value = useMemo(
    () => ({ session, busy, login, logout }),
    [session, busy, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth outside AuthProvider");
  return ctx;
}

/** Server-driven failures rendered by forms/actions. */
export function errorText(error: unknown): string {
  if (error instanceof ApiError) return `${error.message} (${error.code})`;
  if (error instanceof Error) return error.message;
  return String(error);
}
