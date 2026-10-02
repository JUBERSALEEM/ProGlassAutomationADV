"use client";

import { useEffect, useRef, useState } from "react";
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

  // Flow states: "email" (Step A) -> "otp" (Step B)
  const [step, setStep] = useState<"email" | "otp">("email");
  const [email, setEmail] = useState("");
  const [otp, setOtp] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  // Rate-limiting resend countdown (60s minimum interval)
  const [resendCountdown, setResendCountdown] = useState(0);

  const otpInputRef = useRef<HTMLInputElement | null>(null);
  const configured = isSupabaseConfigured();

  // Handle countdown interval
  useEffect(() => {
    if (resendCountdown <= 0) return;
    const timer = setInterval(() => {
      setResendCountdown((prev) => (prev > 1 ? prev - 1 : 0));
    }, 1000);
    return () => clearInterval(timer);
  }, [resendCountdown]);

  // Focus OTP input when switching to OTP step
  useEffect(() => {
    if (step === "otp") {
      otpInputRef.current?.focus();
    }
  }, [step]);

  // ─────────────────────────────────────────────────────────────────────────────
  // STEP A: Request 6-digit OTP code to email
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
      let otpSent = false;
      let lastErrorMessage = "";

      // 1. Direct browser client call using supabase.auth.signInWithOtp
      try {
        const supabase = supabaseBrowser();
        const { error: otpError } = await supabase.auth.signInWithOtp({
          email: cleanEmail,
        });

        if (!otpError) {
          otpSent = true;
        } else {
          lastErrorMessage = otpError.message;
          console.warn("[ReportsAuthForm] Client signInWithOtp error:", otpError);
        }
      } catch (clientErr) {
        console.warn("[ReportsAuthForm] Client fetch failed, attempting server route:", clientErr);
      }

      // 2. Resilient fallback to same-origin server route if browser direct call encountered network/CORS issues
      if (!otpSent) {
        try {
          const res = await fetch("/api/auth/otp/send", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email: cleanEmail }),
          });
          const serverJson = await res.json();
          if (res.ok && serverJson.ok) {
            otpSent = true;
          } else {
            lastErrorMessage = serverJson.error?.message || lastErrorMessage;
          }
        } catch (serverErr) {
          console.error("[ReportsAuthForm] Server route error:", serverErr);
        }
      }

      if (!otpSent) {
        throw new Error(
          lastErrorMessage ||
            t("Failed to send verification code. Please check your email or wait before trying again."),
        );
      }

      // Success: advance to OTP input step and start 60s countdown
      setNotice(t("Verification code sent to your email."));
      setStep("otp");
      setResendCountdown(60);
    } catch (err) {
      console.error("[ReportsAuthForm] Error sending OTP:", err);
      const message =
        err instanceof Error ? err.message : t("Failed to send verification code. Please try again.");
      setError(message);
    } finally {
      setLoading(false);
    }
  };

  // ─────────────────────────────────────────────────────────────────────────────
  // STEP B: Verify 6-digit OTP code and establish authenticated session
  // ─────────────────────────────────────────────────────────────────────────────
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
      let verified = false;
      let lastErrorMessage = "";

      // 1. Direct browser client call using supabase.auth.verifyOtp
      try {
        const supabase = supabaseBrowser();
        const { data, error: verifyError } = await supabase.auth.verifyOtp({
          email: cleanEmail,
          token: cleanToken,
          type: "email",
        });

        if (!verifyError && data.session) {
          verified = true;
        } else if (verifyError) {
          lastErrorMessage = verifyError.message;
          console.warn("[ReportsAuthForm] Client verifyOtp error:", verifyError);
        }
      } catch (clientErr) {
        console.warn("[ReportsAuthForm] Client verify fetch failed, attempting server route:", clientErr);
      }

      // 2. Resilient fallback to server route if browser encountered network/transport issue
      if (!verified) {
        try {
          const res = await fetch("/api/auth/otp/verify", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email: cleanEmail, token: cleanToken }),
          });
          const serverJson = await res.json();
          if (res.ok && serverJson.ok && serverJson.session) {
            // Synchronize session back into the client singleton
            const supabase = supabaseBrowser();
            await supabase.auth.setSession({
              access_token: serverJson.session.access_token,
              refresh_token: serverJson.session.refresh_token,
            });
            verified = true;
          } else {
            lastErrorMessage = serverJson.error?.message || lastErrorMessage;
          }
        } catch (serverErr) {
          console.error("[ReportsAuthForm] Server verify route error:", serverErr);
        }
      }

      if (!verified) {
        throw new Error(
          lastErrorMessage ||
            t("Invalid or expired verification code. Please check your email or request a new code."),
        );
      }

      // Success: authenticated session confirmed
      setNotice(t("Code verified successfully. Redirecting to reports..."));
      router.push(redirectTo);
      router.refresh();
    } catch (err) {
      console.error("[ReportsAuthForm] Error verifying OTP:", err);
      const message =
        err instanceof Error ? err.message : t("Verification failed. Please try again.");
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
            {step === "email" ? t("Sign In with Email OTP") : t("Enter Verification Code")}
          </h1>
          <p className="mt-2 text-xs leading-relaxed text-text-muted">
            {step === "email"
              ? t("Enter your operator email address. We will send a 6-digit one-time passcode to access protected factory reports.")
              : t.template`Enter the 6-digit verification code sent to ${email}`}
          </p>
        </div>

        {/* Alerts */}
        {error && (
          <div
            role="alert"
            className="mt-5 rounded-xl border border-state-danger/40 bg-state-danger/10 p-3 text-xs text-state-danger leading-relaxed flex items-start gap-2"
          >
            <span aria-hidden="true" className="shrink-0 font-bold">!</span>
            <span>{error}</span>
          </div>
        )}

        {notice && (
          <div
            role="status"
            className="mt-5 rounded-xl border border-state-success/40 bg-state-success/10 p-3 text-xs text-state-success leading-relaxed flex items-start gap-2"
          >
            <span aria-hidden="true" className="shrink-0 font-bold">✓</span>
            <span>{notice}</span>
          </div>
        )}

        {!configured && (
          <div className="mt-5 rounded-xl border border-state-warning/40 bg-state-warning/10 p-3 text-2xs text-state-warning leading-relaxed">
            {t("Note: Supabase URL/key not configured in local environment. In production, registered users authenticate against Supabase Auth.")}
          </div>
        )}

        {/* STEP A: Email Address Submission */}
        {step === "email" ? (
          <form onSubmit={handleSendOtp} className="mt-6 space-y-4">
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
        ) : (
          /* STEP B: 6-Digit OTP Verification */
          <form onSubmit={handleVerifyOtp} className="mt-6 space-y-4">
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
                className="w-full text-center tracking-[0.35em] font-mono text-xl sm:text-2xl font-bold rounded-xl border border-line-strong/80 bg-ink-950/80 px-3.5 py-3 text-brand-100 placeholder:text-text-faint focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400 transition-colors"
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
