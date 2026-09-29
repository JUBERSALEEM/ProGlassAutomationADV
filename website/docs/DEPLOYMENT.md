# Deploying the ProGlass Automation website

A complete, step-by-step guide: from the code on this branch to
`https://proglassautomation.com` serving it, with a certificate, on a host that
redeploys itself every time the branch moves.

Written for the person who will actually do it. Every command is meant to be
copy-pasted, every value that must be typed is called out, and the two or three
places where people commonly get stuck are flagged where they happen rather than
in a troubleshooting appendix at the end.

---

## Contents

- [0 · What you are deploying](#0--what-you-are-deploying)
- [1 · Get the code onto `main`](#1--get-the-code-onto-main)
- [2 · Run it on your own machine first](#2--run-it-on-your-own-machine-first)
- [3 · Deploy to Vercel](#3--deploy-to-vercel)
- [4 · Connect the domain](#4--connect-the-domain)
- [5 · Verify it is really live](#5--verify-it-is-really-live)
- [6 · Every change after this](#6--every-change-after-this)
- [7 · The contact form needs a mail transport](#7--the-contact-form-needs-a-mail-transport)
- [8 · Optional: automatic checks on GitHub](#8--optional-automatic-checks-on-github)
- [9 · Other hosts](#9--other-hosts)
- [10 · Troubleshooting](#10--troubleshooting)
- [Appendix A · DNS records at a glance](#appendix-a--dns-records-at-a-glance)
- [Appendix B · Command cheat sheet](#appendix-b--command-cheat-sheet)

---

## 0 · What you are deploying

**This repository is two things at once**, and that is the single most important
fact in this guide:

| Path | What it is | Deployed? |
|---|---|---|
| `website/` | the Next.js site — this is what goes live | **yes** |
| everything else (`Views/`, `Services/`, `App.xaml`, …) | the .NET 8 WPF desktop application | no — it ships as a Windows installer |

So every build and deploy setting points at the **`website`** sub-folder. If a
host is ever told to build the repository root, it will look for a
`package.json` that does not exist and fail — or, worse, look for a `.sln` and
try to build a Windows desktop app on Linux.

**What the site is:** 27 prerendered routes, three server endpoints
(`/api/og` for social images, `/api/demo-request` for the contact form,
`/api/health`), no database, no login, no session. It is a static site with a
couple of functions attached.

**What it needs to run:** Node.js 22 and npm. Nothing else — no environment
variables are required for a build, and none are required to serve.

**Where the domain is configured in the code:** `website/src/lib/site.ts`, the
`url` field. The canonical tags, the Open Graph URLs, `sitemap.xml` and
`robots.txt` are all derived from that one value. If you deploy to a different
domain, change it there and nowhere else:

```ts
// website/src/lib/site.ts
url: "https://proglassautomation.com",
```

---

## 1 · Get the code onto `main`

The work for the site currently lives on a feature branch. Decide how it reaches
`main` — both are fine, pick one:

**Option A — a pull request (recommended, and it gives you a preview deploy).**

```bash
# on your machine, in a clone of the repository
git fetch origin
git checkout main
git merge --ff-only origin/main          # make sure main is current
git checkout arena/01a0ebfc-proglassautomationadv
git push origin arena/01a0ebfc-proglassautomationadv   # already pushed; harmless
```

Then open a pull request from `arena/01a0ebfc-proglassautomationadv` into
`main` on GitHub and merge it. Step 3 deploys from whatever branch you point it
at, and a preview deployment is created for the pull request automatically — so
you can look at the real thing on a real URL before it is public.

**Option B — merge locally and push.**

```bash
git checkout main
git pull
git merge --no-ff arena/01a0ebfc-proglassautomationadv -m "website: deploy"
git push origin main
```

Either way, confirm what arrived:

```bash
git checkout main && git pull
ls website/package.json          # must exist
cat website/src/lib/site.ts | head -25   # check the url value is your domain
```

> **If the site should live in this repository for good**, also make `main` the
> branch the host watches in step 3. Vercel watches `main` by default.

---

## 2 · Run it on your own machine first

Do this before touching a host. A build that fails here will fail there, and
here you can read the error.

```bash
git clone https://github.com/JUBERSALEEM/ProGlassAutomationADV.git
cd ProGlassAutomationADV/website     # ← the website folder, not the root
npm ci                               # installs the exact tree in package-lock.json
npm run build                        # production build; must end with 27/27 pages
npm start                            # serves the build on http://localhost:3000
```

Open `http://localhost:3000` and click around. Then, in a second terminal:

```bash
cd ProGlassAutomationADV/website
python3 scripts/audit-site.py --budget
```

You want `FAILURES: 0` and `WARNINGS: 0`. This audit is the same gate the site
is held to in review: it crawls every route, checks every internal link and
image, the per-page metadata, one `h1` per page, image `alt` text, duplicate
ids, and the bundle budget.

> **Audit the built site, not the dev server.** `npm run dev` serves HTML with
> the development runtime inlined, so a page that is comfortably inside the
> budget in production will report as over it against a dev server. The numbers
> above — and in [step 5](#5--verify-it-is-really-live) — are from `npm start`.

### If you see `Module not found: Can't resolve …`

the code is **not** broken — **dependencies are not installed**. It almost
always happens because someone ran `npx next dev` or `npx next build` directly,
without installing the project first: `npx` happily downloads a copy of Next
into its cache, so *Next itself* runs — while every other package the project
needs is still missing, and the first import from one of them fails.

(The specific case that used to be common here was
`Can't resolve 'marked'` from the blog loader. The blog no longer uses a
third-party Markdown parser — parsing lives in `src/lib/markdown.ts`, written
for this site — so the site's only runtime dependencies are `next`, `react` and
`react-dom`, and there is correspondingly less to go missing.)

```bash
cd website
npm ci
npm run dev        # or: npm run build && npm start
```

Three things now stop you hitting this again:

- the commands are **`npm run dev`** and **`npm run build`**, never `npx next …`;
- `scripts/check-deps.mjs` refuses to continue when the tree is incomplete, and
  prints exactly what to run — it is wired to `predev` and `prebuild`;
- `next.config.ts` runs the same check (from `scripts/dep-check.mjs`), because
  config is loaded on **every** path into Next, including `npx next build`,
  which runs no npm script at all. So even that route now answers with the
  reason rather than a module-resolution trace naming an import.

`npm run doctor` prints the state of the tree at any time — the revision it is
reading, whether all declared dependencies are installed, and whether anything
under `src/` imports a package `package.json` does not declare. **`npm run fix`**
is the one command that repairs a tree that is already broken: it installs
whatever is missing, clears `.next`, prints the revision it just repaired, and
says what to run next.

**Switching revisions with a dev server running** is the one case the checks
cannot catch, because nothing is being compiled for the first time: a dev server
started against an older tree keeps the module graph it compiled, and can keep
replaying the same error — the same file content, from a tree that no longer
contains it — until it is restarted. After `git pull` or `git checkout`:

```bash
# stop the dev server first (Ctrl-C), then:
rm -rf website/.next
cd website && npm ci && npm run dev
```

On a Linux server or in CI, `npm ci` needs the committed
`website/package-lock.json` — it is in the repository, and it is what makes the
build reproducible. Keep it committed.

---

## 3 · Deploy to Vercel

Vercel is the natural host here: it detects Next.js, builds it, gives every
branch a preview URL, and provisions the certificate for your domain. The free
Hobby tier covers a site like this comfortably.

### 3.1 Create the project

1. Sign in at <https://vercel.com> — **Continue with GitHub** is the account you
   want, so the repository list is available.
2. **Add New… → Project**.
3. Find `JUBERSALEEM/ProGlassAutomationADV` in the list and press **Import**.
   If it is not listed, press **Adjust GitHub App Permissions** and grant access
   to that repository.

### 3.2 Set the build settings — the one field that matters

Vercel opens a configuration screen before the first deploy. Everything is
auto-detected **except the folder**, which you must set:

| Field | Value |
|---|---|
| **Root Directory** | **`website`** — press *Edit* and choose the `website` folder |
| Framework Preset | `Next.js` (auto-detected once the root directory is set) |
| Build Command | leave as `next build` — or `npm run build`, which is identical |
| Output Directory | leave as `.next` (Default) |
| Install Command | leave as `npm ci` (Default) |
| Node.js Version | **22.x** — *Settings → General → Node.js Version* if it is not already |

> **This is the step people get wrong.** With the root directory left at the
> repository root, the build looks for `package.json` in `ProGlassAutomationADV/`
> — which is a .NET repository — and fails. The site is in `website/`.

**Environment variables:** none are required. You may add these two later if you
want the contact form to deliver mail (see [step 7](#7--the-contact-form-needs-a-mail-transport)):

| Name | Value | Needed? |
|---|---|---|
| `DEMO_REQUEST_INBOX` | `proglassautomation@gmail.com` | optional — this is already the built-in default |
| `DEMO_REQUEST_PROVIDER` | e.g. `resend` | optional — enables real delivery once a transport is wired |

Press **Deploy**. The first build takes about a minute. When it finishes you get
a URL like `proglass-automation.vercel.app` — **open it and check the site
works** before going anywhere near DNS. This URL keeps working forever, and it is
what you fall back to if a domain record is wrong.

### 3.3 What happens on every deploy afterwards

- a push to `main` → production deployment, on the domain;
- a push to any other branch → a *preview* deployment with its own URL, listed on
  the project's **Deployments** tab and commented onto the pull request;
- **Instant Rollback** on the Deployments tab puts the previous build back in
  seconds if something ships broken.

### 3.4 Deploying with the CLI instead (optional)

If you would rather not use the dashboard:

```bash
npm i -g vercel
cd ProGlassAutomationADV/website
vercel login
vercel            # first run: links the folder, asks the same questions, deploys a preview
vercel --prod     # promotes it to production
```

Answer **`./`** when it asks for the directory (you are already inside
`website/`), and accept the detected Next.js settings.

---

## 4 · Connect the domain

Two halves: tell Vercel the domain is yours, and tell DNS where Vercel is.

### 4.1 Add the domain in Vercel

1. Vercel Dashboard → your project → **Settings → Domains**.
2. Type `proglassautomation.com` and press **Add**.
3. Add the `www` form too: `www.proglassautomation.com`.
4. Vercel now shows, for each name, **the exact records it wants**. Use those
   values — the ones printed in the dashboard for *your* project are
   authoritative, because the CNAME target is project-specific
   (`…vercel-dns-0xx.com`). The general values, which are what you will usually
   see, are in [Appendix A](#appendix-a--dns-records-at-a-glance).

### 4.2 Add the records at your registrar

Open the DNS panel of whoever sold you the domain — GoDaddy, Namecheap,
Cloudflare, Hostinger, Google Domains, … The panel is usually called **DNS**,
**DNS Zone**, **Advanced DNS** or **Manage DNS**.

Add these two records, deleting anything that already occupies the same name:

| Type | Name / Host | Value / Points to | TTL |
|---|---|---|---|
| `A` | `@` (some panels want it blank, or the full domain) | `76.76.21.21` | 3600 / Auto |
| `CNAME` | `www` | `cname.vercel-dns.com` | 3600 / Auto |

Notes that save an hour:

- **Delete the old `A`/`ALIAS`/`CNAME` on `@` and `www` first.** A stale record
  from a previous host (a parking page, a website builder) blocks verification
  and is the most common cause of "Vercel says invalid configuration".
- **Do not point `www` at an IP.** A CNAME is required for the subdomain, and
  some registrars helpfully auto-create an `A` for `www` when you add the apex
  record — remove it.
- **If the domain is behind Cloudflare's proxy** (the orange cloud), switch it to
  **DNS only** (grey cloud) while Vercel verifies and issues the certificate.
  Proxying in front of Vercel breaks the certificate challenge.
- If your registrar offers **nameserver delegation** instead, you can point the
  domain at Vercel entirely: choose **Vercel DNS** in Vercel's domain settings
  and set the registrar's nameservers to `ns1.vercel-dns.com` and
  `ns2.vercel-dns.com`. Then Vercel manages every record and you add nothing
  else. This is the tidier option, and it is required later if you ever want a
  wildcard subdomain.

### 4.3 Choose which one is canonical, and let the other redirect

Pick **one** primary hostname and redirect the other to it. For a marketing site
the www-canonical setup is the more robust of the two, because `www` is a CNAME
and Vercel can move its underlying addresses without you editing DNS:

1. In **Settings → Domains**, find the domain you want as primary —
   `www.proglassautomation.com` — open its menu and choose **Set as Primary**.
2. Vercel offers the matching redirect; accept it, so
   `proglassautomation.com` → `https://www.proglassautomation.com` with a 308.
3. If you prefer the bare domain as the public address (shorter to say, and what
   most people type), set `proglassautomation.com` as primary instead — the
   redirect then runs the other way. The site's canonical tags already follow
   whatever you put in `site.ts`, so either choice is consistent.

Whatever you decide, make `website/src/lib/site.ts` agree with it — that value
is what appears in `<link rel="canonical">`, the sitemap, and the Open Graph
URLs:

```ts
url: "https://www.proglassautomation.com",   // if www is canonical
// url: "https://proglassautomation.com",    // if the apex is canonical (default)
```

### 4.4 Wait for DNS, then check

Propagation is usually minutes; it can take up to 24–48 hours for the worst
registrars. On a machine with `dig`:

```bash
dig +short A proglassautomation.com          # expect 76.76.21.21 (or your project's value)
dig +short CNAME www.proglassautomation.com  # expect cname.vercel-dns.com (or your project's target)
dig +short NS proglassautomation.com         # ns1/ns2.vercel-dns.com if you delegated
```

Then, once Vercel shows **Valid Configuration** on both names:

```bash
curl -sI https://proglassautomation.com | head -1     # HTTP/2 200
curl -sI https://www.proglassautomation.com | head -1 # 308 redirect, if www is not primary
```

**The certificate is automatic.** Vercel issues and renews a Let's Encrypt
certificate as soon as DNS validates — there is no separate step, no certbot, no
renewal to remember. Do not add a CAA record unless you already have one; if you
do have one, it must allow `letsencrypt.org`, for example:

```
proglassautomation.com.  CAA  0 issue "letsencrypt.org"
```

---

## 5 · Verify it is really live

Do not sign this off on "the page loaded". Five minutes with this list prevents
almost every embarrassing launch.

**In the browser**

- [ ] `https://proglassautomation.com` — the padlock is present, no certificate
      warning, and the address does not flip between `www` and the apex on every
      page.
- [ ] Walk the navigation: Home, Product, Optimization, Resources, About,
      Developer, Sponsors, Pricing, Screenshots, Worked Example, Blog, Contact.
- [ ] Open the theme studio (corner button) and step the palette families — the
      site should repaint, and the choice should survive a reload.
- [ ] Click a screenshot to open the lightbox; arrow keys and Escape work.
- [ ] Open the site on a phone (or narrow the window to 360 px): the top panel
      collapses to the menu button, and nothing overflows sideways.
- [ ] Open the browser console: no red errors, on any page.

**The machine checks**

```bash
cd website
python3 scripts/audit-site.py https://proglassautomation.com --budget
```

The audit accepts any origin — run it against production, not just localhost.
Expect `FAILURES: 0` and `WARNINGS: 0`.

```bash
# every page answers 200
curl -s https://proglassautomation.com/sitemap.xml | grep -o '<loc>[^<]*' | sed 's/<loc>//' \
  | while read -r u; do printf '%s %s\n' "$(curl -s -o /dev/null -w '%{http_code}' "$u")" "$u"; done

# the www/apex decision is enforced, one way round
curl -sI https://proglassautomation.com | grep -i '^location'
curl -sI https://www.proglassautomation.com | grep -i '^location'

# the security headers from next.config.ts are present in production
curl -sI https://proglassautomation.com | grep -iE 'content-security-policy|strict-transport-security|x-frame-options'

# a social card renders (this is what a WhatsApp or LinkedIn link shows)
curl -s -o /tmp/og.png -w '%{http_code} %{content_type} %{size_download}\n' \
  'https://proglassautomation.com/api/og?title=ProGlass%20Automation'

# the demo form reports its own transport state
curl -s https://proglassautomation.com/api/demo-request
```

**Then hand it to the indexers**

1. <https://search.google.com/search-console> → **Add property** → *Domain* →
   verify with the TXT record it gives you (leave that record in place
   permanently).
2. **Sitemaps** → submit `https://proglassautomation.com/sitemap.xml`.
3. **URL Inspection** → paste the homepage → *Request indexing*.
4. Optional: <https://www.bing.com/webmasters> accepts an import from Search
   Console.

The site already ships `robots.txt`, a sitemap, canonical URLs, Open Graph and
Twitter metadata, and JSON-LD structured data — there is nothing to build, only
to submit.

---

## 6 · Every change after this

Nothing. That is the point of deploying from Git: **push to `main`, and the site
rebuilds and goes live about a minute later.**

```bash
git checkout main
git pull
# ... edit, test locally with npm run dev ...
npm run build && npm run typecheck && npm run lint     # the same gates CI runs
git commit -am "site: …"
git push
```

Good practice while working on something larger:

- push the work to a branch — the preview URL on the pull request lets you and
  anyone else review the real site before it is public;
- make changes to post content in `website/posts/*.md` and to copy in
  `website/src/data/*.ts`; both are plain files, and both fail the build loudly
  if something is wrong (a missing title, an unknown category, a broken link);
- if a deployment must be undone, use **Instant Rollback** rather than
  force-pushing history.

**Two things that are easy to forget**

1. Changing `website/src/lib/site.ts` (`url`, `version`, contact details) changes
   the canonical domain, the sitemap and the structured data — redeploy after
   changing it.
2. Changing anything in `README_ASSETS/` (the brand banner) requires re-encoding
   the site's derivatives before the site picks it up:

```bash
cd website
npx tsx scripts/render-hero-banner.mts    # only if you want the banner redrawn
npx tsx scripts/render-hero-backdrop.mts  # …and the home hero's backdrop
npm run assets:optimize -- --from ..
```

---

## 7 · The contact form needs a mail transport

`/api/demo-request` is the single integration point for email, and it is
deliberately honest: with no transport configured it returns `503` and tells the
visitor to email `proglassautomation@gmail.com` directly, rather than silently
swallowing their message. You can see its current state from any machine:

```bash
curl -s https://proglassautomation.com/api/demo-request
# {"endpoint":"/api/demo-request","method":"POST","configured":false,…}
```

To make it deliver, pick a provider (Resend, Postmark, SendGrid, Mailgun, or a
SMTP relay) and wire it at the marked block in
`website/src/app/api/demo-request/route.ts`:

```ts
// ── Replace this block with the real transport ──────────────────────
// Example (Resend): the SDK call belongs here, using process.env.RESEND_API_KEY.
```

Then set the environment variables in Vercel (**Settings → Environment
Variables**, applied to Production *and* Preview) and redeploy:

| Name | Example | Note |
|---|---|---|
| `DEMO_REQUEST_PROVIDER` | `resend` | flips `configured` to `true` |
| `RESEND_API_KEY` | `re_…` | the provider's key — a secret; never commit it |
| `DEMO_REQUEST_INBOX` | `proglassautomation@gmail.com` | where requests land |

Secrets belong in Vercel's environment variables, never in the repository. A key
that has ever been committed should be revoked and reissued.

---

## 8 · Optional: automatic checks on GitHub

A ready-made workflow exists for this site: it runs typecheck, lint, the build
and the crawl audit, plus the bundle budget, on every push and pull request that
touches `website/`.

The file is `.github/workflows/website.yml`. If it is not in the repository yet,
add it through GitHub's web UI — **Add file → Create new file**, name it
`.github/workflows/website.yml`, paste the contents, commit to `main`. (The
web UI is the reliable path here: automation tokens are usually not permitted to
create workflow files.)

Once it is there, every push gets a green tick or a red cross with the failing
step's log attached, and `main` can be protected so a red build cannot merge.

---

## 9 · Other hosts

Vercel is the recommendation, because Next.js is its native framework. These are
the real alternatives, with what each costs you.

| Host | How | Trade-off |
|---|---|---|
| **Netlify** | Import the repo, set **Base directory = `website`**, build `npm run build`, publish `.next`; the Next.js runtime plugin is applied automatically | Works well. Slightly less first-class than Vercel for Next 16 features; check the plugin's Next 16 support before committing |
| **Cloudflare Pages** | Build command `npm run build`, root `website`, and use the `@cloudflare/next-on-pages` adapter | Cheapest and fastest edge, but the adapter is an extra moving part on every Next upgrade |
| **A VPS you manage** (Hetzner, DigitalOcean, Lightsail) | `git clone`, `npm ci`, `npm run build`, run `npm start` under `pm2` or a systemd unit, reverse-proxy with nginx, certificate with certbot | No platform limits and no build minutes; you own updates, monitoring and renewals. Nginx `proxy_set_header Host` and the `X-Forwarded-Proto` header must be passed through, or redirects and the canonical URL will be wrong |
| **GitHub Pages** | ❌ **not without changing the site** | Pages serves static files only. This site has three server routes — `/api/og` (social images), `/api/demo-request` and `/api/health` — and `output: "export"` is deliberately not set in `next.config.ts`. You would have to drop the social-image route and the contact endpoint, then re-point everything at a static export |

Whichever you choose, the two settings from [step 3.2](#32-set-the-build-settings--the-one-field-that-matters) apply
everywhere: **the site is in `website/`**, and **Node 22**.

---

## 10 · Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `Module not found: Can't resolve …` (any package) | dependencies were never installed in `website/` — usually because `npx next build` was run instead of `npm run build` | `cd website && npm ci`, then `npm run build`. The pre-build guard prints this for you before Next starts |
| `Can't resolve …` for a package you have never heard of, after `npm ci` | the file being compiled is from an older revision of the tree, or a dependency was removed and the import was left behind — installing cannot satisfy either | `npm run doctor` names the package and the file; `git log --oneline -1` says which copy you are building. Take the current revision |
| the same old error keeps coming back after the fix, same file content each time | the dev server started against an older tree is still serving its compiled module graph | stop it, `rm -rf website/.next`, `npm ci`, `npm run dev`, then hard-reload the browser tab |
| the dev overlay keeps showing the same error — same file content — after the fix is in the tree | a development overlay is drawn by the page that hit the error and stays on screen until that page is reloaded; a dev server can also replay the module graph it compiled earlier | `npm run fix`, restart `npm run dev`, then reload the tab completely (`Ctrl-Shift-R`). A production server (`npm start`) cannot show that overlay at all |
| Build fails: *no `package.json`* / it tries to build a `.sln` | the host's root directory is the repository root | set **Root Directory = `website`** |
| Build fails on Node version or on `node:fs` | Node too old, or the WPF project was built instead | **Node.js Version = 22.x** in the project settings |
| Site deploys but every page is unstyled | the build output directory was changed from `.next` | reset **Output Directory** to the default |
| Vercel: *Invalid Configuration* on the domain | a stale `A`/`CNAME` from a previous host still exists, or Cloudflare's proxy is on | delete the old records, add the ones in Appendix A, set Cloudflare to **DNS only**, press **Refresh** |
| Domain works, `www` shows a certificate error | only one of the two names was added in Vercel | add both, then let the non-primary one redirect |
| Redirect loop between `www` and the apex | both names are set to redirect, or the primary is not set | pick one primary in **Settings → Domains**; the other redirects to it |
| `/api/og` returns 500 in production | a font the image route fetches is missing, or the route was bundled without its assets | redeploy after confirming the route works locally with `npm start` |
| Blog posts render as plain text, or a heading disappears | the post uses a construct `src/lib/markdown.ts` does not implement (footnotes, reference links, setext headings, indented code blocks) | write it as one of the supported constructs — see the header comment in that file; the parser is deliberately a documented subset rather than a guess |
| The contact form says *no mail transport configured* | `DEMO_REQUEST_PROVIDER` is unset | that is deliberate — see [step 7](#7--the-contact-form-needs-a-mail-transport) |
| Old content after a deploy | browser or CDN cache | hard-reload (`Ctrl/Cmd + Shift + R`); confirm the deployment's *Ready* timestamp is newer than your commit |
| `HSTS preload` warning from a checker | the header is sent with `preload` before the domain is submitted to the preload list | harmless; submit at <https://hstspreload.org> only once you are certain every subdomain will stay HTTPS — preload is hard to undo |
| `The build cache is stale` locally | a `.next` directory left from a different commit | the `dev` script tells you; `rm -rf .next` and rebuild |
| The audit reports `/how-it-works` or `/developer` over the HTML budget | it was run against `npm run dev`, whose HTML carries the dev runtime | audit the production build: `npm run build && npm start`, then re-run |
| A build on a fresh machine behaves differently from yours | `npm install` resolved newer minor versions than your lockfile pins | always `npm ci`; it installs exactly what `package-lock.json` records |

---

## Appendix A · DNS records at a glance

Replace `proglassautomation.com` with your own domain. The dashboard values for
*your* project win if they differ — the CNAME target in particular is
project-specific.

| # | Type | Name / Host | Value | TTL | Why |
|---|---|---|---|---|---|
| 1 | `A` | `@` | `76.76.21.21` | 3600 | the apex cannot be a CNAME (DNS reserves the name for `NS`/`SOA`), so it points at Vercel's anycast address |
| 2 | `CNAME` | `www` | `cname.vercel-dns.com` | 3600 | subdomains can be CNAMEs, which lets Vercel move addresses without you editing DNS |
| 3 | `TXT` | `_vercel` | *(shown in the Vercel dashboard)* | 3600 | only if Vercel asks, to prove ownership |
| 4 | `TXT` | `@` | *(from Google Search Console)* | 3600 | optional, for Search Console verification — keep it permanently |
| 5 | `CAA` | `@` | `0 issue "letsencrypt.org"` | 3600 | **only if you already use CAA**; it must permit Lets Encrypt or the certificate will not issue |

Delegating the whole domain instead? Set the registrar's nameservers to
`ns1.vercel-dns.com` and `ns2.vercel-dns.com`, enable **Vercel DNS**, and skip
records 1–3.

---

## Appendix B · Command cheat sheet

```bash
# ── work on the site locally ────────────────────────────────────────────
cd ProGlassAutomationADV/website
npm ci                                   # install exactly what the lockfile pins
npm run dev                              # http://localhost:3000, hot reload
npm run typecheck && npm run lint        # the two static gates
npm run build                            # production build, 27/27 pages prerendered
npm start                                # serve the production build
python3 scripts/audit-site.py --budget   # crawl localhost and enforce the budget

# ── audit a deployment ──────────────────────────────────────────────────
python3 scripts/audit-site.py https://proglassautomation.com --budget

# ── brand assets (only when artwork changes) ────────────────────────────
npx tsx scripts/render-hero-banner.mts   # redraw README_ASSETS/hero.png from the data
npx tsx scripts/render-hero-backdrop.mts # redraw the home hero's backdrop
npm run assets:optimize                  # re-encode both in AVIF/WebP at every width
npm run assets:optimize -- --from ..     # re-encode the site's avif/webp/png derivatives

# ── Vercel CLI ──────────────────────────────────────────────────────────
npm i -g vercel
vercel login
vercel                                   # preview deployment
vercel --prod                            # production deployment
vercel logs                              # runtime logs
vercel domains ls                        # domains across the team
vercel domains inspect proglassautomation.com   # the exact records this project needs
vercel domains add proglassautomation.com
vercel certs ls                          # has the certificate been issued?

# ── DNS checks ──────────────────────────────────────────────────────────
dig +short A proglassautomation.com
dig +short CNAME www.proglassautomation.com
dig +short NS proglassautomation.com
curl -sI https://proglassautomation.com | head -5
```

---

*Last reviewed: 2026-09-29. If a host changes its interface, the values in this
guide that matter are the ones in [Appendix A](#appendix-a--dns-records-at-a-glance)
and the root-directory setting in [step 3.2](#32-set-the-build-settings--the-one-field-that-matters);
everything else is those two facts applied.*
