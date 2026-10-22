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
const communitySqlV2 = read("../supabase-global-chat-v2-replies-receipts.sql");
const communityTypes = read("../src/types/global-chat.ts");
const accountWorkspace = read("../src/components/account/CustomerAccountWorkspace.tsx");
const profileHelpers = read("../src/lib/user-profile.ts");
const profileSql = read("../supabase-user-profiles.sql");
const nextConfig = read("../next.config.ts");
const supabaseLib = read("../src/lib/supabase.ts");
const websiteSession = read("../src/lib/website-session.ts");
const operatorSession = read("../src/lib/useOperatorSession.ts");

test("global account route recognizes a saved session before reusing the sign-in form", () => {
  assert.match(accountPage, /CustomerAccountWorkspace/);
  assert.match(accountWorkspace, /useWebsiteSession/);
  assert.match(websiteSession, /onAuthStateChange/);
  assert.match(websiteSession, /recoverWebsiteSession/);
  assert.match(accountWorkspace, /Checking your saved sign-in/);
  assert.match(accountWorkspace, /Saved sign-in found/);
  assert.match(accountWorkspace, /router\.replace\(redirectTo\)/);
  assert.match(accountWorkspace, /<ReportsAuthForm variant="account" defaultRedirect="\/account" \/>/);
  assert.match(authForm, /function safeRedirectPath/);
  assert.match(authForm, /export function safeAuthRedirect/);
  assert.match(authForm, /startsWith\("\/\/"\)/);
  assert.match(authForm, /includes\("\\\\"\)/);
  assert.match(authForm, /signupRequested \? "otp" : "password"/);
  assert.match(authForm, /Customer account/);
  assert.match(authForm, /Inquiries & chat/);
  assert.match(authForm, /name="email"/);
  assert.match(authForm, /name="password"/);
  assert.match(authForm, /Saved sign-in/);
  assert.match(authForm, /stays saved on this device and renews automatically/);
});

test("customer account dashboard manages the public chat profile and password without exposing private data", () => {
  assert.match(accountWorkspace, /Saved sign-in active/);
  assert.match(accountWorkspace, /Your WhatsApp-style chat profile/);
  assert.match(accountWorkspace, /Choose your profile icon/);
  assert.match(accountWorkspace, /AVATAR_ICON_OPTIONS/);
  assert.match(accountWorkspace, /AVATAR_COLOR_OPTIONS/);
  assert.match(accountWorkspace, /from\("user_profiles"\)/);
  assert.match(accountWorkspace, /upsert\(previewProfile, \{ onConflict: "user_id" \}\)/);
  assert.match(accountWorkspace, /auth\.updateUser\(\{\s*data:/);
  assert.match(accountWorkspace, /avatar_icon: saved\.avatar_icon/);
  assert.match(accountWorkspace, /Update password/);
  assert.match(accountWorkspace, /auth\.signOut\(\)/);
  assert.match(accountWorkspace, /customerInquiryChatPath\(\)/);
  assert.match(accountWorkspace, /href="\/chat"/);
  assert.match(accountWorkspace, /your email stays private/i);
  assert.doesNotMatch(accountWorkspace, /inquiry_customer_profiles|production-room|service_role/i);
});

test("user profile SQL is chat-safe, own-row writable, participant-readable and realtime enabled", () => {
  assert.match(profileSql, /CREATE TABLE IF NOT EXISTS public\.user_profiles/);
  assert.match(profileSql, /display_name text NOT NULL/);
  assert.match(profileSql, /avatar_icon text NOT NULL/);
  assert.match(profileSql, /avatar_color text NOT NULL/);
  assert.match(profileSql, /ALTER TABLE public\.user_profiles ENABLE ROW LEVEL SECURITY/);
  assert.match(profileSql, /REVOKE ALL ON TABLE public\.user_profiles FROM PUBLIC, anon/);
  assert.match(profileSql, /Users read own or community participant profiles/);
  assert.match(profileSql, /m\.author_user_id = public\.user_profiles\.user_id/);
  assert.match(profileSql, /r\.user_id = public\.user_profiles\.user_id/);
  assert.match(profileSql, /Users create own profile/);
  assert.match(profileSql, /Users update own profile/);
  assert.match(profileSql, /user_id = auth\.uid\(\)/);
  assert.match(profileSql, /ALTER TABLE public\.user_profiles REPLICA IDENTITY FULL/);
  assert.match(profileSql, /ALTER PUBLICATION supabase_realtime ADD TABLE public\.user_profiles/);
  assert.match(profileHelpers, /AVATAR_ICON_OPTIONS/);
  assert.match(profileHelpers, /normalizeUserProfile/);
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
  assert.match(communityWorkspace, /Every signed-in ProGlass user can read this room/);
  assert.match(communityWorkspace, /private inquiry chat/i);
  assert.match(communityHelpers, /GLOBAL_CHAT_MAX_BODY = 1000/);
  assert.match(communityHelpers, /mergeGlobalChatMessages/);
  assert.match(authForm, /redirectTo === "\/chat"/);
  assert.match(navbar, /Community Chat/);
  assert.match(navbar, /href: "\/chat"/);
  assert.match(siteNav, /label: "Community Chat", href: "\/chat"/);
  assert.match(bubble, /Open community chat/);
  assert.match(bubble, /href=\{href\("\/chat"\)\}/);
  assert.match(workspace, /href="\/chat"/);
  assert.match(workspace, /Community chat/);
});

test("community chat v2 SQL adds message replies and per-message receipts safely", () => {
  assert.match(communitySqlV2, /ADD COLUMN IF NOT EXISTS reply_to_message_id uuid REFERENCES public\.global_chat_messages\(id\)/);
  assert.match(communitySqlV2, /DROP FUNCTION IF EXISTS public\.post_global_chat_message\(text\)/);
  assert.match(communitySqlV2, /post_global_chat_message\(p_body text, p_reply_to_message_id uuid DEFAULT NULL\)/);
  assert.match(communitySqlV2, /no longer available/);
  assert.match(communitySqlV2, /CREATE TABLE IF NOT EXISTS public\.global_chat_receipts/);
  assert.match(communitySqlV2, /PRIMARY KEY \(message_id, user_id\)/);
  assert.match(communitySqlV2, /ALTER TABLE public\.global_chat_receipts ENABLE ROW LEVEL SECURITY/);
  assert.match(communitySqlV2, /REVOKE ALL ON TABLE public\.global_chat_receipts FROM anon/);
  assert.match(communitySqlV2, /REVOKE INSERT, UPDATE, DELETE ON TABLE public\.global_chat_receipts FROM authenticated/);
  assert.match(communitySqlV2, /Signed-in users read global chat receipts/);
  assert.match(communitySqlV2, /mark_global_chat_receipts_delivered\(p_message_ids uuid\[\]\)/);
  assert.match(communitySqlV2, /mark_global_chat_receipts_read\(p_message_ids uuid\[\]\)/);
  assert.match(communitySqlV2, /cardinality\(p_message_ids\) > 200/);
  assert.match(communitySqlV2, /m\.author_user_id <> auth\.uid\(\)/);
  assert.match(communitySqlV2, /COALESCE\(public\.global_chat_receipts\.read_at, EXCLUDED\.read_at\)/);
  assert.match(communitySqlV2, /ALTER TABLE public\.global_chat_receipts REPLICA IDENTITY FULL/);
  assert.match(communitySqlV2, /ALTER PUBLICATION supabase_realtime ADD TABLE public\.global_chat_receipts/);
  assert.doesNotMatch(communitySqlV2, /service_role/i);
});

test("community chat auth and realtime setup cannot restart or crash the room on duplicate events", () => {
  assert.match(communityWorkspace, /function sameChatUser/);
  assert.match(communityWorkspace, /sameChatUser\(current, nextUser\) \? current : nextUser/);
  assert.match(communityWorkspace, /activeUserIdRef/);
  assert.match(communityWorkspace, /realtimeSessionId/);
  assert.match(communityWorkspace, /global-community-chat-\$\{user\.id\}-\$\{realtimeSessionId\}/);
  assert.match(communityWorkspace, /Live updates could not start/);
  assert.match(communityWorkspace, /normalizeGlobalChatMessage\(payload\.new\)/);
  assert.match(communityWorkspace, /normalizeGlobalChatReceipt\(payload\.new\)/);
  assert.match(communityHelpers, /export function normalizeGlobalChatMessage/);
  assert.match(communityHelpers, /export function normalizeGlobalChatReceipt/);
  assert.match(communityHelpers, /export function normalizeGlobalChatReadState/);
  assert.match(nextConfig, /wss:\/\/afpbdyamedpanwijxghk\.supabase\.co/);
  assert.match(communityWorkspace, /realtime\.setAuth\(accessToken\)/);
  assert.match(communityWorkspace, /websiteSession\.accessToken/);
  assert.match(websiteSession, /getSession\(\)/);
  assert.match(communityWorkspace, /table: "user_profiles"/);
  assert.match(communityWorkspace, /status === "SUBSCRIBED".*refreshLatestRef\.current/s);
  assert.match(communityWorkspace, /window\.setInterval/);
  assert.match(communityWorkspace, /\}, 2000\)/);
  assert.match(communityWorkspace, /visibilitychange/);
  assert.match(communityWorkspace, /window\.addEventListener\("focus"/);
  assert.match(communityWorkspace, /window\.addEventListener\("online"/);
});

test("community chat is a compact right-aligned panel with visible receipt sync", () => {
  assert.match(communityWorkspace, /max-w-\[370px\]/);
  assert.match(communityWorkspace, /max-h-\[640px\]/);
  assert.match(communityWorkspace, /justify-end/);
  assert.match(communityWorkspace, /data-chat-size="compact-370x640"/);
  assert.match(communityWorkspace, /CHANNEL_ERROR/);
  assert.match(communityWorkspace, /Refresh messages now/);
  assert.match(communityWorkspace, /Edit profile/);
  assert.match(communityWorkspace, /markReceiptsReadUpTo\(readAt\)/);
  assert.match(communityWorkspace, /markVisibleMessagesRead/);
  assert.match(communityWorkspace, /scheduleVisibleMessagesRead/);
  assert.match(communityWorkspace, /Seen by \$\{receiptSummary\.readCount\} · Info/);
  assert.match(communityWorkspace, /Delivered to \$\{receiptSummary\.deliveredCount\} · Info/);
  assert.match(communityWorkspace, /ChatAvatar/);
  assert.match(communityWorkspace, /profileFor\(receipt\.user_id/);
});

test("account and every chat share one recoverable saved website session", () => {
  assert.match(supabaseLib, /supabaseAuthStorageKey/);
  assert.match(supabaseLib, /sb-\$\{projectRef\}-auth-token/);
  assert.match(supabaseLib, /persistSession: true/);
  assert.match(supabaseLib, /autoRefreshToken: true/);
  assert.match(websiteSession, /useSyncExternalStore/);
  assert.match(websiteSession, /recoverWebsiteSessionAfterUnauthorized/);
  assert.match(websiteSession, /refreshSavedSession/);
  assert.match(websiteSession, /window\.addEventListener\("storage"/);
  assert.match(websiteSession, /document\.addEventListener\("visibilitychange"/);
  assert.match(operatorSession, /useWebsiteSession/);
  assert.match(operatorSession, /requireAuth: session\.requireAuth/);
  assert.match(accountWorkspace, /useWebsiteSession\(\{ enabled: configured \}\)/);
  assert.match(communityWorkspace, /useWebsiteSession\(\{ enabled: configured \}\)/);
  assert.match(workspace, /sessionUnavailable/);
  assert.match(workspace, /Retry saved sign-in/);
  assert.match(bubble, /useWebsiteSession\(\{ enabled: open \}\)/);
  assert.match(bubble, /onUnauthorized=\{handleUnauthorized\}/);
  assert.match(panel, /onUnauthorized/);
  assert.match(panel, /r\.status===401/);
  assert.match(navbar, /href: "\/account"/);
});

test("community chat uses dense one-line bubbles sized for sixteen short messages", () => {
  assert.match(communityWorkspace, /data-visible-message-target="16"/);
  assert.match(communityWorkspace, /data-message-density="16-up"/);
  assert.match(communityWorkspace, /space-y-\[3px\]/);
  assert.match(communityWorkspace, /data-testid="community-chat-message-line"/);
  assert.match(communityWorkspace, /data-testid="community-chat-bubble"/);
  assert.match(communityWorkspace, /py-\[3px\]/);
  assert.match(communityWorkspace, /leading-\[1\.2\]/);
  assert.match(communityWorkspace, /isLastInGroup/);
  assert.match(communityWorkspace, /aria-label=\{own \? "Reply to your message"/);
  assert.doesNotMatch(communityWorkspace, /mt-1 flex flex-wrap items-center gap-x-3/);
});

test("community chat workspace supports WhatsApp-style replies, ticks and who-read-when info", () => {
  assert.match(communityTypes, /reply_to_message_id: string \| null/);
  assert.match(communityTypes, /GlobalChatReceipt/);
  assert.match(communityHelpers, /globalChatMessageExcerpt/);
  assert.match(communityHelpers, /formatGlobalChatDateTime/);
  assert.match(communityHelpers, /summarizeGlobalChatReceipts/);
  assert.match(communityHelpers, /groupGlobalChatReceiptsByMessage/);
  assert.match(communityWorkspace, /replyTarget/);
  assert.match(communityWorkspace, /Replying to/);
  assert.match(communityWorkspace, /Cancel reply/);
  assert.match(communityWorkspace, /p_reply_to_message_id: replyTarget\?\.id \?\? null/);
  assert.match(communityWorkspace, /jumpToQuoted/);
  assert.match(communityWorkspace, /Jump to the original message/);
  assert.match(communityWorkspace, /highlightedMessageId/);
  assert.match(communityWorkspace, /from\("global_chat_receipts"\)/);
  assert.match(communityWorkspace, /mark_global_chat_receipts_delivered/);
  assert.match(communityWorkspace, /mark_global_chat_receipts_read/);
  assert.match(communityWorkspace, /table: "global_chat_receipts"/);
  assert.match(communityWorkspace, /Seen by \$\{receiptSummary\.readCount\}/);
  assert.match(communityWorkspace, /Message info/);
  assert.match(communityWorkspace, /Read by/);
  assert.match(communityWorkspace, /Delivered to/);
  assert.match(communityWorkspace, /formatGlobalChatDateTime\(receipt\.read_at/);
  assert.match(communityWorkspace, /formatGlobalChatDateTime\(receipt\.delivered_at/);
});

test("community room searches the whole table, not only the loaded page", () => {
  assert.match(communityHelpers, /GLOBAL_CHAT_SEARCH_LIMIT = 30/);
  assert.match(communityHelpers, /GLOBAL_CHAT_SEARCH_MIN = 2/);
  assert.match(communityHelpers, /export function globalChatSearchPattern/);
  assert.match(communityHelpers, /\.replace\(\/%\/g, "\\\\%"\)/);
  assert.match(communityHelpers, /return `"%\$\{escaped\}%"`/);
  assert.match(communityWorkspace, /\.ilike\("body", globalChatSearchPattern\(normalizedSearchTerm\)\)/);
  assert.match(communityWorkspace, /\.is\("deleted_at", null\)/);
  assert.match(communityWorkspace, /\.limit\(GLOBAL_CHAT_SEARCH_LIMIT\)/);
  assert.match(communityWorkspace, /isGlobalChatSearchReady\(normalizedSearchTerm\)/);
  assert.doesNotMatch(communityWorkspace, /messages\.filter\(\(message\) => message\.body/);
  assert.match(communityWorkspace, /aria-expanded=\{searchOpen\}/);
  assert.match(communityWorkspace, /aria-controls="global-chat-search"/);
  assert.match(communityWorkspace, /Searches every message in the room, not only the loaded page/);
  assert.match(communityWorkspace, /revealSearchResult/);
});

test("a mention is highlighted only for the member it names", () => {
  assert.match(communityHelpers, /export function globalChatMentionPattern/);
  assert.match(communityHelpers, /export function mentionsGlobalChatUser/);
  assert.match(communityHelpers, /export function splitGlobalChatMentions/);
  assert.match(communityHelpers, /\(\?!\[\\\\p\{L\}\\\\p\{N\}_\]\)/);
  assert.match(communityWorkspace, /function MentionedBody/);
  assert.match(communityWorkspace, /splitGlobalChatMentions\(body, displayName\)/);
  assert.match(communityWorkspace, /\(mentions you\)/);
  assert.match(communityWorkspace, /displayName=\{currentProfile\?\.display_name \?\? ""\}/);
});
