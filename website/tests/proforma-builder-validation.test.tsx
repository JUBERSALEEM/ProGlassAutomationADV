/** @vitest-environment jsdom */
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ProformaBuilder } from "../src/components/inquiries/proforma/ProformaBuilder";
import { ProformaSpecification } from "../src/components/inquiries/proforma/ProformaSpecification";
import { draftIssues, emptyLine, priceDraft } from "../src/lib/proforma-draft";
import { defaultStep4Build } from "../src/lib/proforma-workflow";

afterEach(cleanup);

describe("WPF Step 4 builder and Step 5 cut-list handoff", () => {
  it("generates a DGU specification and applies its estimate to the selected Step 5 group", () => {
    const onApply = vi.fn();
    render(<ProformaBuilder groupNo={2} build={defaultStep4Build("DGU")} onBuildChange={vi.fn()} onApply={onApply} currency="AED" saved={{ revision: 0, groups: [] }} onReuse={vi.fn()} onReplace={vi.fn()} />);
    expect(screen.getByText(/6mm Clear FT Glass \+ 12mm ASP \+ 6mm Clear FT Glass/)).toBeTruthy();
    expect(screen.getByText(/AED/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Apply Step 4 specification and estimate to Step 5 group 2" }));
    expect(onApply).toHaveBeenCalledWith(2, expect.objectContaining({ code: "DGU" }));
  });

  it("keeps Step 5 dimensions and quantities within WPF limits instead of truncating or clamping", () => {
    const line = { ...emptyLine(1), w1: "1000", h1: "1000", qty: "1", rate: "50" };
    const onEdit = vi.fn();
    render(<ProformaSpecification priced={priceDraft([line])} groups={[1]} perGroup={[]} activeGroup={1} currency="AED" totals={{ lines: 1, sqm: 0.5, value: 25 }} rateSettings={{}} onRateChange={vi.fn()} onApplyRates={vi.fn()} onEdit={onEdit} onAddLine={vi.fn()} onRemoveLine={vi.fn()} onDuplicateLine={vi.fn()} onMoveLine={vi.fn()} onAddGroup={vi.fn()} onRemoveGroup={vi.fn()} onFocusGroup={vi.fn()} />);
    fireEvent.change(screen.getByLabelText("Width for line 1"), { target: { value: "10001" } });
    expect(onEdit).toHaveBeenCalledWith(line.key, { w1: "10001" });
    expect(draftIssues([{ ...line, w1: "10001" }]).join(" ")).toContain("primary panel side cannot exceed 10000 mm");
    fireEvent.change(screen.getByLabelText("Quantity for line 1"), { target: { value: "10001" } });
    expect(onEdit).toHaveBeenCalledWith(line.key, { qty: "10001" });
    expect(draftIssues([{ ...line, qty: "10001" }]).join(" ")).toContain("quantity cannot exceed 10000");
  });
});
