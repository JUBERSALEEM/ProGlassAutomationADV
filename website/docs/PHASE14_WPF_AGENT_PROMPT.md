# Phase 14: master prompt for the desktop agent

Copy everything between the fences into the agent session connected to
`JUBERSALEEM/ProglassAutomation3.0`. It is self-contained: the desktop agent does
not need this repository.

```
You are working on the WPF desktop app in JUBERSALEEM/ProglassAutomation3.0,
branch arena/da841e06-proglassautomation3-0. The website repository
(JUBERSALEEM/ProGlassAutomationADV) is READ-ONLY context for you. Never modify it,
never add its files here, and never put a service_role key, a password or a JWT
in code, logs or config.

The shop PC syncs the customer inquiry inbox from the website's Supabase project
afpbdyamedpanwijxghk through Inquiry/Services/Inquiry/InquiryInbox. Sign-in is
email/password on /auth/v1/token?grant_type=password. The delivery loop is three
PostgREST calls plus a Storage download:

  1. POST /rest/v1/rpc/plan_customer_inquiry_deliveries_v2   { p_limit }
  2. POST /rest/v1/rpc/plan_customer_inquiry_delivery_documents { p_submission_id }
  3. GET  /storage/v1/object/{bucket}/{key}
  4. POST /rest/v1/rpc/acknowledge_customer_inquiry_delivery_v2
         { p_submission_id, p_manifest_sha256, p_document_count,
           p_routing_key, p_assignment_snapshot }

Optional, both already deployed: update_customer_inquiry_status_v1 and
list_customer_inquiry_communications_v1.

Server side as of today (2026-10-24). Do not try to change any of it.

  * Phase 13 added get_my_inquiry_access_v1(), a self-report that returns
    { authenticated, user_id, email, roles text[], capabilities text[],
      denial_reason }. It returns the caller's own identity only. Call it
      immediately after sign-in.
  * Phase 13 also added list_customer_inquiry_inbox_v1, an operator inbox with
    database-side scope. The desktop does not need it.
  * Phase 14 (applied today) widened the three delivery calls from
    inquiry_bridge alone to inquiry_bridge OR inquiry_admin, so the owner's own
    admin account can drive this PC. Everything else is unchanged: accepted
    only, one acknowledgement per submission, routing key derived from the
    database, document count and assignment snapshot verified server side.
  * Refusals raise SQLSTATE 42501, which PostgREST reports as HTTP 403 with a
    JSON body whose message is "Inquiry bridge or admin role required."
    A bad email/password is HTTP 400 with message "Invalid login credentials".
    Those are two different problems and must never share one message.

There are four defects to fix. Fix them in this order and keep each change
small, testable and free of new dependencies.

DEFECT 1: "Website check failed: Cannot access a disposed object. Object name:
System.Net.HttpClient."

Reproduction: open Inquiries, Inbox, sign in, let the one-minute timer or the
"Check now" button run after the window or the tab has been through a close or
a reload. The message comes from the generic catch in
InquiryInboxViewModel.CheckNowAsync, which wraps the exception thrown inside
InquiryInboxService.SynchronizeAsync, which called through
InquiryInboxClient._http.

Cause: InquiryInboxClient owns a single HttpClient
(Inquiry/Services/Inquiry/InquiryInbox/InquiryInboxClient.cs, constructor sets
Timeout 120s) and InquiryInboxService owns one InquiryInboxClient
(Inquiry/Services/Inquiry/InquiryInbox/InquiryInboxService.cs line ~96). The only
caller of InquiryInboxService.Dispose is
Inquiry/Views/InquiryWorkspaceView.xaml.cs OnOwnerWindowClosed, which disposes
the client while an automatic check may still be in flight, and leaves the
dispatcher timer holding a service that can still be used later.

Required fix:
  * One HttpClient per process, owned by InquiryInboxClient, created once and
    never disposed except at application shutdown. No "using var client" in the
    sync path, and no per-call HttpClient.
  * Add a private Reset() in InquiryInboxClient that disposes the old client and
    creates a new one, and call it from the catch of ObjectDisposedException in
    SendAsync paths, then retry the request exactly once.
  * In InquiryInboxService, keep the CancellationTokenSource of the running sync
    in a field. Dispose must await or cancel that task before touching the
    client, and a disposed service must return a neutral InboxSyncOutcome
    instead of throwing.
  * OnOwnerWindowClosed must unsubscribe, cancel, and only then dispose.
  * Add a regression test with a fake HttpMessageHandler that disposes the
    client and proves the next call still succeeds through Reset().

DEFECT 2: the desk reports "Invalid login credentials" for a permissions
problem, and gives no way to tell the two apart.

Evidence from the screenshots: after a successful sign-in the banner says
"Signed in, this account is not authorized for the website inbox", and the
status says "Supabase sign-in succeeded, but this account is not authorized to
read website submissions". The account was inquiry_admin, which Phase 13 did not
allow into the delivery loop. Now that Phase 14 allows it, this specific account
works, but the diagnosis path is still wrong.

Required fix:
  * After a successful sign-in, call get_my_inquiry_access_v1 and store the
    result on the view model. Show, in the connection card, the email, the
    roles and, when it is not null, the denial_reason verbatim.
  * Before the first delivery call of a check, require that capabilities
    contains "inbox.plan.bridge". If it does not, do not call the RPC at all:
    set the status to a sentence naming the account, its role and the missing
    capability, and disable Check now.
  * Error mapping, applied everywhere:
      400 with message "Invalid login credentials" -> "Sign-in failed: the email
        or password was rejected." Never mention roles here.
      400 with message containing "Email not confirmed" -> "The account exists
        but its email address is not confirmed."
      401 anywhere -> "Session expired, signing in again", then one silent retry,
        then "Sign-in failed".
      403, or any body containing the 42501 message -> "Signed in, but this
        account may not run the desktop inbox." plus the roles from the
        self-report. Never the word credentials.
      404 -> the contract is missing on the website, name the function.
      anything else -> HTTP status plus the server message, truncated to 200
        characters, with any token redacted.
  * Remove the sentence that appends "a website email OTP is not a password" to
    every credential failure. Keep it only when the entered value is six digits,
    because that is the only case where it is true.

DEFECT 3: the Storage download does not send the apikey header.

In InquiryInboxClient.DownloadObjectAsync the request carries only
Authorization: Bearer. Supabase Storage expects both headers, exactly like
supabase-js: apikey with the publishable or anon key, and Authorization with the
user token. Add the apikey header to the storage GET. A download that returns
401 with a body about a missing API key is this defect, not a permissions
problem, so classify it as Remote with the status code preserved and say so in
the row error.

DEFECT 4: a 403 mid-batch aborts the check with a message that blames the wrong
layer.

In InquiryInboxService.SynchronizeCoreAsync, the plan call is outside the per
item try, so a 403 from the plan ends the entire check with
outcome.Error. That is correct, but the status text mixes "not authorized to
access one or more submission documents" with the plan refusal. Split them: a
refusal from the plan or the document plan means "this account may not run the
desktop inbox", a refusal from the download means "this account may not read
these files". Both must set LastSyncStatus to "Access denied" and neither may
clear the ledger.

Also, while you are here:

  * plan_customer_inquiry_deliveries_v2 returns the 20 columns the desktop model
    already declares, including contact_name, phone, customer_reference,
    attention_name, vat_trn_number, salesman_reference, handover_estimator_name,
    project_location, inquiry_type and description. Do not rename properties and
    do not add a second model.
  * acknowledge_customer_inquiry_delivery_v2 is idempotent: an identical replay
    returns 200 with no body. A different acknowledgement for a submission that
    already has a delivery raises 23505 and must be reported as "this submission
    was already delivered by another operator", not as a failure of the check.
  * The delivery row records auth.uid() in bridge_user_id. That column can now
    hold an admin's uuid, so do not label it "bridge" anywhere in the UI or the
    logs.
  * Keep the existing offline-neutral behaviour: an unreachable website is a
    state, never an error, and the local register must keep working.

Acceptance tests you must add or update, all runnable without a network:

  1. Fake handler returns 400 "Invalid login credentials": the status text names
     the credentials, contains no role wording, and the app does not call any
     RPC.
  2. Fake handler returns a successful token then 403 with the 42501 message:
     the status names the account and its role, LastSyncStatus is
     "Access denied", and no download is attempted.
  3. Self-report returns an empty capabilities array: Check now is disabled and
     one RPC is never called.
  4. Self-report returns inbox.plan.bridge: the plan is called once.
  5. Disposed-client regression described in Defect 1.
  6. Storage GET asserts both the apikey and Authorization headers are present.
  7. Replay of an identical acknowledgement returns quietly and the ledger does
     not grow.

Manual verification on the shop PC, in this order:

  a. Sign in as techwithyassh@gmail.com (inquiry_admin). The banner must read
     "Connected", the status must show "1 pending", and one delivery must appear
     for WEB-2026-000007 with 3 files, in
     C:\ProgramData\ProGlass\CustomerInquiryInbox\assigned\EST-PRIMARY-TEST.
  b. Press Test connection: it must say the sign-in and the inbox access both
     succeeded, and name the capability check.
  c. Close and reopen the window, then leave the app open for three minutes: no
     disposed-object message, and the automatic check keeps working.
  d. Sign in with a deliberately wrong password: the message must be about the
     password only.
  e. Sign in as a customer account (for example a website customer): the message
     must be about the role, and must quote the denial_reason from the database.

Do not: change the website repository, change the RPC names or parameter names,
call any Auth Admin endpoint, read auth.users, add a service_role key, store the
password anywhere except the existing DPAPI blob, accept an OTP in the password
field, or make the app depend on the network to open the register.
```

## Why these four

They are the only desktop-side causes this repository can prove from the source
tree plus the live database. Everything else in the screenshots is a
consequence: the 403 was a role decision, now resolved on the website side, and
the credentials message was the app's own fallback wording.

## Verified against the desktop commits

The desktop work landed on `arena/da841e06-proglassautomation3-0` as `7bcdf28`,
`7caf5dc`, `03b22dd`, `56e4edd` and `fabffc8`. Reviewed here against the live
contract on 2026-10-24:

| Contract point | Database says | Desktop does | Verdict |
|---|---|---|---|
| Access report shape | `get_my_inquiry_access_v1` returns `TABLE(authenticated, user_id, email, display_name, roles, capabilities, denial_reason)`, so PostgREST answers with an array of one object | `InquiryInboxAccessReport.Parse` accepts an array or a bare object and reads `email`, `roles`, `capabilities`, `denial_reason` | matches |
| Capability gate | `inquiry_admin` and `inquiry_bridge` both hold `inbox.plan.bridge`; an estimator, an auditor, a viewer and a role-less account hold none | gate runs before plan, document plan, acknowledge and download | matches |
| Refusal shape | `42501` with "Inquiry bridge or admin role required.", which PostgREST reports as 403 | `PermissionDenied` on 403, reported as an access refusal, ledger untouched | matches |
| Storage read | private bucket, object readable by an enabled admin and by the bridge for accepted, complete submissions | GET sends `apikey` and the bearer token | matches |
| Acknowledgement replay | identical replay returns quietly; a different manifest raises `23505` | treated as an idempotent success or a "delivered elsewhere" note | matches |
| Pipeline write-back | admin already held `pipeline.write` before Phase 14 | not gated by the delivery capability, so it is unaffected | matches |
| Original credentials failure | both accounts have a password on the project | a changed account email now refuses to save or test without a new password, which is exactly how the old saved bridge password ended up paired with the admin email | root cause closed |

Still open, and only the shop PC can settle them: `dotnet build` and the
network-free C# checks were not run in the environment that wrote those commits,
and manual steps a to e are unperformed. Nothing in this repository blocks them.

One watch item, not a defect: `InquiryStatusReporter` builds a fresh client per
report, so every status write-back signs in again. Supabase rate limits the
password grant per IP. If heavy editing starts returning `429`, cache one token
per settings object instead of signing in per save.
