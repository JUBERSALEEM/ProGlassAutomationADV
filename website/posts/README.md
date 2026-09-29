# Posts

One Markdown file per post. Nothing else is required — no database, no admin
screen, no account anywhere.

```
posts/
  _template.md                     ← copy this to start a post
  why-the-price-basis-is-stamped.md    → /blog/why-the-price-basis-is-stamped
  guillotine-versus-free-nesting.md    → /blog/guillotine-versus-free-nesting
  why-delivery-balance-is-hard.md      → /blog/why-delivery-balance-is-hard
```

A file whose name starts with `_`, and this README, are ignored — they are not
posts.

## Writing one

1. Copy `_template.md` to a new file. **The file name becomes the web address**,
   so name it in lowercase words separated by hyphens: `cutting-plan-mistakes.md`.
2. Fill in the frontmatter at the top, then write the body in Markdown.
3. Look at it: `npm run dev`, then open <http://localhost:3000/blog>.
4. Publish by deploying the site the way you normally do. That is the whole process.

## The format

```
---
title: The headline
summary: One or two sentences, used on the index and in link previews.
date: 2026-09-29
category: Industry
readingMinutes: 4
draft: false
---

Body in Markdown. `##` for a section, blank line between paragraphs.
```

| Field | Required | Notes |
| --- | --- | --- |
| `title` | yes | The `h1` on the page and the browser tab title. |
| `summary` | yes | Index card, meta description and share preview. |
| `date` | yes | `YYYY-MM-DD`. Sets the order — newest first — and is shown on the post. It does **not** schedule: a post is live as soon as its file is deployed, whatever the date says. |
| `category` | yes | `Product`, `Engineering`, `Industry` or `ProGlass updates`. |
| `readingMinutes` | no | Estimated from the body if omitted. |
| `draft` | no | `true` keeps it off the site completely until you flip it. |

## What the body supports

Headings, paragraphs, **bold**, *italic*, `code`, links (internal and external),
bullet and numbered lists, quotes, tables and fenced code blocks.

Not supported: raw HTML and images. The build stops with the file name and line
number if it finds either, rather than rendering something broken.

## If something is wrong, the build stops

The site is validated when it builds, and the message tells you the file and the
line. For example:

```
posts/my-post.md: line 7: unknown field `summmary`. Allowed fields are title, summary, date, category, readingMinutes, draft.
```

That is on purpose. A typo cannot reach a visitor, and it cannot quietly
half-publish either — a missing field, a bad date, a category that does not
exist, an unclosed code fence or a duplicated web address all fail loudly, in
one place, before anything is deployed.

## Where these files live when the site is hosted

In the same Git repository as everything else. Posts are read at **build** time
and each one becomes a plain HTML page, so the host never runs code to render
them: any static host works, and the built site can be served from a folder.

That also means the posts are versioned, backed up and reviewable like code —
`git log posts/` is the editorial history.
