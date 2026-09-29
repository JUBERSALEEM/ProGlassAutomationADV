/**
 * Guides and resource categories.
 *
 * Blog posts used to live here as data. They are now Markdown files in the
 * `posts/` folder, read by `src/lib/posts.ts` — writing a note means writing a
 * file, not editing this one. What remains here is the guide content, which is
 * structured data because it is rendered as columns rather than as prose.
 *
 * Everything written here explains glass-industry concepts using the real
 * vocabulary the application works in, and every product statement is
 * traceable to the source. There are no invented statistics, no fabricated
 * case studies and no "expert" claims attributed to anyone.
 */

export type GuideSection = { heading: string; body: string; list?: string[] };

export type Guide = {
  id: string;
  title: string;
  summary: string;
  category: "Glass estimation" | "SGU" | "DGU" | "Lamination" | "Optimization" | "Stock" | "Production" | "Workflows";
  readingMinutes: number;
  sections: GuideSection[];
};

export const guides: Guide[] = [
  {
    id: "glass-vocabulary",
    title: "The vocabulary of a glass order",
    summary:
      "ASP, SQM, SGU, DGU, LAM, FT, TRN. The shorthand on a glass quotation is dense, and misreading one term changes the price and the product.",
    category: "Workflows",
    readingMinutes: 5,
    sections: [
      {
        heading: "Build types",
        body: "A glass line is described by how it is constructed, and the four constructions a quotation normally contains are:",
        list: [
          "SGU — a single lite of glass. One pane, one thickness, one treatment.",
          "DGU — a double-glazed unit: two panes separated by a sealed air space.",
          "LAM — laminated glass: two or more panes bonded with PVB interlayers.",
          "DGU + LAM — a double-glazed unit in which one of the two panes is itself laminated.",
        ],
      },
      {
        heading: "Dimensions and area",
        body: "Glass is quoted by area, not by piece. The industry unit is the square metre (SQM), and pieces are described in millimetres: W for width, H for height. A unit with two different panes needs both, which is why quotations often show W1 / H1 / W2 / H2 — the outer pane and the inner pane measured separately. Linear metres (LM) appear where an edge treatment is priced by the length of edge rather than by area.",
      },
      {
        heading: "ASP",
        body: "ASP is the air-space thickness of a double-glazed unit, written in millimetres next to the panes. A build-up reads as a sentence: 6 mm clear FT + 12 mm ASP + 6 mm clear FT. The air space is not a gap that happens; it is created by a spacer bar and sealed, and its width is a priced part of the unit.",
      },
      {
        heading: "FT and treatments",
        body: "FT is float glass — ordinary annealed glass straight from the float line. A treatment is what happens to it afterwards, and it changes both the strength and the price: annealed (untreated), heat strengthened, heat soaked, or tempered. Which one applies depends on whether the glass is a safety-critical location, whether it is laminated or part of an insulating unit, and what the specification demands.",
      },
      {
        heading: "TRN",
        body: "A TRN is a Tax Registration Number. On a UAE tax invoice the customer's TRN is a required field alongside the supplier's, and it is why a glass business cannot price a job in one system and invoice it in another without retyping the customer's legal identity.",
      },
    ],
  },
  {
    id: "cutting-optimization-basics",
    title: "Why a cutting plan is not just a tidy rectangle",
    summary:
      "Edge trims, blade kerf, breakouts and guillotine cuts. Four things that decide whether an optimized layout can actually be cut on the table.",
    category: "Optimization",
    readingMinutes: 6,
    sections: [
      {
        heading: "The sheet is not entirely usable",
        body: "Raw stock arrives with edges that are not fit for a finished product, so a trim is cut off before any piece is placed. The trim is not the same for every glass: a thin pane can take 15 mm all round, a heavy pane may need 50 mm on three edges and none on the top. A layout that ignores this is optimistic by a few millimetres on every side — which is exactly enough to lose a piece.",
      },
      {
        heading: "Kerf is real material",
        body: "Every cut destroys a strip of glass the width of the blade or scoring wheel. Three millimetres between two pieces sounds negligible until there are twenty pieces on a sheet; then it is a whole extra row. A plan that places pieces touching each other is not a tight plan, it is an impossible one.",
      },
      {
        heading: "Not every offcut is worth keeping",
        body: "Cutting a 4 mm sliver off the end of a sheet produces something nobody will ever use. The minimum breakout is the narrowest strip worth keeping. Anything below it should be reported as trim waste rather than counted as reusable stock, or the stock report fills up with glass that does not exist in practice.",
      },
      {
        heading: "Guillotine means the cut goes edge to edge",
        body: "A guillotine table scores a straight line across a rectangle and breaks it. You cannot start a cut in the middle of the glass. So a valid arrangement has to be reachable by a sequence of edge-to-edge splits: cut the sheet in half, cut each half again, and so on. This is the constraint that makes nesting hard, and it is why an arrangement that looks efficient on screen can be uncuttable in reality.",
      },
      {
        heading: "Stages are a shop-floor decision, not just a maths one",
        body: "A two-stage pattern — one direction of cut, then the other — is quick to run and easy to jig. A free guillotine pattern packs tighter but takes more table time and more handling. Neither is wrong; the right answer depends on how busy the table is and what the job is worth.",
      },
    ],
  },
  {
    id: "quote-versus-order",
    title: "What should survive from a quotation to a job order",
    summary:
      "A quotation that gets re-typed into a production document loses details quietly. Here is the shortlist worth protecting.",
    category: "Workflows",
    readingMinutes: 4,
    sections: [
      {
        heading: "The geometry, not a summary",
        body: "Step 4 of a quotation establishes which sheet a group is cut from and what air space the unit has. Step 5 establishes the W1/H1/W2/H2 grid. If the job order receives only a total area and a price, the factory is working from a summary of a decision it cannot see. The grid geometry is what should cross.",
      },
      {
        heading: "Per-group provenance",
        body: "On a multi-section job, each section has its own build-up, its own sheet selection, and therefore its own cost. If the whole quotation is costed with section one's panel, every other section is priced wrong — usually in the optimistic direction.",
      },
      {
        heading: "The cost basis, stamped",
        body: "The sheet price used at the moment of quoting is part of the quotation. When stock prices move next month, that quote should still show the margin it was built on, and the movement should be flagged rather than silently applied.",
      },
      {
        heading: "The colours and treatments, per pane",
        body: "A DGU has two panes, and they rarely match. The inner pane and the outer pane each have their own thickness, colour and treatment. Collapsing them into one description is how a job gets built with the wrong tint on one side.",
      },
    ],
  },
  {
    id: "stock-accuracy",
    title: "Stock accuracy: movements instead of totals",
    summary:
      "A stock figure that is only ever overwritten cannot be audited. Recording buys and uses as movements keeps the balance explainable.",
    category: "Stock",
    readingMinutes: 4,
    sections: [
      {
        heading: "The problem with a number",
        body: "If the stock screen holds one quantity per item and someone edits it down from 40 to 38, the reason is gone. Nobody can say whether those two sheets were sold, broken, cut wrong or counted wrong — and the same edit can happen again next week.",
      },
      {
        heading: "Movements give you a balance with a history",
        body: "Record a purchase: the balance goes up, and the movement says what was bought, at what price and from whom. Record a use: the balance goes down, and the movement says which job it went to. The current figure is then the sum of things that happened, rather than a number somebody typed.",
      },
      {
        heading: "Price both directions",
        body: "A sheet has a purchase price per square metre and a selling price per square metre, and they answer different questions. Balance at cost tells you what is tied up on the racks. Balance at sell tells you what is available to earn. Both belong on the stock screen.",
      },
      {
        heading: "Consumption is the interesting report",
        body: "The total stock figure tells you how much glass you own. Consumption grouped by job tells you where it went, and that is the number that lets you price the next similar job with confidence.",
      },
    ],
  },
];

export const resourceCategories = [
  "Glass estimation",
  "SGU",
  "DGU",
  "Lamination",
  "Optimization",
  "Stock",
  "Production",
  "Workflows",
] as const;
