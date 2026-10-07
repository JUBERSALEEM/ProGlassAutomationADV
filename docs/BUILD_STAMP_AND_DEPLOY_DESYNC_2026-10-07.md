# The build stamp, and why the preview and the live site disagreed — 2026-10-07

Website only (`website/`), plus one section of the deployment guide. Branch
`arena/ff328a9c-proglassautomationadv`, commit `dc70667`.

## What was asked

"In here preview is different and live website is different — find the issue.
Enable date and time in the top panel so we could know which latest time it was
updated. Fix the desync. Update remote and this both together."

## What the build stamp is

A date, a time in UTC and the revision, printed in the top panel of every page,
in the mobile panel, in the footer, and served as JSON by `/api/health`.

| Where | What it shows |
|---|---|
| top panel (from `xl`) | `2026-10-07` / `11:36 UTC`, two lines, mono |
| hover text | `Updated 2026-10-07 11:36 UTC · revision dc70667 · arena/ff328a9c-… · Live site` |
| mobile panel, footer | the same line with the revision |
| `GET /api/health` | `{status, version, revision, branch, environment, builtAt}` |

The values are computed once per build by `scripts/build-stamp.mjs` (Vercel's
own variables when Vercel is building, git otherwise) and published through the
`env` block in `next.config.ts`. That setting substitutes them **as literals**
into the compiled bundle, which is the property that makes the stamp usable: a
page prerendered during the build and a route handler that starts cold an hour
later report the same instant. Reading the clock at request time would have made
the stamp a lie on exactly the pages that render on demand.

Two details that are easy to get wrong and were:

`process.env.PROGLASS_BUILD_TIME` is a *substitution*, not a lookup. A computed
read — `process.env[name]` behind a helper — is invisible to it, so the first
version of `src/lib/buildStamp.ts` worked in `next dev` and rendered
"unknown / unknown" in a production build. The reads are now written out one per
value, and the reason is in the file.

The instant is formatted by slicing the ISO string, not through
`Intl.DateTimeFormat`: an ISO instant is already UTC, so the stamp reads the same
in all fifteen languages without locale data, and a formatter could produce one
string on the server and another in the browser — a hydration mismatch — which a
stamp whose whole purpose is trustworthiness must never be.

## The issue that was found

The repository is deployed by **two Vercel projects**, both building
`website/` of this repository:

| Project | Canonical domain | What it actually serves |
|---|---|---|
| `proglassautomationadv-website` | `proglassautomationadv-website.vercel.app` | **404 `NOT_FOUND`** — the project has no production deployment. Its previews are behind Vercel Authentication, so an unauthenticated visitor gets a login wall. |
| `proglassautomationadv-website-tkim` | `proglassautomationadv-website-tkim.vercel.app` | the site, publicly, on the production domain |

`https://proglassautomationadv-website.vercel.app` is also the repository's
**homepage field** on GitHub, so the link a visitor clicks first is the dead one.

Then the lag itself, measured rather than guessed:

- `arena/ff328a9c-proglassautomationadv` was pushed twice during the afternoon;
  each push produced a **preview** deployment on both projects — Vercel's bot
  comment on PR #22 lists a `-git-…` alias for the latest push on both — and the
  previews are auth-protected, so they are not a URL a visitor can open;
- the **production** domain kept serving the build it was promoted to earlier
  (the deployment that finished at 11:21:16Z, which corresponds to commit
  `c70e7d0`), so it happened to contain this session's task 4 and task 5 work
  but not the commit pushed afterwards;
- proof, not inference: `/api/health` on the production domain answers with
  Next's own 404 page — the route does not exist in that deployment — while the
  branch's latest commit defines it;
- and the same production build does contain the task-4 gate and the 22-size
  sample list (`OptimizationRunGate.tsx`, `demoFor` in `sheetPlan.ts`), which the
  *previous* branch `arena/01a10ad1-…` at `35c5384` does not have at all. So the
  production deployment is a build of this branch, promoted once — not a build of
  some other branch.

In one sentence: **pushes to the branch produce protected previews, the live
domain moves only when a deployment is promoted, and until now nothing on the
page said which build a visitor was looking at.**

Two side findings, both worth knowing:

- `proglassautomation.com` — the domain the canonical URLs, sitemap and Open
  Graph tags are written against (`src/lib/site.ts`) — **is not registered**:
  a DNS lookup at `.com` answers `NXDOMAIN`. Every canonical URL on the site
  currently points at a name nobody owns.
- The repository's homepage field points at the project whose canonical domain
  returns 404.

## The fix delivered here

1. **The stamp**, as described above — so the age of a build is visible on the
   page instead of having to be inferred.
2. **`/api/health`**, prerendered, so the same facts are machine-readable:
   `curl -s https://…/api/health`.
3. **`scripts/audit-site.py` checks the stamp** — the page and its own endpoint
   must state the same instant and revision (a page disagreeing with its
   endpoint is a worse bug than having no stamp), and the new `--fresh` flag
   compares the audited origin's revision with this working tree's:

   ```bash
   python3 scripts/audit-site.py https://proglassautomationadv-website-tkim.vercel.app --fresh
   ```

   That command is the desync detector. It fails with the two revisions named
   instead of leaving the difference to the eye.
4. **The deployment guide** gained the cause and the one-setting fix:
   Vercel → the project → *Settings → Git → Production Branch*. A push to the
   production branch deploys the domain; a push to any other branch produces a
   preview that never moves it, and the build log says `Ready` either way.
5. **The top panel** was re-budgeted for the new control: the ghost "See the
   app" shortcut now waits for 1536px, because the row carries three settings
   controls and a fact, and the shortcut is the only item in it whose
   destination is already one hover away in the Product menu.

## What cannot be done from the repository

Anything in the Vercel dashboard. There is no Vercel credential in this
environment, and the repository token cannot edit the repository's own homepage
field either (`403 Resource not accessible by integration`). So, three actions
that remain for the account holder, in order of value:

1. **Set the production branch** on `proglassautomationadv-website-tkim` to
   `arena/ff328a9c-proglassautomationadv` (or to whichever branch the work is
   going to keep landing on). After that, every push moves the live site.
2. **Delete or repurpose the duplicate project** `proglassautomationadv-website`
   — two projects building the same repository doubles the build time and gives
   two answers to "what is live". If it is kept, its production branch needs the
   same setting, or its domain will keep returning 404.
3. **Fix the repository's homepage field** (About → the Website box) to point at
   the domain that serves the site, and register `proglassautomation.com` — or
   change `site.url` in `src/lib/site.ts` — so the canonical URLs stop pointing
   at a name that does not resolve.

## The panel the stamp went into was over budget, and that is what "cut off" was

Adding a fact to a header assumes the header has room. It did not, and had not
since the row grew its settings controls: measured from the site's own font
files, the row wanted **1474px** of content at a 1280px window and **1318px** at
1024 — roughly 300px and 390px more than either width has. A `nowrap` flex row
does not overflow politely: its children shrink and their labels wrap mid-phrase
("Request a / Demo") inside a bar whose height is fixed, which is what a squeezed
panel looks like.

`scripts/measure-panel.py` now does that arithmetic from the real advance widths
of Inter and JetBrains Mono, with every constant carrying the Tailwind class it
was read from, and `Navbar.tsx` sheds items in a fixed order instead of
squeezing:

| Tier | From | Shows | Needs / has |
|---|---|---|---|
| `wide` (new screen, 1920) | 1920 | everything: nav with glyph icons, palette word, both CTAs, stamp | 1616 / 1777 |
| `2xl` | 1536 | nav, palette word, primary CTA, stamp | 1338 / 1393 |
| `xl` | 1280 | nav without glyph icons, settings, stamp | 1105 / 1169 |
| `lg` | 1024 | Menu button, settings, CTA, stamp | 882 / 929 |
| `sm` | 640 | wordmark, language, reading switch, palette, menu button | 431 / 569 |
| phone | 360 | mark alone, palette, menu button | 182 / 305 |

The nav pill now starts at `xl` rather than `lg`, and between 1024 and 1279 it
opens from a Menu button in the settings cluster — the same sheet the phone menu
uses, already built and already translated. The nav's glyph icons and the
palette's word return at 1920, where everything fits at once, and two visible
choices fall out of the bottom of the ladder: the lockup drops to the mark alone
below `lg`, and the language button waits for `sm`. The full lockup is in the
footer on every screen and all fifteen languages are in the sheet, one tap away.

Run `python3 scripts/measure-panel.py` after touching the panel; it exits 1 when
any tier is over.

## The check that closes the loop

`npm run check:live` (`scripts/check-live.mjs`) asks every origin the site is
meant to be reachable at for its `/api/health` and holds the answer against the
revision this tree is on:

```
the tree:      422172c on arena/ff328a9c-proglassautomationadv
the branch:    422172c

live            https://proglassautomationadv-website-tkim.vercel.app
                no stamp — this build predates /api/health
                NO ANSWER — see the line above

second project  https://proglassautomationadv-website.vercel.app
                unreachable — fetch failed
                NO ANSWER — see the line above
```

Three answers, deliberately distinguished: **current**, **behind** (with the
number of missing commits when the revisions are on the same line) and **no
stamp** — an origin that answers with the site's own 404 for `/api/health`,
which is the state the live domain is in as this is written, and the most
dangerous one because the site looks perfectly healthy while being behind. Exit
code 1 when any origin is not current, so it can gate as well as report.

Note on running it from a sandbox: `vercel.app` is not reachable from every
environment (it was not, from the one this was written in), and the check
reports that per origin rather than failing the whole run. The state of the live
domain recorded above was read through a fetch tool instead, which is why the
evidence in the previous section is timestamps and status codes rather than the
output of this command.

## Verification

```
npx tsc --noEmit                      clean
npm run lint                          clean
npm run build                         399/399
python3 scripts/audit-site.py         0 failures / 0 warnings
python3 scripts/audit-site.py --fresh 0 failures / 0 warnings (tree and build agree)
route sweep                           390/390 built routes at 200
```

On the local origin:

```json
{"status":"ok","version":"2.0.11","revision":"dc70667",
 "branch":"arena/ff328a9c-proglassautomationadv","environment":"local",
 "builtAt":"2026-10-07T11:36:13.575Z"}
```

and the page prints `2026-10-07 11:36 UTC · revision dc70667` — the same
instant, the same revision, from HTML that was prerendered before `next start`
existed.
