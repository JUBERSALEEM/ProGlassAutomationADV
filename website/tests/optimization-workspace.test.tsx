// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { OptimizationWorkspace } from "@/components/optimization/OptimizationWorkspace";

afterEach(cleanup);

describe("localized optimization workspace walkthrough", () => {
  it("exposes the eight WPF workflow tabs in source order", () => {
    render(<OptimizationWorkspace />);

    const tabs = within(screen.getByRole("tablist", { name: "Optimization workflow tabs" })).getAllByRole("tab");
    expect(tabs.map((tab) => tab.textContent?.replace(/^\d{2}/, "").trim())).toEqual([
      "Jobs",
      "Sizes",
      "Cut List",
      "Stocks",
      "Settings",
      "Summary",
      "2D View",
      "Report",
    ]);
  });

  it("labels the browser packer as a sample and distinguishes unavailable machine formats", () => {
    render(<OptimizationWorkspace />);

    expect(screen.getByText(/separate illustrative packing heuristic/i)).toBeTruthy();
    fireEvent.click(screen.getByRole("tab", { name: /08 Report/ }));
    expect(screen.getByText(/Not implemented in the inspected WPF view/i)).toBeTruthy();
    expect(screen.getByText(/Bottero \.OPT/)).toBeTruthy();
    expect(screen.getByText(/Lisec \.DAT/)).toBeTruthy();
    expect(screen.getByText(/Intermac \.ISO/)).toBeTruthy();
  });

  it("runs the sample action into the 2D preview tab", () => {
    render(<OptimizationWorkspace />);

    fireEvent.click(screen.getByRole("button", { name: "Optimize" }));
    expect(screen.getByRole("tab", { name: /07 2D View/ }).getAttribute("aria-selected")).toBe("true");
    expect(screen.getByRole("img", { name: /Sheet 1 layout preview/ })).toBeTruthy();
  });
});
