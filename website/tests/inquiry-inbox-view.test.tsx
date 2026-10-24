import { describe, expect, it } from "vitest";
import {
  activeFilterCount,
  documentLabel,
  EMPTY_FILTERS,
  formatDate,
  inboxQuery,
  parseInboxFilters,
  priorityTone,
  proformaAction,
  statusTone,
} from "../src/lib/inquiry-inbox-view";

function params(query: string) {
  return new URLSearchParams(query);
}

describe("inbox filters", () => {
  it("reads the five URL values the API accepts", () => {
    expect(parseInboxFilters(params("search=ACME&status=Sent&priority=High&hasDocuments=true&page=3"))).toEqual({
      search: "ACME",
      status: "Sent",
      priority: "High",
      hasDocuments: true,
      page: 3,
    });
  });

  it("falls back to an unfiltered first page on nonsense", () => {
    expect(parseInboxFilters(params("page=0&hasDocuments=yes"))).toEqual({ ...EMPTY_FILTERS });
    expect(parseInboxFilters(params("page=not-a-number"))).toEqual({ ...EMPTY_FILTERS });
  });

  it("writes only what is set, and omits the first page", () => {
    expect(inboxQuery(EMPTY_FILTERS)).toBe("");
    expect(inboxQuery({ search: "a b", status: "", priority: "Urgent", hasDocuments: true, page: 1 })).toBe(
      "?search=a+b&priority=Urgent&hasDocuments=true",
    );
    expect(inboxQuery({ ...EMPTY_FILTERS, page: 2 })).toBe("?page=2");
  });

  it("round trips through the query string", () => {
    const filters = { search: "ACME", status: "Confirmed", priority: "Low", hasDocuments: false, page: 4 };
    expect(parseInboxFilters(params(inboxQuery(filters).slice(1)))).toEqual(filters);
  });

  it("counts each narrowing control once", () => {
    expect(activeFilterCount(EMPTY_FILTERS)).toBe(0);
    expect(activeFilterCount({ ...EMPTY_FILTERS, search: "x", hasDocuments: true })).toBe(2);
    expect(activeFilterCount({ ...EMPTY_FILTERS, search: "x", status: "Sent", priority: "High", hasDocuments: true })).toBe(4);
  });
});

describe("proforma entry point", () => {
  const inquiry = { proforma_number: "", proforma_draft: null };

  it("resumes a website draft and reports its revision", () => {
    expect(proformaAction({ proforma_number: "", proforma_draft: { id: "draft", revision: 4 } })).toEqual({
      kind: "resume",
      label: "Resume proforma",
      detail: "Draft revision 4",
    });
  });

  it("prefers the website draft over a desktop reference", () => {
    expect(proformaAction({ proforma_number: "PI-2026-0009", proforma_draft: { id: "draft", revision: 1 } }).kind).toBe("resume");
  });

  it("names the desktop document without claiming it can be opened", () => {
    expect(proformaAction({ proforma_number: "PI-2026-0009", proforma_draft: null })).toEqual({
      kind: "desktop",
      label: "Open proforma",
      detail: "Desktop PI-2026-0009",
    });
  });

  it("offers a new draft when there is neither", () => {
    expect(proformaAction(inquiry)).toEqual({ kind: "create", label: "Create proforma", detail: "No proforma yet" });
  });
});

describe("inbox display", () => {
  it("keeps the status and priority tones the register already used", () => {
    expect(statusTone("Confirmed")).toBe("success");
    expect(statusTone("Cancelled")).toBe("danger");
    expect(statusTone("Hold")).toBe("warning");
    expect(statusTone("Received")).toBe("neutral");
    expect(statusTone("Sent")).toBe("accent");
    expect(priorityTone("Urgent")).toBe("danger");
    expect(priorityTone("High")).toBe("warning");
  });

  it("says a missing date in words rather than a dash", () => {
    expect(formatDate("")).toBe("Not reported");
    expect(formatDate("not a date")).toBe("Not reported");
    expect(formatDate("2026-03-04T09:30:00.000Z")).not.toBe("Not reported");
  });

  it("pluralises documents", () => {
    expect(documentLabel(0)).toBe("0 documents");
    expect(documentLabel(1)).toBe("1 document");
    expect(documentLabel(4)).toBe("4 documents");
  });
});
