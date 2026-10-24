// @vitest-environment jsdom
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";

vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: unknown; children: ReactNode }) => (
    <a href={typeof href === "string" ? href : "#"} {...rest}>
      {children}
    </a>
  ),
}));

import { renderToStaticMarkup } from "react-dom/server";
import { I18nProvider } from "../src/i18n/provider";
import { APP_STRINGS } from "../src/i18n/app-strings";
import { SubmissionCountStrip } from "../src/components/inquiries/WebsiteSubmissionsPanel";
import { InboxCards } from "../src/components/inquiries/InboxCards";
import type { CustomerInquiryStatusCounts } from "../src/types/inquiry-submissions";

function wrap(node: ReactNode) {
  return renderToStaticMarkup(
    <I18nProvider locale="en-US" overlay={APP_STRINGS["en-US"]}>{node}</I18nProvider>,
  );
}

const counts: CustomerInquiryStatusCounts = {
  submitted: 4,
  under_review: 1,
  accepted: 0,
  downloaded: 2,
  rejected: 0,
  closed: 0,
};

describe("the website queue strip inside the desktop inbox", () => {
  it("reports the whole queue and the work still waiting on a person", () => {
    const html = wrap(<SubmissionCountStrip tally={{ allTotal: 7, counts }} />);
    expect(html).toContain("7");
    expect(html).toContain("complete in the website queue");
    expect(html).toContain("5");
    expect(html).toContain("waiting for a first or further review");
    expect(html).toContain("Open the website queue");
    expect(html).toContain('href="/inquiries/customer-submissions"');
  });

  it("links every status, downloaded included, so the queue can be entered filtered", () => {
    const html = wrap(<SubmissionCountStrip tally={{ allTotal: 7, counts }} />);
    for (const status of ["submitted", "under_review", "accepted", "downloaded", "rejected", "closed"]) {
      expect(html).toContain(`/inquiries/customer-submissions?status=${status}`);
    }
    expect(html).toContain("Downloaded");
    expect(html).toContain("Under review");
  });
});

describe("an empty desktop register", () => {
  it("says the register is empty rather than that the filters match nothing", () => {
    const html = wrap(<InboxCards rows={[]} loading={false} onReset={() => {}} filtered={false} />);
    expect(html).toContain("The desktop register is empty");
    expect(html).toContain("published by the Windows bridge");
    expect(html).not.toContain("No inquiries match these filters");
  });

  it("still blames the filters when filters are the reason", () => {
    const html = wrap(<InboxCards rows={[]} loading={false} onReset={() => {}} filtered />);
    expect(html).toContain("No inquiries match these filters");
    expect(html).toContain("Reset filters");
  });
});
