// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { SpecificationRateBar } from "../src/components/inquiries/proforma/SpecificationRateBar";
import { emptyLine, priceDraft, type DraftLine } from "../src/lib/proforma-draft";
import { EMPTY_RATE_SETTING, applyRatePlan, formatRate, parsePercentInput, parseRateInput, planRates, rowsThatWouldChange, settingsFromServer } from "../src/lib/proforma-rates";

// Every describe cleans up, so a render from one case cannot be found by the next.
afterEach(cleanup);

function line(groupNo: number, w1: string, h1: string, qty: string, rate: string): DraftLine {
  return { ...emptyLine(groupNo), w1, h1, qty, rate, glassRef: "DGU 6+12+6" };
}

describe("specification rate entry, as Step 5 does it", () => {
  it("gives a panel at the threshold the surcharge rate and the rest the base rate", () => {
    const priced = priceDraft([line(1, "2000", "2000", "1", "0"), line(1, "1000", "1000", "1", "0")]);
    const plan = planRates(priced, 1, "100", "20");

    expect(plan).not.toBeNull();
    expect(plan?.surchargeRate).toBe(120);
    expect(plan?.rows.map((row) => row.rate)).toEqual([120, 100]);
    expect(plan?.surchargedCount).toBe(1);
    expect(rowsThatWouldChange(plan!)).toBe(2);
  });

  it("rounds the oversize rate the way the desktop does", () => {
    const priced = priceDraft([line(1, "2000", "2000", "1", "0")]);
    // 99.99 * 1.20 = 119.988, which the desktop rounds to 119.99, not 119.98.
    expect(planRates(priced, 1, "99.99", "20")?.surchargeRate).toBe(119.99);
  });

  it("keeps a row below half a square metre on the base rate", () => {
    const priced = priceDraft([line(1, "300", "300", "1", "0")]);
    const plan = planRates(priced, 1, "100", "20");
    expect(plan?.rows[0]?.rate).toBe(100);
    expect(plan?.surchargedCount).toBe(0);
  });

  it("prices only the specification it was asked about", () => {
    const priced = priceDraft([line(1, "1000", "1000", "1", "0"), line(2, "2000", "2000", "1", "0")]);
    const plan = planRates(priced, 2, "100", "20");
    expect(plan?.rows).toHaveLength(1);
    expect(plan?.rows[0]?.rate).toBe(120);
  });

  it("treats an empty percentage as no surcharge", () => {
    const priced = priceDraft([line(1, "2000", "2000", "1", "0")]);
    const plan = planRates(priced, 1, "100", "");
    expect(plan?.percent).toBe(0);
    expect(plan?.surchargeRate).toBe(100);
    expect(plan?.rows[0]?.rate).toBe(100);
  });

  it("refuses to plan without a usable base rate", () => {
    const priced = priceDraft([line(1, "1000", "1000", "1", "0")]);
    expect(planRates(priced, 1, "", "20")).toBeNull();
    expect(planRates(priced, 1, "abc", "20")).toBeNull();
    expect(planRates(priced, 1, "-1", "20")).toBeNull();
    expect(planRates(priced, 1, "1000001", "20")).toBeNull();
  });

  it("leaves an incomplete row on the rate it already carries", () => {
    const priced = priceDraft([{ ...emptyLine(1), rate: "150" }]);
    const plan = planRates(priced, 1, "100", "20");
    expect(plan?.rows[0]?.sqm).toBe(0);
    expect(plan?.rows[0]?.rate).toBe(150);
  });

  it("writes two decimal rates into the rows it covers and nothing into the rest", () => {
    const lines = [line(1, "2000", "2000", "1", ""), line(1, "1000", "1000", "1", ""), line(2, "1000", "1000", "1", "")];
    const plan = planRates(priceDraft(lines), 1, "100", "20");
    const applied = applyRatePlan(lines, plan!);

    expect(applied.map((row) => row.rate)).toEqual(["120.00", "100.00", ""]);
    expect(formatRate(120)).toBe("120.00");
    expect(parseRateInput("1,250.5")).toBe(1250.5);
    expect(parsePercentInput("")).toBe(0);
    expect(parsePercentInput("-5")).toBeNull();
  });
});

describe("the rate bar", () => {
  const priced = priceDraft([line(1, "2000", "2000", "1", "0"), line(1, "1000", "1000", "1", "0")]);

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it("applies the specification when asked", () => {
    const onApply = vi.fn();
    render(
      <SpecificationRateBar
        groupNo={1}
        setting={{ base: "100", percent: "20" }}
        plan={planRates(priced, 1, "100", "20")}
        rowCount={2}
        currency="AED"
        onChange={() => {}}
        onApply={onApply}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: /apply to specification 1/i }));
    expect(onApply).toHaveBeenCalledTimes(1);
    expect(document.body.textContent?.replace(/\s+/g, " ")).toContain("1 of 2 rows reach it");
  });

  it("reports what was typed and keeps the button off until a base rate exists", () => {
    const onChange = vi.fn();
    const { rerender } = render(
      <SpecificationRateBar
        groupNo={1}
        setting={{ base: "", percent: "" }}
        plan={null}
        rowCount={2}
        currency="AED"
        onChange={onChange}
        onApply={() => {}}
      />,
    );

    expect(screen.getByRole("button", { name: /apply to specification 1/i })).toHaveProperty("disabled", true);
    fireEvent.change(screen.getByLabelText(/base rate for specification 1/i), { target: { value: "150" } });
    expect(onChange).toHaveBeenCalledWith({ base: "150" });

    rerender(
      <SpecificationRateBar
        groupNo={1}
        setting={{ base: "150", percent: "20" }}
        plan={planRates(priced, 1, "150", "20")}
        rowCount={2}
        currency="AED"
        onChange={onChange}
        onApply={() => {}}
      />,
    );
    expect(screen.getByRole("button", { name: /apply to specification 1/i })).toHaveProperty("disabled", false);
  });

  it("says where the base rate is stored, so nobody mistakes it for a scratch field", () => {
    render(
      <SpecificationRateBar
        groupNo={1}
        setting={{ base: "100", percent: "0" }}
        plan={planRates(priced, 1, "100", "0")}
        rowCount={2}
        currency="AED"
        onChange={() => {}}
        onApply={() => {}}
      />,
    );
    expect(screen.getByText(/stored with the revision when you save/i)).toBeTruthy();
  });
});

describe("reopening a PI restores the settings the revision was saved with", () => {
  const plan = null;

  it("shows the stored base rate and surcharge in the boxes", () => {
    const settings = settingsFromServer([{ group_no: 1, base_rate: 100, surcharge_percent: 20 }]);

    render(
      <SpecificationRateBar
        groupNo={1}
        setting={settings[1] ?? EMPTY_RATE_SETTING}
        plan={plan}
        rowCount={2}
        currency="AED"
        onChange={vi.fn()}
        onApply={vi.fn()}
      />,
    );

    expect((screen.getByLabelText("Base rate for specification 1") as HTMLInputElement).value).toBe("100");
    expect((screen.getByLabelText("Surcharge percent for specification 1") as HTMLInputElement).value).toBe("20");
  });

  it("leaves the boxes empty for a revision that stored nothing", () => {
    const settings = settingsFromServer([]);

    render(
      <SpecificationRateBar
        groupNo={2}
        setting={settings[2] ?? EMPTY_RATE_SETTING}
        plan={plan}
        rowCount={0}
        currency="AED"
        onChange={vi.fn()}
        onApply={vi.fn()}
      />,
    );

    expect((screen.getByLabelText("Base rate for specification 2") as HTMLInputElement).value).toBe("");
    expect((screen.getByLabelText("Surcharge percent for specification 2") as HTMLInputElement).value).toBe("");
  });

  it("no longer claims the settings are a working aid", () => {
    const settings = settingsFromServer([{ group_no: 1, base_rate: 100, surcharge_percent: 20 }]);

    render(
      <SpecificationRateBar
        groupNo={1}
        setting={settings[1] ?? EMPTY_RATE_SETTING}
        plan={plan}
        rowCount={2}
        currency="AED"
        onChange={vi.fn()}
        onApply={vi.fn()}
      />,
    );

    const notes = screen.getAllByText(/stored with the revision/i);
    expect(notes).toHaveLength(1);
    expect(screen.queryByText(/not stored/i)).toBeNull();
    expect(screen.queryByText(/working aid/i)).toBeNull();
  });
});
