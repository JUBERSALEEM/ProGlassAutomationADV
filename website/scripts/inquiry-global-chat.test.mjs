import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = (path) => readFileSync(new URL(path, import.meta.url), "utf8");

const accountPage = read("../src/app/[locale]/account/page.tsx");
const authForm = read("../src/components/reports/ReportsAuthForm.tsx");
const chatPage = read("../src/app/[locale]/inquiries/chat/page.tsx");
const workspace = read("../src/components/inquiries/CustomerInquiryChatWorkspace.tsx");
const bubble = read("../src/components/support/SupportBubble.tsx");
const panel = read("../src/components/inquiries/InquiryCommunicationPanel.tsx");
const listHook = read("../src/lib/useCustomerInquiries.ts");
const helpers = read("../src/lib/customer-inquiry-chat.ts");
const navbar = read("../src/components/layout/Navbar.tsx");
const siteNav = read("../src/lib/site.ts");
const footer = read("../src/components/layout/Footer.tsx");
const detail = read("../src/components/inquiries/CustomerInquiryDetail.tsx");
const portal = read("../src/components/inquiries/CustomerInquiryPortal.tsx");
const communityPage = read("../src/app/[locale]/chat/page.tsx");
const communityWorkspace = read("../src/components/chat/GlobalChatWorkspace.tsx");
const communityHelpers = read("../src/lib/global-chat.ts");
const communitySql = read("../supabase-global-chat.sql");

test("global account route reuses the existing Supabase form with a safe inquiry-chat return", () => {
  assert.match(accountPage, /ReportsAuthForm variant="account" defaultRedirect="\/inquiries\/chat"/);
  assert.match(authForm, /function safeRedirectPath/);
  assert.match(authForm, /startsWith\("\/\/"\)/);
  assert.match(authForm, /includes\("\\\\"\)/);
  assert.match(authForm, /signupRequested \? "otp" : "password"/);
  assert.match(authForm, /Customer account/);
  assert.match(authForm, /Inquiries & chat/);
});

test("customer chat lists only through the token-bound customer inquiries API hook", () => {
  assert.match(listHook, /fetch\("\/api\/inquiry-submissions"/);
  assert.match(listHook, /Authorization: `Bearer \$\{authToken\}`/);
  assert.match(listHook, /cache: "no-store"/);
  assert.match(listHook, /response\.status === 401/);
  assert.match(workspace, /useCustomerInquiries\(authToken/);
  assert.match(bubble, /enabled: open && Boolean\(authToken\)/);
  assert.doesNotMatch(`${listHook}\n${workspace}\n${bubble}`, /service.?role|SERVICE_ROLE/i);
});

test("workspace and bubble wire chat to the selected own inquiry reference", () => {
  assert.match(chatPage, /initialInquiryId/);
  assert.match(workspace, /submission_reference/);
  assert.match(workspace, /submissionId=\{selected\.id\}/);
  assert.match(workspace, /internalAccess=\{false\}/);
  assert.match(bubble, /submissionId=\{selectedInquiry\.id\}/);
  assert.match(bubble, /internalAccess=\{false\}/);
  assert.match(bubble, /customerInquiryChatPath\(selectedInquiry\.id\)/);
  assert.match(detail, /\/inquiries\/chat\?inquiry=/);
  assert.match(panel, /onCommunicationsChanged/);
  assert.match(panel, /onCommunicationsChanged\?\.\(j\.data\)/);
});

test("customer status pack covers inquiry totals, selected status, documents and chat activity", () => {
  for (const label of [
    "Your inquiries",
    "Submitted",
    "Under review",
    "Accepted / downloaded",
    "Closed / rejected",
    "Current status",
    "Estimated response",
    "Private documents",
    "Chat messages",
    "Open info requests",
    "Last chat activity",
  ]) {
    assert.match(workspace, new RegExp(label.replaceAll("/", "\\/")));
  }
  assert.match(helpers, /countCustomerInquiriesByStatus/);
  assert.match(helpers, /acceptedOrDownloaded/);
  assert.match(helpers, /closedOrRejected/);
  assert.match(helpers, /missing_information_request/);
  assert.match(bubble, /counts\.acceptedOrDownloaded/);
  assert.match(bubble, /communicationStats\.openInformationRequests/);
});

test("signed-out visitors are sent through account before private inquiries or messages", () => {
  assert.match(workspace, /accountPathForRedirect\("\/inquiries\/chat"\)/);
  assert.match(workspace, /accountPathForRedirect\("\/inquiries\/chat", "signup"\)/);
  assert.match(bubble, /accountPathForRedirect\("\/inquiries\/chat", "signup"\)/);
  assert.match(portal, /\/account\?redirect=%2Finquiries%2Fsubmit/);
  assert.match(detail, /\/account\?redirect=/);
  assert.doesNotMatch(`${workspace}\n${bubble}`, /production-room|Production room/i);
});

test("main chrome exposes account and inquiry chat without claiming the website has no sign-in", () => {
  assert.match(navbar, /Customer Account/);
  assert.match(navbar, /My Inquiries & Chat/);
  assert.match(navbar, /Customer sign in \/ sign up/);
  assert.match(siteNav, /\/inquiries\/chat/);
  assert.match(siteNav, /\/inquiries\/submit/);
  assert.match(footer, /optional website account is only for secure customer inquiries/);
  assert.doesNotMatch(footer, /nothing to sign in to on this website/);
  assert.match(bubble, /WhatsApp and email open outside this website/);
  assert.doesNotMatch(bubble, /Nothing here is stored/);
});

test("community chat SQL is signed-in only, RLS protected, RPC written and realtime enabled", () => {
  assert.match(communitySql, /CREATE TABLE IF NOT EXISTS public\.global_chat_messages/);
  assert.match(communitySql, /CREATE TABLE IF NOT EXISTS public\.global_chat_reads/);
  assert.match(communitySql, /CREATE TABLE IF NOT EXISTS public\.global_chat_events/);
  assert.match(communitySql, /ALTER TABLE public\.global_chat_messages ENABLE ROW LEVEL SECURITY/);
  assert.match(communitySql, /ALTER TABLE public\.global_chat_reads ENABLE ROW LEVEL SECURITY/);
  assert.match(communitySql, /ALTER TABLE public\.global_chat_events ENABLE ROW LEVEL SECURITY/);
  assert.match(communitySql, /Signed-in users read global chat/);
  assert.match(communitySql, /auth\.uid\(\) IS NOT NULL/);
  assert.match(communitySql, /REVOKE ALL ON TABLE public\.global_chat_messages, public\.global_chat_reads, public\.global_chat_events FROM anon/);
  assert.match(communitySql, /REVOKE INSERT, UPDATE, DELETE ON TABLE public\.global_chat_messages, public\.global_chat_reads, public\.global_chat_events FROM authenticated/);
  assert.match(communitySql, /post_global_chat_message\(p_body text\)/);
  assert.match(communitySql, /delete_global_chat_message\(p_message_id uuid\)/);
  assert.match(communitySql, /mark_global_chat_read\(p_last_read_at timestamptz/);
  assert.match(communitySql, /SECURITY DEFINER/);
  assert.match(communitySql, /has_inquiry_role\(ARRAY\['inquiry_admin'\]\)/);
  assert.match(communitySql, /You are sending messages too quickly/);
  assert.match(communitySql, /Message limit reached/);
  assert.match(communitySql, /ALTER TABLE public\.global_chat_messages REPLICA IDENTITY FULL/);
  assert.match(communitySql, /ALTER PUBLICATION supabase_realtime ADD TABLE public\.global_chat_messages/);
  assert.doesNotMatch(communitySql, /service_role/i);
});

test("community chat workspace is browser-direct realtime with unread handling and signed-in gates", () => {
  assert.match(communityPage, /GlobalChatWorkspace/);
  assert.match(communityPage, /path: "\/chat"/);
  assert.match(communityWorkspace, /accountPathForRedirect\(GLOBAL_CHAT_PATH\)/);
  assert.match(communityWorkspace, /accountPathForRedirect\(GLOBAL_CHAT_PATH, "signup"\)/);
  assert.match(communityWorkspace, /global-community-chat-/);
  assert.match(communityWorkspace, /postgres_changes/);
  assert.match(communityWorkspace, /table: "global_chat_messages"/);
  assert.match(communityWorkspace, /rpc\("post_global_chat_message"/);
  assert.match(communityWorkspace, /rpc\("delete_global_chat_message"/);
  assert.match(communityWorkspace, /rpc\("mark_global_chat_read"/);
  assert.match(communityWorkspace, /from\("global_chat_reads"\)/);
  assert.match(communityWorkspace, /countUnreadGlobalChatMessages/);
  assert.match(communityWorkspace, /firstUnreadGlobalChatMessageId/);
  assert.match(communityWorkspace, /Load older messages/);
  assert.match(communityWorkspace, /Unread messages/);
  assert.match(communityWorkspace, /Jump to latest/);
  assert.match(communityWorkspace, /visible to all signed-in ProGlass users/);
  assert.match(communityWorkspace, /private inquiry chat/i);
  assert.match(communityHelpers, /GLOBAL_CHAT_MAX_BODY = 1000/);
  assert.match(communityHelpers, /mergeGlobalChatMessages/);
  assert.match(authForm, /redirectTo === "\/chat"/);
  assert.match(navbar, /Community Chat/);
  assert.match(navbar, /href: "\/chat"/);
  assert.match(siteNav, /label: "Community Chat", href: "\/chat"/);
  assert.match(bubble, /Open community chat/);
  assert.match(bubble, /accountPathForRedirect\("\/chat"\)/);
  assert.match(workspace, /href="\/chat"/);
  assert.match(workspace, /Community chat/);
});
