import { describe, expect, it } from "vitest";
import {
  draftIssues,
  draftTotals,
  emptyLine,
  fromSavedLine,
  groupTotals,
  groupsOf,
  newIdempotencyKey,
  newLineKey,
  nextGroupNo,
  priceDraft,
  removeGroup,
  toLineInputs,
  type DraftLine,
} from "../src/lib/proforma-draft";

function one(lines: DraftLine[]) {
  const [row] = priceDraft(lines);
  if (!row) throw new Error("expected a priced line");
  return row;
}

function line(groupNo: number, w1: string, h1: string, qty: string, rate: string, extra: Partial<DraftLine> = {}): DraftLine {
  return { ...emptyLine(groupNo), w1, h1, qty, rate, glassRef: extra.glassRef || "6mm Clear FT", ...extra };
}

describe("panel arithmetic", () => {
  it("matches the database on a plain panel", () => {
    const priced = one([line(1, "1000", "1000", "2", "100")]);
    expect(priced.sqm).toBe(1);
    expect(priced.totalSqm).toBe(2);
    expect(priced.value).toBe(200);
  });

  it("matches the database on the demonstration rows", () => {
    const priced = priceDraft([
      line(1, "1200", "900", "8", "150"),
      line(2, "2000", "1500", "4", "185"),
      line(2, "300", "300", "3", "99.99"),
    ]);
    expect(priced.map((row) => [row.sqm, row.totalSqm, row.value])).toEqual([
      [1.08, 8.64, 1296],
      [3, 12, 2220],
      [0.5, 1.5, 149.99],
    ]);
    expect(draftTotals(priced)).toEqual({ lines: 3, sqm: 22.14, value: 3665.99 });
  });

  it("floors a small panel at half a square metre and says so", () => {
    const priced = one([line(1, "300", "300", "1", "100")]);
    expect(priced.sqm).toBe(0.5);
    expect(priced.floored).toBe(true);
    expect(priced.physicalSqm).toBe(0.09);
  });

  it("does not floor a half typed panel, it reports it as incomplete", () => {
    const priced = one([line(1, "0", "", "1", "100")]);
    expect(priced.sqm).toBe(0);
    expect(priced.complete).toBe(false);
    expect(priced.floored).toBe(false);
  });
});

describe("specification groups", () => {
  it("prices each group on its own", () => {
    const lines = [line(1, "1000", "1000", "1", "100"), line(2, "2000", "2000", "1", "50")];
    const totals = groupTotals(priceDraft(lines));
    expect(totals).toEqual([
      { groupNo: 1, lines: 1, quantity: 1, sqm: 1, value: 100 },
      { groupNo: 2, lines: 1, quantity: 1, sqm: 4, value: 200 },
    ]);
  });

  it("numbers lines inside their own group", () => {
    const priced = priceDraft([line(1, "100", "100", "1", "1"), line(2, "100", "100", "1", "1"), line(1, "100", "100", "1", "1")]);
    expect(priced.map((row) => [row.groupNo, row.lineNo])).toEqual([[1, 1], [2, 1], [1, 2]]);
  });

  it("keeps row identity while the numbers move", () => {
    const draft = line(1, "1000", "1000", "1", "100");
    const before = priceDraft([draft])[0]!.key;
    const after = priceDraft([{ ...draft, w1: "2000" }])[0]!.key;
    expect(after).toBe(before);
    expect(before).not.toBe(priceDraft([line(1, "1000", "1000", "1", "100")])[0]!.key);
  });

  it("removes a group and renumbers the ones after it without re-keying rows", () => {
    const a = line(1, "100", "100", "1", "1");
    const b = line(2, "100", "100", "1", "1");
    const c = line(3, "100", "100", "1", "1");
    const next = removeGroup([a, b, c], 2);
    expect(next.map((row) => row.groupNo)).toEqual([1, 2]);
    expect(next[1]!.key).toBe(c.key);
    expect(groupsOf(next)).toEqual([1, 2]);
    expect(nextGroupNo(next)).toBe(3);
  });
});

describe("saving", () => {
  it("sends groups and positions the database validates", () => {
    const inputs = toLineInputs([line(2, "1200", "900", "8", "150"), line(2, "300", "300", "3", "99.99")]);
    expect(inputs).toEqual([
      { group_no: 2, line_no: 1, glass_ref: "6mm Clear FT", w1: 1200, h1: 900, w2: 0, h2: 0, qty: 8, rate: 150 },
      { group_no: 2, line_no: 2, glass_ref: "6mm Clear FT", w1: 300, h1: 300, w2: 0, h2: 0, qty: 3, rate: 99.99 },
    ]);
  });

  it("refuses to save an empty or incomplete draft", () => {
    expect(draftIssues([])).toEqual(["Add at least one line to the proforma."]);
    expect(draftIssues([line(1, "", "", "1", "100")])).toHaveLength(1);
    expect(draftIssues([line(1, "1000", "1000", "0", "100")])).toHaveLength(1);
    expect(draftIssues([line(1, "1000", "1000", "1", "100")])).toEqual([]);
  });

  it("round trips a saved line back into the editor", () => {
    const saved = {
      group_no: 2,
      line_no: 3,
      glass_ref: "DGU 6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass",
      w1: 1200,
      h1: 900,
      w2: 800,
      h2: 600,
      qty: 4,
      approved_rate: 185,
      sqm: 1.08,
      total_sqm: 4.32,
      line_value: 799.2,
    };
    const draft = fromSavedLine(saved);
    expect(draft).toMatchObject({ groupNo: 2, w1: "1200", h1: "900", w2: "800", h2: "600", qty: "4", rate: "185" });
    expect(priceDraft([draft])[0]!.value).toBe(799.2);
  });
});

describe("row identity", () => {
  it("makes a fresh key per line", () => {
    expect(newLineKey()).not.toBe(newLineKey());
  });

  it("makes a canonical idempotency key the database will accept", () => {
    const key = newIdempotencyKey();
    expect(key).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i);
    expect(newIdempotencyKey()).not.toBe(key);
  });
});
