/**
 * The collaboration page's data.
 *
 * ProGlass Automation is one person's work and needs to stop being that. This
 * file states who would change it, what each kind of partner would actually
 * do, and — the part that matters most — what is *not* being claimed.
 *
 * Two rules hold everything here:
 *
 *   1. **No partnership is asserted.** Guardian, AGC, Şişecam and Emirates
 *      Glass are named because their published performance data is what an
 *      estimator in this region reaches for when a specification lands. None of
 *      them has been contacted, and the page says so in the same breath as it
 *      names them. A logo wall implying otherwise is the one thing this site
 *      has refused to build everywhere else.
 *
 *   2. **Nothing here is an offer of investment.** "Investor" appears because
 *      funding is a real conversation this project needs to have; the page also
 *      says plainly that it is not a prospectus or a solicitation, so nobody
 *      mistakes a public web page for a securities document.
 *
 * Prose lives in the page, facts live here, and both are translated by the same
 * mechanism as the rest of the site.
 */

export type CollaboratorRole = {
  id: string;
  /** What kind of partner this is. */
  title: string;
  /** Who it actually is, in one line. */
  who: string;
  /** What they would bring. */
  brings: string;
  /** What they get out of it. */
  gets: string;
};

/**
 * Six kinds of help. Ordered by how much of the bottleneck each one removes,
 * which is the only ordering that matters on a one-person project.
 */
export const collaboratorRoles: CollaboratorRole[] = [
  {
    id: "fabricator",
    title: "A glass fabricator",
    who: "Someone at a manufacturer or a large processor who owns the performance data for their own products.",
    brings: "Published U-value, solar factor and light-transmission figures, and permission to carry them.",
    gets: "Their data inside the software their customers quote with — attributed, and only as published.",
  },
  {
    id: "investor",
    title: "An investor or backer",
    who: "Someone who would rather fund a specific industrial product than a general pitch.",
    brings: "Money to buy hours and machines, so the roadmap stops moving at the speed of one person's evenings.",
    gets: "The state of the build as it is — the open findings included — and a direct line to the person writing it.",
  },
  {
    id: "hardware",
    title: "A laptop or PC provider",
    who: "A vendor, a refurbisher, or anyone with a spare business-grade machine.",
    brings: "A development machine and a second machine to test on. Both are borrowed or shared today.",
    gets: "Named credit in the release the machine supports, and a real product that a small business depends on.",
  },
  {
    id: "engineer",
    title: "A software collaborator",
    who: "A WPF or .NET engineer who has read the audits on the developer page and wants the open ones closed.",
    brings: "Capacity on the engine and the production modules — the eight gaps named on the sponsorship page.",
    gets: "Work on a codebase with named invariants, a dated paper trail, and tests that say what is true.",
  },
  {
    id: "translator",
    title: "A translator or localisation reviewer",
    who: "A glass-industry native speaker in any of the fifteen languages the application ships.",
    brings: "Review of the machine-filled strings against the product's own vocabulary.",
    gets: "The application's XAML dictionary as the source of truth, so the review has something to check against.",
  },
  {
    id: "operator",
    title: "An operator or shop owner",
    who: "Someone who runs the day this software is for, willing to say where it is wrong.",
    brings: "Half an hour, once, describing how a real job moves through their shop.",
    gets: "Their complaint written down as a finding with a name on it, and answered on the next release.",
  },
];

/**
 * The module this page is really asking about.
 *
 * A performance-data calculator is the one feature every fabricator's customer
 * asks for and no estimator wants to assemble by hand: the U-value, the solar
 * factor and the light transmission of the exact build-up being quoted, sitting
 * in the quote rather than in a PDF from another window.
 *
 * It cannot be built from published standards alone — a manufacturer's figures
 * are the manufacturer's to publish — which is why it needs a data owner rather
 * than more evenings.
 */
export const performanceModule = {
  /** What the module would be called in the application. */
  name: "Performance data calculator",
  /** Its state today, stated first because it is the honest answer. */
  status: "Not built. No fabricator's data is in the application today.",
  /** Why the software cannot simply derive it. */
  why: [
    "A quotation already carries every number in a build-up — glass, interlayer, air space, spacer, gas, coating, thickness. ProGlass prices all of it without leaving the screen.",
    "The one thing it cannot carry is what the finished build-up performs, because a U-value or a solar factor belongs to the company that tested the product, not to the standard it was tested against.",
    "So an estimator does the last step by hand: another window, another PDF, a figure typed into a quote, and nothing that checks it afterwards.",
  ],
  /** What the data owner supplies. */
  fromPartner: [
    "Your published figures for the build-ups you already sell.",
    "Whatever form they already exist in — a table, a data sheet, an export. No new testing.",
    "Permission to carry them, and a named contact for when one changes.",
  ],
  /** What the software does with them. */
  fromProGlass: [
    "The figure appears against the build-up being quoted, next to its price.",
    "A performance sheet prints from the same data, for submittals.",
    "The source is named wherever the number appears — yours, as published.",
    "Updating a figure is a data edit, not a release.",
  ],
  /** Rules the module would hold to, which appear on the page as a caption. */
  rules: [
    "No figure is derived, estimated or filled in from a competitor's product.",
    "No figure is shown without naming who published it.",
    "A build-up with no data source shows no performance figure at all.",
  ],
};

/**
 * The companies whose data an estimator in this region reaches for.
 *
 * Named as data owners, not as partners. The page states in the same breath
 * that none of them is involved — see the honesty line in the page's own copy.
 * A list of names presented as a target is honest; the same list presented
 * beside a logo treatment would not be.
 */
export const dataOwners: { name: string; note: string }[] = [
  { name: "Guardian Glass", note: "Published performance data across coated and laminated ranges." },
  { name: "AGC", note: "Public performance tables for architectural glass products." },
  { name: "Şişecam", note: "Regional architectural ranges with published values." },
  { name: "Emirates Glass", note: "Gulf-region manufacturer with published product data." },
  {
    name: "Any other fabricator",
    note: "The module is not built around one supplier. A second data set is what makes it useful.",
  },
];

/** What a partner gets, stated without inflation. */
export const whatYouGet = [
  {
    title: "The roadmap, honestly",
    body: "Including the findings that are still open and the ones that need a decision rather than a patch. The developer page lists them; that list is the same one the author works from.",
  },
  {
    title: "A direct line",
    body: "No account manager, no support tier, no queue. Questions reach the person who can change the software, which is also the only reason a product this specific can keep improving.",
  },
  {
    title: "Named credit",
    body: "Funding, a machine, a data set or a review is named in the release note of the version it appears in — by name, if that is what you want, or as an anonymous backer if it is not.",
  },
];

/** What is not on offer. The counterpart of the sponsorship page's own list. */
export const notOnOffer = [
  "A promise of return, equity, or any financial instrument. Nothing here is a prospectus or a solicitation of investment.",
  "Exclusivity by market, sector or customer list.",
  "A performance figure the software cannot attribute to the company that published it.",
  "A logo on this site implying a partnership that has not been agreed in writing.",
];

/** How a conversation starts. Four steps, none of them a form. */
export const howItStarts = [
  {
    step: "01",
    title: "One email, in plain words",
    body: "Who you are, what you have, and which of the six roles above you are looking at. Three sentences is enough.",
  },
  {
    step: "02",
    title: "An honest answer",
    body: "Including a no. A project this size cannot absorb a partner whose work it cannot look after, and saying so early is cheaper than saying it late.",
  },
  {
    step: "03",
    title: "A small first piece",
    body: "One data set, one machine, one module, one language. Small enough that both sides can stop after it without damage.",
  },
  {
    step: "04",
    title: "Then the terms, in writing",
    body: "What was agreed, what it is worth, and what appears on this site — written down before any work starts, and named in the release it lands in.",
  },
];

/** The questions a partner actually asks before writing. */
export const collabFaq: { question: string; answer: string }[] = [
  {
    question: "Is this an investment opportunity?",
    answer:
      "It is a conversation about funding a product that exists and is in use, and it is not an offer or a solicitation of investment. No shares, no instruments, no forecast. If funding is what interests you, the first email gets the build's real state — including what is unfinished — before any terms are discussed.",
  },
  {
    question: "Who owns performance data a fabricator supplies?",
    answer:
      "The fabricator does. Supplying it grants permission to carry it and to attribute it; it does not transfer it, and it does not stop being the fabricator's to withdraw. The module shows a figure only where it can name who published it, so removing a data set removes the figure rather than leaving an orphan.",
  },
  {
    question: "What happens to the software if the developer stops?",
    answer:
      "It keeps running. ProGlass is a Windows desktop application with a local SQLite database, licensed rather than rented — there is no server that has to stay up and no account that expires. What would stop is maintenance and new work, which is the honest reason this page exists.",
  },
  {
    question: "Do I have to be technical, or in software?",
    answer:
      "No. Two of the six roles above need a machine, one needs a data set and one needs half an hour of an operator's day. The author came to this from the glass trade rather than from software, and the request is deliberately written so the same is possible for whoever answers it.",
  },
  {
    question: "What would the first month of funding actually buy?",
    answer:
      "Hours, mostly — evenings are the current limit — and a second machine to test on. The roadmap that those hours go into is public: the eight gaps on the sponsorship page, and the open findings listed on the developer page. Nothing is spent on marketing, and no figure here is a projection of profit.",
  },
];
