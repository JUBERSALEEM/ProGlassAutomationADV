// @vitest-environment node
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CustomerInquiryStatus } from "../src/types/inquiry-submissions";

/*
 * A stand-in for PostgREST, strict enough to prove the server's query: it
 * records the filters, the range and the head-counts the API asks for, and
 * answers from a fixture that mirrors production (seven complete submissions,
 * four submitted, one under review, two downloaded).
 */
type Row = Record<string, unknown> & {
  id: string;
  submission_reference: string;
  status: CustomerInquiryStatus;
  submitted_at: string;
  company_name: string;
  project_name: string;
  project_location: string;
  customer_reference: string;
  upload_state: string;
  inquiry_customer_documents: Array<{ id: string }>;
};

function makeRow(index: number, status: CustomerInquiryStatus, uploaded: string, documents: number): Row {
  return {
    id: `00000000-0000-4000-8000-${String(index).padStart(12, "0")}`,
    submission_reference: `WEB-2026-${String(index).padStart(6, "0")}`,
    contact_name: "Contact",
    company_name: `Company ${index}`,
    phone: "+971 50 000 0000",
    customer_reference: `REF-${index}`,
    attention_name: "",
    vat_trn_number: "",
    salesman_reference: "",
    handover_estimator_name: "",
    project_id: null,
    project_name: `Project ${index}`,
    project_location: "Sharjah",
    inquiry_type: "quotation",
    description: "Internal description",
    status,
    submitted_at: uploaded,
    status_updated_at: uploaded,
    accepted_at: null,
    downloaded_at: status === "downloaded" ? uploaded : null,
    closed_at: null,
    estimated_response_at: null,
    operator_note: "",
    pipeline_status: null,
    pipeline_note: null,
    pipeline_updated_at: null,
    pipeline_updated_by: null,
    upload_state: "complete",
    inquiry_customer_documents: Array.from({ length: documents }, (_, doc) => ({ id: `doc-${index}-${doc}` })),
  };
}

const DATA: Row[] = [
  makeRow(3, "downloaded", "2026-10-16T14:02:11.000Z", 13),
  makeRow(2, "submitted", "2026-10-16T08:22:16.000Z", 1),
  makeRow(7, "downloaded", "2026-10-17T11:19:40.000Z", 3),
  makeRow(6, "under_review", "2026-10-17T07:00:19.000Z", 1),
  makeRow(5, "submitted", "2026-10-17T06:53:14.000Z", 1),
  makeRow(4, "submitted", "2026-10-17T05:51:40.000Z", 3),
  makeRow(1, "submitted", "2026-10-16T07:14:21.000Z", 1),
];

type Call = { table: string; select?: string; head: boolean; countRequested: boolean; eqs: Array<[string, unknown]>; or?: string; order: string[]; range?: [number, number] };

const calls: Call[] = [];

class FakeQuery {
  private call: Call;
  constructor(table: string) {
    this.call = { table, head: false, countRequested: false, eqs: [], order: [] };
  }
  select(_columns?: string, options?: { count?: string; head?: boolean }) {
    this.call.select = _columns;
    this.call.head = Boolean(options?.head);
    this.call.countRequested = Boolean(options?.count);
    return this;
  }
  eq(column: string, value: unknown) {
    this.call.eqs.push([column, value]);
    return this;
  }
  or(expression: string) {
    this.call.or = expression;
    return this;
  }
  order(column: string) {
    this.call.order.push(column);
    return this;
  }
  range(from: number, to: number) {
    this.call.range = [from, to];
    return this;
  }
  maybeSingle() {
    calls.push(this.call);
    if (this.call.table === "inquiry_operator_roles") {
      return Promise.resolve({ data: { role: "inquiry_admin", enabled: true }, error: null });
    }
    const rows = this.matching();
    return Promise.resolve(rows.length === 1 ? { data: rows[0], error: null } : { data: null, error: null });
  }
  then(resolve: (value: unknown) => unknown, reject?: (reason: unknown) => unknown) {
    calls.push(this.call);
    return Promise.resolve(this.answer()).then(resolve, reject);
  }
  private matching(): Row[] {
    const or = this.call.or;
    const terms = or
      ? or.split(",").map((piece) => {
          const pattern = piece.slice(piece.indexOf(".ilike.") + ".ilike.".length);
          return pattern.replaceAll("%", "").toLowerCase();
        })
      : [];
    return DATA.filter((row) => {
      if (row.upload_state !== "complete") return false;
      for (const [column, value] of this.call.eqs) {
        if (column === "upload_state" || column === "status") {
          if (row[column] !== value) return false;
        }
      }
      if (or) {
        const haystack = [row.submission_reference, row.company_name, row.project_name, row.project_location, row.customer_reference]
          .map((value) => String(value).toLowerCase())
          .join(" ");
        if (!terms.every((term) => haystack.includes(term))) return false;
      }
      return true;
    });
  }
  private answer() {
    const rows = this.matching().sort((left, right) =>
      String(right.submitted_at).localeCompare(String(left.submitted_at)) || String(right.submission_reference).localeCompare(String(left.submission_reference)),
    );
    if (this.call.head) return { data: null, count: rows.length, error: null };
    const [from, to] = this.call.range ?? [0, rows.length - 1];
    return { data: rows.slice(from, to + 1), count: this.call.countRequested ? rows.length : null, error: null };
  }
}

const client = {
  auth: { getUser: async () => ({ data: { user: { id: "ee646814-0d2f-47b0-be96-68fcb51dda54" } }, error: null }) },
  from: (table: string) => new FakeQuery(table),
};

vi.mock("../src/lib/supabase", () => ({
  isSupabaseConfigured: () => true,
  isSupabaseUnavailableError: () => false,
  supabaseServerWithToken: () => client,
}));

import { listOperatorCustomerInquiries } from "../src/lib/inquiry-submissions-api";

/** The one list request, not the role read and not the six head counts. */
function listCall(): Call | undefined {
  return calls.filter((call) => call.table === "inquiry_customer_submissions" && !call.head).at(-1);
}

beforeEach(() => {
  calls.length = 0;
});

describe("the operator submission query", () => {
  it("returns every complete submission and the counts each tab prints", async () => {
    const page = await listOperatorCustomerInquiries("admin-token");

    expect(page.rows).toHaveLength(7);
    expect(page.total).toBe(7);
    expect(page.allTotal).toBe(7);
    expect(page.page).toBe(1);
    expect(page.pageSize).toBe(25);
    expect(page.pageCount).toBe(1);
    expect(page.counts).toEqual({ submitted: 4, under_review: 1, accepted: 0, downloaded: 2, rejected: 0, closed: 0 });

    // The list request itself carries the six-status tally as separate counts.
    const list = listCall();
    expect(list?.eqs).toEqual([["upload_state", "complete"]]);
    expect(list?.order).toEqual(["submitted_at", "submission_reference"]);
    expect(list?.range).toEqual([0, 24]);
    const counts = calls.filter((call) => call.table === "inquiry_customer_submissions" && call.head);
    expect(counts).toHaveLength(6);
    expect(counts.map((call) => call.eqs.find(([column]) => column === "status")?.[1])).toEqual([
      "submitted",
      "under_review",
      "accepted",
      "downloaded",
      "rejected",
      "closed",
    ]);
  });

  it("filters by a status in the database, not in the browser", async () => {
    const page = await listOperatorCustomerInquiries("admin-token", { status: "under_review" });
    expect(page.rows).toHaveLength(1);
    expect(page.rows[0]?.submission_reference).toBe("WEB-2026-000006");
    expect(page.total).toBe(1);
    expect(page.counts.submitted).toBe(4);

    const downloaded = await listOperatorCustomerInquiries("admin-token", { status: "downloaded" });
    expect(downloaded.rows.map((row) => row.submission_reference)).toEqual(["WEB-2026-000007", "WEB-2026-000003"]);

    const submitted = await listOperatorCustomerInquiries("admin-token", { status: "submitted" });
    expect(submitted.rows).toHaveLength(4);
  });

  it("pages with a range and reports the page count", async () => {
    const page = await listOperatorCustomerInquiries("admin-token", { page: 2, pageSize: 2 });
    expect(page.rows.map((row) => row.submission_reference)).toEqual(["WEB-2026-000005", "WEB-2026-000004"]);
    expect(page.total).toBe(7);
    expect(page.pageCount).toBe(4);
    expect(listCall()?.range).toEqual([2, 3]);
  });

  it("searches the five operator columns with a sanitised pattern", async () => {
    const page = await listOperatorCustomerInquiries("admin-token", { q: "WEB-2026-000003" });
    expect(page.rows.map((row) => row.submission_reference)).toEqual(["WEB-2026-000003"]);
    const filter = listCall()?.or;
    expect(filter).toContain("submission_reference.ilike.%WEB-2026-000003%");
    expect(filter).not.toContain("**");

    const bracketed = await listOperatorCustomerInquiries("admin-token", { q: "Company 2, (draft)" });
    expect(bracketed.rows).toHaveLength(0);
    const sanitised = listCall()?.or;
    expect(sanitised).toContain("Company 2 draft");
    expect(sanitised).not.toContain("(");
  });

  it("shapes a list row for the register without internal fields", async () => {
    const page = await listOperatorCustomerInquiries("admin-token", { status: "downloaded", pageSize: 10 });
    const row = page.rows[0];
    expect(row?.document_count).toBe(3);
    expect(row).not.toHaveProperty("inquiry_customer_documents");
    expect(row).not.toHaveProperty("description");
    expect(row).not.toHaveProperty("phone");
  });

  it("clamps an out of range page size instead of scanning the table", async () => {
    const page = await listOperatorCustomerInquiries("admin-token", { pageSize: 5000, page: -3 });
    expect(page.pageSize).toBe(100);
    expect(page.page).toBe(1);
    expect(listCall()?.range).toEqual([0, 99]);
  });
});
