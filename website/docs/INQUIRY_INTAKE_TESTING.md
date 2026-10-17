# Phase 8A Customer Inquiry Intake — Non-Production Test Procedure

This procedure is for an approved disposable Supabase project only. Do not apply the migration to production. Do not use customer data or service-role credentials.

## 1. Repository gates (no database mutation)

From `website/`:

```powershell
npm ci
npm run test:inquiry-security
npm run typecheck
npm run lint
npm run build
```

Expected current baseline: inquiry security 39/39; TypeScript, ESLint, and build pass; build includes `/inquiries/submit`, `/inquiries/submissions/[id]`, and the customer-submission API routes.

## 2. Migration review and authorized application

Review `supabase-inquiry-intake.sql`. It is additive to the already-applied `supabase-inquiries.sql`. Apply it only after explicit authorization. Expected additions:

- `inquiry_customer_submissions`, `inquiry_customer_documents`, `inquiry_customer_events`
- private `inquiry-intake-documents` bucket with 30 MB object limit
- customer ownership RLS and internal-role read policies
- constrained create/register/review/download-authorization RPCs
- no direct authenticated table mutation grants and no Storage UPDATE/DELETE policy

### Staged-upload follow-up

After the base, fields, and limits migrations are verified, review and separately apply `supabase-inquiry-intake-phase8a-staged-uploads.sql`. It adds only staged-upload state and RPCs; it backfills existing submissions as complete and preserves the previous atomic endpoint during the migration-first deployment window.

Read-only verification after authorized application:

```sql
select column_name, data_type, column_default
from information_schema.columns
where table_schema='public' and table_name='inquiry_customer_submissions'
  and column_name in ('upload_state','expected_document_count','finalized_at')
order by column_name;

select routine_name, count(*) as overloads
from information_schema.routines
where routine_schema='public'
  and routine_name in ('create_customer_inquiry_draft','finalize_customer_inquiry_submission')
group by routine_name order by routine_name;

select upload_state, count(*)
from public.inquiry_customer_submissions
group by upload_state order by upload_state;

select count(*) as incomplete_legacy_rows
from public.inquiry_customer_submissions
where upload_state='complete' and finalized_at is null;
```

Expected immediately after migration: three columns, one overload for each new RPC, all pre-existing rows in `complete`, and `incomplete_legacy_rows = 0`.

## 3. Runtime

Configure the existing non-production `NEXT_PUBLIC_SUPABASE_URL` and publishable/anon key outside Git. Never use a service-role key. Rebuild after changing `NEXT_PUBLIC_*` values:

```powershell
npm run build
npm run start
```

## 4. Customer test matrix

1. Open `/en-US/inquiries/submit` signed out; verify the form is gated.
2. Use email OTP to sign in as customer A and return to `/inquiries/submit`.
3. Submit valid fields with one small PDF; expect a `WEB-YYYY-NNNNNN` reference.
4. Verify it appears in history and opens its detail page.
5. Download the PDF; verify filename/content and a `download_authorized` audit event.
6. Test valid JPG/PNG/GIF/WebP/BMP, UTF-8 CSV, and non-macro XLSX.
7. Verify rejection of empty, over-30-MB, more-than-15, renamed/spoofed, macro/malformed XLSX, invalid UTF-8 CSV, DWG/DXF/SVG/EXE, unsafe filename, and cross-origin requests.
8. Sign in as customer B; prove customer A history/detail/document API calls return no data/404.
9. Prove anonymous API list/create/detail/download calls return 401.
10. Confirm bucket remains private and direct unauthenticated object requests fail.

## 5. Database evidence

Record only sanitized counts/statuses. Confirm:

- each submission owner equals the authenticated customer
- document object keys follow `<customer UUID>/<submission UUID>/<document UUID>` and contain no business names
- byte size and lowercase SHA-256 are recorded
- submitted/document/download events exist
- no public URLs or signed URLs are stored
- customer cannot insert/update/delete tables directly
- customer cannot update/delete Storage objects

## 6. Acceptance boundary

Phase 8A can be live-accepted only after this matrix passes. Phase 8B internal review and Phase 8C cloud-to-Windows pull bridge are separate incomplete phases. Do not use the old `inquiries:bridge:publish` command for website submissions; it is the preserved Windows-to-cloud desktop projection publisher.
