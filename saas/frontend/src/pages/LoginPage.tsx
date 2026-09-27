import { useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { errorText, useAuth } from "../auth/AuthContext";
import { ApiError } from "../api/client";

/**
 * Login — POST /api/v1/auth/login { tenantSlug, username, password }.
 * Failures arrive as the { code, message } envelope: 401 bad
 * credentials, 423 lockout (with the server's Retry-After semantics).
 */
export default function LoginPage() {
  const { login, busy } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [tenantSlug, setTenantSlug] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);

  const from = (location.state as { from?: string } | null)?.from ?? "/";

  async function onSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    try {
      await login(tenantSlug.trim(), username.trim(), password);
      navigate(from, { replace: true });
    } catch (err) {
      if (err instanceof ApiError && err.status === 423) {
        setError(`${err.message} — the account is temporarily locked.`);
      } else {
        setError(errorText(err));
      }
    }
  }

  return (
    <div className="login-wrap">
      <form className="login-card" onSubmit={onSubmit}>
        <div className="brand login-brand">
          <span className="brand-mark">PG</span>
          <div>
            <div className="brand-name">ProGlass Cloud</div>
            <div className="brand-sub">Sign in to your tenant</div>
          </div>
        </div>

        <label>
          Tenant
          <input
            value={tenantSlug}
            onChange={(e) => setTenantSlug(e.target.value)}
            placeholder="company-slug"
            autoComplete="organization"
            required
            autoFocus
          />
        </label>
        <label>
          Username
          <input
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="username"
            required
          />
        </label>
        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />
        </label>

        {error && <div className="note bad">{error}</div>}

        <button className="btn primary" type="submit" disabled={busy}>
          {busy ? "Signing in…" : "Sign in"}
        </button>

        <p className="login-hint">
          Accounts are provisioned by your tenant administrator. Imported
          WPF users must reset their password on first sign-in
          (must-reset flag).
        </p>
      </form>
    </div>
  );
}
