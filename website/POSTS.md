# Posts: how publishing works here, and what I recommend

Written after reading what this site is actually for. Short version:

1. **Keep the blog.** It is already built, it costs nothing to run, and it is the
   only place on the site that can answer a question a buyer types into Google.
2. **Publishing is a Markdown file.** You already have the folder: `posts/`.
   Writing a post means adding one `.md` file. No code, no admin screen, no
   database, no third-party service.
3. **Do not add a CMS, a hosting database or a "post storage" service yet.**
   For a site with one author and a handful of posts, every one of those adds a
   dependency — an account, a bill, a thing that can go down — to solve a
   problem you do not have.

---

## The question you asked, answered directly

> *"I may have to make post code-wise each time… or I can use other services…
> or should I keep it as it is, no post required?"*

**You do not need code, and you do not need a service.** After the change just
made, a post is a text file:

```
posts/cutting-plan-mistakes.md      →  https://yoursite.com/blog/cutting-plan-mistakes
```

That is the entire publishing mechanism. The file name is the web address; the
fields at the top are the title, the summary, the date and the category; the
rest is ordinary Markdown. `posts/_template.md` is a copy-paste starting point
and `posts/README.md` is the format reference — both sit in the folder with the
posts, where you will actually be when you need them.

**No post at all is also a legitimate answer.** The rest of the site — the
features, the screenshots, the worked example, the pricing and the developer
story — is what sells the software. A blog is a way to be found by someone who
does not know your product exists yet. If you never write one, nothing breaks:
the three notes that are there stay useful, and the folder just sits quiet.

What kills a blog is not having none. It is having five posts that stop in
March. So: write when you have something real to say, and do not start a
calendar you will not keep.

---

## What was actually changed

| Before | After |
| --- | --- |
| Posts lived in `src/data/resources.ts` as TypeScript objects. Writing one meant editing source code, matching the exact object shape, and risking a build break in an unrelated part of the site. | One `.md` file per post in `posts/`. Nothing else is touched. |
| Content and code were in the same file, so `git log` mixed editorial history with engineering history. | `git log posts/` is the editorial history. |
| A malformed post failed at build time with a TypeScript error pointing into a data file. | Failing posts name the file, the line and the fix — see below. |
| Any `/blog/<anything>` returned HTTP 200 with a "not found" page (a soft 404 search engines will index). | Unknown addresses are a real 404, and the route never executes at request time at all. |

Ten failure modes were tested and all ten stop the build with a readable
message rather than publishing something broken:

```
posts/my-post.md: line 3: unknown field `summmary`. Allowed fields are title, summary, date, category, readingMinutes, draft.
posts/my-post.md: the frontmatter is missing `category`. Required: title, summary, date, category.
posts/my-post.md: `category: Marketing` is not one of the categories this site uses. Use one of: Product / Engineering / Industry / ProGlass updates.
posts/my-post.md: `date: 2026-02-31` is not a real date.
posts/my-post.md: line 1 of the body contains raw HTML. Post content is rendered as React elements, so HTML is not supported.
posts/my-post.md: a fenced code block is opened with ``` but never closed.
```

---

## Where the posts live, and what "hosting storage" means here

**The posts live in Git, next to the code.** `posts/*.md` are committed with
the site, so they are versioned, backed up by your remote, and revertible. There
is no separate storage bucket, no database table and no upload step.

**The host never stores your posts as data.** When the site builds, each post is
converted into a normal HTML page. What gets deployed is a folder of finished
pages and images. Any provider serves that identically:

| Where you host | What it costs | Does it work? |
| --- | --- | --- |
| This same machine / a PC in the office (`npm run start`) | nothing | yes — that is what runs locally today |
| Vercel, Netlify, Cloudflare Pages, GitHub Pages | free tier is enough for a site this size | yes |
| Any shared cPanel host, uploading the built `website` folder | what you already pay | yes |
| A VPS with nginx | your server bill | yes — it is static files |

Because the post folder is read at **build** time only, a host never needs Node,
a database, or write access. Nothing about the posts can break at 3am because a
service was down.

### Running it locally, right now

```bash
cd website
npm install
npm run dev            # http://localhost:3000 — edit a post and refresh
```

To reproduce exactly what a host would serve:

```bash
npm run build
npm run start
```

---

## When I would change this advice

I would add a tool the moment one of these becomes true — and not before.

| If this becomes true | Add this | Why it still fits what you have |
| --- | --- | --- |
| You want to write posts from your phone, or someone non-technical will write them | A **Git-based CMS** (Decap CMS, TinaCMS, Sveltia) | It edits the *same* `posts/*.md` files through a browser form and commits them. The folder stays the source of truth, so trying it costs nothing and leaving costs nothing. |
| You want comments | Disqus, Giscus, or nothing | Comments on a product site mostly attract spam. I would leave them out. |
| You want subscribers | A newsletter service (Buttondown, MailerLite) | Sends from their servers; nothing to store on your host. |
| You want to know what people read | A privacy-respecting analytics service | Deliberately not installed today — see the privacy page, which says so. Adding one means updating that page in the same change. |
| The site becomes a marketing platform with a team | A headless CMS or a move to a framework built for content | Only worth the migration if writing is somebody's job. |

What I would avoid: a WordPress-style database, a page builder, or anything that
stores your content somewhere you cannot `git clone` it back from. Your content
should be plain text you own that works in any editor in twenty years.

---

## What a post should be about

The blog's job is narrower than "content marketing". It should answer questions
that a glass-fabrication business actually searches, and be written by someone
who runs one — which is you.

Good subjects, all of which are already in your head:

- Why a quotation must keep the price basis it was quoted on.
- What a guillotine does to a nesting plan, in plain language.
- How a delivery balance goes wrong across three movements.
- What an SGU, DGU, LAM and DGU + LAM actually are, and when each is used.
- Why offcut stock is invisible in a spreadsheet.

Subjects to avoid:

- Anything that needs a number you have not measured — no "save 30% of your
  time". The site says this explicitly, and a blog post is not an exception.
- Anything that names a customer, a price you charge, or a supplier's rate.
- Restating a features page in longer words. If it reads like the features page,
  it does not need to exist.

Three posts that answer a real question beat thirty that do not.

---

## The honest summary

Keep the blog, publish by writing a file, host it wherever you already host the
site, and add a CMS only if writing from a phone or with a second person ever
becomes real. You have no infrastructure problem to solve here — you have a
writing problem, and that one is best solved by writing.
