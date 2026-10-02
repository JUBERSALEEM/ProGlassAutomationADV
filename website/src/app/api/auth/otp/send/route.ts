import { NextResponse, type NextRequest } from "next/server";
import { isSupabaseConfigured, supabaseServer } from "@/lib/supabase";

export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
  try {
    let body: { email?: string };
    try {
      body = await request.json();
    } catch {
      return NextResponse.json(
        { ok: false, error: { message: "Invalid request payload. JSON required.", code: "BAD_REQUEST" } },
        { status: 400 },
      );
    }

    const { email } = body || {};
    if (!email || typeof email !== "string") {
      return NextResponse.json(
        { ok: false, error: { message: "Valid email address is required.", code: "MISSING_EMAIL" } },
        { status: 400 },
      );
    }

    const cleanEmail = email.trim().toLowerCase();
    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailPattern.test(cleanEmail)) {
      return NextResponse.json(
        { ok: false, error: { message: "Please provide a valid email address.", code: "INVALID_EMAIL" } },
        { status: 400 },
      );
    }

    if (!isSupabaseConfigured()) {
      return NextResponse.json(
        {
          ok: false,
          error: {
            message:
              "Supabase authentication service is not configured in this environment (NEXT_PUBLIC_SUPABASE_URL is missing).",
            code: "CONFIG_MISSING",
          },
        },
        { status: 503 },
      );
    }

    const supabase = supabaseServer();
    const { error } = await supabase.auth.signInWithOtp({
      email: cleanEmail,
    });

    if (error) {
      console.warn("[api/auth/otp/send] Supabase signInWithOtp error:", error.message, error.status);
      return NextResponse.json(
        {
          ok: false,
          error: {
            message: error.message || "Failed to send verification code.",
            code: error.status === 429 ? "RATE_LIMITED" : "OTP_SEND_FAILED",
            status: error.status || 400,
          },
        },
        { status: error.status || 400 },
      );
    }

    return NextResponse.json(
      {
        ok: true,
        message: "Verification code sent to your email.",
      },
      {
        status: 200,
        headers: { "Cache-Control": "no-store, max-age=0" },
      },
    );
  } catch (err) {
    console.error("[api/auth/otp/send] Unexpected error:", err);
    return NextResponse.json(
      {
        ok: false,
        error: {
          message: err instanceof Error ? err.message : "Internal server error.",
          code: "SERVER_ERROR",
        },
      },
      { status: 500 },
    );
  }
}
