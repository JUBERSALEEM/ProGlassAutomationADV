// @vitest-environment jsdom

import React from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { CustomerInquiryPortal } from "@/components/inquiries/CustomerInquiryPortal";

const authMocks = vi.hoisted(() => ({
  refreshSession: vi.fn(),
  signOut: vi.fn(),
  requireAuth: vi.fn(),
  recoverAfterUnauthorized: vi.fn(),
}));

vi.mock("@/lib/useOperatorSession", () => ({
  useOperatorSession: () => ({
    checking: false,
    authToken: "test-access-token",
    isAuthRequired: false,
    userEmail: "customer@example.com",
    signOut: authMocks.signOut,
    requireAuth: authMocks.requireAuth,
    recoverAfterUnauthorized: authMocks.recoverAfterUnauthorized,
    unavailable: false,
    error: "",
    refreshSession: authMocks.refreshSession,
  }),
}));

vi.mock("@/components/ui/LocaleLink", () => ({
  LocaleLink: ({ href, children, ...props }: React.AnchorHTMLAttributes<HTMLAnchorElement>) => React.createElement("a", { href, ...props }, children),
}));

vi.mock("@/components/inquiries/InquiryPromptFlow", () => ({
  InquiryPromptFlow: ({ children }: { children: React.ReactNode }) => React.createElement("div", null, children),
}));

function apiResponse(data: unknown) {
  return { ok: true, status: 200, json: async () => ({ ok: true, data }) };
}

function mockCustomerApi() {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    return url.endsWith("/profile")
      ? apiResponse({ profile: null, projects: [] })
      : apiResponse([]);
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("customer inquiry portal layout", () => {
  it("keeps drawing uploads available alongside the full-width PI Steps 4–7 flow", async () => {
    mockCustomerApi();
    const { container } = render(<CustomerInquiryPortal intent="proforma_invoice" />);

    const technicalSection = await screen.findByRole("region", { name: "Technical glass specification" });
    expect(screen.getByText("Supporting project files")).toBeTruthy();
    expect(screen.getByText("Choose drawings or drop them here")).toBeTruthy();
    expect(container.querySelector('input[name="documents"]')).toBeTruthy();
    expect(container.querySelector("#inquiry-documents-help")?.textContent).toContain("0/14 optional files");
    expect(screen.getByText(/one unpriced Inquiry PDF is generated;/i)).toBeTruthy();
    expect(technicalSection.parentElement?.className).toContain("mt-4");
    expect(technicalSection.parentElement?.className).not.toContain("grid-cols-2");
    expect(screen.getByLabelText("Proforma technical specification generator")).toBeTruthy();
  });

  it("keeps optional file upload controls on a general inquiry", async () => {
    mockCustomerApi();
    render(<CustomerInquiryPortal intent="quotation" />);

    expect(await screen.findByText("Supporting project files")).toBeTruthy();
    expect(screen.getByText("Choose drawings or drop them here")).toBeTruthy();
  });
});
