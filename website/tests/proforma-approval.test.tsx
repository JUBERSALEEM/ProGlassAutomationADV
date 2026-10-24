import { describe, expect, it } from "vitest";

import { isAwaitingProformaApproval } from "@/lib/inquiry-submission-status";

/*
 * A proforma invoice request is delivered like any other submission, so
 * "awaiting approval" cannot be a delivery state. It is a decision a coordinator
 * still owes, and it has to survive the desk downloading the submission — which
 * is exactly why the flag is derived from the type and the status rather than
 * from a second stored column that could drift.
 */
describe("isAwaitingProformaApproval", () => {
  it("is true while nobody has decided, including after the desk downloaded it", () => {
    for (const status of ["submitted", "under_review", "downloaded"]) {
      expect(isAwaitingProformaApproval({ inquiry_type: "proforma_invoice", status })).toBe(true);
    }
  });

  it("is false once the request has been decided", () => {
    for (const status of ["accepted", "rejected", "closed"]) {
      expect(isAwaitingProformaApproval({ inquiry_type: "proforma_invoice", status })).toBe(false);
    }
  });

  it("is false for every other inquiry type", () => {
    for (const inquiry_type of ["quotation", "drawing_review", "technical", "order_handover", "other"]) {
      expect(isAwaitingProformaApproval({ inquiry_type, status: "submitted" })).toBe(false);
    }
  });
});
