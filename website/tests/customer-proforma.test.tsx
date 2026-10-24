import { describe, expect, it, vi } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";

import { SharePanelView } from "../src/components/inquiries/proforma/ProformaSharePanel";
import { toCustomerSummary } from "../src/lib/customer-proforma-api";
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

describe("who holds a proforma, and what the operator can do about it", () => {
  it("says a document has to be saved before it can be shared", () => {
    const html = render(
      <SharePanelView state={state({ proforma_id: null, current_revision: 0 })} busy={false} error={null} notice={null} onShare={noop} onRevoke={noop} />,
    );
    expect(html).toContain("Save the proforma once");
    expect(html).not.toContain("Share revision");
  });

  it("refuses to pretend there is somebody to share with when the inquiry is not linked", () => {
    const html = render(
      <SharePanelView state={state({ customer_linked: false })} busy={false} error={null} notice={null} onShare={noop} onRevoke={noop} />,
    );
    expect(html).toContain("not linked to a customer account");
    expect(html).not.toContain("Share revision");
  });

  it("offers the current revision when nothing has been shared", () => {
    const html = render(
      <SharePanelView state={state()} busy={false} error={null} notice={null} onShare={noop} onRevoke={noop} />,
    );
    expect(html).toContain("Not shared with the customer");
    expect(html).toContain("Share revision 3 with the customer");
  });

  it("reports a stale share instead of claiming the customer has the new figures", () => {
    const html = render(
      <SharePanelView
        state={state({ share: { revision: 2, shared_at: "2026-10-01T09:00:00.000Z" }, stale: true })}
        busy={false}
        error={null}
        notice={null}
        onShare={noop}
        onRevoke={noop}
      />,
    );
    expect(html).toContain("Shared revision 2");
    expect(html).toContain("now on revision");
    expect(html).toContain("Share revision 3");
  });

  it("shows the shared revision and offers withdrawal", () => {
    const html = render(
      <SharePanelView
        state={state({ share: { revision: 3, shared_at: "2026-10-02T09:00:00.000Z" } })}
        busy={false}
        error={null}
        notice={null}
        onShare={noop}
        onRevoke={noop}
      />,
    );
    expect(html).toContain("at revision 3");
    expect(html).toContain("Withdraw the customer");
  });

  it("keeps the action off for a role that cannot share", () => {
    const html = render(
      <SharePanelView state={state({ can_write: false })} busy={false} error={null} notice={null} onShare={noop} onRevoke={noop} />,
    );
    expect(html).toContain("read only for this role");
    expect(html).toContain("disabled");
  });

  it("surfaces a server refusal beside the action", () => {
    const html = render(
      <SharePanelView
        state={state()}
        busy={false}
        error="Sharing a proforma needs an enabled inquiry administrator role."
        notice={null}
        onShare={vi.fn()}
        onRevoke={noop}
      />,
    );
    expect(html).toContain("inquiry administrator role");
  });
});

describe("the customer list reads only what the database returned", () => {
  it("maps a shared row, including when it was shared", () => {
    const summary = toCustomerSummary({
      id: "p1",
      proforma_number: "PI-WEB-2026-000042",
      inquiry_number: "INQ-2026-0007",
      revision: 2,
      status: "Sent",
      currency: "AED",
      line_count: 4,
      total_sqm: 18.5,
      total_value: 12400.5,
      updated_at: "2026-10-03T10:00:00.000Z",
      inquiry_proforma_shares: [{ shared_at: "2026-10-03T11:00:00.000Z", revision: 2 }],
    });

    expect(summary).toEqual({
      id: "p1",
      proforma_number: "PI-WEB-2026-000042",
      inquiry_number: "INQ-2026-0007",
      revision: 2,
      status: "Sent",
      currency: "AED",
      line_count: 4,
      total_sqm: 18.5,
      total_value: 12400.5,
      updated_at: "2026-10-03T10:00:00.000Z",
      shared_at: "2026-10-03T11:00:00.000Z",
    });
  });

  it("leaves shared_at empty when no share row came back, so the list can drop it", () => {
    const summary = toCustomerSummary({ id: "p2", proforma_number: "PI-WEB-2026-000043" });
    expect(summary.shared_at).toBe("");
    expect(summary.status).toBe("Draft");
  });

  it("maps a legacy Void as Cancelled", () => {
    const summary = toCustomerSummary({ id: "p3", status: "Void", inquiry_proforma_shares: [{ shared_at: "x" }] });
    expect(summary.status).toBe("Cancelled");
  });
});
