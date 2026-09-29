/**
 * FAQ entries.
 *
 * Answers are limited to what the repository supports. Where the repository
 * does not answer a question (prices, release dates), the answer says so
 * instead of inventing one.
 */

export type FaqItem = { question: string; answer: string; group: "Product" | "Deployment" | "Licensing" };

export const faq: FaqItem[] = [
  {
    group: "Product",
    question: "Is ProGlass Automation a cloud application?",
    answer:
      "No. It is a Windows desktop application built with WPF on .NET 8. Your business data lives in SQLite database files in your own user profile on your own machine, and it works without an internet connection. There is no web dashboard, no tenant, and no online account to sign in to.",
  },
  {
    group: "Product",
    question: "What kinds of glass can it price?",
    answer:
      "Four build types: SGU (single glazing), DGU (double glazing with an air space), LAM (lamination with PVB) and DGU + LAM (a laminated pane inside a double-glazed unit). Thicknesses, colours and treatments are per-pane, and the DGU build-up offers a 6–32 mm air space with six spacer systems.",
  },
  {
    group: "Product",
    question: "Does the optimizer tell me what I will save?",
    answer:
      "No, and it deliberately does not promise a number. It reports what it actually produced: sheets used, placed area, utilization per sheet and across the run, offcut and scrap, unplaced pieces, and how far the plan sits from an area-only lower bound. Any savings figure depends on your stock, your cutting list and your margins — not on a marketing claim.",
  },
  {
    group: "Product",
    question: "What does the optimization engine export?",
    answer:
      "Four printable report sections — industrial cut schedule, nested sheets summary, merged specifications summary and stock plates allocation — plus CSV and Excel, and machine-ready files: generic DXF, Bottero .OPT, Intermac .ISO and Lisec .DAT.",
  },
  {
    group: "Product",
    question: "Does it connect to a tax authority?",
    answer:
      "No. VAT handling is built in — a single centralised calculator applying 5% UAE VAT with VAT-from-gross back-calculation and a 1 fils tolerance — and there is a VAT Submission report that gathers the period's figures. Filing is something you do; the application prepares the numbers.",
  },
  {
    group: "Product",
    question: "How do I know a cutting plan is safe to send to the table?",
    answer:
      "Every plan is checked by two independent oracles before it can leave the module. One verifies geometry: margins, rotation policy, dimension integrity, no overlaps, kerf consumed. The other verifies conservation: every input piece placed exactly once, nothing missing and nothing duplicated. A plan that fails either check is still shown for inspection but is never exported and never becomes production work.",
  },
  {
    group: "Deployment",
    question: "What do I need to run it?",
    answer:
      "A Windows PC. The application targets .NET 8 on Windows (x64) and stores its data locally. A shared database file can be pointed at for a small LAN setup.",
  },
  {
    group: "Deployment",
    question: "How many people can use it?",
    answer:
      "That depends on the edition. Basic is a single user; Business covers up to five users; Factory and Enterprise are unlimited. Users, roles and twenty-nine granular capabilities are managed in System Center, and permission checks are enforced at the service boundary rather than only in the interface.",
  },
  {
    group: "Deployment",
    question: "How is my data protected?",
    answer:
      "Data stays on your machine in local SQLite files. Passwords are stored with PBKDF2-HMAC-SHA256 at 210,000 iterations and a per-user random salt; API keys are encrypted with Windows user-level protection (DPAPI). Backups are written to your local Backups folder and are never uploaded automatically.",
  },
  {
    group: "Deployment",
    question: "Can I try it before buying?",
    answer:
      "Yes. The application issues an automatic 30-day evaluation trial the first time it runs on a machine. During an active trial every feature set is available, including the production stages and the optimization engine.",
  },
  {
    group: "Licensing",
    question: "How is it licensed?",
    answer:
      "With a signed licence file activated offline — there is no licence server that has to be reachable. Editions are Trial, Basic, Business, Factory and Enterprise, and features are gated by edition: the production stages and the optimization engine are Factory and above, profitability reporting is Business and above.",
  },
  {
    group: "Licensing",
    question: "What happens when a licence expires?",
    answer:
      "The application degrades to read-only rather than locking you away from your records, so you can still open and export your data. A licence that expires during a grace period behaves the same way. Reactivating restores write access.",
  },
  {
    group: "Licensing",
    question: "What does it cost?",
    answer:
      "Pricing is quoted per business rather than listed here as a self-serve plan. Use the contact form to request pricing, and say how many users and which modules you need — the editions above are the vocabulary, but the commercial terms are not fixed in this website.",
  },
];

export const faqGroups: FaqItem["group"][] = ["Product", "Deployment", "Licensing"];
