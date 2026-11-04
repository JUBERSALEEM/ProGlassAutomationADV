// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ProformaTechnicalSpecFlow } from "@/components/inquiries/ProformaTechnicalSpecFlow";

afterEach(cleanup);

function completeActiveSpecStock() {
  screen.getAllByLabelText("Stock sheet W (mm)").forEach((input) => fireEvent.change(input, { target: { value: "3210" } }));
  screen.getAllByLabelText("Stock sheet H (mm)").forEach((input) => fireEvent.change(input, { target: { value: "2250" } }));
}

describe("customer PI technical specification flow", () => {
  it("collects WPF Steps 4–7 as technical-only data and validates a cutting-list line", () => {
    const { container } = render(<ProformaTechnicalSpecFlow />);

    expect(screen.getByText("04 · Glass specification")).toBeTruthy();
    expect(screen.getAllByLabelText("Thickness")).toHaveLength(2);
    expect(screen.getAllByLabelText("Treatment")).toHaveLength(2);
    expect(screen.getByLabelText("Spacer width")).toBeTruthy();
    expect(container.textContent).not.toMatch(/AED|Margin \(%\)|Profit|Rate \(\/ sqm\)/i);
    completeActiveSpecStock();

    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    expect(screen.getByText("05 · Cutting list")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Import CSV / Excel" })).toBeTruthy();
    for (const label of ["Export Excel", "Export CSV", "Export PDF", "Share technical PDF", "QUICK layout", "OPTIMIZE layouts"]) {
      expect(screen.queryByRole("button", { name: label })).toBeNull();
    }
    expect(screen.getByRole("button", { name: "Import CSV / Excel" })).toBeTruthy();
    expect(screen.getByRole("table", { name: "Editable cutting list for Spec Group 1" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Spec Group 1 · DGU" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    expect(screen.getByRole("alert").textContent).toContain("positive width, height");
    fireEvent.change(screen.getByLabelText("Row 1, Width 1 in millimetres"), { target: { value: "1200" } });
    fireEvent.change(screen.getByLabelText("Row 1, Height 1 in millimetres"), { target: { value: "1800" } });
    fireEvent.change(screen.getByLabelText("Row 1, Quantity"), { target: { value: "2" } });
    expect(screen.getByLabelText("Row 1, SQM").textContent).toBe("2.160");
    expect(screen.getByLabelText("Row 1, Total SQM").textContent).toBe("4.320");
    expect(screen.getByLabelText("Spec Group 1 total SQM").textContent).toBe("4.320");
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));

    expect(screen.getByText("06 · Fabrication details")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Panel extra / overlap"), { target: { value: "With Overlap As Per DWG" } });
    fireEvent.change(screen.getByLabelText("Spacer extra"), { target: { value: "With U-Insert As Per DWG" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));

    expect(screen.getByText("Review technical request · no prices shown")).toBeTruthy();
    const paper = screen.getByRole("article", { name: "Technical specification review preview" });
    expect(paper.textContent).toContain("Suggested processing:");
    expect(paper.textContent).toContain("U-Insert fitting");
    expect(paper.textContent).toContain("DGU secondary silicone bed");
    expect(paper.textContent).toContain("Total SQM");
    expect(paper.textContent).toContain("4.320");
    expect(screen.getByLabelText(/I have reviewed this technical request/)).toBeTruthy();
    expect(container.querySelector<HTMLInputElement>('input[name="pi_spec_complete"]')?.value).toBe("false");
    fireEvent.click(screen.getByLabelText(/I have reviewed this technical request/));
    const specification = container.querySelector<HTMLInputElement>('input[name="pi_specification_json"]');
    const complete = container.querySelector<HTMLInputElement>('input[name="pi_spec_complete"]');
    expect(complete?.value).toBe("true");
    const payload = JSON.parse(specification?.value || "{}") as { specs: Array<{ panelExtra: string; spacerExtra: string }>; rows: Array<{ width1: string; height1: string; quantity: string }> };
    expect(payload.specs[0]?.panelExtra).toBe("With Overlap As Per DWG");
    expect(payload.specs[0]?.spacerExtra).toBe("With U-Insert As Per DWG");
    expect(payload.rows[0]).toMatchObject({ width1: "1200", height1: "1800", quantity: "2" });
    expect(JSON.stringify(payload)).not.toMatch(/AED|margin|yield|profit|rate:/i);
  });

  it("calculates WPF SQM with the 0.5 minimum and derives total SQM from quantity", () => {
    render(<ProformaTechnicalSpecFlow />);
    completeActiveSpecStock();
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    fireEvent.change(screen.getByLabelText("Row 1, Width 1 in millimetres"), { target: { value: "400" } });
    fireEvent.change(screen.getByLabelText("Row 1, Height 1 in millimetres"), { target: { value: "300" } });
    fireEvent.change(screen.getByLabelText("Row 1, Quantity"), { target: { value: "2" } });
    expect(screen.getByLabelText("Row 1, SQM").textContent).toBe("0.500");
    expect(screen.getByLabelText("Row 1, Total SQM").textContent).toBe("1.000");
    expect(screen.getByLabelText("Spec Group 1 total SQM").textContent).toBe("1.000");
  });

  it("keeps the Step 4 construction type above its own Step 5 list and allows grouped editing", () => {
    const { container } = render(<ProformaTechnicalSpecFlow />);
    fireEvent.click(screen.getByRole("button", { name: "+ Add specification" }));
    fireEvent.change(screen.getByLabelText("Glass construction"), { target: { value: "LAM" } });
    expect(screen.getAllByLabelText("Thickness")).toHaveLength(2);
    expect(screen.getByLabelText("Interlayer type")).toBeTruthy();
    expect(screen.getByLabelText("Interlayer thickness")).toBeTruthy();
    expect(screen.getByLabelText("Interlayer colour")).toBeTruthy();
    expect(screen.queryByLabelText("Spacer width")).toBeNull();
    completeActiveSpecStock();
    fireEvent.click(screen.getByRole("button", { name: "Spec Group 1 · DGU" }));
    completeActiveSpecStock();

    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    const secondGroup = screen.getByRole("region", { name: "Cutting list for Spec Group 2" });
    const firstGroup = screen.getByRole("region", { name: "Cutting list for Spec Group 1" });
    fireEvent.change(within(firstGroup).getByLabelText("Row 1, Width 1 in millimetres"), { target: { value: "1000" } });
    fireEvent.change(within(firstGroup).getByLabelText("Row 1, Height 1 in millimetres"), { target: { value: "1500" } });
    fireEvent.click(within(secondGroup).getByRole("button", { name: "+ Add row" }));
    fireEvent.change(within(secondGroup).getByLabelText("Row 2, Width 1 in millimetres"), { target: { value: "900" } });
    fireEvent.change(within(secondGroup).getByLabelText("Row 2, Height 1 in millimetres"), { target: { value: "2100" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    fireEvent.click(screen.getByLabelText(/I have reviewed this technical request/));

    const specification = JSON.parse(container.querySelector<HTMLInputElement>('input[name="pi_specification_json"]')?.value || "{}") as { specs: Array<{ kind: string }>; rows: Array<{ reference: string; specId: string; width1: string; height1: string; quantity: string }> };
    expect(specification.specs.map((spec) => spec.kind)).toEqual(["DGU", "LAM"]);
    expect(specification.rows).toContainEqual(expect.objectContaining({ reference: "A2", specId: "spec-2", width1: "900", height1: "2100", quantity: "1" }));
  });

  it("pastes spreadsheet rows into the aligned grid and generates rows in bulk", () => {
    render(<ProformaTechnicalSpecFlow />);
    completeActiveSpecStock();
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    const reference = screen.getByLabelText("Row 1, Glass reference");
    fireEvent.paste(reference, { clipboardData: { getData: () => "A1\t1200\t1800\t\t\t2\nA2\t800\t900\t\t\t1" } });
    expect((screen.getByLabelText("Row 1, Glass reference") as HTMLInputElement).value).toBe("A1");
    expect((screen.getByLabelText("Row 2, Width 1 in millimetres") as HTMLInputElement).value).toBe("800");
    fireEvent.change(screen.getByLabelText("Generate rows"), { target: { value: "2" } });
    fireEvent.click(screen.getByRole("button", { name: "Add generated rows" }));
    expect(screen.getByLabelText("Row 4, Quantity")).toBeTruthy();
    expect(screen.getByText(/4 \/ 120,000 total rows/)).toBeTruthy();
  });

  it("requires valid stock-sheet dimensions for every active pane before advancing from Step 4", () => {
    render(<ProformaTechnicalSpecFlow />);
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    expect(screen.getByText(/valid stock sheet width and height for every pane/i)).toBeTruthy();
    expect(screen.getByText("04 · Glass specification")).toBeTruthy();
    completeActiveSpecStock();
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    expect(screen.getByText("05 · Cutting list")).toBeTruthy();
  });

  it("only marks the PI specification complete at review when the cutting list is valid", async () => {
    const onCompleteChange = vi.fn();
    const { container } = render(<ProformaTechnicalSpecFlow onCompleteChange={onCompleteChange} />);
    completeActiveSpecStock();
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    fireEvent.change(screen.getByLabelText("Row 1, Width 1 in millimetres"), { target: { value: "1200" } });
    fireEvent.change(screen.getByLabelText("Row 1, Height 1 in millimetres"), { target: { value: "1800" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    fireEvent.click(screen.getByLabelText(/I have reviewed this technical request/));
    await vi.waitFor(() => expect(container.querySelector<HTMLInputElement>('input[name="pi_spec_complete"]')?.value).toBe("true"));

    fireEvent.click(screen.getByRole("button", { name: "← Back" }));
    fireEvent.click(screen.getByRole("button", { name: "← Back" }));
    fireEvent.change(screen.getByLabelText("Row 1, Height 1 in millimetres"), { target: { value: "" } });
    await vi.waitFor(() => expect(container.querySelector<HTMLInputElement>('input[name="pi_spec_complete"]')?.value).toBe("false"));
    await vi.waitFor(() => expect(onCompleteChange).toHaveBeenLastCalledWith(false));
  });
});
