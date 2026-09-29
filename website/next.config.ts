import type { NextConfig } from "next";

/**
 * ProGlass Automation — product website configuration.
 *
 * This is a marketing/product site for a Windows desktop application.
 * There is no backend, no database and no session — the only server-side
 * surface is Next.js itself. `output: "export"` is intentionally NOT set so
 * the /api/demo-request route can exist as the single, obvious integration
 * point for a real mail transport.
 */
const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  images: {
    formats: ["image/avif", "image/webp"],
    deviceSizes: [360, 480, 640, 768, 1024, 1280, 1440, 1920, 2560],
  },
};

export default nextConfig;
