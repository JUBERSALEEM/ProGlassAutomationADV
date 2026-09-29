/**
 * Resources and blog content.
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

export type Post = {
  slug: string;
  title: string;
  summary: string;
  date: string;
  readingMinutes: number;
  category: "Product" | "Engineering" | "Industry" | "ProGlass updates";
  body: { heading?: string; paragraphs: string[]; list?: string[] }[];
};

export const posts: Post[] = [
  {
    slug: "why-the-price-basis-is-stamped",
    title: "Why every quoted figure carries its price basis",
    summary:
      "An ERP that recalculates yesterday's quotation at today's glass prices is not an ERP. What ProGlass stores instead, and why it matters more than any feature.",
    date: "2026-09-20",
    readingMinutes: 5,
    category: "Engineering",
    body: [
      {
        paragraphs: [
          "There is a failure mode in quotation software that is worse than a crash, because it does not look like a failure. The software is asked for last month's job. It reopens the quotation, re-reads the current sheet prices, and recomputes. The margin on screen is now a number that nobody ever quoted.",
          "Nothing warns you. The screen looks normal. The report is internally consistent. And the number is wrong in the only way that matters: it is not the number the business acted on.",
        ],
      },
      {
        heading: "Material costs move",
        paragraphs: [
          "Glass is bought on sheets and priced by area, and those buy prices change. When they do, every stored quotation that recalculates itself silently becomes a different quotation. If your profitability reporting reads those figures, your historical margin data is rewritten every time a supplier changes a rate.",
        ],
      },
      {
        heading: "What ProGlass does instead",
        paragraphs: [
          "The profit and loss snapshot is written once, at Step 9 of the proforma, and it records the cost basis it was computed with. Job profitability, statements and reports read that stored snapshot rather than recomputing it. There is exactly one pricing engine in the application, and it runs at the moment of quoting.",
          "When the sheet-store prices move, the affected rows are flagged rather than rescored. You can see that the basis has moved, decide what to do about it, and the historical figure stays what it was.",
        ],
      },
      {
        heading: "Why this is the whole product",
        paragraphs: [
          "Feature lists are easy to write and hard to compare. This property is not a feature — it is what separates a system that holds business records from a calculator with a database attached. A price you cannot trust after the fact is not a price; it is a current estimate.",
        ],
      },
    ],
  },
  {
    slug: "guillotine-versus-free-nesting",
    title: "Guillotine or free nesting: what decides the layout",
    summary:
      "Two optimizers can produce very different-looking plans for the same cutting list, and both can be correct. The difference is what the table can physically do.",
    date: "2026-09-12",
    readingMinutes: 6,
    category: "Industry",
    body: [
      {
        paragraphs: [
          "A nesting problem in the abstract is: fit these rectangles into that rectangle with as little waste as possible. On a glass table it is a narrower problem: fit these rectangles in such a way that a sequence of straight, edge-to-edge cuts separates them, in an order an operator can actually perform.",
        ],
      },
      {
        heading: "The pinwheel problem",
        paragraphs: [
          "Free nesting algorithms like to produce pinwheel arrangements — rectangles that spiral around each other. They are efficient on paper and impossible on a guillotine. You cannot score a line from one edge of the sheet to the other without also cutting through a piece that is already in place.",
        ],
      },
      {
        heading: "What guillotine costs you",
        paragraphs: [
          "Constraining to guillotine cuts loses some yield. That is the honest trade: a packed-pinwheel layout may report a higher utilization than a guillotine layout, while being uncuttable. A number you cannot execute is worse than no number at all.",
        ],
      },
      {
        heading: "Two stages, three stages, or free",
        paragraphs: [
          "The trade continues inside the guillotine family. Two-stage patterns restrict the cut direction: first all the cuts one way, then all the cuts the other. They are fast, easy to set up and easy to jig, and they leave a bit more waste. Free guillotine allows unlimited depth and packs tighter, at the cost of more table time. Three-stage sits in between. Which one is right depends on your table, your volume and the job.",
        ],
      },
      {
        heading: "The thing to ask any optimizer for",
        paragraphs: [
          "Ask it to show the cut sequence. A layout drawing shows where the pieces are; a cut sequence shows how they come free. If a plan can report its pieces but not its cuts, it has modelled the arrangement and not the operation.",
        ],
      },
    ],
  },
  {
    slug: "why-delivery-balance-is-hard",
    title: "Why delivery balance is harder than it looks",
    summary:
      "A job goes out over three movements and the fourth is a replacement for a broken pane. The arithmetic that answers 'what is still owed' is easy to get wrong.",
    date: "2026-09-05",
    readingMinutes: 5,
    category: "Industry",
    body: [
      {
        paragraphs: [
          "A glass order is rarely delivered once. It goes out in whatever the vehicle and the site can take, and the balance follows. The question 'how much of this job has actually gone out' sounds trivial and is not.",
        ],
      },
      {
        heading: "Three ways it goes wrong",
        paragraphs: [],
        list: [
          "Double counting: the balance is recomputed from the original order each time, and a piece that was dispatched in movement one gets counted again in movement two.",
          "Over-delivery: nothing prevents the second delivery note from listing more pieces than remain, so the job ships 12 of 10 pieces and the invoice has to be unravelled.",
          "The replacement problem: a broken pane is re-cut and re-delivered. Is that a movement against the original balance, or a new line? If it is answered differently by different people, the balance drifts.",
        ],
      },
      {
        heading: "One place owns the maths",
        paragraphs: [
          "The fix is not a better screen; it is deciding that exactly one piece of code owns the pending-piece calculation, and that nothing else is allowed to work it out independently. In ProGlass that lives in the delivery-note validation service, which refuses a movement that would exceed what was ordered.",
        ],
      },
      {
        heading: "Why it is worth the trouble",
        paragraphs: [
          "The balance is what the customer will be told on the phone, what the driver will load against, and what the invoice is eventually raised for. When three people have three answers, the job costs more than the glass did.",
        ],
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

export function postBySlug(slug: string): Post | undefined {
  return posts.find((p) => p.slug === slug);
}
