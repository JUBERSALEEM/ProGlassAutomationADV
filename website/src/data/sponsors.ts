/**
 * Sponsorship and collaboration.
 *
 * Everything here is stated as a request, never as an achievement. ProGlass
 * Automation is developed by one person (ViewModels/About/AboutViewModel.cs),
 * it has no marketing budget, and the gaps below are the real ones that slow a
 * WPF desktop product down. Nothing on this page implies a sponsor exists, and
 * no amount, tier or reward is invented — funding terms are discussed, not
 * published.
 *
 * Where a need maps to a place in the codebase, the file is named, so a
 * contributor can go and read the thing before offering to help.
 */

export type SponsorNeed = {
  id: string;
  title: string;
  /** The work as it actually stands. */
  status: string;
  /** What help would change. */
  ask: string;
  /** Repo paths, when the work maps to code. */
  evidence?: string;
  /** Rough shape of the contribution. */
  shape: "Time" | "Money" | "Hardware" | "Reach";
  icon: "code" | "test" | "translate" | "hardware" | "docs" | "design";
};

export const sponsorNeeds: SponsorNeed[] = [
  {
    id: "wpf-depth",
    title: "WPF and .NET engineering",
    status:
      "The application is one WPF solution: 42 navigation targets across 26 view-model areas, and a cutting-optimization engine of 29 files. The commercial chain is complete; the production stages are the current wave of work.",
    ask: "An experienced WPF/.NET developer who can take a module end to end — view, view-model, service, tests — without being walked through the pattern each time.",
    evidence: "ViewModels/ProGlassAutomation (42 targets) · Services/Optimization/GuillotinePacker.cs",
    shape: "Time",
    icon: "code",
  },
  {
    id: "test-depth",
    title: "Test engineering",
    status:
      "There are 2,369 xUnit test methods across 288 files, and the workflow invariants — piece flow clamping, VAT tolerance, price-basis stamping — are the ones that must never regress.",
    ask: "Someone who enjoys breaking software on purpose: property-based tests for the nesting engine, fault injection around the document chain, and regression cases written from real customer scenarios.",
    evidence: "Tests/ (288 files) · Tests/Security/SilentCatchAuditTests.cs",
    shape: "Time",
    icon: "test",
  },
  {
    id: "translation",
    title: "Translation and localisation review",
    status:
      "15 locales ship as pure XAML resource dictionaries, so a new language needs no C# change at all. Machine-drafted strings are not the same as reviewed ones, and glass vocabulary does not translate itself.",
    ask: "Native speakers who know the glass trade in their market — Arabic, Hindi, Urdu, Bengali, Russian, Turkish and the rest — to review the existing dictionaries rather than only add new ones.",
    evidence: "Themes/Languages/*.xaml (15 locales) · MainWindow.xaml language switcher",
    shape: "Time",
    icon: "translate",
  },
  {
    id: "factory-testing",
    title: "A real cutting table to test against",
    status:
      "The engine writes generic DXF and a cut-list CSV today. Bottero .OPT, Intermac .ISO and Lisec .DAT are declared in the report tab: those formats are not published — vendor lists call the same outputs OPT101.CNC and *.CUT — so each will be written against one real sample file from that table.",
    ask: "A fabricator willing to send one sample file exported by their Bottero, Intermac or Lisec table — and, if willing, to test-cut the generic DXF and say what the controller objected to. A machine day is worth more than any specification document.",
    evidence: "Services/Optimization/ (exporters) · Views/Optimization/Tabs/ReportTabView.xaml",
    shape: "Hardware",
    icon: "hardware",
  },
  {
    id: "documentation",
    title: "Operator documentation",
    status:
      "The product is honest about what it does, but the in-repo documentation is written for engineers. A shop-floor user needs a task, a screen and a result — not a module map.",
    ask: "Someone who writes procedures for real operators: task-based guides for the eight production stages, the proforma wizard and the sheet store, written from the screen rather than from the code.",
    evidence: "docs/WORKFLOW.md · docs/PRODUCTION_SCREENS_DESIGN_2026-09-11.md",
    shape: "Time",
    icon: "docs",
  },
  {
    id: "design-critique",
    title: "Interface critique",
    status:
      "The interface is drawn from one design system — tokens, not hard-coded colours — with four visual families and 16 accent presets. It has never been reviewed by an outside designer.",
    ask: "A UI designer to critique the information density of the production stages and the estimation grid. Software that is used eight hours a day earns harder questions than a landing page does.",
    evidence: "Themes/Styles/*Tokens.xaml · Services/Theme/AccentPresets.cs",
    shape: "Time",
    icon: "design",
  },
  {
    id: "running-costs",
    title: "Running costs",
    status:
      "There is no subscription, no telemetry and no funding behind the project. Certificates, signing, the test hardware and the time spent on a release all come out of one person's pocket.",
    ask: "Straightforward financial support. There are no tiers and no published amounts — you say what the project is worth to you and what you would like in return, and it is a conversation.",
    shape: "Money",
    icon: "hardware",
  },
  {
    id: "reach",
    title: "Reach",
    status:
      "A glass fabricator in the UAE runs a business the rest of the trade never hears about. Word of mouth is the only distribution this product has.",
    ask: "If the software does something useful for you, tell one other fabricator. That is genuinely the most effective thing on this list.",
    shape: "Reach",
    icon: "design",
  },
];

export type SponsorFaq = { question: string; answer: string };

export const sponsorFaq: SponsorFaq[] = [
  {
    question: "Does sponsoring buy influence over the product?",
    answer:
      "No. Sponsorship funds work; it does not buy a roadmap. Feature requests are welcome from sponsors and non-sponsors equally, and they are judged on whether they fit a real glass-fabrication workflow. What a sponsor does get is a direct line to the developer and early sight of what is being built.",
  },
  {
    question: "Will the application become subscription-only?",
    answer:
      "No. Licensing is a single signed licence file activated offline, in five editions from Basic to Enterprise, with a 30-day full-feature trial. Nothing about sponsorship changes that: the model is a licence you own, not a subscription you rent.",
  },
  {
    question: "Is there a GitHub Sponsor button, an Open Collective or a Patreon?",
    answer:
      "Not yet. Until one exists, sponsorship and collaboration are arranged by email — write to the address on the contact page and say what you would like to support. Setting up a platform people can actually pay through is on the list, and it will be announced here rather than quietly assumed.",
  },
  {
    question: "I am a developer, not a funder. What is the most useful thing I can do?",
    answer:
      "Read one module and tell us what is wrong with it. A concrete critique of the piece-flow clamping in the production stages, or of a nesting heuristic, is worth more than a generic offer to help. The repository is structured so a single module can be understood without the whole system.",
  },
  {
    question: "Can a company sponsor in exchange for a logo or a credit?",
    answer:
      "A credit is reasonable and can be arranged. There is no sponsorship page to put a logo on yet, and this website does not display customers or partners because it has none to display — so where a credit appears will be part of the conversation rather than a promise made here.",
  },
  {
    question: "Does the project accept contributions to the source?",
    answer:
      "Yes, in the same way as the sponsored work above: a scoped change, with tests, that a maintainer can review. The engineering rules the codebase holds itself to are documented in the repository, and a contribution that ignores them will be sent back rather than quietly merged.",
  },
];
