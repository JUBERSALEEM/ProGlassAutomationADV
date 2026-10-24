// @vitest-environment jsdom
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, describe, expect, it, vi } from "vitest";

import { ProformaDocument, printDocument, printSqm } from "../src/components/inquiries/proforma/ProformaDocument";
import { buildProformaDocument, documentCaveats, draftDiffersFromSaved } from "../src/lib/proforma-document";
import type { ProformaInquiryHeader, ProformaLine, ProformaRecord } from "../src/types/proforma";

function proforma(overrides: Partial<ProformaRecord> = {}): ProformaRecord {
  return {
    id: "11111111-1111-4111-8111-111111111111",
    inquiry_id: "22222222-2222-4222-8222-222222222222",
    proforma_number: "PI-WEB-2026-000007",
    source_system: "website",
    source_proforma_number: "",
    inquiry_number: "INQ-2026-0007",
    customer_name: "Gulf Facades LLC",
    status: "Draft",
    currency: "AED",
    revision: 3,
    line_count: 3,
    total_sqm: 6.5,
    total_value: 1100,
    created_at: "2026-10-20T08:00:00.000Z",
    updated_at: "2026-10-24T09:30:00.000Z",
    ...overrides,
  };
}

function line(overrides: Partial<ProformaLine> = {}): ProformaLine {
  return {
    group_no: 1,
    line_no: 1,
    glass_ref: "DGU 6+12+6",
    w1: 2000,
    h1: 1500,
    w2: 0,
    h2: 0,
    qty: 2,
    approved_rate: 150,
    sqm: 3,
    total_sqm: 6,
    line_value: 900,
    ...overrides,
  };
}

const inquiry: ProformaInquiryHeader = {
  id: "22222222-2222-4222-8222-222222222222",
  inquiry_number: "INQ-2026-0007",
  customer_name: "Gulf Facades LLC",
  project_name: "Marina Tower Phase 1",
  status: "Prepared",
  proforma_number: "",
  inquiry_value: 0,
};

describe("the saved document model", () => {
  it("groups stored lines and adds the stored figures without repricing them", () => {
    const model = buildProformaDocument(proforma(), [
      line({ group_no: 2, line_no: 1, sqm: 0.5, total_sqm: 0.5, approved_rate: 99.99, line_value: 50 }),
      line(),
    ]);

    expect(model.groups.map((group) => group.groupNo)).toEqual([1, 2]);
    expect(model.groups[0]?.value).toBe(900);
    expect(model.lineCount).toBe(2);
    expect(model.totalSqm).toBe(6.5);
    expect(model.totalValue).toBe(950);
  });

  it("reprints a stored figure that disagrees with the dimensions instead of recomputing it", () => {
    // A 100 x 100 panel would price at 0.5 m2 and 50.00 at a rate of 100.
    // The stored row says otherwise on purpose: the document must show the record.
    const stored = line({ w1: 100, h1: 100, qty: 1, approved_rate: 100, sqm: 0.5, total_sqm: 0.5, line_value: 999.99 });
    const model = buildProformaDocument(proforma({ line_count: 1, total_sqm: 0.5, total_value: 999.99 }), [stored]);

    expect(model.groups[0]?.lines[0]?.sqm).toBe(0.5);
    expect(model.groups[0]?.lines[0]?.value).toBe(999.99);
    expect(model.totalValue).toBe(999.99);
    expect(model.balanced).toBe(true);
  });

  it("reports a header that disagrees with its lines and never repairs it", () => {
    const model = buildProformaDocument(proforma({ line_count: 3, total_sqm: 6.5, total_value: 1100 }), [line()]);

    expect(model.balanced).toBe(false);
    expect(model.integrity.lineCountMatches).toBe(false);
    expect(model.integrity.totalSqmMatches).toBe(false);
    expect(model.integrity.totalValueMatches).toBe(false);
    // Both figures survive untouched: the header's and the one the lines add up to.
    expect(model.header.totalValue).toBe(1100);
    expect(model.totalValue).toBe(900);
  });

  it("forgives drift below half a fil and half of the fourth decimal", () => {
    const model = buildProformaDocument(proforma({ line_count: 1, total_sqm: 6.00002, total_value: 900.004 }), [line()]);
    expect(model.balanced).toBe(true);

    const worse = buildProformaDocument(proforma({ line_count: 1, total_sqm: 6.5, total_value: 900.02 }), [line()]);
    expect(worse.balanced).toBe(false);
  });

  it("states the caveats a stored record actually carries", () => {
    const consistent = proforma({ line_count: 1, total_sqm: 6, total_value: 900 });
    const draft = buildProformaDocument(consistent, [line()]);
    expect(documentCaveats(consistent, draft)).toEqual([
      "This revision is a draft. It has not been issued and it is not an offer.",
    ]);

    const cancelled = proforma({ status: "Cancelled" });
    expect(documentCaveats(cancelled, buildProformaDocument(cancelled, [line()]))[0]).toContain("cancelled");
  });
});

describe("unsaved work", () => {
  const saved = [line()];
  const input = {
    group_no: 1,
    line_no: 1,
    glass_ref: "DGU 6+12+6",
    w1: 2000,
    h1: 1500,
    w2: 0,
    h2: 0,
    qty: 2,
    rate: 150,
  };

  it("sees no difference between the saved rows and an untouched draft", () => {
    expect(draftDiffersFromSaved([input], saved)).toBe(false);
  });

  it("sees a changed rate, a changed quantity and an added line", () => {
    expect(draftDiffersFromSaved([{ ...input, rate: 151 }], saved)).toBe(true);
    expect(draftDiffersFromSaved([{ ...input, qty: 3 }], saved)).toBe(true);
    expect(draftDiffersFromSaved([input, { ...input, line_no: 2 }], saved)).toBe(true);
  });
});

describe("the document view", () => {
  it("prints the stored revision, including the draft caveat and the numbers", () => {
    const markup = renderToStaticMarkup(
      <ProformaDocument proforma={proforma()} inquiry={inquiry} lines={[line()]} />,
    );

    expect(markup).toContain("PI-WEB-2026-000007");
    expect(markup).toContain("Revision 3");
    expect(markup).toContain("Gulf Facades LLC");
    expect(markup).toContain("INQ-2026-0007");
    expect(markup).toContain("900.00");
    expect(markup).toContain("not recalculated");
    expect(markup).toContain("has not been issued");
    expect(markup).toContain("Print document");
  });

  it("warns that unsaved grid edits are not part of the printed revision", () => {
    const markup = renderToStaticMarkup(
      <ProformaDocument proforma={proforma()} inquiry={inquiry} lines={[line()]} hasUnsavedChanges />,
    );
    expect(markup).toContain("unsaved changes");
    expect(markup).toContain("not part of revision 3");
  });

  it("keeps four stored decimals instead of rounding them away", () => {
    expect(printSqm(6)).toBe("6.00");
    expect(printSqm(0.5004)).toBe("0.5004");
    expect(printSqm(12.3456)).toBe("12.3456");
  });

  it("asks the browser to print", () => {
    const spy = vi.spyOn(window, "print").mockImplementation(() => {});
    printDocument();
    expect(spy).toHaveBeenCalledTimes(1);
    spy.mockRestore();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });
});
