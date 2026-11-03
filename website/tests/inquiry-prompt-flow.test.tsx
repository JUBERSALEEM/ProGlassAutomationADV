// @vitest-environment jsdom

import { createRef } from "react";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

afterEach(cleanup);
import { InquiryPromptFlow } from "@/components/inquiries/InquiryPromptFlow";

describe("guided inquiry prompt flow", () => {
  const hints = [
    { group: "Customer + project", title: "Company name", body: "Enter the company name.", example: "Al Noor Glass LLC" },
    { group: "Customer + project", title: "Contact name", body: "Who should we contact?", example: "Aisha Khan" },
    { group: "Request details", title: "Project requirements", body: "Describe the project." },
  ];

  it("keeps all answers in one form while advancing and returning with the arrow controls", async () => {
    const formRef = createRef<HTMLFormElement>();
    render(
      <form ref={formRef}>
        <InquiryPromptFlow hints={hints}>
          <label>Company name<input name="company_name" required defaultValue="Al Noor Glass LLC" /></label>
          <label>Contact name<input name="contact_name" required /></label>
          <label>Project requirements<textarea name="description" required minLength={10} /></label>
        </InquiryPromptFlow>
      </form>,
    );

    expect(screen.getByText("Al Noor Glass LLC")).toBeTruthy();
    expect(screen.getByText("1")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Next question" }));
    await waitFor(() => expect(screen.getByLabelText("Contact name")).toBe(document.activeElement));
    expect(screen.getByText("Who should we contact?")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Previous question" }));
    await waitFor(() => expect(screen.getByLabelText("Company name")).toBe(document.activeElement));
    fireEvent.click(screen.getByRole("button", { name: "Next question" }));
    fireEvent.change(screen.getByLabelText("Contact name"), { target: { value: "Aisha Khan" } });
    fireEvent.keyDown(screen.getByLabelText("Contact name"), { key: "Enter" });

    await waitFor(() => expect(screen.getByLabelText("Project requirements")).toBe(document.activeElement));
    expect(new FormData(formRef.current!).get("company_name")).toBe("Al Noor Glass LLC");
    expect(new FormData(formRef.current!).get("contact_name")).toBe("Aisha Khan");
  });

  it("does not advance past an empty required prompt and shows contextual examples", () => {
    render(
      <form>
        <InquiryPromptFlow hints={hints}>
          <label>Company name<input name="company_name" required /></label>
          <label>Contact name<input name="contact_name" required /></label>
          <label>Project requirements<textarea name="description" required minLength={10} /></label>
        </InquiryPromptFlow>
      </form>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Next question" }));
    expect(screen.getByLabelText("Company name").closest("[data-intake-prompt]")?.getAttribute("hidden")).toBe(null);
    expect(screen.getByText("Al Noor Glass LLC")).toBeTruthy();
  });
});
