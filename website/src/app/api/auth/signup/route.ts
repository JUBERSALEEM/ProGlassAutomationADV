import { NextResponse, type NextRequest } from "next/server";
import { isSupabaseConfigured, supabaseServer } from "@/lib/supabase";

export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
  try {
    let body: { email?: string; password?: string };
    try {
      body = await request.json();
    } catch {
      return NextResponse.json(
        { ok: false, error: { message: "Invalid request payload. JSON required.", code: "BAD_REQUEST" } },
        { status: 400 },
      );
    }

    const { email, password } = body || {};

    if (!email || typeof email !== "string" || !password || typeof password !== "string") {
      return NextResponse.json(
        { ok: false, error: { message: "Email and password are required.", code: "MISSING_FIELDS" } },
        { status: 400 },
      );
    }

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email.trim())) {
      return NextResponse.json(
        { ok: false, error: { message: "Please provide a valid email address.", code: "INVALID_EMAIL" } },
        { status: 400 },
      );
    }

    if (password.length < 6) {
      return NextResponse.json(
        {
          ok: false,
          error: { message: "Password must be at least 6 characters.", code: "PASSWORD_TOO_SHORT" },
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
    const { data, error } = await supabase.auth.signUp({
      email: email.trim(),
      password,
    });

    if (error) {
      console.warn("[api/auth/signup] Supabase signUp error:", error.message, error.status);
      return NextResponse.json(
        {
          ok: false,
          error: {
            message: error.message || "Registration failed.",
            code: "SIGNUP_FAILED",
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
        headers: {
          "Cache-Control": "no-store, max-age=0",
        },
      },
    );
  } catch (err) {
    console.error("[api/auth/signup] Unexpected server error:", err);
    return NextResponse.json(
      {
        ok: false,
        error: {
          message: err instanceof Error ? err.message : "Internal registration server error.",
          code: "SERVER_ERROR",
        },
      },
      { status: 500 },
    );
  }
}
