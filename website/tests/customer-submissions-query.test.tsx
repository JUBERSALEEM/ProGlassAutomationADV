// @vitest-environment node
import { describe, expect, it } from "vitest";
import {
  OPERATOR_PAGE_SIZE_DEFAULT,
  operatorPageRange,
  operatorSearchFilter,
  operatorSearchTerm,
  parseOperatorSubmissionQuery,
} from "../src/lib/inquiry-submissions-api";
import {
  isReviewLocked,
  isReviewStatus,
  reviewSelection,
  statusLabel,
  statusSummary,
  statusTone,
} from "../src/lib/inquiry-submission-status";
import {
  CUSTOMER_INQUIRY_REVIEW_STATUSES,
  CUSTOMER_INQUIRY_STATUSES,
  type CustomerInquiryStatus,
} from "../src/types/inquiry-submissions";

/*
 * The register used to narrow two hundred rows in the browser and offer five of
 * the six statuses in its filter: `downloaded` was missing, so the two rows in
 * that state could not be isolated. These tests pin the vocabulary and the
 * query the server now runs, which is the part an admin session depends on.
 */

describe("the submission status vocabulary", () => {
  it("carries all six statuses the database allows, downloaded included", () => {
    expect([...CUSTOMER_INQUIRY_STATUSES]).toEqual([
      "submitted",
      "under_review",
      "accepted",
      "downloaded",
      "rejected",
      "closed",
    ]);
    expect(CUSTOMER_INQUIRY_STATUSES).toContain("downloaded");
  });

  it("names and colours every status, so no tab can render blank", () => {
    for (const status of CUSTOMER_INQUIRY_STATUSES) {
      expect(statusLabel(status)).not.toBe(status);
      expect(statusLabel(status).length).toBeGreaterThan(2);
      expect(["neutral", "accent", "success", "warning", "danger"]).toContain(statusTone(status));
      expect(statusSummary(status)).not.toBe("");
    }
    expect(statusLabel("downloaded")).toBe("Downloaded");
    expect(statusLabel("under_review")).toBe("Under review");
  });

  it("separates the statuses an operator reads from the four a review writes", () => {
    expect([...CUSTOMER_INQUIRY_REVIEW_STATUSES]).toEqual(["under_review", "accepted", "rejected", "closed"]);
    expect(isReviewStatus("submitted")).toBe(false);
    expect(isReviewStatus("downloaded")).toBe(false);
    expect(isReviewStatus("accepted")).toBe(true);
  });
});

describe("the review screen opens on a status it may actually write", () => {
  const cases: Array<[CustomerInquiryStatus, string]> = [
    ["submitted", "under_review"],
    ["under_review", "under_review"],
    ["accepted", "accepted"],
    ["downloaded", ""],
    ["rejected", "rejected"],
    ["closed", "closed"],
  ];

  it.each(cases)("a %s submission opens on %s", (status, expected) => {
    expect(reviewSelection(status)).toBe(expected);
  });

  it("returns a value the review RPC accepts, or nothing at all", () => {
    for (const status of CUSTOMER_INQUIRY_STATUSES) {
      const selection = reviewSelection(status);
      expect(selection === "" || isReviewStatus(selection)).toBe(true);
    }
  });

  it("never offers to reopen a rejected or closed review", () => {
    expect(isReviewLocked("rejected")).toBe(true);
    expect(isReviewLocked("closed")).toBe(true);
    expect(isReviewLocked("submitted")).toBe(false);
    expect(isReviewLocked("downloaded")).toBe(false);
  });
});

describe("the operator queue query", () => {
  it("defaults to every status, page one, twenty five rows", () => {
    expect(parseOperatorSubmissionQuery()).toEqual({ status: "", search: "", page: 1, pageSize: OPERATOR_PAGE_SIZE_DEFAULT });
  });

  it("keeps a known status and drops an unknown one instead of failing the page", () => {
    expect(parseOperatorSubmissionQuery({ status: "downloaded" }).status).toBe("downloaded");
    expect(parseOperatorSubmissionQuery({ status: "under_review" }).status).toBe("under_review");
    expect(parseOperatorSubmissionQuery({ status: "submitted" }).status).toBe("submitted");
    expect(parseOperatorSubmissionQuery({ status: "archived" }).status).toBe("");
    expect(parseOperatorSubmissionQuery({ status: null }).status).toBe("");
  });

  it("bounds the page and the page size", () => {
    expect(parseOperatorSubmissionQuery({ page: "-4" }).page).toBe(1);
    expect(parseOperatorSubmissionQuery({ page: "not a number" }).page).toBe(1);
    expect(parseOperatorSubmissionQuery({ page: "3" }).page).toBe(3);
    expect(parseOperatorSubmissionQuery({ pageSize: "0" }).pageSize).toBe(1);
    expect(parseOperatorSubmissionQuery({ pageSize: "5000" }).pageSize).toBe(100);
    expect(parseOperatorSubmissionQuery({ pageSize: "abc" }).pageSize).toBe(OPERATOR_PAGE_SIZE_DEFAULT);
  });

  it("turns a page into one inclusive range", () => {
    expect(operatorPageRange(1, 25)).toEqual([0, 24]);
    expect(operatorPageRange(2, 25)).toEqual([25, 49]);
    expect(operatorPageRange(3, 10)).toEqual([20, 29]);
    expect(operatorPageRange(1, 1)).toEqual([0, 0]);
  });

  it("strips the characters PostgREST and ilike would read as syntax", () => {
    expect(operatorSearchTerm("  Gulf   Facade ")).toBe("Gulf Facade");
    expect(operatorSearchTerm("100%,(draft)")).toBe("100 draft");
    expect(operatorSearchTerm("a*b\\c\"d'e")).toBe("a b c d e");
    expect(operatorSearchTerm("x".repeat(200)).length).toBe(80);
  });

  it("searches the five columns an operator would type into", () => {
    const filter = operatorSearchFilter("Marina");
    expect(filter).toBe(
      [
        "submission_reference.ilike.%Marina%",
        "company_name.ilike.%Marina%",
        "project_name.ilike.%Marina%",
        "project_location.ilike.%Marina%",
        "customer_reference.ilike.%Marina%",
      ].join(","),
    );
    expect(operatorSearchFilter("   ")).toBeNull();
    expect(operatorSearchFilter("%")).toBeNull();
  });

  it("keeps a filter chain free of the wildcard a user could inject", () => {
    const filter = operatorSearchFilter("WEB-2026-000003");
    expect(filter).toContain("submission_reference.ilike.%WEB-2026-000003%");
    expect(filter).not.toContain("**");
    expect(filter?.match(/%/g)?.length).toBe(10);
  });
});
