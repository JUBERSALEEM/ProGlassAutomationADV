// @vitest-environment jsdom
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";

vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: unknown; children: ReactNode }) => (
    <a href={typeof href === "string" ? href : "#"} {...rest}>
      {children}
    </a>
  ),
}));

vi.mock("next/navigation", () => ({
  usePathname: () => "/inquiries",
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), prefetch: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));

const session = {
  checking: false,
  userEmail: "customer@example.com",
  authToken: "customer-token" as string | null,
  isAuthRequired: false,
  unavailable: false,
  error: null as string | null,
  refreshSession: async () => null,
  signOut: async () => {},
  requireAuth: () => {},
};

vi.mock("../src/lib/useOperatorSession", () => ({
  useOperatorSession: () => session,
}));

/* The register has its own tests; here it only has to prove that a staff
   account reaches it and a customer never does. */
vi.mock("../src/components/inquiries/InquiriesDashboard", () => ({
  InquiriesDashboard: () => <div data-testid="operator-inbox">operator register</div>,
}));

vi.mock("../src/components/inquiries/InquiryAccessGate", () => ({
  InquiryAccessGate: ({ redirectPath }: { redirectPath: string }) => (
    <div data-testid="access-gate">refused at {redirectPath}</div>
  ),
}));

import { I18nProvider } from "../src/i18n/provider";
import { APP_STRINGS } from "../src/i18n/app-strings";
import { InquiryLanding } from "../src/components/inquiries/InquiryLanding";
import type { OperatorAccess } from "../src/lib/inquiries-api";

function access(role: string | null, enabled = true): OperatorAccess {
  return {
    email: "someone@example.com",
    user_id: "11111111-1111-4111-8111-111111111111",
    role,
    enabled,
    can_read_inbox: enabled && (role === "inquiry_admin" || role === "inquiry_auditor"),
    can_write_proforma: enabled && role === "inquiry_admin",
  };
}

function respondWith(payload: OperatorAccess | null, status = 200) {
  const fetchMock = vi.fn(async () => {
    return {
      ok: status === 200,
      status,
      json: async () =>
        status === 200 && payload
          ? { ok: true, data: payload }
          : { ok: false, error: { message: "Unable to read your access." } },
    } as unknown as Response;
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderLanding() {
  return render(
    <I18nProvider locale="en-US" overlay={APP_STRINGS["en-US"]}>
      <InquiryLanding />
    </I18nProvider>,
  );
}

beforeEach(() => {
  session.checking = false;
  session.authToken = "customer-token";
  session.userEmail = "customer@example.com";
  vi.unstubAllGlobals();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("the inquiries page and who may see what", () => {
  it("gives a signed-out visitor the public customer page, not the register", async () => {
    session.authToken = null;
    const fetchMock = respondWith(null);
    renderLanding();

    expect(await screen.findByRole("heading", { name: /your inquiries with proglass/i })).toBeTruthy();
    expect(screen.getByRole("link", { name: /sign in to submit an inquiry/i })).toBeTruthy();
    expect(screen.queryByTestId("operator-inbox")).toBeNull();
    expect(screen.queryByTestId("access-gate")).toBeNull();
    // Nothing to ask the server when there is no session.
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("gives a plain customer account the customer surfaces and no operator register", async () => {
    const fetchMock = respondWith(access(null));
    renderLanding();

    expect(await screen.findByRole("heading", { name: /your inquiries with proglass/i })).toBeTruthy();
    expect(screen.getByRole("link", { name: /submit an inquiry/i })).toBeTruthy();
    expect(screen.getByRole("link", { name: /my inquiries and messages/i })).toBeTruthy();
    expect(screen.getByRole("link", { name: /proforma invoices shared with me/i })).toBeTruthy();
    expect(screen.queryByTestId("operator-inbox")).toBeNull();
    expect(screen.queryByTestId("access-gate")).toBeNull();
    // The internal queue is not advertised to a customer at all.
    expect(document.body.textContent).not.toContain("Open the website queue");
    expect(document.body.textContent).not.toContain("Customer submissions");
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/inquiries/access",
      expect.objectContaining({ cache: "no-store" }),
    );
  });

  it("sends an auditor or admin to the register", async () => {
    respondWith(access("inquiry_admin"));
    renderLanding();

    await waitFor(() => expect(screen.getByTestId("operator-inbox")).toBeTruthy());
    expect(screen.queryByRole("heading", { name: /your inquiries with proglass/i })).toBeNull();
  });

  it("sends an auditor to the register as well", async () => {
    respondWith(access("inquiry_auditor"));
    renderLanding();

    await waitFor(() => expect(screen.getByTestId("operator-inbox")).toBeTruthy());
  });

  it("points an estimator at the assignment workspace instead of the register", async () => {
    respondWith(access("inquiry_estimator"));
    renderLanding();

    expect(await screen.findByRole("heading", { name: /work from the estimator workspace/i })).toBeTruthy();
    expect(screen.getByRole("link", { name: /open the estimator workspace/i }).getAttribute("href")).toBe(
      "/inquiries/estimator",
    );
    expect(screen.queryByTestId("operator-inbox")).toBeNull();
  });

  it("tells the bridge account what it is instead of showing it a register", async () => {
    respondWith(access("inquiry_bridge"));
    renderLanding();

    expect(await screen.findByRole("heading", { name: /shop pc's sync account/i })).toBeTruthy();
    expect(screen.queryByTestId("operator-inbox")).toBeNull();
  });

  it("refuses a role that is switched off", async () => {
    respondWith(access("inquiry_admin", false));
    renderLanding();

    await waitFor(() => expect(screen.getByTestId("access-gate")).toBeTruthy());
    expect(screen.getByTestId("access-gate").textContent).toContain("/inquiries");
    expect(screen.queryByTestId("operator-inbox")).toBeNull();
  });

  it("fails closed when the access read fails", async () => {
    respondWith(null, 503);
    renderLanding();

    expect(await screen.findByRole("heading", { name: /could not be read/i })).toBeTruthy();
    expect(screen.getByRole("button", { name: /try again/i })).toBeTruthy();
    expect(screen.queryByTestId("operator-inbox")).toBeNull();
  });
});
