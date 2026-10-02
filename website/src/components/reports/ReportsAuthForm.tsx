"use client";

import { useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { isSupabaseConfigured, supabaseBrowser } from "@/lib/supabase";
import { Logo } from "@/components/brand/Logo";
import { Button } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { useT } from "@/i18n/provider";

export function ReportsAuthForm() {
  const t = useT();
  const router = useRouter();
  const searchParams = useSearchParams();
  const redirectTo = searchParams.get("redirect") || "/reports";

  const [mode, setMode] = useState<"signin" | "register">("signin");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const configured = isSupabaseConfigured();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setNotice(null);

    const cleanEmail = email.trim();
    if (!cleanEmail) {
      setError(t("Please enter your email address."));
      return;
    }

    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailPattern.test(cleanEmail)) {
      setError(t("Please enter a valid email address."));
      return;
    }

    if (password.length < 6) {
      setError(t("Password must be at least 6 characters."));
      return;
    }

    if (mode === "register" && password !== confirmPassword) {
      setError(t("Passwords do not match."));
      return;
    }

    setLoading(true);

    try {
      // 1. First attempt authenticated request via server-side route
      // This protects against browser CORS failures, ad-blockers, and network fetch rejections
      const endpoint = mode === "signin" ? "/api/auth/signin" : "/api/auth/signup";
      let res: Response | null = null;
      let serverJson: {
        ok: boolean;
        session?: { access_token: string; refresh_token: string };
        user?: { id: string; email?: string };
        error?: { message?: string; code?: string };
      } | null = null;

      try {
        res = await fetch(endpoint, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ email: cleanEmail, password }),
        });
        serverJson = await res.json();
      } catch (fetchErr) {
        console.warn("[ReportsAuthForm] Server endpoint fetch issue, attempting direct browser client:", fetchErr);
      }

      // 2. If server route gave a decisive response, handle it
      if (serverJson) {
        if (!serverJson.ok) {
          throw new Error(serverJson.error?.message || t("Authentication failed. Please check your credentials."));
        }

        // Successfully authenticated via server route
        if (serverJson.session) {
          try {
            if (configured) {
              const supabase = supabaseBrowser();
              await supabase.auth.setSession({
                access_token: serverJson.session.access_token,
                refresh_token: serverJson.session.refresh_token,
              });
            }
          } catch (syncErr) {
            console.warn("[ReportsAuthForm] Client session sync warning:", syncErr);
          }

          setNotice(
            mode === "signin"
              ? t("Authenticated successfully. Redirecting to reports...")
              : t("Operator account created and authenticated. Redirecting..."),
          );
          router.push(redirectTo);
          router.refresh();
          return;
        }

        // Sign up succeeded but email confirmation is enabled on Supabase
        setNotice(
          t(
            "Account registration submitted. If email confirmation is enabled on your Supabase project, please check your email inbox before logging in.",
          ),
        );
        setMode("signin");
        return;
      }

      // 3. Fallback to direct client-side Supabase Auth if API route is unreachable
      if (!configured) {
        throw new Error(
          t("Supabase credentials are not configured in this environment. In production, configure NEXT_PUBLIC_SUPABASE_URL and NEXT_PUBLIC_SUPABASE_ANON_KEY."),
        );
      }

      const supabase = supabaseBrowser();
      if (mode === "signin") {
        const { error: signInError } = await supabase.auth.signInWithPassword({
          email: cleanEmail,
          password,
        });

        if (signInError) {
          throw signInError;
        }

        setNotice(t("Authenticated successfully. Redirecting to reports..."));
        router.push(redirectTo);
        router.refresh();
      } else {
        const { data: signUpData, error: signUpError } = await supabase.auth.signUp({
          email: cleanEmail,
          password,
        });

        if (signUpError) {
          throw signUpError;
        }

        if (signUpData.session) {
          setNotice(t("Account created and authenticated. Redirecting..."));
          router.push(redirectTo);
          router.refresh();
        } else {
          setNotice(
            t(
              "Account registration submitted. If email confirmation is enabled on your Supabase project, please check your email inbox before logging in.",
            ),
          );
          setMode("signin");
        }
      }
    } catch (err) {
      console.error("[ReportsAuthForm] Authentication exception:", err);
      let message = t("Authentication request failed. Please check your credentials.");
      if (err instanceof Error) {
        if (/fetch failed|failed to fetch|networkerror/i.test(err.message)) {
          message = t(
            "Network connection failed. Please verify your internet connection or check that the server is reachable.",
          );
        } else {
          message = err.message;
        }
      }
      setError(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="w-full max-w-md mx-auto">
      <div className="rounded-panel border border-line-strong/80 bg-ink-900/80 p-7 sm:p-9 shadow-raised backdrop-blur-xl">
        {/* Brand Header */}
        <div className="flex flex-col items-center text-center">
          <Link href="/" aria-label="ProGlass Automation Home" className="transition-transform hover:scale-105">
            <Logo size={42} />
          </Link>

          <div className="mt-4 flex items-center gap-2">
            <span className="eyebrow text-brand-300">{t("Operations Suite")}</span>
            <span className="text-text-faint">/</span>
            <span className="eyebrow text-text-muted">{t("Reports Access")}</span>
          </div>

          <h1 className="mt-2 text-xl font-bold tracking-tight text-text-primary sm:text-2xl">
            {mode === "signin" ? t("Sign In to Reports") : t("Register Operator Account")}
          </h1>
          <p className="mt-2 text-xs leading-relaxed text-text-muted">
            {mode === "signin"
              ? t("Authenticated access to live factory operations, Excel Sync transactions, and financial analytics.")
              : t("Create registered operator credentials to access protected reports and dashboard data.")}
          </p>
        </div>

        {/* Mode Selector Tabs */}
        <div className="mt-6 grid grid-cols-2 gap-1 rounded-xl border border-line-strong/60 bg-ink-950/60 p-1">
          <button
            type="button"
            onClick={() => {
              setMode("signin");
              setError(null);
            }}
            className={`rounded-lg py-2 text-xs font-semibold transition-all ${
              mode === "signin"
                ? "bg-brand-500/20 text-brand-200 shadow-sm border border-brand-400/30"
                : "text-text-muted hover:text-text-primary"
            }`}
          >
            {t("Sign In")}
          </button>
          <button
            type="button"
            onClick={() => {
              setMode("register");
              setError(null);
            }}
            className={`rounded-lg py-2 text-xs font-semibold transition-all ${
              mode === "register"
                ? "bg-brand-500/20 text-brand-200 shadow-sm border border-brand-400/30"
                : "text-text-muted hover:text-text-primary"
            }`}
          >
            {t("Register")}
          </button>
        </div>

        {/* Alerts */}
        {error && (
          <div
            role="alert"
            className="mt-4 rounded-xl border border-state-danger/40 bg-state-danger/10 p-3 text-xs text-state-danger leading-relaxed flex items-start gap-2"
          >
            <span aria-hidden="true" className="shrink-0 font-bold">!</span>
            <span>{error}</span>
          </div>
        )}

        {notice && (
          <div
            role="status"
            className="mt-4 rounded-xl border border-state-success/40 bg-state-success/10 p-3 text-xs text-state-success leading-relaxed flex items-start gap-2"
          >
            <span aria-hidden="true" className="shrink-0 font-bold">✓</span>
            <span>{notice}</span>
          </div>
        )}

        {!configured && (
          <div className="mt-4 rounded-xl border border-state-warning/40 bg-state-warning/10 p-3 text-2xs text-state-warning leading-relaxed">
            {t("Note: Supabase URL/key not configured in local environment. In production, registered users authenticate against Supabase Auth.")}
          </div>
        )}

        {/* Form */}
        <form onSubmit={handleSubmit} className="mt-6 space-y-4">
          <div>
            <label
              htmlFor="reports-email"
              className="block text-2xs font-mono uppercase tracking-wider text-text-secondary font-medium mb-1.5"
            >
              {t("Email address")}
            </label>
            <input
              id="reports-email"
              type="email"
              required
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="operator@proglass.ae"
              className="w-full rounded-xl border border-line-strong/80 bg-ink-950/80 px-3.5 py-2.5 text-sm text-text-primary placeholder:text-text-faint focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400 transition-colors"
            />
          </div>

          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label
                htmlFor="reports-password"
                className="block text-2xs font-mono uppercase tracking-wider text-text-secondary font-medium"
              >
                {t("Password")}
              </label>
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="text-2xs font-mono text-text-faint hover:text-text-secondary transition-colors"
              >
                {showPassword ? t("Hide") : t("Show")}
              </button>
            </div>
            <input
              id="reports-password"
              type={showPassword ? "text" : "password"}
              required
              minLength={6}
              autoComplete={mode === "signin" ? "current-password" : "new-password"}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              className="w-full rounded-xl border border-line-strong/80 bg-ink-950/80 px-3.5 py-2.5 text-sm text-text-primary placeholder:text-text-faint focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400 transition-colors"
            />
          </div>

          {mode === "register" && (
            <div>
              <label
                htmlFor="reports-confirm-password"
                className="block text-2xs font-mono uppercase tracking-wider text-text-secondary font-medium mb-1.5"
              >
                {t("Confirm password")}
              </label>
              <input
                id="reports-confirm-password"
                type={showPassword ? "text" : "password"}
                required
                minLength={6}
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="••••••••"
                className="w-full rounded-xl border border-line-strong/80 bg-ink-950/80 px-3.5 py-2.5 text-sm text-text-primary placeholder:text-text-faint focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400 transition-colors"
              />
            </div>
          )}

          <div className="pt-2">
            <Button
              type="submit"
              variant="primary"
              size="lg"
              loading={loading}
              className="w-full justify-center text-sm font-semibold"
            >
              {mode === "signin" ? t("Sign In to Reports") : t("Create Registered Account")}
            </Button>
          </div>
        </form>

        {/* Security Scope Footer */}
        <div className="mt-7 pt-5 border-t border-line/50 text-center space-y-2">
          <div className="flex items-center justify-center gap-2">
            <Badge tone="neutral" className="font-mono text-2xs">
              {t("RLS Protected")}
            </Badge>
            <span className="text-2xs text-text-faint font-mono">
              {t("Encrypted Session")}
            </span>
          </div>

          <p className="text-2xs text-text-muted leading-relaxed">
            {t("Public marketing pages remain accessible to all visitors. Authentication is strictly required for live operational reports and database records.")}
          </p>

          <div className="pt-2">
            <Link
              href="/"
              className="text-xs text-brand-300 hover:text-brand-200 transition-colors font-medium"
            >
              ← {t("Return to ProGlass Automation Home")}
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
