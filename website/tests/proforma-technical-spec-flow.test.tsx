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
    expect(screen.getByRole("button", { name: "Export Excel" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Export CSV" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Export PDF" })).toBeTruthy();
    expect(screen.getAllByRole("button", { name: "Run quick" })).toHaveLength(2);
    expect(screen.getAllByRole("button", { name: "Optimize" })).toHaveLength(2);
    expect(screen.getByRole("heading", { name: "Spec Group 1 · DGU" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    expect(screen.getByRole("alert").textContent).toContain("positive width, height");
    fireEvent.change(screen.getByLabelText("W1 (mm)"), { target: { value: "1200" } });
    fireEvent.change(screen.getByLabelText("H1 (mm)"), { target: { value: "1800" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));

    expect(screen.getByText("06 · Fabrication details")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Panel extra / overlap"), { target: { value: "With Overlap As Per DWG" } });
    fireEvent.change(screen.getByLabelText("Spacer extra"), { target: { value: "With U-Insert As Per DWG" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));

    expect(screen.getByText("Technical review · no prices shown")).toBeTruthy();
    const specification = container.querySelector<HTMLInputElement>('input[name="pi_specification"]');
    const complete = container.querySelector<HTMLInputElement>('input[name="pi_spec_complete"]');
    expect(complete?.value).toBe("true");
    expect(specification?.value).toContain("CUSTOMER PI TECHNICAL SPECIFICATION — NO PRICING");
    expect(specification?.value).toContain("With Overlap As Per DWG");
    expect(specification?.value).toContain("With U-Insert As Per DWG");
    expect(specification?.value).toContain("1200 × 1800 mm; quantity 1");
    expect(specification?.value).not.toMatch(/AED|margin|yield|profit|rate:/i);
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
    const secondGroup = screen.getByRole("region", { name: "Step 4 specification group 2 and Step 5 cutting list" });
    fireEvent.click(within(secondGroup).getByRole("button", { name: "+ Add line" }));
    fireEvent.change(within(secondGroup).getByLabelText("W1 (mm)"), { target: { value: "900" } });
    fireEvent.change(within(secondGroup).getByLabelText("H1 (mm)"), { target: { value: "2100" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));

    const specification = container.querySelector<HTMLInputElement>('input[name="pi_specification"]')?.value || "";
    expect(specification).toContain("Spec Group 1: DGU");
    expect(specification).toContain("Spec Group 2: LAM");
    expect(specification).toContain("A2 [Spec Group 2]: 900 × 2100 mm; quantity 1");
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
    fireEvent.change(screen.getByLabelText("W1 (mm)"), { target: { value: "1200" } });
    fireEvent.change(screen.getByLabelText("H1 (mm)"), { target: { value: "1800" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    await vi.waitFor(() => expect(container.querySelector<HTMLInputElement>('input[name="pi_spec_complete"]')?.value).toBe("true"));

    fireEvent.click(screen.getByRole("button", { name: "← Back" }));
    fireEvent.click(screen.getByRole("button", { name: "← Back" }));
    fireEvent.change(screen.getByLabelText("H1 (mm)"), { target: { value: "" } });
    await vi.waitFor(() => expect(container.querySelector<HTMLInputElement>('input[name="pi_spec_complete"]')?.value).toBe("false"));
    await vi.waitFor(() => expect(onCompleteChange).toHaveBeenLastCalledWith(false));
  });
});
