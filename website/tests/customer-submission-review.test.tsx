// @vitest-environment jsdom
import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: unknown; children: ReactNode }) => (
    <a href={typeof href === "string" ? href : "#"} {...rest}>
      {children}
    </a>
  ),
}));

vi.mock("../src/lib/useOperatorSession", () => ({
  useOperatorSession: () => ({
    checking: false,
    userEmail: "operator@example.com",
    authToken: "operator-token",
    isAuthRequired: false,
    unavailable: false,
    error: null,
    refreshSession: async () => null,
    signOut: async () => {},
    requireAuth: () => {},
  }),
}));

import { I18nProvider } from "../src/i18n/provider";
import { APP_STRINGS } from "../src/i18n/app-strings";
import { CustomerSubmissionReview } from "../src/components/inquiries/CustomerSubmissionReview";
import type { CustomerInquiryStatus, CustomerInquirySubmission } from "../src/types/inquiry-submissions";

const ID = "00000000-0000-4000-8000-000000000003";

function submission(status: CustomerInquiryStatus, overrides: Partial<CustomerInquirySubmission> = {}): CustomerInquirySubmission {
  return {
    id: ID,
    submission_reference: "WEB-2026-000003",
    contact_name: "Ayesha Khan",
    company_name: "Gulf Facade LLC",
    phone: "+971 50 000 0000",
    customer_reference: "GF-118",
    attention_name: "",
    vat_trn_number: "",
    salesman_reference: "",
    handover_estimator_name: "",
    project_id: null,
    project_name: "Marina Tower, level 12",
    project_location: "Dubai Marina",
    inquiry_type: "quotation",
    description: "Supply and install 22.14 m2 of 12 mm toughened glass.",
    status,
    submitted_at: "2026-10-16T14:02:11.000Z",
    status_updated_at: "2026-10-17T09:30:00.000Z",
    accepted_at: null,
    downloaded_at: status === "downloaded" ? "2026-10-17T10:00:00.000Z" : null,
    closed_at: null,
    estimated_response_at: null,
    operator_note: "",
    pipeline_status: null,
    pipeline_note: null,
    pipeline_updated_at: null,
    pipeline_updated_by: null,
    documents: [
      {
        id: "11111111-1111-4111-8111-111111111111",
        original_filename: "marina-tower-schedule.pdf",
        extension: "pdf",
        detected_mime: "application/pdf",
        byte_size: 240_000,
        content_sha256: "a".repeat(64),
        uploaded_at: "2026-10-16T14:01:00.000Z",
      },
    ],
    ...overrides,
  };
}

type Call = { url: string; method: string; body: unknown };

function mockFetch(item: CustomerInquirySubmission) {
  const calls: Call[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? "GET";
      const body = init?.body ? JSON.parse(String(init.body)) : null;
      calls.push({ url, method, body });
      const data = method === "PATCH" ? submission(item.status, { ...item, status: body?.status ?? item.status }) : item;
      return { ok: true, status: 200, json: async () => ({ ok: true, data }) } as unknown as Response;
    }),
  );
  return calls;
}

function renderReview() {
  return render(
    <I18nProvider locale="en-US" overlay={APP_STRINGS["en-US"]}>
      <CustomerSubmissionReview id={ID} />
    </I18nProvider>,
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("a submission that was already delivered to the desktop", () => {
  it("shows the downloaded status and does not pretend it can be re-saved as is", async () => {
    mockFetch(submission("downloaded"));
    renderReview();

    await screen.findByText("marina-tower-schedule.pdf");
    const badges = Array.from(document.querySelectorAll(".badge-pill")).map((badge) => badge.textContent);
    expect(badges).toContain("Downloaded");

    const select = screen.getByLabelText("Review status") as HTMLSelectElement;
    expect(select.value).toBe("");
    // Nothing is auto-accepted and nothing is submitted until the operator says so.
    expect((screen.getByRole("button", { name: /save reviewed status/i }) as HTMLButtonElement).disabled).toBe(true);
    expect(document.body.textContent).toContain("already handed to the desktop");
    expect(document.body.textContent).toContain("Choose a review status");
    // The current state is listed, disabled, so it is never mistaken for a choice.
    const current = Array.from(select.options).find((option) => option.value === "downloaded");
    expect(current?.disabled).toBe(true);
  });

  it("sends exactly the status the operator chose, and only when they save", async () => {
    const calls = mockFetch(submission("downloaded"));
    renderReview();
    await screen.findByText("marina-tower-schedule.pdf");

    expect(calls.filter((call) => call.method === "PATCH")).toHaveLength(0);

    fireEvent.change(screen.getByLabelText("Review status"), { target: { value: "accepted" } });
    fireEvent.click(screen.getByRole("button", { name: /save reviewed status/i }));

    await waitFor(() => {
      expect(calls.filter((call) => call.method === "PATCH")).toHaveLength(1);
    });
    const patch = calls.find((call) => call.method === "PATCH");
    expect(patch?.url).toBe(`/api/inquiry-submissions/operator/${ID}`);
    expect(patch?.body).toMatchObject({ status: "accepted", operatorNote: "" });
    await screen.findByText("Review saved and audited.");
  });

  it("offers the documents for download without changing the review", async () => {
    const calls = mockFetch(submission("downloaded"));
    renderReview();
    await screen.findByText("marina-tower-schedule.pdf");

    expect(document.body.textContent).toContain("235 KB");
    expect(screen.getByRole("button", { name: /^download$/i })).toBeTruthy();
    expect(calls.filter((call) => call.method === "PATCH")).toHaveLength(0);
    expect(calls[0]?.url).toBe(`/api/inquiry-submissions/operator/${ID}`);
  });
});

describe("the status the review may write", () => {
  it("opens a submitted submission on Under review and says that saving moves it", async () => {
    mockFetch(submission("submitted"));
    renderReview();
    await screen.findByText("marina-tower-schedule.pdf");

    const select = screen.getByLabelText("Review status") as HTMLSelectElement;
    expect(select.value).toBe("under_review");
    expect(document.body.textContent).toContain("moves the submission from Submitted to Under review");
  });

  it("locks a rejected review instead of offering to reopen it", async () => {
    mockFetch(submission("rejected"));
    renderReview();
    await screen.findByText("marina-tower-schedule.pdf");

    const select = screen.getByLabelText("Review status") as HTMLSelectElement;
    expect(select.disabled).toBe(true);
    expect(select.value).toBe("rejected");
    expect(Array.from(select.options).map((option) => option.value)).toEqual(["rejected"]);
    expect(document.body.textContent).toContain("cannot be reopened");
  });
});
