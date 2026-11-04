/** @vitest-environment jsdom */
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

afterEach(cleanup);
import { ProformaBuilder } from "../src/components/inquiries/proforma/ProformaBuilder";

function mount(onAddLine = vi.fn()) {
  render(
    <ProformaBuilder
      activeGroup={1}
      groups={[1]}
      currency="AED"
      saved={{ revision: 0, groups: [] }}
      issues={[]}
      onAssign={vi.fn()}
      onAddLine={onAddLine}
      onReuse={vi.fn()}
      onReplace={vi.fn()}
    />,
  );
  return onAddLine;
}

describe("staff proforma builder WPF row limits", () => {
  it("blocks a primary panel side above SpecGroupService.ValidateRow's 10000 mm limit", () => {
    const onAddLine = mount();
    fireEvent.change(screen.getByLabelText("Width"), { target: { value: "10001" } });
    fireEvent.change(screen.getByLabelText("Height"), { target: { value: "1000" } });
    fireEvent.click(screen.getByRole("button", { name: "Add line to specification 1" }));
    expect(screen.getByRole("alert").textContent).toContain("cannot exceed 10000 mm");
    expect(onAddLine).not.toHaveBeenCalled();
  });

  it("blocks a quantity above SpecGroupService.ValidateRow's 10000 limit", () => {
    const onAddLine = mount();
    fireEvent.change(screen.getByLabelText("Width"), { target: { value: "1000" } });
    fireEvent.change(screen.getByLabelText("Height"), { target: { value: "1000" } });
    fireEvent.change(screen.getByLabelText("Quantity"), { target: { value: "10001" } });
    fireEvent.click(screen.getByRole("button", { name: "Add line to specification 1" }));
    expect(screen.getByRole("alert").textContent).toContain("quantity cannot exceed 10000");
    expect(onAddLine).not.toHaveBeenCalled();
  });
});
