import { NextResponse, type NextRequest } from "next/server";
import { isSupabaseConfigured, supabaseServer } from "@/lib/supabase";

export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
  try {
    let body: { email?: string; token?: string };
    try {
      body = await request.json();
    } catch {
      return NextResponse.json(
        { ok: false, error: { message: "Invalid request payload. JSON required.", code: "BAD_REQUEST" } },
        { status: 400 },
      );
    }

    const { email, token } = body || {};
    if (!email || typeof email !== "string" || !token || typeof token !== "string") {
      return NextResponse.json(
        { ok: false, error: { message: "Email and verification code are required.", code: "MISSING_FIELDS" } },
        { status: 400 },
      );
    }

    const cleanEmail = email.trim().toLowerCase();
    const cleanToken = token.trim().replace(/\D/g, "");

    if (cleanToken.length !== 6) {
      return NextResponse.json(
        {
          ok: false,
          error: { message: "Please enter a valid 6-digit verification code.", code: "INVALID_TOKEN_FORMAT" },
        },
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
    const { data, error } = await supabase.auth.verifyOtp({
      email: cleanEmail,
      token: cleanToken,
      type: "email",
    });

    if (error) {
      console.warn("[api/auth/otp/verify] Supabase verifyOtp error:", error.message, error.status);
      return NextResponse.json(
        {
          ok: false,
          error: {
            message: error.message || "Invalid or expired verification code.",
            code: "VERIFICATION_FAILED",
            status: error.status || 400,
          },
        },
        { status: error.status || 400 },
      );
    }

    return NextResponse.json(
      {
        ok: true,
        session: data.session,
        user: data.user
          ? {
              id: data.user.id,
              email: data.user.email,
              role: data.user.role,
            }
          : null,
      },
      {
        status: 200,
        headers: { "Cache-Control": "no-store, max-age=0" },
      },
    );
  } catch (err) {
    console.error("[api/auth/otp/verify] Unexpected error:", err);
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
