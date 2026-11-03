// @vitest-environment jsdom

import React from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

const session = vi.hoisted(() => ({ checking: false, user: null as unknown, userEmail: null as string | null }));

vi.mock("@/lib/website-session", () => ({ useWebsiteSession: () => session }));
vi.mock("@/components/ui/LocaleLink", () => ({
  LocaleLink: ({ href, children, ...props }: React.AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }) => React.createElement("a", { href, ...props }, children),
}));
vi.mock("@/components/brand/Logo", () => ({ Logo: () => null }));
vi.mock("@/components/layout/LanguageMenu", () => ({ LanguageMenu: () => null }));
vi.mock("@/components/layout/ThemeModeSwitch", () => ({ ThemeModeSwitch: () => null }));
vi.mock("@/components/layout/ThemeToggle", () => ({ ThemeToggle: () => null }));

import { WelcomeHeader } from "@/components/layout/WelcomeHeader";

afterEach(() => {
  cleanup();
  session.checking = false;
  session.user = null;
  session.userEmail = null;
});

describe("global customer-account access", () => {
  it("uses one unified account link before sign-in", () => {
    render(<WelcomeHeader />);
    expect(screen.getByRole("link", { name: "Sign in or create a customer account" }).getAttribute("href"))
      .toBe("/account?redirect=%2Finquiries%2Fchat");
    expect(screen.queryByText("Log in")).toBeNull();
    expect(screen.queryByText("Sign up")).toBeNull();
  });

  it("shows the signed-in customer's name in that same account link", () => {
    session.userEmail = "aisha@example.com";
    session.user = { email: "aisha@example.com", user_metadata: { full_name: "Aisha Khan" } };
    render(<WelcomeHeader />);
    const account = screen.getByRole("link", { name: "Customer account for Aisha Khan" });
    expect(account.textContent).toBe("Aisha Khan");
    expect(account.getAttribute("href")).toBe("/account");
    expect(account.getAttribute("title")).toBe("aisha@example.com");
  });
});
