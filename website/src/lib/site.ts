/**
 * Single source of site-wide facts.
 *
 * EVERYTHING IN THIS FILE IS TRACEABLE TO THE WPF REPOSITORY.
 * The path to the evidence is given beside each fact in `src/data/*.ts`.
 *
 * There is deliberately no invented statistic, customer count, award,
 * certification or performance percentage anywhere in this data layer.
 */

export const site = {
  name: "ProGlass Automation",
  legalName: "ProGlass Automation",
  tagline: "Glass business software, built around your workflow.",
  /** `<Version>` in ProGlassAutomation.csproj — the single version source.
   *  This is the release line the website ships beside. The development
   *  line reads 2.0.15 (Records Sync and Document Register work, October
   *  2026); it is shown on the developer page, never swapped into product
   *  claims, so a reader always knows which is which. */
  version: "2.0.11",
  /** The development line's `<Version>` — same file, on that branch. */
  developmentVersion: "2.0.15",
  copyrightOwner: "Juber Saleem",
  developerName: "Juber Saleem",
  developerEmail: "proglassautomation@gmail.com",
  /** The public repository — linked by the footer's GitHub channel chip. */
  repositoryUrl: "https://github.com/JUBERSALEEM/ProGlassAutomationADV",
  /**
   * WhatsApp support line — international format for `wa.me` links (digits
   * only, country code first: e.g. 9715XXXXXXXX for UAE). Set in one of two
   * places: the NEXT_PUBLIC_WHATSAPP_NUMBER environment variable (Vercel
   * dashboard, never committed), or it falls back to the number set in
   * commit by the owner (+971 55 911 7727) below.
   */
  whatsappNumber: process.env.NEXT_PUBLIC_WHATSAPP_NUMBER ?? "971559117727",
  developerLocation: "India",
  /** From Themes/Languages/en-US.xaml → About.Copyright */
  copyrightLine: "© 2026 Juber Saleem. Made in India. All rights reserved.",
  /**
   * Public origin used in canonical URLs, sitemap, robots and OG links.
   *
   * This was a registered-name placeholder — and it returned NXDOMAIN for a
   * week, so crawlers and auditors read "no canonical tag" because the
   * canonical pointed at a domain name nobody was serving. Default is now
   * the deployment that serves traffic today. The day a real domain is
   * attached, set NEXT_PUBLIC_SITE_URL in Vercel and this flips with no code
   * change — which is why the variable is public.
   */
  url: process.env.NEXT_PUBLIC_SITE_URL ?? "https://proglassautomationadv-website-tkim.vercel.app",
  platform: "Windows desktop (WPF)",
  framework: ".NET 8 · WPF",
  database: "SQLite · EF Core 8",
  pattern: "MVVM",
  language: "C# 12",
  locales: 15,
  /** Configured in Services/Database — one DbContext per bounded context. */
  databaseCount: 16,
  /** Folders carrying both views and view-models, one per business area. */
  moduleCount: 25,
  serviceDomainCount: 43,
  /**
   * Counts taken from the application tree, not from the README's dated
   * table. Method: production and test `.cs` outside `obj/`, `bin/` and
   * `.git/`; the test figure counts live `[Fact]` and `[Theory]` attributes.
   */
  statsMeasuredOn: "29 September 2026",
  testCount: 2369,
  testFiles: 288,
  testInlineCases: 1275,
  productionFiles: 1391,
  productionLines: 299946,
  xamlFiles: 346,
  xamlLines: 143610,
  roleCount: 8,
  /** Services/Audit — live `Module.Capability` permission constants. */
  permissionCount: 34,
} as const;

export const nav = {
  product: [
    { label: "Features", href: "/features" },
    { label: "How It Works", href: "/how-it-works" },
    { label: "Worked Example", href: "/worked-example" },
    { label: "Screenshots", href: "/screenshots" },
    { label: "Pricing", href: "/pricing" },
    { label: "Reports", href: "/reports?status=Confirmed" },
    { label: "Job Order", href: "/job-orders" },
    { label: "Deliveries", href: "/deliveries" },
  ],
  primary: [
    { label: "Features", href: "/features" },
    { label: "Optimization", href: "/optimization" },
    { label: "How It Works", href: "/how-it-works" },
    { label: "Worked Example", href: "/worked-example" },
    { label: "Screenshots", href: "/screenshots" },
    { label: "Pricing", href: "/pricing" },
    { label: "About", href: "/about" },
    { label: "Developer", href: "/developer" },
    { label: "Collaborate", href: "/collaborate" },
    { label: "Resources", href: "/resources" },
    { label: "Sponsors", href: "/sponsors" },
  ],
  footer: {
    product: [
      { label: "Features", href: "/features" },
      { label: "Optimization", href: "/optimization" },
      { label: "How It Works", href: "/how-it-works" },
      { label: "Worked Example", href: "/worked-example" },
      { label: "Screenshots", href: "/screenshots" },
      { label: "Pricing", href: "/pricing" },
      { label: "Reports", href: "/reports?status=Confirmed" },
      { label: "Job Order", href: "/job-orders" },
      { label: "Deliveries", href: "/deliveries" },
    ],
    company: [
      { label: "About ProGlass", href: "/about" },
      { label: "Developer", href: "/developer" },
      { label: "Collaborate", href: "/collaborate" },
      { label: "Sponsors", href: "/sponsors" },
      { label: "Contact", href: "/contact" },
    ],
    resources: [
      { label: "Resources", href: "/resources" },
      { label: "Blog", href: "/blog" },
    ],
    legal: [
      { label: "Privacy", href: "/legal/privacy" },
      { label: "Terms", href: "/legal/terms" },
      { label: "License", href: "/legal/license" },
    ],
  },
} as const;

export const ctas = {
  primary: { label: "Request a Demo", href: "/contact" },
  secondary: { label: "Explore ProGlass", href: "/features" },
} as const;
