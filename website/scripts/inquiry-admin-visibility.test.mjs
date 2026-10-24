import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

/*
 * An authenticated inquiry_admin could not see all seven complete website
 * submissions. The database was never the reason: Row Level Security returned
 * seven rows for that identity, the list query has no status filter, and both
 * the detail read and the document download are authorized for an operator.
 *
 * What the old operator UI did was fetch at most two hundred rows and narrow
 * them in the browser, offer five of the six statuses in its filter (downloaded
 * was missing), print no total, and hide the whole queue behind one small link
 * inside an inbox that lists the desktop register, which is empty until the
 * first handoff. These checks keep the fix in place.
 */

const read = (path) => readFileSync(new URL(path, import.meta.url), "utf8");

const register = read("../src/components/inquiries/CustomerSubmissionRegister.tsx");
const review = read("../src/components/inquiries/CustomerSubmissionReview.tsx");
const tabs = read("../src/components/inquiries/SubmissionStatusTabs.tsx");
const pager = read("../src/components/inquiries/SubmissionPager.tsx");
const panel = read("../src/components/inquiries/WebsiteSubmissionsPanel.tsx");
const dashboard = read("../src/components/inquiries/InquiriesDashboard.tsx");
const api = read("../src/lib/inquiry-submissions-api.ts");
const listRoute = read("../src/app/api/inquiry-submissions/operator/route.ts");
const detailRoute = read("../src/app/api/inquiry-submissions/operator/[id]/route.ts");
const types = read("../src/types/inquiry-submissions.ts");
const submissionsPage = read("../src/app/[locale]/inquiries/customer-submissions/page.tsx");

const operatorSection = api.slice(
  api.indexOf("export async function listOperatorCustomerInquiries"),
  api.indexOf("export async function reviewOperatorCustomerInquiry"),
);

test("one status vocabulary, all six values, and downloaded among them", () => {
  assert.match(types, /export const CUSTOMER_INQUIRY_STATUSES = \["submitted", "under_review", "accepted", "downloaded", "rejected", "closed"\] as const;/);
  assert.match(types, /export const CUSTOMER_INQUIRY_REVIEW_STATUSES = \["under_review", "accepted", "rejected", "closed"\] as const;/);
  assert.match(tabs, /CUSTOMER_INQUIRY_STATUSES\.map/);
  assert.match(register, /isCustomerInquiryStatus/);
  // No screen may type the status list out again, which is how downloaded went missing.
  for (const source of [register, tabs, panel, review]) {
    assert.doesNotMatch(source, /"submitted",\s*"under_review",\s*"accepted",\s*"rejected",\s*"closed"/);
  }
});

test("the queue is filtered, searched, counted and paged by the database", () => {
  assert.match(operatorSection, /\.eq\("upload_state", "complete"\)/);
  assert.match(operatorSection, /if \(query\.status\) request = request\.eq\("status", query\.status\);/);
  assert.match(operatorSection, /if \(searchFilter\) request = request\.or\(searchFilter\);/);
  assert.match(operatorSection, /\.range\(from, to\)/);
  assert.match(operatorSection, /\{ count: "exact" \}/);
  assert.match(api, /CUSTOMER_INQUIRY_STATUSES\.map\(async \(status\) =>/);
  assert.match(api, /\{ count: "exact", head: true \}/);
  // The old shape pulled two hundred rows and narrowed them in the browser.
  assert.doesNotMatch(operatorSection, /\.limit\(200\)/);
  assert.doesNotMatch(register, /rows\.filter\(/);
});

test("the route forwards status, search, page and page size to that query", () => {
  assert.match(listRoute, /listOperatorCustomerInquiries\(token\(request\),\{/);
  assert.match(listRoute, /status:params\.get\("status"\)/);
  assert.match(listRoute, /q:params\.get\("q"\)/);
  assert.match(listRoute, /page:params\.get\("page"\)/);
  assert.match(listRoute, /pageSize:params\.get\("pageSize"\)/);
  assert.match(listRoute, /"X-Robots-Tag":"noindex, nofollow"/);
  // Same token-bound, same-origin, complete-only contract as before the fix.
  assert.match(detailRoute, /sec-fetch-site/);
  assert.match(api, /inquiry_operator_roles/);
  assert.match(api, /\["inquiry_auditor", "inquiry_admin"\]/);
});

test("the register shows the tally, the range, and every status it was given", () => {
  assert.match(register, /SubmissionStatusTabs/);
  assert.match(register, /SubmissionPager/);
  assert.match(register, /formatNumber\(counts\.submitted\)/);
  assert.match(register, /formatNumber\(counts\.under_review\)/);
  assert.match(register, /formatNumber\(counts\.downloaded\)/);
  assert.match(pager, /complete submission/);
  assert.match(pager, /onPage\(page \+ 1\)/);
  assert.match(register, /statusTone\(r\.status\)/);
  assert.match(register, /href=\{`\/inquiries\/customer-submissions\/\$\{r\.id\}`\}/);
  // Opening a submission is a link, never a status write.
  assert.doesNotMatch(register, /method: "PATCH"/);
  assert.doesNotMatch(register, /method: "POST"/);
});

test("a filter can be linked to, so the inbox can send an operator straight to it", () => {
  assert.match(submissionsPage, /initialStatus/);
  assert.match(submissionsPage, /searchParams/);
  assert.match(submissionsPage, /initialPage/);
  assert.match(register, /initialStatus\?: string/);
  assert.match(register, /initialPage\?: number/);
  assert.match(register, /params\.set\("status", status\)/);
});

test("the desktop inbox announces the website queue instead of looking empty", () => {
  assert.match(dashboard, /WebsiteSubmissionsPanel/);
  assert.match(panel, /\/inquiries\/customer-submissions\?status=\$\{status\}/);
  assert.match(panel, /complete in the website queue/);
  assert.match(panel, /Open the website queue/);
  // A failed tally never breaks the page it decorates.
  assert.match(panel, /if \(!response\.ok\) return;/);
});

test("review stays explicit: no auto-accept, no silent re-queue, no auto-download", () => {
  assert.match(review, /CUSTOMER_INQUIRY_REVIEW_STATUSES/);
  assert.match(review, /reviewSelection/);
  assert.match(review, /if \(!status\) \{/);
  assert.match(review, /already handed to the desktop/);
  assert.match(review, /cannot be reopened/);
  // Exactly one PATCH exists, and it is the one the operator presses.
  assert.equal((review.match(/method: "PATCH"/g) ?? []).length, 1);
  // Documents download through the operator's own session and the existing route.
  assert.match(review, /\/api\/inquiry-submissions\/\$\{item\.id\}\/documents\/\$\{document\.id\}/);
  assert.match(review, /Authorization:`Bearer \$\{authToken\}`/);
  assert.match(review, /Download/);
});

test("no access was widened to make the rows appear", () => {
  for (const source of [register, review, tabs, pager, panel, dashboard, api, listRoute, detailRoute, types]) {
    assert.doesNotMatch(source, /service_role/i);
    assert.doesNotMatch(source, /DISABLE ROW LEVEL SECURITY/i);
    assert.doesNotMatch(source, /USING \(true\)/i);
    assert.doesNotMatch(source, /SUPABASE_SERVICE/i);
  }
});
