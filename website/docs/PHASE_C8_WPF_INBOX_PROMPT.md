# Phase C8: prompt for the desktop inbox fixes

Copy everything between the fences into the agent session connected to
`JUBERSALEEM/ProglassAutomation3.0`. It is self-contained: the desktop agent does
not need this repository. Everything below was verified against production
`afpbdyamedpanwijxghk` on 2026-10-24, read-only.

```
You are working on the WPF desktop app in JUBERSALEEM/ProglassAutomation3.0,
branch arena/da841e06-proglassautomation3-0. The website repository
(JUBERSALEEM/ProGlassAutomationADV) is READ-ONLY context for you. Never modify
it, never add its files here, and never put a service_role key, a password or a
JWT in code, logs or config.

CONTEXT — what the production database actually holds

The website has eight complete customer submissions. Not one of them is
currently `accepted`. The website's delivery function is gated on

    upload_state = 'complete' AND status = 'accepted' AND no delivery row yet

so for this desk today the correct, expected answer is **0 ready**. That is not
a failure, not a 403, not a credentials problem and not something to fix by
widening anything. Two submissions were accepted in the past and both are
delivered:

    WEB-2026-000003  13 documents  delivered 2026-10-17 11:02 UTC
    WEB-2026-000007   3 documents  delivered 2026-10-24 08:39:28 UTC
                     routing assigned/EST-PRIMARY-TEST
                     bridge_user_id up17mix@gmail.com

The desk currently shows WEB-2026-000007 as "Failed", with "No files",
"0 downloaded, 0 already on disk", while the server has its delivery row and has
already moved the submission to `downloaded`. Once that acknowledgement exists,
no inbox call can ever offer that submission again:

  * plan_customer_inquiry_deliveries_v2 excludes it (delivery row present, and
    the status is no longer `accepted`);
  * plan_customer_inquiry_delivery_documents raises P0002 for anything that is
    not `accepted`, so the file list cannot even be requested;
  * the storage rule for the bridge role requires `accepted`, so the bridge
    credential can no longer read those three objects. An `inquiry_admin`
    session still can, through the website;
  * inquiry_customer_deliveries has RLS enabled with no policies at all, so the
    desk cannot read a delivery row to tell "already delivered" from "nothing
    was accepted". It only ever sees an empty plan.

There are five desktop defects. Fix them in this order. Keep each change small,
testable, and free of new dependencies.

DEFECT 1 — the acknowledgement is written before the files are proven.
The server recorded a delivery for WEB-2026-000007 with three documents, and the
PC has no files for it. The acknowledgement must be the last call in the batch,
after the payload exists and verifies on disk.

Required behaviour, in this exact order, per submission:
  1. download every document in plan_customer_inquiry_delivery_documents into
     <root>\incoming\<submission_id>.<guid>.partial\documents\
  2. verify each file's length and SHA-256 against the plan's byte_size and
     content_sha256; on any mismatch delete the .partial folder and fail the row
     with "integrity check failed for <filename>", without acknowledging;
  3. write manifest.json (the same canonical JSON the reference implementation
     in website/scripts/inquiry-inbox-bridge.mjs builds: version 2, routing,
     submission, documents with localFile), then READY containing the
     manifest's SHA-256;
  4. Directory.Move the .partial folder to its final routed path
     (<root>\assigned\<ESTIMATOR_CODE>\<reference>_<submission_id> or
      <root>\unassigned\<reference>_<submission_id>);
  5. only then call acknowledge_customer_inquiry_delivery_v2 with exactly
     p_manifest_sha256 = SHA-256 of the manifest.json bytes you kept,
     p_document_count = the number of documents in the plan,
     p_routing_key = the plan's routing_key verbatim,
     p_assignment_snapshot = the plan's assignment_snapshot verbatim;
  6. write delivery.json into the folder (submission_id, submission_reference,
     manifest_sha256, document_count, routing_key, acked_at_utc, acked_by email).
The server validates all of those: document_count must equal the number of
available documents, the snapshot must equal customer_inquiry_assignment_snapshot
exactly, the routing key must be `unassigned` or `assigned/<CODE>`, the hash must
be 64 lowercase hex characters, and the count must be 0..15.

DEFECT 2 — a later failure must never destroy a delivered payload.
Split the sync into two phases with two outcomes:
  * Fetch: plan, download, verify, place, acknowledge (Defect 1). A Fetch
    failure may delete only its own .partial folder.
  * Import: write the placed submission into the local WPF register/workbook.
    An Import failure must NOT delete the folder, must NOT re-download, and must
    NOT acknowledge again. Mark the item ImportPending with the exception text
    in the row, keep the folder, and retry only the Import on the next check.
WEB-2026-000007 is in the opposite state today: acknowledged, no payload. That
must become impossible.

DEFECT 3 — an already-delivered submission must not be presented as Failed.
Before an item is marked Failed, ask whether it is still in the plan. A ledger
item that is no longer offered by the plan and has no local payload is
DeliveredElsewhere, with the copy:

  "Delivered to this PC on <acked_at, Dubai time>; the files are missing here.
   Fetch them from the website (Customer submissions -> open the submission ->
   Download) or restore them from C:\ProgramData\ProGlass\CustomerInquiryInbox."

Rules for that state:
  * never retry the acknowledgement. An identical replay returns 200 quietly; a
    different manifest raises 23505, which must read "this submission was
    already delivered by another operator". Neither may enter a retry loop;
  * do not offer "Download again": plan_customer_inquiry_delivery_documents
    raises P0002 for a downloaded submission and the bridge credential can no
    longer read the objects;
  * if the folder does exist locally, offer "Re-import from disk" and verify the
    manifest hashes before importing;
  * never delete a delivery row, a submission row or a storage object from the
    desktop. The delivery row is the audit record of a real handoff.

DEFECT 4 — "0 ready" must explain itself and never look like a failure.
After a successful plan call that returns no rows:
  * status: "No submissions are waiting.";
  * one line beneath it: "The website hands a submission to this PC only after a
    ProGlass operator accepts it; everything accepted and not yet delivered
    appears here.";
  * no error state, no red banner, no ledger mutation, and Check now stays
    enabled. An empty plan is not an empty register, and the local register must
    keep working offline exactly as before.
Keep the Phase 14 error mapping unchanged: 403 or any 42501 body -> "signed in,
but this account may not run the desktop inbox" plus the roles from
get_my_inquiry_access_v1; 400 "Invalid login credentials" -> credentials only,
never roles; 401 -> one silent re-sign-in, then a failure; 404 -> name the
missing function. Keep showing the signed-in email, the roles and the
`inbox.plan.bridge` capability check in the connection card.

DEFECT 5 — say which identity acted, and keep two machines apart.
The delivery row records auth.uid(), which is now either up17mix@gmail.com
(inquiry_bridge) or techwithyassh@gmail.com (inquiry_admin). Both hold
`inbox.plan.bridge`, so either may drive this PC, but only one machine may
acknowledge a submission. Show the signed-in email next to the check result and
stamp `acked_by` and `acked_at_utc` in delivery.json. A second PC that finds a
submission already delivered must show DeliveredElsewhere (Defect 3), never
Failed, and must never re-acknowledge.

Do not add the review/accept step to the desktop. Accepting a submission is an
operator decision on the website (review_customer_inquiry_submission, admin
only). The desk must never call it, and must never write a submission status.

CONTRACT YOU MUST NOT CHANGE (all deployed; do not rename anything)
  1. POST /rest/v1/rpc/plan_customer_inquiry_deliveries_v2   { p_limit }
  2. POST /rest/v1/rpc/plan_customer_inquiry_delivery_documents { p_submission_id }
  3. GET  /storage/v1/object/inquiry-intake-documents/<key>   (apikey + bearer)
  4. POST /rest/v1/rpc/acknowledge_customer_inquiry_delivery_v2
          { p_submission_id, p_manifest_sha256, p_document_count,
            p_routing_key, p_assignment_snapshot }
  5. OPTIONAL: update_customer_inquiry_status_v1,
               list_customer_inquiry_communications_v1

ACCEPTANCE TESTS — network-free, fake HttpMessageHandler, one per line
  1. plan returns [] -> "No submissions are waiting.", ledger unchanged, no ack,
     Check now still enabled.
  2. download returns 401 -> row names Storage and the status code, no ack,
     .partial removed.
  3. byte size or SHA-256 mismatch -> no ack, .partial removed, row says
     "integrity check failed for <filename>".
  4. ack throws after a good download -> folder kept, item ImportPending, next
     check does not re-ack.
  5. import throws after a good ack -> folder kept, ack not repeated,
     "Re-import from disk" offered.
  6. identical ack replay -> quiet, ledger does not grow.
  7. ack 23505 -> "delivered by another operator", no retry loop, not Failed.
  8. a submission that vanished from the plan while a ledger row exists -> 
     DeliveredElsewhere with the website instruction, ack never called.
  9. plan 403 -> "Access denied", ledger intact, no download attempted.
 10. storage GET carries both apikey and Authorization (Phase 14 test 6 stays).

MANUAL VERIFICATION ON THE SHOP PC, IN THIS ORDER
  a. Sign in as techwithyassh@gmail.com: Connected, capabilities include
     inbox.plan.bridge, status "No submissions are waiting. The website hands a
     submission to this PC only after a ProGlass operator accepts it; ...".
  b. On the website, as that account, open /inquiries/customer-submissions and
     accept WEB-2026-000001 (one document). Press Check now: one row, one file,
     C:\ProgramData\ProGlass\CustomerInquiryInbox\unassigned\
     WEB-2026-000001_<submission-id>, and the delivery row appears only after
     READY exists.
  c. The old 000007 row reads "Delivered to this PC on 24 Oct 2026, 12:39
     (Dubai); the files are missing here." No Retry, no crash, no re-ack.
  d. Close and reopen the window, leave it open three minutes: no
     disposed-object message, automatic checks keep working.
  e. Sign in with a customer account: role refusal quoting denial_reason, never
     the word credentials.

DO NOT
  * change the website repository, the RPC names or their parameters;
  * call review_customer_inquiry_submission, or write any submission status;
  * insert, update or delete rows in inquiry_customer_deliveries or
    inquiry_customer_submissions;
  * touch inquiry_sources, its enabled flag or its bridge_user_id;
  * add a service_role key, read auth.users, or call any Auth Admin endpoint;
  * store the password anywhere except the existing DPAPI blob;
  * accept an OTP in the password field;
  * make the register depend on the network, or a check depend on the plan being
    non-empty.

ONE WEBSITE FACT TO EXPECT, NOT WORK AROUND
The website now routes /inquiries by the account's role. A customer sees their
own inquiry surfaces; inquiry_bridge is told it is a sync identity; only
inquiry_auditor and inquiry_admin reach the register. That is deliberate. Do not
try to reach the website registers from the desktop at all.
```

## Why these five

They are the desktop-side causes this repository can prove from the live
database plus the published contract:

| Evidence | What it means |
| --- | --- |
| `inquiry_customer_deliveries` row for `WEB-2026-000007`, written 08:39:28 by `up17mix@gmail.com`, `document_count = 3` | the acknowledgement was sent and accepted |
| submission `downloaded_at = 2026-10-24 08:39:28`, `status = 'downloaded'` | the server then closed the handoff; the plan will never offer it again |
| the desk shows the same submission as Failed with no files | the payload was never placed, or was removed after the ack — Defects 1 and 2 |
| the three objects are still `available`, sizes and hashes recorded | the download itself was possible; nothing on the server lost the files |
| six complete submissions with `accepted_at` null | "0 ready" is the correct answer until an operator accepts them — Defect 4 |
| `inquiry_customer_deliveries` has RLS and no policies | the desk cannot distinguish "delivered" from "not accepted" on its own — Defect 3 |
| storage policy for `inquiry_bridge` requires `accepted` | after an ack the bridge credential cannot re-read the files; recovery needs an admin session — Defect 3 |

## What the website changed in the same turn

`/inquiries` now decides by role instead of showing every signed-in account the
operator register. A customer gets their own page; `inquiry_bridge` and
`inquiry_estimator` get a notice that names the role; `inquiry_auditor` and
`inquiry_admin` get the register. No database policy, grant, role or bridge
setting was changed, and no submission was accepted or acknowledged to produce
this. See `CUSTOMER_AND_STAFF_INQUIRY_SURFACES.md`.

---

# Addendum for the desktop agent: instant sync, and show the files

The website side changed after the prompt above: a complete submission is now
handed to the desk **as soon as the customer sends it**, with no acceptance step
(`supabase-inquiry-intake-phaseC9-auto-handoff.sql`). A `proforma_invoice`
request is the one exception — it is finalized in `under_review` and stays
invisible until a coordinator accepts it.

That makes the check interval the thing that decides how "instant" the desk
feels. Paste this second block into the same session.

```
Two more changes, in the same repository. Do not touch the delivery contract.

CHANGE 6 — the automatic check should feel instant.
Today the view model keeps `PollMinutes` with a default of 5 and options
1, 5, 15, 30, 60 (Inquiry/ViewModels/InquiryInboxViewModel.cs ~line 159), so a
customer's inquiry can sit on the website for five minutes before this PC knows
about it. The website now offers a submission the moment it is finalized, so the
delay is entirely ours.

Required:
  * Replace `PollMinutes` with `PollSeconds`: default 30, options 15, 30, 60,
    300, 900. Keep the existing clamp-on-set and the AutoSyncText line, which
    must read "Automatic check every 30 seconds" / "…every 1 minute".
  * Migrate the saved settings object: if it only has the old minute value,
    convert it to seconds (minutes × 60) clamped to the new range, save once, and
    never read the minute field again. An unreadable or absent value is 30.
  * Run one check immediately after a successful sign-in, and one when the
    window becomes active again (Window.Activated), debounced to at most one
    check per 10 seconds.
  * Never run two checks at once: one in-flight guard that returns the running
    task instead of starting a second. Cancellation on close must still leave the
    next window able to check.
  * Check now stays as it is and is never disabled because the plan is empty.
  * Acceptance test: settings without the new key migrate to 30 seconds;
    a stored 5 minutes migrates to 300; two rapid Activated events produce one
    plan call; a check that overlaps a running one does not call the plan twice.

CHANGE 7 — show the folder and the files, not just a count.
The grid shows "Files" as a number (`DocumentsText` bound to a DataGridTextColumn
in Inquiry/Views/InquiryInboxView.xaml ~line 290). A shop user cannot see what
actually arrived or where it went.

Required:
  * Each delivered row must expose the full local folder
    (…\CustomerInquiryInbox\{assigned\<CODE>|unassigned}\<reference>_<submission_id>)
    and the manifest's document list: original filename, byte size, and the
    first 12 characters of its SHA-256.
  * Clicking the Files cell (or a "View files" button on the row) opens a detail
    panel for that submission: the reference, the routing key, the full folder
    path, one row per file, and three actions — Open folder (explorer.exe with
    the folder path), Open file (the selected file), and Copy path.
  * If the folder or a file is missing, say which one and when the manifest
    expected it, instead of showing an empty list.
  * The inbox header shows the inbox root path with a Copy button, so support can
    ask for it over the phone.
  * Never open Explorer automatically after a download, never auto-print, and
    never run anything from the downloaded folder.
  * Acceptance tests: a row with three files lists three names and sizes from
    the manifest; a row whose folder was deleted shows "files missing here" and
    does not throw; Open folder is a no-op on a missing folder with a message.

Keep every rule from the first block: verify before acknowledging, acknowledge
once, never delete a delivered payload, never show a delivered submission as
Failed, and never widen anything.
```
