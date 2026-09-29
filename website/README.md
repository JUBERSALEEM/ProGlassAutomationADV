# ProGlass Automation — website

The marketing and product website for the ProGlass Automation desktop
application. It is a Next.js (App Router) + TypeScript project that lives
entirely in this folder. It does not build, reference or modify the WPF
application, and nothing in this folder is part of the shipped product.

## Running it

```bash
npm install
npm run dev        # http://localhost:3000
```

```bash
npm run lint       # eslint src scripts
npm run typecheck  # tsc --noEmit
npm run build      # production build, all pages prerendered
npm run start      # serve the production build
```

Optional, after a build is being served:

```bash
python3 scripts/audit-site.py            # crawl http://127.0.0.1:3000
python3 scripts/audit-site.py https://…  # or any deployed origin
```

If a build or a dev server is already failing, **`npm run fix`** is the one
command that repairs the state: it installs whatever is missing, clears the
build cache, reports which revision it just repaired, and tells you what to run
next. `npm run doctor` only reports.

Run `npm run doctor` at any time to see the revision in the tree, whether every
declared dependency is installed, and whether anything under `src/` imports a
package `package.json` does not declare.

The site's only runtime dependencies are Next, React and React DOM. Markdown for
the blog is parsed by `src/lib/markdown.ts`, written for this project, so a build
depends on nothing that can go missing from a package registry.

The audit checks every internal link and image, the per-page metadata
(title, description, canonical, Open Graph, viewport), one-`h1`-per-page,
missing image `alt` text, and a list of banned marketing phrases.

## Deploying it

See **[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)** — a step-by-step guide from this
branch to `https://proglassautomation.com`, covering the Vercel project settings
(the site lives in `website/`, which is the one setting people get wrong), the
DNS records for the domain, the certificate, the launch checklist, and the
`Module not found` build error that appears when dependencies have not been
installed.

The short version, once the repository is on `main`:

```
Vercel → Add New → Project → import this repository
Root Directory: website      Node.js: 22.x      Environment variables: none
Deploy → then Settings → Domains → add the domain and its www form
→ add the two DNS records Vercel shows at your registrar → done
```

## Pages

| Route | What it is |
|---|---|
| `/` | The product story: what it is, who it is for, the document chain, calculators, the proforma wizard, the optimization engine, the modules, the interface, about, FAQ, demo request |
| `/features` | Every module group with the behaviour it covers |
| `/optimization` | The cutting-optimization engine, in depth |
| `/how-it-works` | The workflow, stage by stage, with the guarantee each handoff carries |
| `/screenshots` | Categorised gallery with a keyboard- and touch-navigable lightbox |
| `/pricing` | Editions and licence gating — no price list exists, and the page says so |
| `/about` | What the product is, who it is for, how it is built |
| `/developer` | The person who writes it, the stack and the engineering principles |
| `/contact` | Demo-request form and the direct address |
| `/resources` | Reference guides |
| `/blog`, `/blog/[slug]` | Short notes, one file each in `src/data/resources.ts` |
| `/legal/privacy`, `/legal/terms`, `/legal/license` | Website and licence terms |

`/sitemap.xml` and `/robots.txt` are generated from the same data.

## Where the content lives

Every word of product copy is data, not markup:

```
src/lib/site.ts                 identity, contact, version, measured counts
src/data/modules.ts             the module catalogue (rail label, capability, evidence)
src/data/features.ts            feature groups shown on / and /features
src/data/optimization.ts        thickness profiles, search stages, plate patterns,
                                cut complexities, the engine pipeline, result fields
src/data/workflows.ts           the inquiry → statement chain and its status vocabularies
src/data/editions.ts            editions, licence gates, open modules, licence facts
src/data/faq.ts                 questions, grouped Product / Deployment / Licensing
src/data/screenshots.ts         the screenshot registry
src/data/resources.ts           guides and blog posts
src/data/assets.ts              image paths for the real product assets
```

## The honesty rules this site is built on

These are not style preferences; breaking one is a defect:

1. **Every product statement is traceable to the WPF source tree.** Most
   content records an `evidence` path naming the file that defines the
   behaviour, and several pages render it.
2. **No invented numbers.** There is no savings percentage, customer count,
   award, certification or performance claim anywhere. The counts that do
   appear (view areas, databases, test methods, languages) are measured from
   the application tree, labelled with the date and the method.
3. **No fabricated screenshots.** `src/data/screenshots.ts` gives every entry a
   `source` of `capture`, `repo` or `pending`. `pending` renders an explicit
   "capture pending" plate carrying the real window chrome — never a mock-up
   presented as the application.
4. **No SaaS vocabulary and no cloud claims.** No account, no tenancy, no
   subscription dashboard. The allowed calls to action are Request a Demo,
   Contact, Get in Touch and Learn More.
5. **Licensing is described at customer level only.** `/legal/license` follows
   `docs/LICENSE.md`: the licence file, activation paths, trial, grace and
   device activations. It deliberately does not describe how a licence is
   produced or validated.

When adding a page, follow the same pattern: put the facts in `src/data/`,
name the evidence, and state the limit of what is being claimed.

## Adding a screenshot

1. Capture the real screen and save it to
   `public/images/<category>/<descriptive-kebab-case-name>.png` — for example
   `public/images/optimization/optimization-2d-view-single-sheet.png`.
2. In `src/data/screenshots.ts`, set `src` to that path, `width` and `height`
   to the real pixel size, and `source: "capture"`.
3. Run `npm run assets:optimize` if you want AVIF/WebP derivatives plus PNGs in
   the public folder. Add the source PNG back to `public/images/<category>/`
   first — the script is a no-op when its inputs are absent.
4. Re-run `scripts/audit-site.py` after a build to confirm the new file is
   served.

Until a real capture exists, leave the entry as `pending`. The placeholder
exists so that no page has to show something the product did not render.

## Wiring the demo-request form

`src/app/api/demo-request/route.ts` is the single integration point. It
validates on the server and, with no transport configured, answers
`configured: false` and tells the visitor the message was not delivered —
the form never fakes a success.

To make it deliver:

1. Add your transport to the route (Resend, Postmark, SES, SendGrid, a webhook,
   or your own SMTP relay).
2. Put the credential in an environment variable. Nothing is read from the
   repository, and no credential belongs in it.
3. Set `DEMO_REQUEST_PROVIDER` so the route can report the truth about itself,
   and optionally `DEMO_REQUEST_INBOX` for the destination address.

The route stores nothing: no database, no server-side log of personal data.

## Notes

- Fonts are self-hosted from `src/fonts` through `next/font/local`, so no page
  makes a request to a third-party font host.
- The only product imagery that ships is the real artwork from the WPF
  repository (`Resources/Glass3D/*`, the application icon and the brand
  banner), re-encoded to WebP at the widths the layout requests.
- `prefers-reduced-motion: reduce` is honoured globally, and every animation
  collapses while its content stays visible.
