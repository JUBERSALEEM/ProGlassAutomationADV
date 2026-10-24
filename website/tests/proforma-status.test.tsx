import { describe, expect, it } from "vitest";

import {
  IMPLEMENTED_TRANSITIONS,
  PENDING_TRANSITIONS,
  PROFORMA_STATUSES,
  isLegacyStatus,
  normaliseProformaStatus,
} from "../src/lib/proforma-status";
import { settingsFromServer, settingsPayload, EMPTY_RATE_SETTING } from "../src/lib/proforma-rates";

describe("the approved proforma status vocabulary", () => {
  it("is exactly the five approved words", () => {
    expect(PROFORMA_STATUSES).toEqual(["Draft", "Sent", "Approved", "Issued", "Cancelled"]);
  });

  it("shows a legacy Void row as Cancelled", () => {
    expect(normaliseProformaStatus("Void")).toBe("Cancelled");
    expect(isLegacyStatus("Void")).toBe(true);
    expect(isLegacyStatus("Cancelled")).toBe(false);
  });

  it("keeps every approved word as it is stored", () => {
    for (const status of PROFORMA_STATUSES) {
      expect(normaliseProformaStatus(status)).toBe(status);
    }
  });

  it("falls back to Draft for a value it does not recognise", () => {
    expect(normaliseProformaStatus(null)).toBe("Draft");
    expect(normaliseProformaStatus(undefined)).toBe("Draft");
    expect(normaliseProformaStatus("")).toBe("Draft");
    expect(normaliseProformaStatus("SENT")).toBe("Draft");
  });

  it("claims only the transition that has an operation behind it", () => {
    // Phase C7 made sharing the document with the customer account real, and that
    // operation is what moves Draft to Sent. Acceptance and issuance still have no
    // operation, so they must not appear here.
    expect(IMPLEMENTED_TRANSITIONS).toHaveLength(1);
    expect(IMPLEMENTED_TRANSITIONS[0]).toMatchObject({ from: "Draft", to: "Sent" });
    expect(IMPLEMENTED_TRANSITIONS.some((transition) => transition.to === "Approved")).toBe(false);
    expect(IMPLEMENTED_TRANSITIONS.some((transition) => transition.to === "Issued")).toBe(false);
  });

  it("records the transitions that are still missing as pending", () => {
    const pending = PENDING_TRANSITIONS.map((transition) => `${transition.from}->${transition.to}`);
    expect(pending).toContain("Sent->Approved");
    expect(pending).toContain("Sent->Issued");
    expect(pending).toContain("Draft->Cancelled");
  });
});

describe("specification settings stored with a revision", () => {
  it("loads the stored base rate and surcharge into the rate boxes", () => {
    const settings = settingsFromServer([
      { group_no: 2, base_rate: 80, surcharge_percent: 0 },
      { group_no: 1, base_rate: 100, surcharge_percent: 20 },
    ]);

    expect(settings).toEqual({
      1: { base: "100", percent: "20" },
      2: { base: "80", percent: "0" },
    });
  });

  it("leaves a specification the revision has no row for empty", () => {
    // An older proforma has no settings rows at all. An empty box is honest; a
    // zero would look like a saved price of nothing.
    expect(settingsFromServer([])).toEqual({});
    expect(settingsFromServer([])[1]).toBeUndefined();
    expect(EMPTY_RATE_SETTING).toEqual({ base: "", percent: "" });
  });

  it("sends only the specifications that carry a usable base rate", () => {
    const payload = settingsPayload({
      2: { base: "80", percent: "" },
      1: { base: "100", percent: "20" },
      3: { base: "", percent: "15" },
    });

    expect(payload).toEqual([
      { group_no: 1, base_rate: 100, surcharge_percent: 20 },
      { group_no: 2, base_rate: 80, surcharge_percent: 0 },
    ]);
  });

  it("keeps the payload in specification order", () => {
    const payload = settingsPayload({
      3: { base: "10", percent: "0" },
      1: { base: "10", percent: "0" },
      2: { base: "10", percent: "0" },
    });

    expect(payload.map((group) => group.group_no)).toEqual([1, 2, 3]);
  });

  it("drops a specification number the database would refuse", () => {
    const payload = settingsPayload({
      0: { base: "10", percent: "0" },
      51: { base: "10", percent: "0" },
      7: { base: "10", percent: "0" },
    });

    expect(payload).toEqual([{ group_no: 7, base_rate: 10, surcharge_percent: 0 }]);
  });

  it("drops a base rate above the stored maximum", () => {
    expect(settingsPayload({ 1: { base: "1000001", percent: "0" } })).toEqual([]);
    expect(settingsPayload({ 1: { base: "1000000", percent: "0" } })).toEqual([
      { group_no: 1, base_rate: 1_000_000, surcharge_percent: 0 },
    ]);
  });

  it("reads a percentage typed with a percent sign or a comma", () => {
    expect(settingsPayload({ 1: { base: "1,250", percent: "20%" } })).toEqual([
      { group_no: 1, base_rate: 1250, surcharge_percent: 20 },
    ]);
  });

  it("round-trips through the server shape without changing a figure", () => {
    const stored = settingsFromServer([{ group_no: 1, base_rate: 105.5, surcharge_percent: 12.25 }]);
    expect(settingsPayload(stored)).toEqual([
      { group_no: 1, base_rate: 105.5, surcharge_percent: 12.25 },
    ]);
  });
});
