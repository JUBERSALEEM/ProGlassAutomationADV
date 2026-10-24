import { describe, expect, it } from "vitest";
import {
  INQUIRY_PIPELINE_STATUSES,
  formatInquiryPipelineTime,
  hasInquiryPipelineStatus,
  inquiryHandoffLabel,
  inquiryHandoffSummary,
  inquiryPipelineLabel,
  inquiryPipelineSummary,
  isInquiryHandedOff,
  isInquiryPipelineStatus,
  withoutPipelineFields,
} from "../src/lib/inquiry-pipeline";

describe("inquiry pipeline status", () => {
  it("accepts exactly what the database function accepts", () => {
    for (const status of INQUIRY_PIPELINE_STATUSES) expect(isInquiryPipelineStatus(status)).toBe(true);
    for (const other of ["", "Prepared", "shipped", "sent ", null, undefined, 3, {}]) {
      expect(isInquiryPipelineStatus(other)).toBe(false);
    }
  });

  it("labels each status in the shop's words", () => {
    expect(inquiryPipelineLabel("prepared")).toBe("Prepared");
    expect(inquiryPipelineLabel("hold")).toBe("On hold");
    expect(inquiryPipelineLabel("cancelled")).toBe("Cancelled");
  });

  it("shows an unexpected value plainly rather than hiding it", () => {
    expect(inquiryPipelineLabel("archived")).toBe("archived");
    expect(inquiryPipelineLabel(null)).toBe("");
  });
});

describe("inquiry pipeline display", () => {
  it("treats an inquiry the desktop never reported on as empty", () => {
    expect(hasInquiryPipelineStatus(null)).toBe(false);
    expect(hasInquiryPipelineStatus({})).toBe(false);
    expect(hasInquiryPipelineStatus({ pipeline_status: null })).toBe(false);
    expect(hasInquiryPipelineStatus({ pipeline_status: "sent" })).toBe(true);
  });

  it("returns an empty string instead of a placeholder glyph for missing times", () => {
    expect(formatInquiryPipelineTime(null)).toBe("");
    expect(formatInquiryPipelineTime("")).toBe("");
    expect(formatInquiryPipelineTime("not a date")).toBe("");
  });

  it("formats the update time in shop time", () => {
    const formatted = formatInquiryPipelineTime("2026-10-23T10:00:00.000Z", "en-GB");
    expect(formatted).toContain("23 Oct 2026");
    // 10:00 UTC is 14:00 in Dubai, whatever the machine running the test believes.
    expect(formatted).toContain("14:00");
  });

  it("builds one line from status, note and time", () => {
    expect(
      inquiryPipelineSummary(
        {
          pipeline_status: "prepared",
          pipeline_note: "Cut list checked",
          pipeline_updated_at: "2026-10-23T10:00:00.000Z",
        },
        "en-GB",
      ),
    ).toBe("Prepared, Cut list checked, updated 23 Oct 2026, 14:00");
  });

  it("omits a blank note and a missing time", () => {
    expect(inquiryPipelineSummary({ pipeline_status: "sent", pipeline_note: "   " }, "en-GB")).toBe("Sent");
    expect(inquiryPipelineSummary({})).toBe("");
  });
});

describe("customer-facing payloads", () => {
  it("strips the four workflow columns and keeps everything else", () => {
    const row = {
      id: "s1",
      submission_reference: "WEB-2026-000007",
      status: "accepted",
      pipeline_status: "prepared",
      pipeline_note: "internal",
      pipeline_updated_at: "2026-10-23T10:00:00.000Z",
      pipeline_updated_by: "0568fecb-0f8e-4f1d-9d77-845a5be0cbbc",
    };
    const cleaned = withoutPipelineFields(row);
    expect(cleaned).toEqual({
      id: "s1",
      submission_reference: "WEB-2026-000007",
      status: "accepted",
    });
    // The source row is untouched, so an operator response can still read it.
    expect(row.pipeline_status).toBe("prepared");
  });
});

describe("desktop handoff", () => {
  it("separates the queue from the handoff", () => {
    expect(inquiryHandoffLabel({ status: "accepted" })).toBe("Ready for desktop");
    expect(inquiryHandoffLabel({ status: "downloaded" })).toBe("Handed to desktop");
    expect(isInquiryHandedOff({ status: "downloaded" })).toBe(true);
    expect(isInquiryHandedOff({ status: "accepted" })).toBe(false);
  });

  it("says nothing for a submission the desktop has not reached", () => {
    for (const status of ["submitted", "under_review", "rejected", "closed", "", null, undefined]) {
      expect(inquiryHandoffLabel({ status })).toBe("");
      expect(inquiryHandoffSummary({ status })).toBe("");
    }
  });

  it("dates the handoff in shop time", () => {
    const line = inquiryHandoffSummary(
      { status: "downloaded", downloaded_at: "2026-10-23T10:00:00.000Z" },
      "en-GB",
    );
    expect(line).toBe("Handed to desktop, 23 Oct 2026, 14:00");
  });

  it("does not invent a time when the stamp is missing", () => {
    expect(inquiryHandoffSummary({ status: "downloaded" }, "en-GB")).toBe("Handed to desktop");
  });
});
