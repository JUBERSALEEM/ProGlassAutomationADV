# Website × Vercel × Supabase — Cross-Check

**Date:** 2026-10-18 · **Tree examined:** this repository at `7d4696b`
(branch `arena/f2590052-proglassautomationadv`, clean working tree)
**Method:** read `website/` end-to-end; read live Vercel project/deployment/
env/domain state via API; read live Supabase projects/orgs via API; probed
both public hosts' `/api/health`; held the three answers against each other.
Nothing was changed on Vercel or Supabase. The only env value decrypted was
`NEXT_PUBLIC_SUPABASE_URL` (public by design).

---

## Verdict

**The live site is exactly this tree, and the wiring is consistent where it
counts.** Production on Vercel is commit `7d4696b` built today at 19:15 UTC —
the same commit this checkout sits on. Version `2.0.11` agrees in all four
places it lives (`ProGlassAutomation.csproj`, `website/package.json`,
`site.ts`, live `/api/health`). The Supabase URL Vercel injects matches the
origin pinned in the production CSP.

Three real findings, in severity order:

1. 🔴 **The live Supabase project (`afpbdyamedpanwijxghk`) is not owned by
   the Supabase account connected here** — the live database cannot be
   inspected from this seat (§3).
2. ⚠️ **A second Vercel project still builds every push but serves nothing**
   — its public host returns a platform 404 today (§2).
3. ⚠️ **`public.records` has no `CREATE TABLE` anywhere in this repo** —
   the 17 SQL files cannot rebuild the database from scratch (§3).

---

## 1 · What `website/` is

A Next.js 16.3.6 / React 19.2 app (TypeScript, Tailwind 3.4), version
`2.0.11`, private package. It is two sites in one tree:

- **Marketing/product site** for the WPF ERP (features, pricing, worked
  example, blog, screenshots, legal) — all claims traceable to the app tree.
- **Operator surface** backed by Supabase: records & reports dashboards,
  deliveries tracker, job-order production board, and a full inquiry
  pipeline (customer intake → operator review → estimator queue →
  communications → production handoff).

| Measure | Count |
|---|---|
| `src` TS/TSX files / lines | 285 / ~92,300 |
| Page routes (under `[locale]`) | 34, rendered × 15 locales |
| API route handlers | 27 |
| Component files | 117 |
| Test files (`tests/` + `scripts/*.test.*`) | 36 |
| Supabase SQL files | 17 (22 tables, 39 functions) |
| Runtime deps of note | `@supabase/supabase-js 2.117.2`, `@vercel/speed-insights`, `motion`, `@react-pdf/renderer`, `fflate` |

No `vercel.json` (config lives in the Vercel project settings), no `.env*`
committed, `.vercel` and `.env*.local` git-ignored. `next.config.ts`
intentionally omits `output: "export"` — API routes, `no-store` exports and
server PDF rendering need a server runtime.

---

## 2 · Vercel cross-check

Two projects on team `proglassautomation` point at this one GitHub repo
(`JUBERSALEEM/ProGlassAutomationADV`), both with Root Directory `website` —
so **every push is built twice**.

| | `proglassautomationadv-website-tkim` (live) | `proglassautomationadv-website` (second) |
|---|---|---|
| Project ID | `prj_Sq6ovv1G8a2tEbUB2rwsdsy2IHF6` | `prj_5PVaerg48N5lFQc9UEyn5SB2Pe42` |
| Framework | `nextjs` ✅ | `null` (manual commands, "sourceless") |
| Production branch | `arena/23bacfef-proglassautomationadv` | `arena/01a0ebfc-proglassautomationadv` |
| Production deployment | `7d4696b`, READY, **2026-10-18 19:15 UTC** | `8247268`, READY per API, **2026-10-05** (13 days stale) |
| Public check today | `/api/health` → `{"revision":"7d4696b","version":"2.0.11",…}` ✅ | `/` and `/api/health` → **404 NOT_FOUND** (Vercel platform page) |
| Env vars | 3 (below) | 1 — `DEMO_REQUEST_INBOX` only; **no Supabase vars** |
| Node | 24.x | 24.x |
| Speed Insights | receiving data | no data |

### Consistent ✅

- **Live = this commit.** Live health reports branch `arena/23bacfef…` and
  revision `7d4696b`; that branch on GitHub is exactly `7d4696b`, and this
  session's branch was cut from it. Build ran ~1 minute after the commit.
- `site.url` fallback in code (`…-tkim.vercel.app`) is the live host, and
  `scripts/check-live.mjs` names the same two hosts. Docs and reality agree.
- Repository has **no `main` branch** (15 heads on origin, incl.
  `wpf-2.0.14`); both projects instead name an `arena/…` production branch,
  exactly as `docs/DEPLOYMENT.md` §3.3/§5 warns.

### Mismatches / risks

- **V1 — Work on the current branch cannot go live by itself.** Production
  follows `arena/23bacfef…`; pushes to `arena/f2590052…` produce *previews*
  only. Going live means merging/fast-forwarding into `arena/23bacfef…` or
  changing Settings → Git → Production Branch.
- **V2 — The second project is dead weight that's also dead.** Its
  production is frozen on the Oct-5 commit, it has no Supabase env (so even
  its fresh previews run `isSupabaseConfigured() === false`), and today its
  canonical host answers a Vercel platform 404 despite the API showing a
  READY aliased deployment. It still consumes a build per push. Recommend:
  pause or delete it (history lives in git), or deliberately revive it.
- **V3 — Supabase env vars are Production-only on the live project.**
  `NEXT_PUBLIC_SUPABASE_URL` / `NEXT_PUBLIC_SUPABASE_ANON_KEY` target
  `["production"]`. Preview deployments therefore render all data pages in
  their unconfigured/preview mode. This is acknowledged in `supabase.ts`
  comments — but it means **records/reports/deliveries/inquiries cannot be
  QA'd with real data in previews**.
- **V4 — Two stale deploy hooks** on the live project (`latest` →
  `arena/ff328a9c…`, `new hook` → `arena/b24b721c…`). Pinging either
  rebuilds old branches as this project's deployments. Recommend deleting
  or renaming loudly.
- **V5 — Historical BLOCKED deployments** (Oct 6, branch `arena/01a10c0d…`):
  `COMMIT_AUTHOR_REQUIRED` — commits authored as `jubersaleem01@gmail.com`
  couldn't be mapped to a Vercel seat. Previews only; production never
  blocked; recent deploys clean. No action unless that author identity
  returns.
- **V6 — Doc drift (cosmetic).** `DEPLOYMENT.md` says Node 22.x; both
  projects run 24.x and build fine. Update the table or pin `engines`.
- **Domains:** account holds **zero custom domains**; live project serves
  only `proglassautomationadv-website-tkim.vercel.app`. Consistent with the
  NXDOMAIN note in `site.ts`. When `proglassautomation.com` is attached,
  set `NEXT_PUBLIC_SITE_URL` (currently unset → fallback active) so
  canonicals/sitemap/OG flip with it.

### Env vars on the live project (complete list)

| Key | Target | Verdict |
|---|---|---|
| `NEXT_PUBLIC_SUPABASE_URL` | production | `https://afpbdyamedpanwijxghk.supabase.co` — **matches CSP pin** in `next.config.ts` ✅ |
| `NEXT_PUBLIC_SUPABASE_ANON_KEY` | production | present (value not decrypted — no need) ✅ |
| `DEMO_REQUEST_INBOX` | production + preview | present (sensitive) ✅ |
| `NEXT_PUBLIC_SITE_URL` | — unset | fallback in `site.ts` is correct today |
| `NEXT_PUBLIC_WHATSAPP_NUMBER` | — unset | fallback `971559117727` in code is active |
| `SUPABASE_SERVICE_ROLE_KEY` | — unset | **correct**: code only *reports* its presence (`hasServerServiceKey`) and `supabase.ts` actively refuses service keys |
| `DEMO_REQUEST_PROVIDER` / `RESEND_API_KEY` | — unset | demo form runs on default FormSubmit transport per `demo-request/route.ts` |

---

## 3 · Supabase cross-check

### The account problem 🔴

The Supabase account reachable from here sees exactly **one** organization
(`PGAI`, slug `vercel_icfg_Tss2PqnQbEEVZEjElXlr3ChZ` — a Vercel Marketplace
integration org) with **one** project:

| Project | Ref | Status |
|---|---|---|
| `supabase-pink-river` | `furnzdasosaxfaccovnb` | **INACTIVE** (paused; health API: "No active services available"), created 2026-07-21 |

The project the website actually uses — **`afpbdyamedpanwijxghk`**, proven
three ways (Vercel env value, CSP `connect-src` pin in `next.config.ts`,
diagnostics code) — returns **403** on lookup: it lives under a different
Supabase account/org than the connected one.

**Consequence, stated plainly:** everything below is a *repo-side*
consistency check (code ↔ committed SQL). The live database's tables, RLS
policies, auth users, storage contents and row counts were **not verified**
and cannot be from this seat until the owning Supabase account is connected.
The paused `supabase-pink-river` appears unrelated to this website (a
marketplace/v0-era project); the site has never pointed at it.

### Code ↔ committed SQL (repo-side) ✅ with one hole

- **Tables:** code queries 15 tables via `.from(...)`. 14 have
  `CREATE TABLE` in `website/supabase*.sql` (inquiries, documents, versions,
  customer submissions/profiles/projects/documents, estimators,
  assignments, communications, operator roles, sources,
  production_job_orders + operations). Extra SQL tables (event logs,
  sync runs, deliveries ledger…) are written via RPC/triggers — expected.
- **RPCs:** all **25** distinct `.rpc(...)` names in `src`/`scripts` have a
  matching `CREATE FUNCTION` in the SQL files. SQL defines 14 further
  functions that are triggers, RLS helpers (`has_inquiry_role`), internal
  calls (`assert_delivery_within_balance`) or superseded v1s — normal.
- **Storage:** the one bucket used in code, `inquiry-intake-documents`, is
  created in SQL (private, size-limited). ✅
- **Edge Functions:** none in repo, none expected. ✅
- ⚠️ **S1 — `public.records` DDL is missing from the repo.** It is the most
  queried table (14 call sites; records-api, deliveries-api,
  dashboard aggregates) and is referenced by `supabase-dashboard.sql`,
  `supabase-deliveries.sql` and `supabase-job-orders.sql` — but no
  `CREATE TABLE …records…` exists in any `.sql` or `.md` in this repository.
  It was evidently created outside these files. **A fresh Supabase project
  rebuilt from this repo's SQL would fail.** Recommend exporting the live
  DDL (once the owning account is reachable) into
  `website/supabase-records.sql`.
- ⚠️ **S2 — SQL is dashboard-applied, not migration-tracked.** The 17 files
  (dashboard, deliveries, inquiries, intake phases 8a–8c, estimator phases
  9a–9e incl. an RLS-repair, job-orders) carry their order in their names.
  With no access to the live migration history (see above), *which files
  have actually been run against `afpbdy…`* is unverifiable from here —
  the repo's own runtime probes (e.g. deliveries-api's "ledger not
  installed" path) are currently the only ground truth.
- ✅ **Client hygiene:** one credential site (`src/lib/supabase.ts`), lazy
  construction so builds without env don't die, JWT role guard that refuses
  a pasted `service_role` key, sessionless per-call server client,
  `supabaseServerWithToken` for RLS-as-user reads. The anon key + RLS model
  described in its header matches how the API routes actually call it.

---

## 4 · Recommended actions (in order)

1. **Connect the Supabase account that owns `afpbdyamedpanwijxghk`** (or
   confirm who/where it is). Until then the live DB is a black box to any
   audit from here. While there: export `public.records` DDL into the repo
   (S1) and confirm which of the 17 SQL files are applied (S2).
2. **Decide the second Vercel project's fate** — pause/delete
   `proglassautomationadv-website`, or fix + repurpose it (V2). Also delete
   the two stale deploy hooks on the live project (V4).
3. **Choose the go-live path for new work** — merge session branches into
   `arena/23bacfef…`, or repoint Production Branch (V1). Consider
   establishing a real `main` to end the per-session repointing.
4. **Decide if previews may read real data.** If yes, add the two
   `NEXT_PUBLIC_SUPABASE_*` vars to the Preview target too (V3). If no,
   keep as-is — it's a defensible posture, now a documented one.
5. Housekeeping: Node 22.x→24.x in `DEPLOYMENT.md` (V6); decide whether the
   paused `supabase-pink-river` project should exist at all.

## Resolution log

- **2026-10-18 — V2 resolved: duplicate Vercel project deleted.** At the
  owner's explicit confirmation, project `proglassautomationadv-website`
  (`prj_5PVaerg48N5lFQc9UEyn5SB2Pe42`) was permanently deleted via the Vercel
  API, together with all its deployments. Pre-delete safety facts, verified
  the same day: its only domain was the auto `*.vercel.app` host (no custom
  domains anywhere on the account), its only env var was `DEMO_REQUEST_INBOX`
  (also present on the live project), its production code remains on GitHub
  (`arena/01a0ebfc-proglassautomationadv` at `8247268`), and it had no
  Supabase connection. Verified after: API lookup of the project ID returns
  404 "Project not found", and the live project's `/api/health` still answers
  revision `7d4696b` — the live site was not touched. Every push now builds
  exactly once. `scripts/check-live.mjs` no longer lists the deleted origin.
- **2026-10-18 — V4 resolved: stale deploy hooks revoked.** The owner removed
  both hooks (`latest` → `arena/ff328a9c…`, `new hook` → `arena/b24b721c…`)
  in the dashboard; verified via API the same day — the live project's
  `link.deployHooks` is now `[]`. Production Branch confirmed unchanged:
  `arena/23bacfef-proglassautomationadv`.
- **Still open:** V1/V3 decisions, S1/S2 (need the Supabase account that owns
  `afpbdy…`).

## Not verified (explicitly)

Live DB schema/RLS/data/auth/storage for `afpbdy…` (403, §3) · Vercel
runtime logs · whether the second project's 404 is expiry, protection or
alias loss (API vs public fetch disagree — worth one look in the dashboard)
· DNS for `proglassautomation.com` (no domain on the account to check).
