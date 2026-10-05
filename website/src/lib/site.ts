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
  /** `<Version>` in ProGlassAutomation.csproj — the single version source. */
  version: "2.0.11",
  copyrightOwner: "Juber Saleem",
  developerName: "Juber Saleem",
  developerEmail: "proglassautomation@gmail.com",
  developerLocation: "India",
  /** From Themes/Languages/en-US.xaml → About.Copyright */
  copyrightLine: "© 2026 Juber Saleem. Made in India. All rights reserved.",
  url: "https://proglassautomation.com",
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
    { label: "Proforma Invoices", href: "/reports?status=Confirmed" },
    { label: "Job Orders", href: "/job-orders" },
    { label: "Delivery Notes", href: "/deliveries" },
  ],
  primary: [
    { label: "Features", href: "/features" },
    { label: "Job Orders", href: "/job-orders" },
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
      { label: "Proforma Invoices", href: "/reports?status=Confirmed" },
      { label: "Job Orders", href: "/job-orders" },
      { label: "Delivery Notes Tracker", href: "/deliveries" },
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
