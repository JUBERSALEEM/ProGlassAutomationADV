// @vitest-environment jsdom

import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Field } from "@/components/ui/Field";
import { Feedback } from "@/components/ui/Feedback";
import { Progress, Skeleton, Spinner } from "@/components/ui/Loading";

describe("approved field contract", () => {
  it("connects label, hint, and error semantics to the native control", () => {
    render(
      <Field
        label="Email"
        hint="Used for your reply"
        message="Enter a valid address"
        state="error"
        control={<input type="email" />}
      />,
    );
    const input = screen.getByRole("textbox", { name: "Email" });
    expect(input.getAttribute("aria-invalid")).toBe("true");
    const described = input.getAttribute("aria-describedby") ?? "";
    expect(described).toContain(screen.getByText("Used for your reply").id);
    expect(described).toContain(screen.getByRole("alert").id);
  });
});

describe("feedback and loading contracts", () => {
  it("announces errors assertively without color-only meaning", () => {
    render(<Feedback tone="error" title="Could not save" description="Try again." />);
    expect(screen.getByText("Could not save").closest("section")?.getAttribute("aria-live")).toBe("assertive");
    expect(screen.getByText("Try again.")).toBeTruthy();
  });

  it("uses native progress semantics and clamps values", () => {
    render(<Progress label="Uploading" value={130} />);
    const progress = screen.getByRole("progressbar", { name: "Uploading" });
    expect(progress.getAttribute("value")).toBe("100");
  });

  it("keeps skeleton decorative and spinner named", () => {
    const { container } = render(<><Skeleton /><Spinner label="Loading records" /></>);
    expect(container.querySelector(".skeleton")?.getAttribute("aria-hidden")).toBe("true");
    expect(screen.getByRole("status").textContent).toContain("Loading records");
  });
});
