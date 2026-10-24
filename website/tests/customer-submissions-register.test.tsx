// @vitest-environment jsdom
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: unknown; children: ReactNode }) => (
    <a href={typeof href === "string" ? href : "#"} {...rest}>
      {children}
    </a>
  ),
}));

const session = {
  checking: false,
  userEmail: "operator@example.com",
  authToken: "operator-token",
  isAuthRequired: false,
  unavailable: false,
  error: null,
  refreshSession: async () => null,
  signOut: async () => {},
  requireAuth: () => {},
};

vi.mock("../src/lib/useOperatorSession", () => ({
  useOperatorSession: () => session,
}));

import { I18nProvider } from "../src/i18n/provider";
import { APP_STRINGS } from "../src/i18n/app-strings";
import { CustomerSubmissionRegister } from "../src/components/inquiries/CustomerSubmissionRegister";
import type {
  CustomerInquiryListItem,
  CustomerInquiryPage,
  CustomerInquiryStatus,
  CustomerInquiryStatusCounts,
} from "../src/types/inquiry-submissions";

const ZERO: CustomerInquiryStatusCounts = { submitted: 0, under_review: 0, accepted: 0, downloaded: 0, rejected: 0, closed: 0 };

function row(seq: number, status: CustomerInquiryStatus, reference = `WEB-2026-${String(seq).padStart(6, "0")}`): CustomerInquiryListItem {
  const index = seq;
  return {
    id: `00000000-0000-4000-8000-${String(seq).padStart(12, "0")}`,
    submission_reference: reference,
    contact_name: `Contact ${index}`,
    company_name: `Company ${index}`,
    customer_reference: "",
    attention_name: "",
    vat_trn_number: "",
    salesman_reference: "",
    handover_estimator_name: "",
    project_id: null,
    project_name: `Project ${index}`,
    project_location: "Sharjah",
    inquiry_type: "quotation",
    status,
    submitted_at: `2026-10-1${(index % 9) + 1}T07:14:21.000Z`,
    status_updated_at: `2026-10-1${(index % 9) + 1}T07:14:21.000Z`,
    accepted_at: null,
    downloaded_at: status === "downloaded" ? `2026-10-1${(index % 9) + 1}T09:00:00.000Z` : null,
    closed_at: null,
    estimated_response_at: null,
    operator_note: "",
    pipeline_status: null,
    pipeline_note: null,
    pipeline_updated_at: null,
    pipeline_updated_by: null,
    document_count: (index % 13) + 1,
  };
}

const ROWS = [
  row(1, "submitted"),
  row(2, "submitted"),
  row(3, "downloaded"),
  row(4, "submitted"),
  row(5, "submitted"),
  row(6, "under_review"),
  row(7, "downloaded"),
];

const COUNTS: CustomerInquiryStatusCounts = { ...ZERO, submitted: 4, under_review: 1, downloaded: 2 };

function pageOf(rows: CustomerInquiryListItem[], overrides: Partial<CustomerInquiryPage> = {}): CustomerInquiryPage {
  return {
    rows,
    total: rows.length,
    page: 1,
    pageSize: 25,
    pageCount: rows.length === 0 ? 0 : 1,
    counts: COUNTS,
    allTotal: 7,
    ...overrides,
  };
}

function respondWith(handler: (url: URL) => CustomerInquiryPage) {
  const calls: URL[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = new URL(String(input), "http://localhost");
    calls.push(url);
    return {
      ok: true,
      status: 200,
      json: async () => ({ ok: true, data: handler(url) }),
    } as unknown as Response;
  });
  vi.stubGlobal("fetch", fetchMock);
  return calls;
}

function renderRegister(props: { initialStatus?: string; initialSearch?: string; initialPage?: number } = {}) {
  return render(
    <I18nProvider locale="en-US" overlay={APP_STRINGS["en-US"]}>
      <CustomerSubmissionRegister {...props} />
    </I18nProvider>,
  );
}

beforeEach(() => {
  vi.unstubAllGlobals();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("the website submission register", () => {
  it("shows all seven complete submissions with their status", async () => {
    respondWith(() => pageOf(ROWS));
    renderRegister();

    for (const item of ROWS) {
      expect(await screen.findByText(item.submission_reference)).toBeTruthy();
    }
    // The tally is the server's, so the page can prove how many rows exist.
    expect(document.body.textContent).toContain("4 submitted");
    expect(document.body.textContent).toContain("1 under review");
    expect(document.body.textContent).toContain("2 downloaded");
    // The two downloaded rows are visible, not filtered out of existence.
    const badges = Array.from(document.querySelectorAll(".badge-pill")).filter((badge) => badge.textContent === "Downloaded");
    expect(badges.length).toBe(2);
    // And the page says what it is showing.
    expect(document.body.textContent).toContain("Showing");
    expect(document.body.textContent).toContain("of");
  });

  it("asks the server for a status instead of narrowing rows in the browser", async () => {
    const calls = respondWith((url) => {
      const status = url.searchParams.get("status");
      if (status === "under_review") {
        return pageOf([row(6, "under_review")], {
          counts: { ...COUNTS },
          total: 1,
          allTotal: 7,
        });
      }
      return pageOf(ROWS);
    });
    renderRegister();
    await screen.findByText("WEB-2026-000001");

    fireEvent.click(screen.getByRole("button", { name: /under review/i }));

    await waitFor(() => {
      expect(calls.some((url) => url.searchParams.get("status") === "under_review")).toBe(true);
    });
    await waitFor(() => {
      expect(screen.queryByText("WEB-2026-000001")).toBeNull();
    });
    expect(screen.getByText("WEB-2026-000006")).toBeTruthy();
    const last = calls[calls.length - 1];
    expect(last?.searchParams.get("status")).toBe("under_review");
    expect(last?.searchParams.get("page")).toBe("1");
  });

  it("pages through the queue and reports the range from the server count", async () => {
    const firstPage = Array.from({ length: 25 }, (_, index) => row(100 + index, "submitted", `WEB-2026-P1-${String(index + 1).padStart(4, "0")}`));
    const secondPage = [row(201, "submitted", "WEB-2026-P2-0001"), row(202, "submitted", "WEB-2026-P2-0002")];
    const calls = respondWith((url) => {
      const page = Number(url.searchParams.get("page") ?? "1");
      return page === 2
        ? pageOf(secondPage, { page: 2, total: 27, pageCount: 2, counts: { ...COUNTS, submitted: 27 }, allTotal: 27 })
        : pageOf(firstPage, { page: 1, total: 27, pageCount: 2, counts: { ...COUNTS, submitted: 27 }, allTotal: 27 });
    });
    renderRegister();
    await screen.findByText("WEB-2026-P1-0001");
    expect(document.body.textContent).toContain("Showing 1 to 25 of 27");

    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));

    await screen.findByText("WEB-2026-P2-0001");
    const last = calls[calls.length - 1];
    expect(last?.searchParams.get("page")).toBe("2");
    expect(document.body.textContent).toContain("Showing 26 to 27 of 27");
  });

  it("opens on the status a link asked for and can reset back to everything", async () => {
    const calls = respondWith((url) =>
      url.searchParams.get("status") === "downloaded"
        ? pageOf(ROWS.filter((item) => item.status === "downloaded"))
        : pageOf(ROWS),
    );
    renderRegister({ initialStatus: "downloaded" });

    await screen.findByText("WEB-2026-000003");
    expect(calls[0]?.searchParams.get("status")).toBe("downloaded");
    expect(screen.queryByText("WEB-2026-000001")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: /reset filters/i }));

    await screen.findByText("WEB-2026-000001");
    expect(calls[calls.length - 1]?.searchParams.get("status")).toBeNull();
  });

  it("explains an empty filter rather than showing a blank page", async () => {
    respondWith(() => pageOf([], { total: 0, pageCount: 0 }));
    renderRegister({ initialStatus: "accepted" });

    expect(await screen.findByText("No submissions match these filters")).toBeTruthy();
    // Both the filter bar and the empty state offer the way back.
    expect(screen.getAllByRole("button", { name: /reset filters/i }).length).toBeGreaterThanOrEqual(1);
  });
});
