/**
 * The single fetch path for the SPA (Phase 28).
 *
 * - Relative URLs ONLY: same-origin /api/v1 in production (nginx edge),
 *   the Vite dev proxy in development. No host is ever hardcoded.
 * - Bearer access token; a 401 triggers ONE refresh attempt, then the
 *   session ends (the refresh-token rotation is server-side, Phase 4).
 * - Every failure normalizes to ApiError { status, code, message } —
 *   the API's { code, message } envelope (clients branch on the CODE).
 */

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    message: string,
  ) {
    super(message);
  }
}

/** The API's shared error envelope (GlobalExceptionHandlerMiddleware). */
interface ErrorEnvelope {
  code?: string;
  message?: string;
}

export interface TokenPair {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
}

export interface AuthSession {
  tenantId: string;
  tenantSlug: string;
  userId: string;
  username: string;
  displayName: string;
  role: string;
  mustResetPassword: boolean;
  tokens: TokenPair;
}

const STORAGE_KEY = "pglass.session";

export function loadSession(): AuthSession | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthSession) : null;
  } catch {
    return null;
  }
}

export function saveSession(session: AuthSession | null): void {
  if (session) localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  else localStorage.removeItem(STORAGE_KEY);
}

let session: AuthSession | null = loadSession();

export function currentSession(): AuthSession | null {
  return session;
}

function applySession(next: AuthSession | null): void {
  session = next;
  saveSession(next);
}

async function rawRequest(
  method: string,
  path: string,
  body?: unknown,
  token?: string,
): Promise<Response> {
  return fetch(path, {
    method,
    headers: {
      ...(body !== undefined ? { "Content-Type": "application/json" } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
  });
}

async function parse<T>(response: Response): Promise<T> {
  if (response.status === 204) return undefined as T;
  const text = await response.text();
  const json = text ? (JSON.parse(text) as T) : (undefined as T);
  return json;
}

function toApiError(response: Response, payload: unknown): ApiError {
  const envelope = (payload ?? {}) as ErrorEnvelope;
  return new ApiError(
    response.status,
    envelope.code ?? `http.${response.status}`,
    envelope.message ?? response.statusText ?? "Request failed",
  );
}

/** Login (anonymous) — stores the session. */
export async function login(
  tenantSlug: string,
  username: string,
  password: string,
): Promise<AuthSession> {
  const response = await rawRequest("POST", "/api/v1/auth/login", {
    tenantSlug,
    username,
    password,
  });
  const payload = await parse<AuthSession | ErrorEnvelope>(response);
  if (!response.ok) throw toApiError(response, payload);
  applySession(payload as AuthSession);
  return payload as AuthSession;
}

/** Logout — revokes the refresh token server-side, clears local state. */
export async function logout(): Promise<void> {
  const refresh = session?.tokens.refreshToken;
  if (refresh) {
    // Best effort — an expired access token is still accepted here
    // (the refresh token identifies the session).
    await rawRequest("POST", "/api/v1/auth/logout", { refreshToken: refresh }).catch(
      () => undefined,
    );
  }
  applySession(null);
}

async function refreshTokens(): Promise<boolean> {
  const refresh = session?.tokens.refreshToken;
  if (!refresh) return false;
  const response = await rawRequest("POST", "/api/v1/auth/refresh", {
    refreshToken: refresh,
  });
  if (!response.ok) return false;
  const tokens = await parse<TokenPair>(response);
  applySession({ ...(session as AuthSession), tokens });
  return true;
}

export async function api<T>(
  method: string,
  path: string,
  body?: unknown,
): Promise<T> {
  const token = session?.tokens.accessToken;
  let response = await rawRequest(method, path, body, token);
  if (response.status === 401 && (await refreshTokens())) {
    response = await rawRequest(method, path, body, session?.tokens.accessToken);
  }
  if (response.status === 401) applySession(null);
  const payload = response.status === 204 ? undefined : await response.text().then((t) => (t ? JSON.parse(t) : undefined));
  if (!response.ok) throw toApiError(response, payload);
  return payload as T;
}

/** GET helper. */
export const get = <T>(path: string) => api<T>("GET", path);
/** POST helper. */
export const post = <T>(path: string, body?: unknown) => api<T>("POST", path, body);
/** PUT helper. */
export const put = <T>(path: string, body?: unknown) => api<T>("PUT", path, body);
/** DELETE helper. */
export const del = <T>(path: string) => api<T>("DELETE", path);
