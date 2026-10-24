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
import { InboxCards } from "../src/components/inquiries/InboxCards";
import { InboxSummary } from "../src/components/inquiries/InboxSummary";
import { ProformaSpecification } from "../src/components/inquiries/proforma/ProformaSpecification";
import { groupTotals, priceDraft, type DraftLine } from "../src/lib/proforma-draft";
import type { InquiryListRow } from "../src/types/inquiries";

function wrap(node: ReactNode) {
  return renderToStaticMarkup(
    <I18nProvider locale="en-US" overlay={APP_STRINGS["en-US"]}>
      {node}
    </I18nProvider>,
  );
}

const baseRow: InquiryListRow = {
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
  received_at: "2026-03-02T08:00:00.000Z",
  source_updated_at: "2026-03-04T09:30:00.000Z",
  proforma_number: "",
  job_order_number: "",
  availability: "available",
  attachment_count: 3,
  published_at: "2026-03-04T10:00:00.000Z",
  proforma_draft: null,
};

function draftLine(groupNo: number, w1: string, h1: string, qty: string, rate: string): DraftLine {
  return { key: `line-${groupNo}-${w1}-${h1}`, groupNo, glassRef: "6mm Clear FT", w1, h1, w2: "", h2: "", qty, rate };
}

describe("inbox card surface", () => {
  it("renders the four API indicators and names the scope", () => {
    const html = wrap(<InboxSummary summary={{ total: 1204, active: 87, confirmed: 61, withDocuments: 43 }} />);
    expect(html).toContain("With documents");
    expect(html).toContain("Publication summary, all inquiries");
    expect(html).toContain("1,204");
    expect(html).toContain("87");
    expect(html).toContain("dense-card-grid");
    expect(html).toContain("sm:grid-cols-4");
  });

  it("renders one card per inquiry at every width, with both entry points", () => {
    const html = wrap(
      <InboxCards
        rows={[
          baseRow,
          { ...baseRow, id: "b7c1e45f-ea0b-4c3f-9a1e-6f2f0b5c7d22", inquiry_number: "INQ-2026-0043", proforma_draft: { id: "draft", revision: 3 } },
          { ...baseRow, id: "c8d2e45f-ea0b-4c3f-9a1e-6f2f0b5c7d33", inquiry_number: "INQ-2026-0044", proforma_number: "PI-2026-0009" },
        ]}
        loading={false}
        onReset={() => {}}
      />,
    );
    expect(html).toContain("INQ-2026-0042");
    expect(html).toContain("Gulf Facade LLC");
    expect(html).toContain("Marina Tower, level 12");
    expect(html).toContain("Create proforma");
    expect(html).toContain("Resume proforma");
    expect(html).toContain("Desktop PI-2026-0009");
    expect(html).toContain("/inquiries/8f14e45f-ea0b-4c3f-9a1e-6f2f0b5c7d11");
    expect(html).toContain("/inquiries/8f14e45f-ea0b-4c3f-9a1e-6f2f0b5c7d11/proforma");
    expect(html).toContain("sm:grid-cols-2 xl:grid-cols-3");
    expect(html).not.toContain("Estimator");
    expect(html).not.toContain("Assigned to");
  });

  it("says what an empty result means and how to leave it", () => {
    const html = wrap(<InboxCards rows={[]} loading={false} onReset={() => {}} />);
    expect(html).toContain("No inquiries match these filters");
    expect(html).toContain("Reset filters");
  });

  it("shows a skeleton block rather than nothing on a first load", () => {
    const html = wrap(<InboxCards rows={[]} loading onReset={() => {}} />);
    expect(html).toContain('aria-busy="true"');
    expect(html).toContain("animate-pulse");
  });
});

describe("proforma specification surface", () => {
  it("renders the ten Step 5 columns, numbered groups and the priced totals", () => {
    const lines = [
      draftLine(1, "1200", "900", "8", "150"),
      draftLine(2, "2000", "1500", "4", "185"),
      draftLine(2, "300", "300", "3", "99.99"),
    ];
    const priced = priceDraft(lines);
    const html = wrap(
      <ProformaSpecification
        priced={priced}
        groups={[1, 2]}
        perGroup={groupTotals(priced)}
        activeGroup={2}
        currency="AED"
        totals={{ lines: 3, sqm: 22.14, value: 3665.99 }}
        onEdit={() => {}}
        onAddLine={() => {}}
        onRemoveLine={() => {}}
        onAddGroup={() => {}}
        onRemoveGroup={() => {}}
        onFocusGroup={() => {}}
      />,
    );
    for (const heading of ["Glass reference", "W1", "H1", "W2", "H2", "Qty", "SQM", "Total SQM", "Approved rate", "Value"]) {
      expect(html).toContain(heading);
    }
    expect(html).toContain("Specification 1");
    expect(html).toContain("Specification 2");
    expect(html).toContain("Receiving builder lines");
    expect(html).toContain("Assign builder here");
    expect(html).toContain("1,296.00");
    expect(html).toContain("2,220.00");
    expect(html).toContain("149.99");
    expect(html).toContain("3,665.99");
    expect(html).toContain("min");
    expect(html).not.toMatch(/margin|profit|cost per|optimis/i);
  });
});
