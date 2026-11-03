// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

afterEach(cleanup);
import { ProformaTechnicalSpecFlow } from "@/components/inquiries/ProformaTechnicalSpecFlow";

describe("customer PI technical specification flow", () => {
  it("collects WPF Steps 4–7 as technical-only data and validates a cutting-list line", () => {
    const { container } = render(<ProformaTechnicalSpecFlow />);

    expect(screen.getByText("04 · Glass specification")).toBeTruthy();
    expect(screen.getAllByLabelText("Thickness")).toHaveLength(2);
    expect(screen.getAllByLabelText("Treatment")).toHaveLength(2);
    expect(screen.getByLabelText("Spacer width")).toBeTruthy();
    expect(container.textContent).not.toMatch(/AED|Margin \(%\)|Profit|Rate \(\/ sqm\)/i);

    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    expect(screen.getByText("05 · Cutting list")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    expect(screen.getByRole("alert").textContent).toContain("positive width, height and quantity");
    fireEvent.change(screen.getByLabelText("W1 (mm)"), { target: { value: "1200" } });
    fireEvent.change(screen.getByLabelText("H1 (mm)"), { target: { value: "1800" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));

    expect(screen.getByText("06 · Fabrication details")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Panel extra / overlap"), { target: { value: "With Overlap As Per DWG" } });
    fireEvent.change(screen.getByLabelText("Spacer extra"), { target: { value: "With U-Insert As Per DWG" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));

    expect(screen.getByText("Technical review · no prices shown")).toBeTruthy();
    expect(screen.queryByText(/WPF Step 5 cutting-list fields/)).toBeNull();
    const specification = container.querySelector<HTMLInputElement>('input[name="pi_specification"]');
    const complete = container.querySelector<HTMLInputElement>('input[name="pi_spec_complete"]');
    expect(complete?.value).toBe("true");
    expect(specification?.value).toContain("CUSTOMER PI TECHNICAL SPECIFICATION — NO PRICING");
    expect(specification?.value).toContain("With Overlap As Per DWG");
    expect(specification?.value).toContain("With U-Insert As Per DWG");
    expect(specification?.value).toContain("1200 × 1800 mm; quantity 1");
    expect(specification?.value).not.toMatch(/AED|margin|yield|profit|rate:/i);
  });

  it("supports multiple WPF-style spec groups and assigns grid lines to a group", () => {
    const { container } = render(<ProformaTechnicalSpecFlow />);
    fireEvent.click(screen.getByRole("button", { name: "+ Add glass specification" }));
    fireEvent.change(screen.getByLabelText("Glass construction"), { target: { value: "LAM" } });
    expect(screen.getAllByLabelText("Thickness")).toHaveLength(2);
    expect(screen.getByLabelText("Interlayer type")).toBeTruthy();
    expect(screen.getByLabelText("Interlayer thickness")).toBeTruthy();
    expect(screen.getByLabelText("Interlayer colour")).toBeTruthy();
    expect(screen.queryByLabelText("Spacer width")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    fireEvent.change(screen.getByLabelText("Spec group"), { target: { value: "spec-2" } });
    fireEvent.change(screen.getByLabelText("W1 (mm)"), { target: { value: "900" } });
    fireEvent.change(screen.getByLabelText("H1 (mm)"), { target: { value: "2100" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));
    fireEvent.click(screen.getByRole("button", { name: "Continue →" }));

    const specification = container.querySelector<HTMLInputElement>('input[name="pi_specification"]')?.value || "";
    expect(specification).toContain("Spec Group 1: DGU");
    expect(specification).toContain("Spec Group 2: LAM");
    expect(specification).toContain("A1 [Spec Group 2]: 900 × 2100 mm; quantity 1");
  });
});
