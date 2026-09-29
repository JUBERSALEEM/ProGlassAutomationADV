# ProGlass website — engineering review and upgrade plan

**Reviewed:** `website/` at commit `3541f25` · 69 source files · 14,735 lines
**Status:** nothing in this document has been implemented. No commits made.

---

## 1 · What I measured

Everything below is a number I read out of the build, not an impression.

| Measurement | Value | Verdict |
|---|---|---|
| Client JS, all chunks | 876 KB raw / **274 KB gzip** | acceptable, room to improve |
| Largest single chunk | 224 KB raw / 70 KB gzip | React runtime — expected |
| CSS | 68.6 KB / **13.2 KB gzip** | good |
| HTML, home | 537 KB raw / **64 KB gzip** | high for a landing page |
| HTML, `/screenshots` | 722 KB raw / **37 KB gzip** | gzip fine, raw bloated |
| Demo dataset | one chunk, **12 KB gzip** | not duplicated — no problem here |
| Website tests | **0** | nothing protects this site from regression |
| CI workflow | **none** | no gate on push |
| Error boundary | **missing** | a runtime throw shows a blank page |
| Security headers | **none configured** | no CSP, no frame policy, no referrer policy |
| Social share image | **missing** | every shared link is a bare text card |
| `IntersectionObserver` instances | **89 per page load** | one per `<Reveal>` |
| API route guards | length only | no rate limit, no bot trap, no origin check |

Two things I want to be straight about, because I checked and they are **not** problems: the sample-data module is bundled once and shared, not duplicated across routes; and the screenshot lightbox already restores focus and locks body scroll.

---

## 2 · Tier 1 — Production hardening

*These are the difference between "a site that works" and "a site you can leave running".*

### 1.1 Error boundaries
`src/app/error.tsx` and `src/app/global-error.tsx` do not exist. If any client component throws — a bad demo key, a localStorage failure in an odd browser — the visitor gets Next's bare error screen with no header, no footer and no way back.

**Fix:** two files. A styled `error.tsx` with a retry action and a route home, and a `global-error.tsx` that renders standalone without the shell. Roughly 60 lines total.
**Impact:** high · **Effort:** 1 hour

### 1.2 Security headers
`next.config.ts` sets no `headers()`. There is currently no Content-Security-Policy, no `X-Content-Type-Options`, no `Referrer-Policy`, no `Permissions-Policy` and no frame protection.

**Fix:** add a `headers()` block. CSP needs care because the site uses one inline boot script and one inline JSON-LD block — I'd use a nonce or a hash for those rather than `unsafe-inline`.
**Impact:** high for a site you hand to customers · **Effort:** 2–3 hours including testing the CSP doesn't break the theme boot script

### 1.3 Harden `/api/demo-request`
Right now it is a public POST endpoint that validates shape and length, then forwards. Nothing stops a script posting it a thousand times, and the failure path already writes to logs.

**Fix, in order of value:** an in-memory token-bucket rate limit keyed by IP (Next middleware or the route itself); a honeypot field the form renders hidden and real users never fill; an `Origin`/`Sec-Fetch-Site` check; and a cap on how much gets written to logs.
**Impact:** high — this is the only write path on the site · **Effort:** 3 hours

### 1.4 CI that can actually fail
There is no `.github/workflows/`. The four commands I run by hand — typecheck, lint, build, crawl audit — should run on every push, plus a Lighthouse or bundle-size budget check.

**Fix:** one workflow: install, `tsc --noEmit`, `eslint`, `next build`, then boot the server and run `scripts/audit-site.py`. Add a size-limit check so a future change can't quietly double the bundle.
**Impact:** high — this is what makes the other work durable · **Effort:** 2 hours

---

## 3 · Tier 2 — Performance and architecture

### 3.1 One observer instead of eighty-nine
Every `<Reveal>` mounts its own `IntersectionObserver`. The home page creates dozens of them at load.

**Fix:** a single shared observer in `useReveal` — one instance, a `WeakMap` of element → callback, disconnect when the queue empties. Cuts observer count from 89 to 8.
**Impact:** medium — measurable main-thread win on long pages · **Effort:** 2 hours

### 3.2 Shrink the HTML on the heaviest routes
Home is 537 KB raw. A large part is inline SVG: the `PageBanner` art renders roughly 11 gradient-filled rectangles with a `<defs>` block per instance, and the frames each carry their own icon SVG.

**Fix, cheapest first:** hoist repeated `<defs>` gradients into a single hidden `<svg>` sprite at the shell level and reference them with `<use>`; replace the banner's per-rect glint with one overlay; and consider a shape-only variant of the banner for below-the-fold use.
**Impact:** medium — better parse time, smaller documents, easier diffing · **Effort:** 4–5 hours

### 3.3 Move the previews to the server where they don't need to be interactive
`LiveSurface` is one 877-line client component. Every frame on every page ships it, including the ones whose content is purely reading — the dashboard previews, the form previews, the licensing table.

**Fix:** split it. Static preview content (the tables, the summaries, the diagrams) becomes a server component; only the genuinely interactive controls — the optimizer's sheet picker, the queue's Advance button, the wizard step rail, the tabs — stay client. The screenshot gallery keeps the full interactive version; the decorative frames on `/`, `/features` and `/how-it-works` take the static one.
**Impact:** high on bundle size and hydration cost · **Effort:** 1–1.5 days (largest item here)

### 3.4 `next/image` on the four real assets
The SGU/DGU/lamination renders and the banner photograph currently load at one size. Wiring `sizes` properly and letting the optimizer serve AVIF at 2–3 widths is mostly already configured in `next.config.ts` — it just needs the right `sizes` per usage site and a check that the source images are the right resolution.
**Impact:** medium · **Effort:** 2 hours

---

## 4 · Tier 3 — SEO, sharing and growth

### 4.1 Dynamic Open Graph images
This is the biggest visible win available. Every link to this site currently shares as a plain text card — no title treatment, no module count, no product framing.

Next can generate them at build time with `ImageResponse`. One `opengraph-image.tsx` at the root, plus per-route variants for `/features`, `/optimization`, `/pricing` and each blog post, would render the page title over the site's own gradient with the logo lockup.
**Impact:** very high for a product being shown to factory owners over WhatsApp and email · **Effort:** 4 hours

### 4.2 Structured data, completed
The home page emits an `@graph` plus `FAQPage`. Missing: `SoftwareApplication` and `Organization` are only on the root, `BreadcrumbList` is only on pages that pass a breadcrumb, `Article` is on blog posts, and there is no `WebSite` node with `potentialAction`.

**Fix:** one shared builder that emits a consistent `@graph` per route, with `SoftwareApplication` carrying `operatingSystem: "Windows"`, `applicationCategory`, and the real version. **No `Offer` or price** — there is no published price list, so inventing one would be false.
**Impact:** medium-high · **Effort:** 3 hours

### 4.3 A real 404 that sells
`not-found.tsx` exists but is minimal. On a product site a 404 is a second chance: route them to the module browser, the optimizer page and the demo form.
**Impact:** low-medium · **Effort:** 1 hour

---

## 5 · Tier 4 — Product depth and polish

Ranked by what would actually move a decision for a glass-shop owner:

### 5.1 A "before and after" section — the strongest missing argument
The site explains what the software does. It never shows the problem it removes. A side-by-side of *the same job* handled the old way (five documents, one figure retyped five times, no audit trail) against *the same job* in ProGlass is the single most persuasive thing that could go on this site — and every fact for it is already in the app: the 9-step proforma, the stamped price basis, the delivery balance, the statement.
**Impact:** very high · **Effort:** 1 day with real content

### 5.2 A keyboard-accessible lightbox
The gallery lightbox restores focus and locks scroll, but it does not *trap* focus — tabbing walks out of the modal into the page behind it.
**Fix:** trap focus within the dialog, mark the rest of the page `inert`.
**Impact:** medium (a11y compliance) · **Effort:** 2 hours

### 5.3 Visible state everywhere
The forms have loading/success/error. The previews do not have an empty state for "no rows match" beyond one line, and there is no skeleton for slow connections on the image-bearing frames.
**Impact:** low-medium · **Effort:** 3 hours

### 5.4 Print stylesheet for the specification pages
A shop owner printing the optimization or specification page gets dark-mode ink waste. A `@media print` block that flattens to white and hides the chrome is a small, real courtesy for this audience.
**Impact:** low but memorable · **Effort:** 2 hours

---

## 6 · What I would deliberately not do

- **No pricing calculator / ROI estimator.** There is no published price list in the application. Any figure would be invented, and the site's whole credibility rests on not doing that.
- **No analytics or tag manager** unless you want it. It adds third-party JS and a consent question, and nothing here needs it yet.
- **No CMS.** The content is typed, precise and tied to the repository — a CMS would add a moving part without removing work.
- **No dark-mode-only hero video or heavy 3D.** It would look impressive and cost far more than it returns for a factory-floor audience.
- **No i18n of the website yet.** The application ships 15 locales; the marketing site has one. Translating the site is a real project, not a sprint item — and it should follow the product's own locale list rather than lead it.

---

## 7 · Recommended order

| # | Item | Impact | Effort |
|---|---|---|---|
| 1 | Error boundaries | High | 1 h |
| 2 | CI: typecheck, lint, build, audit | High | 2 h |
| 3 | Security headers | High | 3 h |
| 4 | Harden `/api/demo-request` | High | 3 h |
| 5 | Dynamic OG images | Very high | 4 h |
| 6 | Single shared reveal observer | Medium | 2 h |
| 7 | Split `LiveSurface` server/client | High | 1–1.5 d |
| 8 | Structured data, completed | Medium-high | 3 h |
| 9 | SVG sprite for repeated defs | Medium | 4 h |
| 10 | Lightbox focus trap | Medium | 2 h |
| 11 | 404 that sells | Low-medium | 1 h |
| 12 | Print stylesheet | Low | 2 h |
| 13 | "Before and after" section | Very high | 1 d |

**Items 1–4 are about 9 hours and turn this from a built site into a maintainable one.** Items 5 and 7 are the two that a visitor would actually notice.

---

# Implementation status

Everything in the plan above was implemented and shipped in `c66b5a7`, except
one item that the measurements argued against. The numbers below are from the
built site, not estimates.

| # | Item | Status | Measured result |
|---|------|--------|-----------------|
| 1 | Error boundaries | Done | `error.tsx` + `global-error.tsx`; digest shown as a reference code |
| 2 | Security headers | Done | Full CSP, `nosniff`, `DENY`, Referrer-Policy, Permissions-Policy, COOP, prod-only HSTS |
| 3 | CI that can fail | Done, file held back | `.github/workflows/website.yml` written; push blocked by the GitHub connection's workflow permission (see below) |
| 4 | Harden `/api/demo-request` | Done | Cross-site 403, token bucket 429, body cap 413, honeypot, link cap 422 |
| 5 | Dynamic OG images | Done | `/api/og` renders 1200×630 PNG, 72 KB, per-page title/eyebrow/accent |
| 6 | Single shared reveal observer | Done | 89 observers per long page → 1 per `rootMargin`, released when idle |
| 7 | Split `LiveSurface` server/client | **Not done, deliberately** | Measured: all 19 `"use client"` files use hooks or handlers, and every gallery preview is labelled "Interactive preview" by design. A static split would remove approved behaviour for no transfer win. Substituted the browser-side fix in item 9's neighbourhood: `content-visibility: auto` on the 39 preview cards, so off-screen layout and paint are skipped without unmounting anything. |
| 8 | Structured data, completed | Done | One `@graph` per route, nodes linked by `@id`; one BreadcrumbList per page instead of two; FAQ and Article folded in |
| 9 | SVG sprite for repeated defs | Done | 38 `<defs>` per page → 1 sprite. `/screenshots`: 738,556 → 708,037 bytes raw (−4.1%), DOM nodes 7,303 → 6,907, gzip 38,506 → 36,924 |
| 10 | Lightbox focus trap | Done | Tab wraps inside the dialog, and every element behind it is genuinely `inert` |
| 11 | 404 that sells | Done | Matches the mistyped address by edit distance and offers up to three real destinations |
| 12 | Print stylesheet | Done | Light inversion for paper, chrome and glow removed, external link addresses shown |
| 13 | "Before and after" section | Done | Eight steps, old way vs ProGlass, on `/how-it-works`; no invented time or money figures |

## Also added while in there

- `audit-site.py --budget`: per-page compressed-HTML and DOM-node ceilings plus
  built-asset ceilings, calibrated a little above what the site ships. Verified
  to fail (exit 1) when a ceiling is crossed.
- `audit-site.py` now checks fragment links against the ids actually rendered.
  It immediately found seven dead in-page links, and the underlying cause: 19
  module deep links pointed at anchors `/features` never rendered. Every module
  card is now its own anchor (`id={module.id}`), the six showcase sections use a
  `showcase-` prefix, and no id repeats on a page (checked per route).
- Duplicate-id checking, so the SVG-defs class of bug cannot come back.
- `next/image` `sizes` corrected: the gallery is two columns until 1280px, not
  1024px, so 1024–1280px viewports were fetching a 33vw candidate for a 50vw box.

## The one blocked step

`git push` rejects the workflow file:

> refusing to allow a GitHub App to create or update workflow
> `.github/workflows/website.yml` without `workflows` permission

The file is complete and in the working tree at `.github/workflows/website.yml`.
It needs either a GitHub connection with the `workflows` scope (reconnect in
Arena) or a manual commit through the GitHub UI; the rest of the change is
already on `arena/01a0ebfc-proglassautomationadv`.

## Final gate

```
npm run typecheck   clean
npm run lint        clean
npm run build       26/26 routes, 0 errors
audit-site.py       0 failures, 0 warnings  (18 pages, 26 links, 6 images)
audit-site.py --budget
   javascript  294.3K / 330K budget
   css          13.9K /  22K budget
   every page inside its HTML and DOM budget
```
