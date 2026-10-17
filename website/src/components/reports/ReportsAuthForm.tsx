"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { isSupabaseConfigured, supabaseBrowser } from "@/lib/supabase";
import { Logo } from "@/components/brand/Logo";
import { Button } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { useT } from "@/i18n/provider";

/**
 * Helper to distinguish browser/network transport failures from Supabase Auth API errors.
 */
function isNetworkError(err: unknown): boolean {
  if (!err) return false;
  if (typeof err === "object") {
    const name = "name" in err && typeof err.name === "string" ? err.name : "";
    const msg = "message" in err && typeof err.message === "string" ? err.message : "";
    if (name === "AuthRetryableFetchError" || name === "TypeError") {
      if (/failed to fetch|network|fetch failed|load failed|abort|timeout/i.test(msg)) {
        return true;
      }
    }
    if (/failed to fetch|network error|networkrequestfailed|load failed/i.test(msg)) {
      return true;
    }
  }
  return false;
}

/**
 * Reports & Records Authentication Form — Passwordless Email OTP.
 *
 * Authoritative Architecture:
 *   Browser → supabase.auth.signInWithOtp({ email }) → Supabase Auth → Gmail SMTP → Operator Inbox
 *   Browser → supabase.auth.verifyOtp({ email, token, type: "email" }) → Valid Session → /reports
 */
export function ReportsAuthForm() {
  const t = useT();
  const router = useRouter();
  const searchParams = useSearchParams();
  const redirectTo = searchParams.get("redirect") || "/reports";
  const customerAccess = redirectTo.includes("/inquiries");

  // Password is the normal return path; email OTP also serves signup/recovery.
  const [mode, setMode] = useState<"password" | "otp">("password");
  const [step, setStep] = useState<"email" | "otp" | "set-password">("email");
  const [email, setEmail] = useState("");
  const [otp, setOtp] = useState("");
  const [password, setPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  // Rate-limiting resend countdown (60s minimum interval mandated by Supabase SMTP)
  const [resendCountdown, setResendCountdown] = useState(0);

  const otpInputRef = useRef<HTMLInputElement | null>(null);
  const configured = isSupabaseConfigured();

  // Handle 60s cooldown timer
  useEffect(() => {
    if (resendCountdown <= 0) return;
    const timer = setInterval(() => {
      setResendCountdown((prev) => (prev > 1 ? prev - 1 : 0));
    }, 1000);
    return () => clearInterval(timer);
  }, [resendCountdown]);

  // Auto-focus OTP input when advancing to Step B
  useEffect(() => {
    if (step === "otp") {
      otpInputRef.current?.focus();
    }
  }, [step]);

  // ─────────────────────────────────────────────────────────────────────────────
  // STEP A: Request 6-digit OTP code to email via Supabase Auth
  // ─────────────────────────────────────────────────────────────────────────────
  const handleSendOtp = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    setError(null);
    setNotice(null);

    const cleanEmail = email.trim().toLowerCase();
    if (!cleanEmail) {
      setError(t("Please enter your email address."));
      return;
    }

    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailPattern.test(cleanEmail)) {
      setError(t("Please enter a valid email address."));
      return;
    }

    if (!configured) {
      setError(
        t(
          "Supabase authentication is not configured in this environment (NEXT_PUBLIC_SUPABASE_URL is missing).",
        ),
      );
      return;
    }

    setLoading(true);

    try {
      const supabase = supabaseBrowser();
      const { error: otpError } = await supabase.auth.signInWithOtp({
        email: cleanEmail,
      });

      if (otpError) {
        // Distinguish browser/network failure from Supabase Auth API error
        if (isNetworkError(otpError)) {
          console.error("[ReportsAuthForm] Browser network error contacting Supabase Auth:", {
            name: otpError.name,
            message: otpError.message,
          });
          setError(t("Unable to contact authentication service. Please try again."));
          return;
        }

        // Safely extract error metadata (never log credentials, tokens, or secrets)
        const errMeta = {
          message: otpError.message,
          code: otpError.code,
          status: otpError.status,
          name: otpError.name,
        };
        console.error("[ReportsAuthForm] Supabase signInWithOtp error details:", errMeta);

        // Format detailed error for operator UI: "Error sending verification code: <message> (status: <status>, code: <code>)"
        const statusDetails = [
          otpError.status ? `status: ${otpError.status}` : null,
          otpError.code ? `code: ${otpError.code}` : null,
        ]
          .filter(Boolean)
          .join(", ");

        const formattedError = statusDetails
          ? `Error sending verification code: ${otpError.message} (${statusDetails})`
          : `Error sending verification code: ${otpError.message}`;

        setError(formattedError);
        return;
      }

      // Success: advance to OTP input step and start 60s cooldown
      setNotice(t("Verification code sent to your email."));
      setStep("otp");
      setResendCountdown(60);
    } catch (err) {
      console.error("[ReportsAuthForm] Unexpected exception sending OTP:", err);
      if (isNetworkError(err)) {
        setError(t("Unable to contact authentication service. Please try again."));
      } else {
        const rawMessage = err instanceof Error ? err.message : String(err);
        setError(`Error sending verification code: ${rawMessage}`);
      }
    } finally {
      setLoading(false);
    }
  };

  const handlePasswordSignIn = async (e: React.FormEvent) => {
    e.preventDefault(); setError(null); setNotice(null);
    const cleanEmail = email.trim().toLowerCase();
    if (!cleanEmail || !password) { setError(t("Enter your email and password.")); return; }
    if (!configured) { setError(t("Supabase authentication is not configured in this environment.")); return; }
    setLoading(true);
    try {
      const { data, error: signInError } = await supabaseBrowser().auth.signInWithPassword({ email: cleanEmail, password });
      if (signInError || !data.session) throw signInError || new Error("Authentication session could not be established.");
      setNotice(t("Signed in successfully. Redirecting...")); router.push(redirectTo); router.refresh();
    } catch (reason) {
      setError(isNetworkError(reason) ? t("Unable to contact authentication service. Please try again.") : t("Email or password is incorrect. Use an email code if you need to set or reset your password."));
    } finally { setLoading(false); }
  };

  const handleSetPassword = async (e: React.FormEvent) => {
    e.preventDefault(); setError(null);
    if (newPassword.length < 8) { setError(t("Password must contain at least 8 characters.")); return; }
    if (newPassword !== confirmPassword) { setError(t("Passwords do not match.")); return; }
    setLoading(true);
    try {
      const { error: updateError } = await supabaseBrowser().auth.updateUser({ password: newPassword });
      if (updateError) throw updateError;
      setNotice(t("Password saved. You can use it for future sign-ins.")); router.push(redirectTo); router.refresh();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t("Password could not be saved."));
    } finally { setLoading(false); }
  };

  // STEP B: Verify the email code, then offer a reusable password.
  const handleVerifyOtp = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setNotice(null);

    const cleanEmail = email.trim().toLowerCase();
    const cleanToken = otp.trim().replace(/\D/g, "");

    if (cleanToken.length !== 6) {
      setError(t("Please enter the complete 6-digit verification code."));
      return;
    }

    setLoading(true);

    try {
      const supabase = supabaseBrowser();
      const { data, error: verifyError } = await supabase.auth.verifyOtp({
        email: cleanEmail,
        token: cleanToken,
        type: "email",
      });

      if (verifyError) {
        // Distinguish browser/network failure from Supabase Auth API error
        if (isNetworkError(verifyError)) {
          console.error("[ReportsAuthForm] Browser network error verifying OTP:", {
            name: verifyError.name,
            message: verifyError.message,
          });
          setError(t("Unable to contact authentication service. Please try again."));
          return;
        }

        const errMeta = {
          message: verifyError.message,
          code: verifyError.code,
          status: verifyError.status,
          name: verifyError.name,
        };
        console.error("[ReportsAuthForm] Supabase verifyOtp error details:", errMeta);

        const statusDetails = [
          verifyError.status ? `status: ${verifyError.status}` : null,
          verifyError.code ? `code: ${verifyError.code}` : null,
        ]
          .filter(Boolean)
          .join(", ");

        const formattedError = statusDetails
          ? `Verification failed: ${verifyError.message} (${statusDetails})`
          : `Verification failed: ${verifyError.message}`;

        setError(formattedError);
        return;
      }

      if (!data.session) {
        throw new Error(t("Authentication session could not be established. Please try again."));
      }

      // The verified session is allowed to establish or replace its own password.
      setNotice(t("Code verified. Set a password for faster future sign-ins."));
      setStep("set-password");
    } catch (err) {
      console.error("[ReportsAuthForm] Unexpected exception verifying OTP:", err);
      if (isNetworkError(err)) {
        setError(t("Unable to contact authentication service. Please try again."));
      } else {
        const rawMessage = err instanceof Error ? err.message : String(err);
        setError(`Verification failed: ${rawMessage}`);
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="w-full max-w-md mx-auto">
      <div className="rounded-panel border border-line-strong/80 bg-ink-900/80 shadow-raised backdrop-blur-xl card-pad">
        {/* Brand Header */}
        <div className="flex flex-col items-center text-center">
          <Link href="/" aria-label="ProGlass Automation Home" className="transition-transform hover:scale-105">
            <Logo size={42} />
          </Link>

          <div className="mt-3.5 flex items-center gap-2">
            <span className="eyebrow text-brand-300">{t(customerAccess ? "Customer portal" : "Operations Suite")}</span>
            <span className="text-text-faint">/</span>
            <span className="eyebrow text-text-muted">{t(customerAccess ? "Inquiry access" : "Reports Access")}</span>
          </div>

          <h1 className="mt-2 text-xl font-bold tracking-tight text-text-primary sm:text-2xl">
            {step === "otp" ? t("Enter Verification Code") : step === "set-password" ? t("Set your password") : t("Sign in")}
          </h1>
          <p className="mt-2 text-xs leading-relaxed text-text-muted">
            {step === "otp" ? t.template`Enter the 6-digit verification code sent to ${email}` : step === "set-password" ? t("Create a reusable password so future sign-ins do not require email access.") : mode === "password" ? t("Sign in with your email and password.") : t("Use an email code to sign up, recover access, or set a password.")}
          </p>
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

        {step === "email" && <div className="mt-5 grid grid-cols-2 rounded-xl border border-line-strong bg-ink-950 p-1" role="tablist" aria-label="Sign-in method">
          <button type="button" role="tab" aria-selected={mode === "password"} onClick={() => { setMode("password"); setError(null); setNotice(null); }} className={`rounded-lg px-3 py-2 text-xs font-semibold ${mode === "password" ? "bg-brand-400 text-accent-ink" : "text-text-muted"}`}>{t("Password")}</button>
          <button type="button" role="tab" aria-selected={mode === "otp"} onClick={() => { setMode("otp"); setError(null); setNotice(null); }} className={`rounded-lg px-3 py-2 text-xs font-semibold ${mode === "otp" ? "bg-brand-400 text-accent-ink" : "text-text-muted"}`}>{t("Email code")}</button>
        </div>}

        {step === "email" && mode === "password" ? (
          <form onSubmit={handlePasswordSignIn} className="mt-5 space-y-3.5">
            <label htmlFor="password-email" className="block text-2xs font-mono uppercase tracking-wider text-text-secondary">{t("Email address")}</label>
            <input id="password-email" type="email" required autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} className="w-full rounded-xl border border-line-strong bg-ink-950 px-3.5 py-2.5 text-sm text-text-primary focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/50" />
            <label htmlFor="password-value" className="block text-2xs font-mono uppercase tracking-wider text-text-secondary">{t("Password")}</label>
            <input id="password-value" type="password" required autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} className="w-full rounded-xl border border-line-strong bg-ink-950 px-3.5 py-2.5 text-sm text-text-primary focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/50" />
            <Button type="submit" variant="primary" size="lg" loading={loading} className="w-full justify-center">{t("Sign in")}</Button>
            <button type="button" onClick={() => setMode("otp")} className="w-full text-xs text-brand-300 underline underline-offset-4">{t("New account or forgot password? Use an email code")}</button>
          </form>
        ) : step === "email" ? (
          <form onSubmit={handleSendOtp} className="mt-5 space-y-3.5">
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

            <div className="pt-2">
              <Button
                type="submit"
                variant="primary"
                size="lg"
                loading={loading}
                className="w-full justify-center text-sm font-semibold"
              >
                {t("Send verification code")}
              </Button>
            </div>
          </form>
        ) : step === "set-password" ? (
          <form onSubmit={handleSetPassword} className="mt-5 space-y-3.5">
            <label htmlFor="new-password" className="block text-2xs font-mono uppercase tracking-wider text-text-secondary">{t("New password")}</label>
            <input id="new-password" type="password" minLength={8} required autoComplete="new-password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} className="w-full rounded-xl border border-line-strong bg-ink-950 px-3.5 py-2.5 text-sm text-text-primary focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/50" />
            <label htmlFor="confirm-password" className="block text-2xs font-mono uppercase tracking-wider text-text-secondary">{t("Confirm password")}</label>
            <input id="confirm-password" type="password" minLength={8} required autoComplete="new-password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} className="w-full rounded-xl border border-line-strong bg-ink-950 px-3.5 py-2.5 text-sm text-text-primary focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/50" />
            <Button type="submit" variant="primary" size="lg" loading={loading} className="w-full justify-center">{t("Save password and continue")}</Button>
            <button type="button" onClick={() => { router.push(redirectTo); router.refresh(); }} className="w-full text-xs text-text-muted underline underline-offset-4">{t("Skip for now")}</button>
          </form>
        ) : (
          /* STEP B: 6-Digit OTP Verification */
          <form onSubmit={handleVerifyOtp} className="mt-5 space-y-3.5">
            <div>
              <div className="flex items-center justify-between mb-1.5">
                <label
                  htmlFor="reports-otp"
                  className="block text-2xs font-mono uppercase tracking-wider text-text-secondary font-medium"
                >
                  {t("Enter verification code")}
                </label>
                <button
                  type="button"
                  onClick={() => {
                    setStep("email");
                    setOtp("");
                    setError(null);
                    setNotice(null);
                  }}
                  className="text-2xs text-brand-300 hover:text-brand-200 transition-colors font-medium"
                >
                  {t("Change email")}
                </button>
              </div>

              <input
                ref={otpInputRef}
                id="reports-otp"
                type="text"
                inputMode="numeric"
                pattern="[0-9]*"
                maxLength={6}
                required
                autoComplete="one-time-code"
                value={otp}
                onChange={(e) => setOtp(e.target.value.replace(/\D/g, ""))}
                placeholder="123456"
                className="w-full text-center tracking-[0.35em] font-mono text-xl sm:text-2xl font-bold rounded-xl border border-line-strong/80 bg-ink-950/80 px-3.5 py-2.5 text-brand-100 placeholder:text-text-faint focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400 transition-colors"
              />
              <p className="mt-1.5 text-center text-3xs text-text-faint">
                {t("Enter the 6-digit numeric code from your inbox")}
              </p>
            </div>

            <div className="pt-2">
              <Button
                type="submit"
                variant="primary"
                size="lg"
                loading={loading}
                disabled={otp.length !== 6}
                className="w-full justify-center text-sm font-semibold"
              >
                {t("Verify and continue")}
              </Button>
            </div>

            {/* Resend OTP with 60s cooldown */}
            <div className="pt-2 text-center">
              {resendCountdown > 0 ? (
                <p className="text-2xs font-mono text-text-faint">
                  {t.template`Resend available in ${resendCountdown} seconds`}
                </p>
              ) : (
                <button
                  type="button"
                  disabled={loading}
                  onClick={() => handleSendOtp()}
                  className="text-xs text-brand-300 hover:text-brand-200 transition-colors font-medium underline underline-offset-4 disabled:opacity-50"
                >
                  {t("Resend verification code")}
                </button>
              )}
            </div>
          </form>
        )}

        {/* Security Scope Footer */}
        <div className="mt-7 pt-4 border-t border-line/50 text-center space-y-2">
          <div className="flex items-center justify-center gap-2">
            <Badge tone="neutral" className="font-mono text-2xs">
              {t("RLS Protected")}
            </Badge>
            <span className="text-2xs text-text-faint font-mono">
              {t("Encrypted Session")}
            </span>
          </div>

          <p className="text-2xs text-text-muted leading-relaxed">
            {t(customerAccess ? "Authentication protects your inquiry details, projects, and private documents." : "Public marketing pages remain accessible to all visitors. Authentication is strictly required for live operational reports and database records.")}
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
