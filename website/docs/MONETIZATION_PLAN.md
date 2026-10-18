# Monetization Plan — Marketing Site → License Sales → Cloud SaaS

**Started:** 2026-10-18 · **Companion audit:** `CROSS_CHECK_2026-10-18.md`
**Strategy in one line:** the website stays one app with two jobs — the
marketing pages are the shop window, the Supabase operator pages become
the paid cloud. Money arrives in sequence, fastest first. A full web
rewrite of the WPF ERP is explicitly *not* the plan (300k lines, factory
floors, offline licensing is a feature there, not a bug).

## Status

| Phase | What | Code state | Owner inputs still needed |
|---|---|---|---|
| **0** | Foundations | ✅ applied 2026-10-18 (this doc, `supabase-records.sql`, `.env.example`) | Connect owning Supabase account; verify `records` DDL live; buy domain; decide prices |
| **1** | Sell desktop licenses online | ✅ applied 2026-10-18 (plans catalog, `/api/checkout`, `/api/webhooks/billing`, `supabase-billing.sql`); **AED prices published same day** — WhatsApp buy path live in code | Payment links in `src/data/plans.ts` (provider account); `BILLING_WEBHOOK_SECRET`; run `supabase-billing.sql` |
| **2** | Cloud subscription (multi-tenant) | ✅ foundation applied 2026-10-18, dormant (`supabase-multitenancy-phase2.sql`, `src/lib/tenant.ts`, flag off) | Apply SQL on live project + backfill default company, then flip `NEXT_PUBLIC_CLOUD_TENANCY=on` |

Everything commerce-shaped ships **inert**: until the owner inputs above
exist, checkout/webhook answer `501`, the pricing page renders exactly
as before, and tenancy resolution returns "off". No behaviour changes
by deploying these commits alone.

## Phase 0 — Foundations

Done in repo: `public.records` DDL reconstructed from code evidence
(`supabase-records.sql`, with verification queries — fixes audit S1);
`.env.example` documents every variable the site reads.

**Owner checklist (cannot be done from a repo):**
1. Identify/connect the Supabase account owning `afpbdyamedpanwijxghk`
   (audit §3 — the connected account gets 403). Run the verification
   queries at the bottom of `supabase-records.sql`; correct that file
   wherever live disagrees.
2. Register `proglassautomation.com` (or chosen domain), attach in
   Vercel, set `NEXT_PUBLIC_SITE_URL`.
3. Decide prices (worksheet below).

## Phase 1 — Sell the license online

The sellable unit already exists in the desktop product:
`Licensing/Contracts/LicensePlanCatalog.cs` — 7d / 30d / 182d / 365d /
730d signed, offline-activated keys, tiered by the 5 editions in
`src/data/editions.ts` (Trial → Enterprise). Phase 1 adds only the cash
register:

- `src/data/plans.ts` mirrors the C# catalog durations (single edit
  point for prices + hosted payment links; `null` price = not sold
  online yet, page keeps "request pricing").
- Pricing page gains a durations strip rendered from that file; a plan
  becomes buyable the moment its price + link are filled.
- `GET /api/checkout?plan=<id>` 307-redirects to the provider's hosted
  checkout (Paddle / Lemon Squeezy / Razorpay payment link). Stable URL
  on our domain, swappable provider behind it.
- `POST /api/webhooks/billing` records paid orders into
  `public.license_orders` via the `record_license_order` RPC
  (`supabase-billing.sql`), gated by `BILLING_WEBHOOK_SECRET`.
- **Key issuance v1 is semi-manual by design:** an order row appears,
  the existing .NET License Generator issues the signed key, the row is
  marked `key_issued`. Automating signing means porting
  `LicenseCrypto.cs` to a server — do that only after order volume
  hurts, and never re-implement crypto casually.

**Published launch prices (2026-10-18, AED excl. 5% VAT, in
`src/data/plans.ts`):**

| Edition | 1 Year | 2 Years (1.6×, ≈20% anchor) | ≈/month on 1 Year |
|---|---|---|---|
| Basic | 1,499 | 2,399 | 125 |
| Business | 3,999 | 6,399 | 333 |
| Factory | 7,999 | 12,799 | 667 |

Reasoning: an SMB fabricator loses more than AED 333/month to one
mis-priced or re-cut job; the optimizer carries Factory. Enterprise
stays quote-only; 7/30-day and 6-month terms exist in the catalog for
negotiation, not self-serve; the in-app 30-day Trial covers evaluation.
Until a provider account exists, priced SKUs sell through WhatsApp
(deep link pre-writes edition/term/price); pasting hosted payment
links into `plans.ts` switches each row to card checkout untouched.

## Phase 2 — Cloud subscription ("ProGlass Cloud")

Sell the already-built operator surface per company per month: customer
inquiry intake portal, estimator queue, records/reports dashboards,
delivery tracking. Desktop stays the factory system of record; sync
(2.0.15 dev line) feeds the cloud.

Applied as dormant foundation:
- `supabase-multitenancy-phase2.sql` — `companies`, `company_members`,
  `subscriptions`; additive nullable `company_id` on `records`,
  `inquiries`, `inquiry_customer_submissions`,
  `production_job_orders`; `current_company_ids()` RLS helper.
  Nothing is backfilled, no policy is tightened, no column is forced
  NOT NULL — activation steps are commented inside the file in order.
- `src/lib/tenant.ts` + `src/types/tenancy.ts` — resolves the caller's
  companies behind `NEXT_PUBLIC_CLOUD_TENANCY`; off by default, and no
  existing route consumes it yet.

**Activation order (owner + a future session, against live):**
1. Phase 0 verification complete (esp. records policies exported).
2. Run `supabase-multitenancy-phase2.sql`; create the default company;
   backfill `company_id`; add members for existing operators.
3. Uncomment/apply the tenant RLS policies in that file per table,
   testing as anon + two different company users.
4. Wire tenant scope into records/deliveries/inquiry APIs; flip the
   flag in Preview first, then Production.
5. Attach subscriptions to billing (Phase 1 webhooks already land
   orders; map plan → `subscriptions` row).

## Sellability backlog (checked 2026-10-18, in money order)

**Sell the WPF app harder — no SaaS required:**
1. **Public trial download (biggest funnel gap).** The app has a
   built-in 30-day all-features trial and OTA updates, yet the website
   offers no download — every evaluation waits on an email reply. Host
   the installer publicly (the repo is private; use a public release
   mirror or Vercel static asset), add `/download` + "Start free trial"
   CTAs. Nothing else converts like the product itself.
2. **In-app conversion path.** Trial countdown + expired-trial screen
   should deep-link to the exact pricing SKU; the existing
   read-only-after-trial behaviour is the paywall, it just needs a door.
3. **Demo videos.** The app can record itself (DemoCenter); publish 3
   short workflow videos (quote→job→delivery→invoice; optimizer;
   owner reports) on the site + YouTube. Motion outsells screenshots.
4. **Arabic-first reach.** ar-AE is already a full locale — point
   UAE/GCC discovery (Google Business, industrial-area directories,
   WhatsApp) at the Arabic pricing page.
5. **SEO content.** Blog has 3 posts; fabricators search problems
   ("glass cutting wastage", "DGU quotation format UAE"). The worked
   example is a ready-made series.
6. **Support/AMC add-on.** Priority WhatsApp support + update
   assurance as an annual add-on — recurring revenue even mid-term.
7. **Referral channel.** Glass machinery/spacer/hardware suppliers in
   GCC industrial zones meet every fabricator monthly; a reseller cut
   beats ads in this niche.

**Then the SaaS layer (Phase 2 activation):**
8. **ProGlass Cloud subscription** (suggested AED 299/company/month):
   inquiry portal free for the fabricator's *customers* (they are the
   acquisition channel), owner dashboards, estimator queue.
9. **Customer tracking links** per delivery ("track your glass
   order") — every shared link advertises ProGlass to the next
   fabricator's customer base.
10. **Multi-site cloud sync** for Enterprise, replacing shared-file
    LAN setups; later, hosted AI credits for the assistant.

**Mechanics:** payment links + `BILLING_WEBHOOK_SECRET` (webhook
already built); founding cohort — first 10 UAE factories at a launch
discount with onboarding, in exchange for the testimonials/case study
the site's honesty policy currently forbids inventing.

## Explicit non-goals

- No desktop ERP rewrite; no static-only downgrade (kills Phase 2);
  no repo split until the cloud has paying tenants.
- No invented prices, ever — the site's honesty policy stands; nulls
  render as "request pricing" until the owner publishes numbers.
- No service_role key in the website (house rule in `src/lib/supabase.ts`
  stands; privileged writes go through SECURITY DEFINER RPCs).
