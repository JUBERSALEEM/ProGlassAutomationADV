# Website renewal plan — promote everything the application has

**2026-10-08 · prepared by the Arena agent · basis: a full read of the website, a
survey of every branch on the remote, and a diff of the application lines**

This document is the phased plan the user asked for: read the website
completely, find its issues, look at the latest WPF branch, and lay out how the
website gets redesigned so it promotes everything the application has. Every
claim below cites what it was measured or read from, in the same discipline the
site itself keeps (`src/data/modules.ts` carries an `evidence` field per
module). Nothing here is aspirational copy.

---

## 1. What the survey found (verified, not assumed)

### 1.1 The branches on this remote, newest first

| Ref | Tip | Date (UTC) | What it is |
|---|---|---|---|
| `arena/ff328a9c-…` (HEAD, default) | `2ebc7f31` | 2026-10-07 12:18 | **Our line.** WPF app at 2.0.11 + the whole website. Tasks 1–7 landed here. |
| `arena/01a10c0d-…` | `e3c2a787` | 2026-10-07 14:14 | **The latest application line.** App `<Version>` **2.0.15**, +10,360 lines across 137 files since 26 Sep. **No `website/` directory** — it cannot collide with us and can be mined freely for content. |
| `wpf-2.0.14` | `d152e065` | 2026-10-05 04:57 | Branch named for the next release; the csproj there still says 2.0.11. Merges PRs #18/#19/#21. |
| `arena/01a10ad1-…` | `35c5384a` | 2026-10-05 10:31 | The commit our branch was cut from. |
| `arena/01a0c955-…` | `de1c42a0` | 2026-09-23 | Older WPF work. |
| `arena/01a08606-…` | `362d22b1` | 2026-09-10 | Older WPF work. |
| `vercel/install-and-configure-…` | `85adbeab` | 2026-09-30 | Vercel Speed Insights install — **consumed this pass** (§3). |
| tag `v1.0.14` | `1e9369e8` | — | Old tag. |

### 1.2 What the latest application line (2.0.15) has that matters to the site

Read from `arena/01a10c0d` (`e3c2a787` and its ancestors since merge-base
`694c4247`, 2026-09-26):

- **Records Sync became a product, not a plumbing detail.** `Services/RecordsSync/`
  (service, `SupabaseRecordsClient`, two test suites) gained a settings page
  (`ViewModels/.../SettingsRecordsSyncPageViewModel.cs`,
  `Views/Settings/Sections/SettingsRecordsSyncSection.xaml`), a sync
  architecture audit and implementation design in `docs/`, diagnosis tooling,
  credential binding to the production project, and retries for fenced writes
  (Oct 6–7). The site's `records-sync` module card pre-dates all of this.
- **`Views/DocumentRegister/`** — the linked document register, added as a real
  module (29d83abf, Oct 6). The site already lists `document-register` with the
  honest note "no published capture yet".
- **Version bumps 2.0.11 → 2.0.13 → 2.0.15** during that work. The site shows
  2.0.11 because it tracks `<Version>` on *its own* line — correct, and now
  visibly behind (see O4).
- The same line confirms the site exists as the Supabase counterpart of the
  app's records: `/api/records`, `/api/job-orders`, `/api/deliveries` are the
  web half of Records Sync. That integration is a differentiator and deserves a
  diagram on the site (Phase 3).

### 1.3 What the website already is (strengths to keep, not redesign away)

- **46 promoted modules** in `src/data/modules.ts`, each with `capabilities`
  and an `evidence` path into the WPF source, grouped into the application's
  real 9 rail groups. The registry cross-checks `GlassShell.xaml`,
  `en-US.xaml`, and `MainViewModel`.
- 15 locales, ~3,625 keys each; `npm run i18n:keys` keeps the key ring at 4250;
  RTL + full-locale URLs.
- A traceable history page (`src/data/developerHistory.ts`), screenshots
  registry (946 lines), live-demo registry (1,367 lines), editions/pricing
  (`trial` / `basic` / `business`…) cross-checked against licensing behaviour.
- The optimization surface rebuilt this week: gated worked example (22 sizes /
  91 pieces / 20 plates / 82.04%), plate CAD views, seed data from the real
  engine docs.
- The deliveries **confirmed-PI tracker with per-PI ledger** — already present
  in our tree (absorbed with the base line).
- Engineering gates that run on every pass: `audit-site.py` (0/0),
  `measure-panel.py` (12 markup assertions + six width tiers), `check:live`,
  `check-build-freshness`, the build stamp + `/api/health` pair.

## 2. Senior audit — issues found

### 2.1 Fixed in this pass

1. **The panel model vs the markup, round two (the leftover from the stamp
   pass).** The measuring tool modelled five cards at `2xl` while the markup
   showed all seven — the two numbers it printed for `2xl`/`wide` were only
   true for a ladder nobody had written. Resolution: written into the ladder,
   not the arithmetic — three steps now (`xl` → 3 cards, `2xl` → 5, `wide` →
   all 7), Menu serves `lg` through `2xl` (`lg:flex wide:hidden`). 12/12 markup
   checks, exit 0, numbers unchanged: +123.0 / +137.6 / +79.8 / +36.4 /
   +50.6 / +115.8. README and the dated desync doc prose re-aligned.
2. **`tailwind.config.ts` comment lied about its own reason to exist** ("six
   nav cards… about 1630 px") — now "seven… about 1660 px", matching the
   measured `wide` row (1661/1777).
3. **Vercel Speed Insights installed** (merged into `wpf-2.0.14` as `85adbeab`
   a week ago but never reached the line the site ships from). `@vercel/
   speed-insights@^2.0.0` + `<SpeedInsights />` in the locale layout. By design
   it renders nothing unless Vercel injects the observability envs, so
   localhost shows no script; first data arrives with the next deployment.

### 2.2 Open issues, ranked

| # | Issue | Evidence | Fix lands in |
|---|---|---|---|
| O1 | **First-load JS is 6.3 MB raw / 2.0 MB gzipped on `/`** — and one shared chunk alone (`…soo5h.js`) is 5.4 MB raw / 1.75 MB gz, referenced by *every* route. On a mid-range phone this is seconds of parse before interaction; it is the single largest smoothness defect. | measured 2026-10-08, 17 chunks on `/` | Phase 5 (diet) with attribution now |
| O2 | **Homepage HTML is 729 KB raw (98.5 KB gz)** — route data and RSC payload in one document. | curl 2026-10-08 | Phase 5 |
| O3 | Production alias still builds/serves `72c2cc5b` (Oct 7 09:22): live `/api/health` 404s. Account-holder path is in the desync doc and was walked through with the user this morning (Production Branch + Promote). | `check:live`, fetch 2026-10-08 | account action (user in progress) |
| O4 | **Version story straddles two lines**: site says 2.0.11 (release line), the dev line is 2.0.15. Needs a policy (proposed: release version on product pages, "development line 2.0.15" on the developer page and history). | csproj on both lines | Phase 1 |
| O5 | **Content stale vs the app**: `developerHistory` ends 26 September; Records Sync settings UI, Document Register, and the Oct 2–7 records/Excel hardening aren't in the narrative; module evidence for `records-sync`/`document-register` predates the new source. | `developerHistory.ts`, git log | Phase 1 |
| O6 | **Two app modules have no coverage at all**: `Views/AI`, `Views/Sharing`. Promoting them requires reading their source first — the traceability rule forbids writing cards from the folder names. | Views/* vs the 46 ids | Phase 1 (research) |
| O7 | 8 locales sit at 80.2% key coverage (`i18n:verify` exits 1 by design today). | i18n tooling | Phase 5 |
| O8 | `site.ts` `url` = `proglassautomation.com` → NXDOMAIN; repo homepage points at a deleted origin (GitHub refuses PATCH from this token). | dns.google, REST 403 | account action |
| O9 | Preview deployments are login-walled; the duplicate Vercel project doubles statuses and build minutes. | bot comments, fetch | account action |
| O10 | Sandbox resets wipe `node_modules`/`.next` and the fonts for the measuring tool — recovery is scripted but costs minutes when it surprises. | happened twice this session | §5 runbook |

**Things checked and found healthy, so they are not in the table:** all 21
locale pages + home return 200; every internal route linked from those pages
returns 200; `sitemap.xml` and `robots` 200; `favicon.ico`, `icon.svg` and both
brand PNGs serve (an earlier "3 broken links" line was a flaw in this audit's
own crawler, which prefixed asset URLs with `/en-US` — corrected, not a site
bug); `tsc` and `eslint` clean; 4250 i18n keys; build 399/399 pages;
`audit-site.py` 0 failures / 0 warnings; no TODO/FIXME in app code (the two
hits are deliberate display strings on the About page).

## 3. The phases

Each phase ships as one or more pushed passes on
`arena/ff328a9c-proglassautomationadv`, under the same gates the site already
has. A phase is *done* when its acceptance list is all-true on the pushed
build, not when the code compiles locally.

### Phase 0 — Ground and gates ✅ (this pass)

The fixes in §2.1, this document, and the gate set below. **Done.**

### Phase 1 — Truth refresh: bring the story level with 2.0.15

*Content work only; no layout risk.*

- Update `developerHistory.ts` with the October paper trail, each entry
  sourced: confirmed-PI ledger (`7dbdd1d5`), Excel/workbook resolution and RPC
  hardening series (Oct 2–3, `b534daee`→`ae952caa`), Records Sync architecture
  audit + implementation design (`87191ccb`, `25a5cb53`), linked document
  register (`29d83abf`), settings UI (`75f119dc`), production binding
  (`e3c2a787`), version bumps 2.0.13/2.0.15.
- Refresh `records-sync` and `document-register` module entries: new
  capabilities + new `evidence` paths (settings section xaml, sync tests,
  docs). Keep the "no published capture" honesty where it still applies.
- Research `Views/AI` and `Views/Sharing` in the WPF source and, *only* if the
  source shows a real feature, add module entries with evidence.
- Land the version policy from O4.
- **Acceptance:** every new sentence carries a repo path in `evidence` or is a
  date/version read from a csproj/log; `i18n:keys` count updated; audit 0/0.

### Phase 2 — Structure: latest UI, organized around what the app actually is

*The "complete redesign" the user asked for — but the theme engine, palettes,
grain and the four reading modes from the task-5 work stay; this is
information architecture and page craft, not a visual reset.*

- **Home** becomes a guided tour of the 9 rail groups (Main → System), each
  linking its modules; today's hero copy and feature grid get reorganized
  around them. First screen must still fit the phone tiers measured in
  `measure-panel.py`.
- **Module index**: the 46 modules get filterable groups + status chips
  (shipped / no-capture-yet / dev-line), cards stay evidence-backed.
- **Nav** stays within the measured ladder (3/5/7 cards) — new top-level items
  are paid for by removing others, per the budget section in the README.
- Page-level polish: consistent section rhythm, tighter type scale verified in
  all 15 locales (Arabic/Urdu line-height and screen-reader order included),
  zero new runtime blur/backdrop layers (the frame-cost budget in the README
  stays the law: gradients over `filter: blur()`, alpha over blend modes).
- **Acceptance:** measure-panel exit 0; Lighthouse-style manual pass on a
  mid-tier phone shows no long-task regressions; every rail group reachable in
  ≤2 clicks from home.

### Phase 3 — Showcases: prove the machinery, don't just name it

- Worked example re-anchored to the latest engine paperwork in-repo
  (`ENGINE_STRATEGY_ANALYSIS_2026-09-26.md`, `OPTIMIZATION_MODULE_SENIOR_AUDIT…`)
  — any changed constants get re-measured, not re-worded.
- Export-format showcase: what `.OPT`, `.DXF`, `.ISO`, `.DAT` actually look
  like coming out of the engine (sample files rendered as downloads/previews
  from real exports).
- Records Sync explainer: one diagram, app ⇄ Supabase ⇄ this site's
  `/records` • `/job-orders` • `/deliveries`, with the settings UI steps from
  the 2.0.15 line.
- The five-stage production pipe (cutting → tempering → lamination …) as one
  scrollable, static-first visual — generated assets, no runtime canvas.
- **Acceptance:** samples byte-match real output formats; the sync diagram's
  steps match the settings page field-for-field.

### Phase 4 — Conversion and trust

- Demo-request endpoint verified end-to-end (where does it write? Supabase
  table or mailbox — document it, add a success state, add it to audit-site).
- Pricing/editions page re-read against `Licensing/` (trial ledger, seats,
  anti-rollback) so the sales copy matches the licence code that enforces it.
- UAE-facing facts kept sharp: VAT 5%, data locality, Windows (WPF) platform
  statement — all already true on the site, keep them heading-visible.
- SEO: sitemap/robots stay green; OG imagery regenerated from the brand
  renderer; `site.ts` `url` follows the domain decision (O8).
- **Acceptance:** a stranger can go home → module → worked example → pricing →
  demo request without a dead end, in `en-US` and `ar-AE`.

### Phase 5 — Smoothness and hardening

- **Bundle diet (O1):** attribute the 5.4 MB shared chunk (bundle-analyzer or
  `--profile` build), then: route-scope the optimization engine, demo
  dashboards and Supabase client behind dynamic imports; split locale dicts
  per-locale if analysis shows them inside shared code. Target: first-load JS
  ≤ 350 KB gz on `/` without losing a feature.
- **HTML diet (O2):** move page data behind streaming boundaries so `/` ships
  its own payload only. Target: ≤ 200 KB raw document.
- Locale completion drive for the 8 at 80.2% (O7) — batch, review, keep the
  English-source-of-truth rule.
- Speed Insights baseline review 7 days after the first production deployment
  that carries it; fold its web-vitals into `audit-site.py` as budgets.
- **Acceptance:** the numbers above, on the pushed build, with Speed Insights
  data agreeing.

## 4. Account-holder list (things no code change can complete)

Progressing, kept here so the plan doesn't pretend otherwise:

1. Production Branch = `arena/ff328a9c-proglassautomationadv` on the `-tkim`
   project + one Promote — *user is at this dialog now*; afterwards
   `npm run check:live` prints CURRENT and the live page shows the GST stamp.
2. Delete/repoint the duplicate Vercel project (O9).
3. Domain: register `proglassautomation.com` or repoint `site.ts` (O8);
   fix the repo homepage URL (needs admin on the repo).

## 5. How we continue — operating procedure

Behind every pass, in this order:

```bash
# 0. after a sandbox reset (files missing, server down, fonts gone):
cd website && npm ci && pip install --break-system-packages fonttools brotli

# 1. the gates, before any commit:
npx tsc --noEmit && npm run lint
npm run i18n:keys                 # key ring printed; update dicts if it moved
python3 scripts/measure-panel.py  # exit 0, N/N markup checks
npm run build                     # 399/399 pages

# 2. serve and prove:
pkill -f "[n]ext-server" && npm run start &
curl -s localhost:3000/api/health           # revision = git log -1 --short=7
python3 scripts/audit-site.py               # 0 failures 0 warnings

# 3. land it together (user's standing rule):
git add -A && git commit && git push origin arena/ff328a9c-proglassautomationadv
# then rebuild so the stamp carries the new revision, restart, verify again.

# 4. live truth (from any machine): npm run check:live
```

Rules that don't move: remote and local land in the same pass; never claim the
live site is current without the stamp agreeing; every product claim cites a
repo path; the panel, plates and theme keep their measured budgets; the live
update never happens from this sandbox — production moves in the Vercel
dashboard, and `check:live` is how everyone else confirms it.

---

### Appendix A — the 46 promoted modules (site registry, 2026-10-08)

`dashboard`, `sheet-store`, `specifications`, `inquiries`, `proforma`,
`job-order`, `document-register`, `tax-invoice`, `optimization`,
`process-work-center`, `production-control-center`, `production-pipeline`,
`production-to-delivery`, `stage-cutting`, `stage-holes-cutouts`,
`stage-polish`, `stage-edge-deletion`, `stage-mitring`, `stage-tempering`,
`stage-pairing`, `stage-doubling-lamination`, `delivery-note`,
`report-generator`, `statement-generator`, `job-profitability`,
`vat-submission`, `reports-hub`, `report-executive`, `report-customer`,
`report-salesman`, `report-company`, `report-project`, `report-pi`, `report-jo`,
`report-delivery`, `report-tax-invoice`, `report-glass-spec`,
`report-profitability`, `report-statement`, `report-vat`, `company-center`,
`user-center`, `system-center`, `logs-activity`, `settings`, `records-sync`.
Application folders with **no** card: `Views/AI`, `Views/Sharing` (+ shell-only
folders by design: Login, Splash, Setup, Personal, Diagnostics, CrashReports,
About, Shared).

### Appendix B — measured numbers this audit runs on

- Panel tiers (fit delta): phone +123.0 · sm +137.6 · lg +79.8 · xl +36.4 ·
  2xl +50.6 · wide +115.8 — `measure-panel.py`, 12/12 checks.
- First-load JS `/`: 17 chunks, 6,342 KB raw / 2,022 KB gzip; one shared chunk
  5,456 KB raw / 1,754 KB gzip, referenced by all probed routes.
- Homepage document: 728,663 B raw / 98,573 B gzip. `.next/static/chunks`
  total 7.2 MB. Build: 399/399 pages static.
- Build stamp contract: `/api/health` ↔ page `title` ↔ `git rev-parse --short=7 HEAD`,
  enforced by `audit-site.py` on every pass.

Related: `docs/BUILD_STAMP_AND_DEPLOY_DESYNC_2026-10-07.md` (the live-site
desync and its three account-holder actions), `website/README.md` §"The top
panel's budget" and §"Which build is this?", `website/docs/DEPLOYMENT.md`.
