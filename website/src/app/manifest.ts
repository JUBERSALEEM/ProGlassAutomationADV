import type { MetadataRoute } from "next";

/** Install/search presentation for the public product site. No application
 * permissions or offline behavior are claimed: operational routes remain
 * authenticated web surfaces backed by their live APIs. */
export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "ProGlass Automation",
    short_name: "ProGlass",
    description: "Glass estimation, optimization, production, delivery and business-control software.",
    start_url: "/",
    scope: "/",
    display: "standalone",
    background_color: "#080516",
    theme_color: "#6546B5",
    icons: [
      { src: "/icon.svg", sizes: "any", type: "image/svg+xml", purpose: "any" },
      { src: "/favicon.ico", sizes: "48x48", type: "image/x-icon" },
    ],
  };
}
