// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { ProformaSpecification } from "../src/components/inquiries/proforma/ProformaSpecification";
import {
  draftIssues,
  duplicateLine,
  emptyLine,
  groupTotals,
  moveLine,
  priceDraft,
  type DraftLine,
} from "../src/lib/proforma-draft";

function line(groupNo: number, w1: string, h1: string, qty: string, rate: string, ref = "DGU 6+12+6"): DraftLine {
  return { ...emptyLine(groupNo), w1, h1, qty, rate, glassRef: ref };
}

describe("duplicating a row", () => {
  it("places the copy directly after the row it copies and keeps every figure", () => {
    const source = line(2, "1200", "900", "8", "150", "LAM 8.8");
    const rows = [line(1, "1000", "1000", "1", "100"), source, line(2, "500", "500", "2", "120")];
    const copied = duplicateLine(rows, source.key);

    expect(copied).toHaveLength(4);
    expect(copied[1]?.key).toBe(source.key);
    expect(copied[2]?.key).not.toBe(source.key);
    expect(copied[2]).toMatchObject({ groupNo: 2, w1: "1200", h1: "900", qty: "8", rate: "150", glassRef: "LAM 8.8" });
  });

  it("renumbers the specification from the order, so the copy is a real second line", () => {
    const source = line(1, "1000", "1000", "1", "100");
    const rows = [source, line(2, "500", "500", "1", "120")];
    const priced = priceDraft(duplicateLine(rows, source.key));

    expect(priced.map((row) => `${row.groupNo}.${row.lineNo}`)).toEqual(["1.1", "1.2", "2.1"]);
    expect(priced[0]?.value).toBe(priced[1]?.value);
  });

  it("does nothing for a key that is not in the draft", () => {
    const rows = [line(1, "1000", "1000", "1", "100")];
    expect(duplicateLine(rows, "line-missing")).toBe(rows);
  });
});

describe("moving a row", () => {
  it("swaps two rows of the same specification and renumbers them", () => {
    const first = line(1, "1000", "1000", "1", "100", "first");
    const second = line(1, "2000", "2000", "1", "100", "second");
    const moved = moveLine([first, second], second.key, -1);
    const priced = priceDraft(moved);

    expect(priced.map((row) => row.glassRef)).toEqual(["second", "first"]);
    expect(priced.map((row) => row.lineNo)).toEqual([1, 2]);
  });

  it("never moves a row into another specification", () => {
    const only = line(1, "1000", "1000", "1", "100", "only");
    const other = line(2, "1200", "1200", "1", "120", "other");
    const rows = [only, other];

    expect(moveLine(rows, only.key, 1)).toBe(rows);
    expect(moveLine(rows, other.key, -1)).toBe(rows);
  });

  it("stops at the top and the bottom", () => {
    const first = line(1, "1000", "1000", "1", "100", "first");
    const second = line(1, "2000", "2000", "1", "100", "second");
    const rows = [first, second];

    expect(moveLine(rows, first.key, -1)).toBe(rows);
    expect(moveLine(rows, second.key, 1)).toBe(rows);
  });

  it("skips over a specification in between rather than crossing it", () => {
    const one = line(1, "1000", "1000", "1", "100", "one");
    const two = line(2, "1100", "1100", "1", "110", "two");
    const three = line(3, "1200", "1200", "1", "120", "three");

    expect(moveLine([one, two, three], one.key, 1)).toEqual([one, two, three]);
  });
});

describe("validation that mirrors the write function", () => {
  it("accepts a complete line", () => {
    expect(draftIssues([line(1, "1000", "1000", "1", "100")])).toEqual([]);
  });

  it("refuses a quantity above the function's limit", () => {
    expect(draftIssues([line(1, "1000", "1000", "100001", "100")]).join(" ")).toContain("cannot exceed 100000");
  });

  it("refuses a rate above the function's limit", () => {
    expect(draftIssues([line(1, "1000", "1000", "1", "1000001")]).join(" ")).toContain("cannot exceed 1000000");
  });

  it("refuses an inner size above 20000 mm", () => {
    const row = { ...line(1, "1000", "1000", "1", "100"), w2: "20001" };
    expect(draftIssues([row]).join(" ")).toContain("inner size cannot exceed 20000 mm");
  });

  it("refuses more than 200 lines and more than 50 specifications", () => {
    const many = Array.from({ length: 201 }, () => line(1, "1000", "1000", "1", "100"));
    expect(draftIssues(many).join(" ")).toContain("at most 200 lines");

    const groups = Array.from({ length: 51 }, (_, index) => line(index + 1, "1000", "1000", "1", "100"));
    expect(draftIssues(groups).join(" ")).toContain("at most 50 specifications");
  });

  it("refuses a specification number outside the range the table allows", () => {
    expect(draftIssues([line(51, "1000", "1000", "1", "100")]).join(" ")).toContain("outside 1 to 50");
  });
});

describe("the row controls", () => {
  const first = line(1, "1000", "1000", "1", "100", "first");
  const second = line(1, "2000", "2000", "1", "100", "second");
  const other = line(2, "1200", "900", "2", "150", "other");
  const priced = priceDraft([first, second, other]);

  function surface(overrides: Partial<Parameters<typeof ProformaSpecification>[0]> = {}) {
    return (
      <ProformaSpecification
        priced={priced}
        groups={[1, 2]}
        perGroup={groupTotals(priced)}
        activeGroup={1}
        currency="AED"
        totals={{ lines: 3, sqm: 10, value: 1000 }}
        rateSettings={{}}
        onRateChange={() => {}}
        onApplyRates={() => {}}
        onEdit={() => {}}
        onAddLine={() => {}}
        onRemoveLine={() => {}}
        onDuplicateLine={() => {}}
        onMoveLine={() => {}}
        onAddGroup={() => {}}
        onRemoveGroup={() => {}}
        onFocusGroup={() => {}}
        {...overrides}
      />
    );
  }

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it("reports a copy of the row that was clicked", () => {
    const onDuplicateLine = vi.fn();
    render(surface({ onDuplicateLine }));

    fireEvent.click(screen.getByRole("button", { name: /duplicate line 2 of specification 1/i }));
    expect(onDuplicateLine).toHaveBeenCalledWith(second.key);
  });

  it("reports a move with its direction and disables the ends", () => {
    const onMoveLine = vi.fn();
    render(surface({ onMoveLine }));

    fireEvent.click(screen.getByRole("button", { name: /move line 2 of specification 1 up/i }));
    expect(onMoveLine).toHaveBeenCalledWith(second.key, -1);

    expect(screen.getByRole("button", { name: /move line 1 of specification 1 up/i })).toHaveProperty("disabled", true);
    expect(screen.getByRole("button", { name: /move line 2 of specification 1 down/i })).toHaveProperty("disabled", true);
    expect(screen.getByRole("button", { name: /move line 1 of specification 2 up/i })).toHaveProperty("disabled", true);
  });
});
