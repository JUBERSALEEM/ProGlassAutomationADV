import { describe, expect, it, vi } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";

import type { NextRequest } from "next/server";
import { GET as getCustomerProformaList } from "../src/app/api/account/proformas/route";
import { GET as getCustomerProformaById } from "../src/app/api/account/proformas/[id]/route";
import { POST as postStaffProformaShare } from "../src/app/api/inquiries/[id]/proforma/share/route";
import { CustomerProformaList, CustomerProformaView } from "../src/components/account/CustomerProformas";
import { SharePanelView } from "../src/components/inquiries/proforma/ProformaSharePanel";
import { CustomerProformaFailure, fetchCustomerProforma, listCustomerProformas } from "../src/lib/customer-proforma-api";
import type { ProformaShareState } from "../src/lib/proforma-api";

function state(overrides: Partial<ProformaShareState> = {}): ProformaShareState {
  return {
    proforma_id: "0c9d4c48-6a1e-4b3f-9a52-2fbb3b1c0001",
    current_revision: 3,
    customer_linked: true,
    share: null,
    stale: false,
    can_write: true,
    ...overrides,
  };
}

function render(node: Parameters<typeof renderToStaticMarkup>[0]) {
  return renderToStaticMarkup(node);
}

const noop = () => {};

describe("customer priced-proforma security boundary", () => {
  it("retires the account listing API without returning priced data, with or without a customer token", async () => {
    for (const authorization of [undefined, "Bearer customer-token"]) {
      const request = new Request("https://example.test/api/account/proformas", {
        headers: authorization ? { Authorization: authorization } : {},
      }) as unknown as NextRequest;
      const response = getCustomerProformaList(request);
      const body = await response.json();
      expect(response.status).toBe(410);
      expect(body.error.code).toBe("CUSTOMER_PRICING_DISABLED");
      expect(JSON.stringify(body)).not.toMatch(/total_value|approved_rate|line_value|margin|cost/i);
    }
  });

  it("blocks direct document-ID access even when a customer supplies a bearer token", async () => {
    const request = new Request("https://example.test/api/account/proformas/known-id", {
      headers: { Authorization: "Bearer customer-token" },
    }) as unknown as NextRequest;
    const response = getCustomerProformaById(request, { params: Promise.resolve({ id: "known-id" }) });
    const body = await response.json();
    expect(response.status).toBe(410);
    expect(body.error.code).toBe("CUSTOMER_PRICING_DISABLED");
    expect(JSON.stringify(body)).not.toMatch(/total_value|approved_rate|line_value|margin|cost/i);
  });

  it("refuses unauthenticated calls to the staff customer-share mutation", async () => {
    const request = new Request("https://example.test/api/inquiries/inquiry-id/proforma/share", {
      method: "POST",
    }) as unknown as NextRequest;
    const response = await postStaffProformaShare(request, { params: Promise.resolve({ id: "inquiry-id" }) });
    expect(response.status).toBe(401);
    const body = await response.json();
    expect(JSON.stringify(body)).not.toMatch(/approved_rate|line_value|total_value/);
  });

  it("keeps the retired service fail-closed if a future route tries to reuse it", async () => {
    await expect(listCustomerProformas("customer-token")).rejects.toBeInstanceOf(CustomerProformaFailure);
    await expect(fetchCustomerProforma("customer-token", "known-proforma-id")).rejects.toBeInstanceOf(CustomerProformaFailure);
  });

  it("renders no priced document from stale account-list or direct-ID component imports", () => {
    const listHtml = render(<CustomerProformaList />);
    const detailHtml = render(<CustomerProformaView id="known-proforma-id" />);
    for (const html of [listHtml, detailHtml]) {
      expect(html).toContain("Priced proformas are not available in customer accounts");
      expect(html).not.toMatch(/approved_rate|line_value|total_value|<table|Print or save as PDF/i);
    }
  });
});

describe("staff-only legacy share audit panel", () => {
  it("does not offer new customer sharing and explains the WPF release boundary", () => {
    const html = render(
      <SharePanelView state={state()} busy={false} error={null} notice={null} onRevoke={noop} />,
    );
    expect(html).toContain("Customer access to priced proformas is disabled");
    expect(html).toContain("WPF workflow");
    expect(html).not.toContain("Share revision");
    expect(html).not.toContain("Withdraw the customer");
    expect(html).not.toContain("<button");
  });

  it("preserves legacy share history and offers withdrawal only while still active", () => {
    const html = render(
      <SharePanelView
        state={state({ share: { revision: 2, shared_at: "2026-10-02T09:00:00.000Z", revoked_at: null } })}
        busy={false}
        error={null}
        notice={null}
        onRevoke={vi.fn()}
      />,
    );
    expect(html).toContain("internal audit ledger");
    expect(html).toContain("Customer accounts cannot read the proforma");
    expect(html).toContain("Mark legacy share withdrawn");
    expect(html).not.toContain("Share revision");
  });

  it("shows a withdrawn record without allowing it to be rewritten or deleted", () => {
    const html = render(
      <SharePanelView
        state={state({
          can_write: false,
          share: {
            revision: 2,
            shared_at: "2026-10-02T09:00:00.000Z",
            revoked_at: "2026-11-04T09:00:00.000Z",
          },
        })}
        busy={false}
        error={null}
        notice={null}
        onRevoke={noop}
      />,
    );
    expect(html).toContain("withdrawn");
    expect(html).toContain("Customer accounts cannot read the proforma");
    expect(html).not.toContain("Mark legacy share withdrawn");
  });
});
