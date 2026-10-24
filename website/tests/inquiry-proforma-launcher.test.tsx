// @vitest-environment jsdom
import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: unknown; children: ReactNode }) => (
    <a href={typeof href === "string" ? href : "#"} {...rest}>
      {children}
    </a>
  ),
}));

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../src/i18n/provider";
import { APP_STRINGS } from "../src/i18n/app-strings";
import { ProformaLauncher } from "../src/components/inquiries/ProformaLauncher";
import type { InquiryListRow } from "../src/types/inquiries";

afterEach(cleanup);

function row(overrides: Partial<InquiryListRow> = {}): InquiryListRow {
  return {
    id: "8f14e45f-ea0b-4c3f-9a1e-6f2f0b5c7d11",
    inquiry_number: "INQ-2026-0042",
    inquiry_reference: "Customer Inquiry",
    customer_name: "Gulf Facade LLC",
    project_name: "Marina Tower, level 12",
    salesman_name: "A. Rahman",
    prepared_by_name: "Estimating",
    status: "Sent",
    priority: "High",
    inquiry_value: 184500,
    received_at: "2026-10-01T08:00:00.000Z",
    source_updated_at: "2026-10-02T08:00:00.000Z",
    proforma_number: "",
    job_order_number: "",
    availability: "available",
    attachment_count: 2,
    published_at: "2026-10-02T08:00:00.000Z",
    proforma_draft: null,
    ...overrides,
  };
}

function wrap(node: ReactNode) {
  return render(<I18nProvider locale="en-US" overlay={APP_STRINGS["en-US"]}>{node}</I18nProvider>);
}

describe("starting a proforma from the register", () => {
  it("offers one obvious action and the customer form", () => {
    wrap(<ProformaLauncher rows={[row()]} total={1} filtered={false} />);

    expect(screen.getByRole("button", { name: "New PI" })).toBeTruthy();
    expect(screen.getByRole("link", { name: "Submit customer inquiry" }).getAttribute("href")).toBe("/inquiries/submit");
  });

  it("lists a published inquiry with a link into its workspace", () => {
    wrap(<ProformaLauncher rows={[row()]} total={1} filtered={false} />);
    fireEvent.click(screen.getByRole("button", { name: "New PI" }));

    expect(screen.getByText("INQ-2026-0042")).toBeTruthy();
    expect(screen.getByText("Gulf Facade LLC · Marina Tower, level 12")).toBeTruthy();
    const link = screen.getByRole("link", { name: "Create PI" });
    expect(link.getAttribute("href")).toBe("/inquiries/8f14e45f-ea0b-4c3f-9a1e-6f2f0b5c7d11/proforma");
  });

  it("calls an existing draft a resume, not a new one", () => {
    wrap(
      <ProformaLauncher
        rows={[row({ proforma_draft: { id: "d1", revision: 3 } })]}
        total={1}
        filtered={false}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: "New PI" }));

    expect(screen.getByRole("link", { name: "Resume PI" })).toBeTruthy();
    expect(screen.getByText("Draft revision 3")).toBeTruthy();
  });

  it("finds an inquiry by customer or project", () => {
    wrap(
      <ProformaLauncher
        rows={[row(), row({ id: "b2", inquiry_number: "INQ-2026-0043", customer_name: "Al Noor Glass", project_name: "Skylight package" })]}
        total={2}
        filtered={false}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: "New PI" }));
    fireEvent.change(screen.getByLabelText("Find an inquiry to start a proforma for"), {
      target: { value: "noor" },
    });

    expect(screen.getByText("INQ-2026-0043")).toBeTruthy();
    expect(screen.queryByText("INQ-2026-0042")).toBeNull();
  });

  it("explains an empty register without being asked", () => {
    wrap(<ProformaLauncher rows={[]} total={0} filtered={false} />);

    // No click: an empty register has to say why it is empty.
    expect(screen.getByText("This register has no published inquiries yet.")).toBeTruthy();
    expect(screen.getByText(/Publish an inquiry from the desktop/)).toBeTruthy();
    expect(screen.getByRole("link", { name: "Review customer submissions" })).toBeTruthy();
    expect(screen.getByRole("link", { name: "Submit a customer inquiry" })).toBeTruthy();
  });

  it("does not pretend the register is empty when a filter is hiding rows", () => {
    wrap(<ProformaLauncher rows={[]} total={0} filtered />);
    fireEvent.click(screen.getByRole("button", { name: "New PI" }));

    expect(screen.getByText("No inquiry matches the current filters.")).toBeTruthy();
    expect(screen.queryByText("This register has no published inquiries yet.")).toBeNull();
  });
});
