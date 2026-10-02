import type { NextConfig } from "next";

import { inspectDeps, formatProblems } from "./scripts/dep-check.mjs";

/**
 * Fail at configuration time, before anything is compiled.
 *
 * Why here, and not only in the npm scripts: `npx next build` never runs a
 * script from package.json. npx fetches its own copy of Next, that copy starts
 * fine, and the build then dies on the first import it cannot resolve — naming
 * the import (once, `marked` from the blog loader) rather than the real cause,
 * which is that the project's node_modules was never installed. Config is
 * loaded on every path into Next, including that one, and this file needs no
 * dependency to be read — the `next` import above is a type and disappears at
 * compile time.
 *
 * A failure here is a sentence, not a module-resolution trace: what is missing,
 * what to run, and which revision the tree says it is. See scripts/dep-check.mjs.
 */
{
  const problems = inspectDeps(process.cwd());
  if (problems.missing.length > 0 || problems.undeclared.length > 0) {
    throw new Error(formatProblems(problems, process.cwd()));
  }
}

/**
 * ProGlass Automation — product website configuration.
 *
 * This is a marketing/product site for a Windows desktop application.
 * There is no backend, no database and no session — the only server-side
 * surface is Next.js itself. `output: "export"` is intentionally NOT set so
 * the /api/demo-request route can exist as the single, obvious integration
 * point for a real mail transport.
 */

/**
 * Content-Security-Policy.
 *
 * Honest note on `script-src 'unsafe-inline'`, because it is the one weak
 * directive here and it deserves an explanation rather than a silent footnote:
 *
 *   Next.js ships its React Server Component payload as inline
 *   `self.__next_f.push(...)` scripts, and on this site they are the majority
 *   of the HTML. Their content is per-page and per-build, so they cannot be
 *   allow-listed by hash. The alternative is a per-request nonce from
 *   middleware — which forces every route to render dynamically and gives up
 *   static prerendering of all 26 routes. For a marketing site that is the
 *   wrong trade.
 *
 * What that leaves is still worth having: `default-src 'self'` means no script,
 * style, frame or connection may come from another origin, so the classic
 * payload — injecting a remote script tag — is blocked. Combined with no
 * user-generated content anywhere on the site and no query parameter echoed
 * into HTML, the practical injection surface is nil.
 *
 * If this site ever grows a form of user-authored content, the nonce middleware
 * becomes mandatory and the static trade has to be reconsidered.
 */
/**
 * Development needs two things production must never have.
 *
 *   `'unsafe-eval'` — React's development build reconstructs callstacks with
 *   `eval()`, and Next's dev server eval-maps modules for hot reload. React
 *   states plainly that it never calls `eval()` in a production build, so the
 *   production policy below does not carry the exception. Shipping
 *   `'unsafe-eval'` to visitors would let an injected string execute; keeping
 *   it out of production is the whole point of having the directive.
 *
 *   `ws:` / `wss:` in `connect-src` — the hot-reload socket is a WebSocket,
 *   which `'self'` does not cover in every engine.
 *
 * This is what the console error "eval() is not supported in this environment"
 * was reporting: a production policy being applied to a development server.
 */
const isDevelopment = process.env.NODE_ENV !== "production";

const csp = [
  "default-src 'self'",
  isDevelopment
    ? "script-src 'self' 'unsafe-inline' 'unsafe-eval'"
    : "script-src 'self' 'unsafe-inline'",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob:",
  "font-src 'self'",
  /* `https://*.supabase.co` in development; in production, strictly allow the project's own origin. */
  isDevelopment
    ? "connect-src 'self' ws: wss: https://*.supabase.co"
    : "connect-src 'self' https://afpbdyamedpanwijxghk.supabase.co",
  "form-action 'self'",
  "frame-ancestors 'none'",
  "base-uri 'self'",
  "object-src 'none'",
  "upgrade-insecure-requests",
].join("; ");

const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  compress: true,

  images: {
    formats: ["image/avif", "image/webp"],
    /**
     * 2880 and 3840 are here for the full-bleed artwork — the hero banner and
     * the page banners. They are drawn edge to edge, so a 1792 px shell on a
     * 1920 px screen at device pixel ratio 2 asks for ~3584 px. Without those
     * two steps the request was answered by enlarging the 2560 px entry, and
     * the artwork looked soft on exactly the large displays it is meant for.
     */
    deviceSizes: [360, 480, 640, 768, 1024, 1280, 1440, 1920, 2560, 2880, 3840],
    /** 92 is used by the brand banner; 75 stays the default elsewhere. */
    qualities: [75, 92],
    minimumCacheTTL: 60 * 60 * 24 * 30,
  },

  /**
   * Response headers.
   *
   * Every one of these is fully enforced — there is no partial credit here:
   * no MIME sniffing, no framing of any kind, a referrer policy that never
   * leaks a full URL to a third party, no access to camera, microphone,
   * location, payment or USB, and no cross-origin window handle.
   *
   * `_next/static` is deliberately absent: Next already serves those immutable
   * and warns if you set it again.
   */
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "Content-Security-Policy", value: csp },
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "X-Frame-Options", value: "DENY" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          {
            key: "Permissions-Policy",
            value: "camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()",
          },
          { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
          { key: "X-DNS-Prefetch-Control", value: "on" },
          ...(process.env.NODE_ENV === "production"
            ? [
                {
                  key: "Strict-Transport-Security",
                  value: "max-age=63072000; includeSubDomains; preload",
                },
              ]
            : []),
        ],
      },
      {
        // The one write path: never cached, never indexed, never embedded.
        source: "/api/:path*",
        headers: [
          { key: "Cache-Control", value: "no-store, no-cache, must-revalidate" },
          { key: "X-Robots-Tag", value: "noindex, nofollow" },
        ],
      },
      {
        // Long-lived but revalidated: illustrations change with the product.
        source: "/images/:path*",
        headers: [{ key: "Cache-Control", value: "public, max-age=2592000, stale-while-revalidate=86400" }],
      },
    ];
  },
};

export default nextConfig;
