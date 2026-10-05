/**
 * Licensing editions — the real tiers from
 * Licensing/Contracts/LicensePayload.cs (`enum LicenseEdition`).
 *
 * The repository defines the tiers and what each one grants. It does NOT
 * contain a price list, so this site publishes no prices and says so, rather
 * than inventing a subscription.
 *
 * Enum values are ordinal-compared by OperationGuard.LicenseEditionAtLeast,
 * so the order below is the commercial order, not an alphabetised list.
 */

export type Edition = {
  id: string;
  name: string;
  order: number;
  positioning: string;
  /** Verbatim from the enum documentation in LicensePayload.cs. */
  definition: string;
  includes: string[];
  /** Modules the edition unlocks that the tier below does not. */
  unlocks?: string[];
  featured?: boolean;
};

export const editions: Edition[] = [
  {
    id: "trial",
    name: "Trial",
    order: 0,
    positioning: "Evaluation",
    definition: "30-day, single activation, all features.",
    includes: [
      "Every feature set enabled, including the production stages and the optimization engine",
      "Recorded by a tamper-evident trial ledger on this machine",
      "Non-renewable by deleting files — the anti-rollback anchor is intact",
    ],
  },
  {
    id: "basic",
    name: "Basic",
    order: 1,
    positioning: "Single user",
    definition: "Single user, inquiry + proforma only.",
    includes: ["Single user on one workstation", "Inquiries and the proforma invoice wizard", "Local SQLite data in your own user profile"],
  },
  {
    id: "business",
    name: "Business",
    order: 2,
    positioning: "Small team",
    definition: "Up to 5 users, full ERP without production optimization.",
    includes: [
      "Up to five users",
      "The full document chain: inquiry → proforma → job order → delivery note → tax invoice → statement",
      "Sheet Store, Specifications, Company Center, System Center and reporting",
      "Job profitability",
    ],
  },
  {
    id: "factory",
    name: "Factory",
    order: 3,
    positioning: "Production floor",
    definition: "Unlimited users, includes cutting-optimization engine.",
    includes: [
      "Unlimited users",
      "Everything in Business",
      "The cutting optimization engine",
      "The production-stage modules: Cutting · Holes & Cutouts · Polish · Edge Deletion · Mitring · Tempering · Pairing · Doubling / Lamination",
    ],
    unlocks: ["Optimization", "Production stages"],
    featured: true,
  },
  {
    id: "enterprise",
    name: "Enterprise",
    order: 4,
    positioning: "Multi-site",
    definition: "Unlimited + multi-site + custom integrations.",
    includes: ["Unlimited users", "Everything in Factory", "Multi-site deployment", "Custom integration work"],
  },
];

/**
 * The real gating map — Services/Licensing/LicenseGate.cs.
 * An empty `required` array is an explicit "available in every edition",
 * written down rather than left implied.
 */
export const licenceGates = [
  {
    group: "Production stage modules",
    requiredFeature: "Cutting.*",
    minimumEdition: "Factory",
    modules: ["Cutting", "Holes & Cutouts", "Polish", "Edge Deletion", "Mitring", "Tempering", "Pairing", "Doubling / Lamination"],
  },
  {
    group: "Cutting-optimization engine",
    requiredFeature: "Cutting.*",
    minimumEdition: "Factory",
    modules: ["Optimization"],
  },
  {
    group: "Profitability reporting",
    requiredFeature: "Reports.*",
    minimumEdition: "Business",
    modules: ["Job Profitability"],
  },
] as const;

/**
 * What is reachable without a licence key.
 *
 * The names are the rail's own, because that is where a reader will go looking
 * for them: the reports hub is "Overview" in the Reports group, and the module
 * the rail calls "Logs & Activity" is listed that way rather than by its
 * internal name. Twenty of these are sidebar entries; Settings and About are
 * reached from the shell's own menu, which is why the count is twenty-two.
 */
export const openModules = [
  "About",
  "Company Center",
  "Dashboard",
  "Delivery Note",
  "Delivery Report",
  "Inquiry",
  "Job Order",
  "Job Order Report",
  "Logs & Activity",
  "Overview (reports hub)",
  "Proforma Invoice",
  "Proforma Invoice Report",
  "Settings",
  "Sheet Store",
  "Specifications",
  "Statement Generator",
  "Statement Report",
  "System Center",
  "Tax Invoice",
  "Tax Invoice Report",
  "User Center",
  "VAT Submission",
] as const;

export const licenceFacts = [
  "Licence files are signed and verified before the application trusts them.",
  "Activation happens offline — no licence server needs to be reachable.",
  "The trial is issued automatically the first time the application runs.",
  "An expired licence degrades to read-only rather than locking you out of your data.",
  "Editions are compared by order, so a stored edition keeps its meaning for existing customers.",
] as const;
