// @vitest-environment jsdom

import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

vi.mock("@/i18n/provider", () => ({
  useT: () => (value: string) => value,
}));

import { Accordion } from "@/components/ui/Accordion";

describe("Accordion disclosure motion", () => {
  it("keeps collapsed interactive content out of focus order and restores it on open", () => {
    render(
      <Accordion
        items={[
          {
            question: "Details",
            body: <a href="/details">Read details</a>,
          },
        ]}
      />,
    );

    const trigger = screen.getByRole("button", { name: "Details" });
    const panel = document.getElementById(trigger.getAttribute("aria-controls") ?? "");
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    expect(panel?.getAttribute("aria-hidden")).toBe("true");
    expect(panel?.hasAttribute("inert")).toBe(true);

    fireEvent.click(trigger);
    expect(trigger.getAttribute("aria-expanded")).toBe("true");
    expect(panel?.getAttribute("aria-hidden")).toBe("false");
    expect(panel?.hasAttribute("inert")).toBe(false);
  });
});
