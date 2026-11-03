// @vitest-environment jsdom

import React from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

const state = vi.hoisted(() => ({
  session: {
    checking: false,
    authToken: "saved-token",
    isAuthRequired: false,
    userEmail: "aisha@example.com",
    requireAuth: () => undefined,
    signOut: async () => undefined,
    unavailable: false,
    error: null as string | null,
    refreshSession: async () => null,
    recoverAfterUnauthorized: async () => null,
  },
  inquiries: {
    inquiries: [] as unknown[],
    loading: false,
    error: "We could not authorize the inquiry-list request. The request was denied.",
    refresh: async () => undefined,
    hasSuccessfulLoad: false,
  },
}));

vi.mock("@/lib/useOperatorSession", () => ({ useOperatorSession: () => state.session }));
vi.mock("@/lib/useCustomerInquiries", () => ({ useCustomerInquiries: () => state.inquiries }));
vi.mock("@/i18n/provider", () => ({ useLocale: () => "en-US" }));
vi.mock("@/components/ui/LocaleLink", () => ({
  LocaleLink: ({ href, children, ...props }: React.AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }) => React.createElement("a", { href, ...props }, children),
}));
vi.mock("@/components/inquiries/InquiryCommunicationPanel", () => ({ InquiryCommunicationPanel: () => null }));

import { CustomerInquiryChatWorkspace } from "@/components/inquiries/CustomerInquiryChatWorkspace";

afterEach(() => {
  cleanup();
  state.session.error = null;
  state.inquiries.error = "We could not authorize the inquiry-list request. The request was denied.";
  state.inquiries.inquiries = [];
  state.inquiries.loading = false;
  state.inquiries.hasSuccessfulLoad = false;
});

describe("customer inquiry-list error state", () => {
  it("does not display zero counts or an empty-account message when list authorization fails", () => {
    render(<CustomerInquiryChatWorkspace />);
    expect(screen.getByText("Signed in as aisha@example.com")).toBeTruthy();
    expect(screen.getByRole("link", { name: "Customer account" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Sign out" })).toBeTruthy();
    expect(screen.getByRole("alert").textContent).toContain("We could not authorize the inquiry-list request");
    expect(screen.queryByRole("region", { name: "Your inquiry status summary" })).toBeNull();
    expect(screen.queryByText("No inquiry to chat about yet")).toBeNull();
  });
});
