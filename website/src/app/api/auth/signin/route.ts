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
    const { data, error } = await supabase.auth.signInWithPassword({
      email: email.trim(),
      password,
    });

    if (error) {
      console.warn("[api/auth/signin] Supabase signIn error:", error.message, error.status);
      const isInvalidCredentials =
        error.message?.toLowerCase().includes("invalid login credentials") ||
        error.message?.toLowerCase().includes("invalid credentials");

      return NextResponse.json(
        {
          ok: false,
          error: {
            message: isInvalidCredentials
              ? "Invalid email or password. Please verify your credentials or register an account."
              : error.message || "Authentication failed.",
            code: error.status === 400 ? "INVALID_CREDENTIALS" : "AUTH_FAILED",
            status: error.status || 401,
          },
        },
        { status: error.status || 401 },
      );
    }

    return NextResponse.json(
      {
        ok: true,
        session: data.session,
        user: {
          id: data.user.id,
          email: data.user.email,
          role: data.user.role,
        },
      },
      {
        status: 200,
        headers: {
          "Cache-Control": "no-store, max-age=0",
        },
      },
    );
  } catch (err) {
    console.error("[api/auth/signin] Unexpected server error:", err);
    return NextResponse.json(
      {
        ok: false,
        error: {
          message: err instanceof Error ? err.message : "Internal authentication server error.",
          code: "SERVER_ERROR",
        },
      },
      { status: 500 },
    );
  }
}
