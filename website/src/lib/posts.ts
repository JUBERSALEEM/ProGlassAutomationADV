import { readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import { marked, type Token } from "marked";

/**
 * Posts, read from `website/posts/*.md`.
 *
 * Writing a post means creating one Markdown file. There is no code to change,
 * no database, no CMS account and no build step to remember beyond the build
 * you already run: the folder is read at build time and every post becomes a
 * static page.
 *
 * Two decisions are worth stating, because they are what keep this simple:
 *
 *   1. **The file name is the URL.** `posts/my-post.md` is served at
 *      `/blog/my-post`. There is no slug field to keep in sync with a file
 *      name, because there is no slug field.
 *   2. **Everything is validated, and a mistake fails the build.** A missing
 *      title, a category that does not exist, a bad date — the build stops and
 *      says which file and which line. A post with a typo in the frontmatter
 *      can never reach a visitor, and it can never render half-formatted
 *      either: the same pass rejects Markdown this site does not render.
 *
 * The Markdown itself is *parsed* here (by `marked`) but never converted to an
 * HTML string. It is turned into React elements in `PostBody`, so there is no
 * `dangerouslySetInnerHTML` anywhere on the blog and no way for post content to
 * inject script into a page.
 */

/** Where posts live, relative to the website root. */
const POSTS_DIR = "posts";

/** The categories a post may use. Kept small on purpose. */
export const POST_CATEGORIES = ["Product", "Engineering", "Industry", "ProGlass updates"] as const;
export type PostCategory = (typeof POST_CATEGORIES)[number];

export type Post = {
  /** Taken from the file name — this is the URL. */
  slug: string;
  title: string;
  summary: string;
  /** ISO date, `YYYY-MM-DD`. */
  date: string;
  category: PostCategory;
  readingMinutes: number;
  draft: boolean;
  /** The Markdown source, rendered by `PostBody`. */
  markdown: string;
  /** `posts/<slug>.md`, shown in error messages and useful in a grep. */
  file: string;
};

/** Frontmatter keys this loader understands. Anything else is a typo. */
const KNOWN_KEYS = ["title", "summary", "date", "category", "readingMinutes", "draft"] as const;
const REQUIRED_KEYS = ["title", "summary", "date", "category"] as const;

/** Files that are documentation, not posts. */
const IGNORED = new Set(["README.md"]);

const WORDS_PER_MINUTE = 200;

class PostError extends Error {
  constructor(file: string, message: string) {
    super(`posts/${file}: ${message}`);
    this.name = "PostError";
  }
}

/** `key: value` lines between the first pair of `---` lines. */
function parseFrontmatter(file: string, source: string): { data: Record<string, string>; body: string } {
  const lines = source.split(/\r?\n/);

  if (lines[0]?.trim() !== "---") {
    throw new PostError(
      file,
      "the file must start with a frontmatter block — a line containing only `---`, " +
        "the fields, then another `---` line. See posts/_template.md.",
    );
  }

  const closing = lines.findIndex((line, index) => index > 0 && line.trim() === "---");
  if (closing === -1) {
    throw new PostError(file, "the frontmatter block is never closed — add a line containing only `---`.");
  }

  const data: Record<string, string> = {};
  lines.slice(1, closing).forEach((line, offset) => {
    const lineNumber = offset + 2;
    if (line.trim() === "" || line.trim().startsWith("#")) return;

    const separator = line.indexOf(":");
    if (separator === -1) {
      throw new PostError(file, `line ${lineNumber}: expected \`key: value\`, found \`${line.trim()}\`.`);
    }

    const key = line.slice(0, separator).trim();
    let value = line.slice(separator + 1).trim();

    if (!KNOWN_KEYS.includes(key as (typeof KNOWN_KEYS)[number])) {
      throw new PostError(
        file,
        `line ${lineNumber}: unknown field \`${key}\`. Allowed fields are ${KNOWN_KEYS.join(", ")}.`,
      );
    }
    if (key in data) {
      throw new PostError(file, `line ${lineNumber}: \`${key}\` is set twice.`);
    }

    // A value may be wrapped in quotes when it contains `#`, a colon at the
    // start, or trailing spaces. Unwrap it; never strip quotes from the middle.
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    data[key] = value;
  });

  for (const key of REQUIRED_KEYS) {
    if (!data[key]) {
      throw new PostError(
        file,
        `the frontmatter is missing \`${key}\`. Required: ${REQUIRED_KEYS.join(", ")}. See posts/_template.md.`,
      );
    }
  }

  return { data, body: lines.slice(closing + 1).join("\n").trim() };
}

function validateCategory(file: string, value: string): PostCategory {
  const match = POST_CATEGORIES.find((category) => category.toLowerCase() === value.toLowerCase());
  if (match) return match;
  throw new PostError(
    file,
    `\`category: ${value}\` is not one of the categories this site uses. Use one of: ${POST_CATEGORIES.join(" / ")}.`,
  );
}

function validateDate(file: string, value: string): string {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    throw new PostError(file, `\`date: ${value}\` must be written as YYYY-MM-DD, for example 2026-09-20.`);
  }
  const parsed = new Date(`${value}T00:00:00Z`);
  if (Number.isNaN(parsed.getTime()) || parsed.toISOString().slice(0, 10) !== value) {
    throw new PostError(file, `\`date: ${value}\` is not a real date.`);
  }
  return value;
}

/**
 * Rejects Markdown this site does not render, rather than rendering it wrong.
 *
 * Raw HTML is the important one: it cannot be rendered here without opening a
 * scripting hole on the blog, and silently dropping it would leave an author
 * wondering where their markup went. Better to stop the build and say so.
 */
function validateMarkdown(file: string, body: string): void {
  const lines = body.split(/\r?\n/);
  let inFence = false;

  lines.forEach((line, index) => {
    const lineNumber = index + 1;

    if (line.trim().startsWith("```")) {
      inFence = !inFence;
      return;
    }
    if (inFence) return;

    if (/^\s*<[a-zA-Z!/]/.test(line)) {
      throw new PostError(
        file,
        `line ${lineNumber} of the body contains raw HTML: \`${line.trim().slice(0, 50)}\`. ` +
          "Post content is rendered as React elements, so HTML is not supported — use Markdown " +
          "(`## heading`, `**bold**`, `- list`, `> note`, tables and fenced code all work).",
      );
    }
    if (line.trim().startsWith("![")) {
      throw new PostError(
        file,
        `line ${lineNumber} of the body contains an image. Images are not supported inside posts yet — ` +
          "use the site's own screenshots, or ask for a post figure type if you need one.",
      );
    }
  });

  if (inFence) {
    throw new PostError(file, "a fenced code block is opened with ``` but never closed.");
  }
}

/** Words in the body, ignoring code fences and Markdown punctuation. */
function estimateReadingMinutes(body: string): number {
  const words = body
    .replace(/```[\s\S]*?```/g, " ")
    .replace(/[#*_>`\-[\]()!]/g, " ")
    .split(/\s+/)
    .filter(Boolean).length;
  return Math.max(1, Math.round(words / WORDS_PER_MINUTE));
}

function parsePost(file: string, source: string): Post {
  const slug = file.replace(/\.md$/, "");

  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug)) {
    throw new PostError(
      file,
      `the file name becomes the article's web address, so it must be lowercase words separated by ` +
        `single hyphens — \`${slug}\` is not. Rename it, for example \`why-prices-move.md\`.`,
    );
  }

  const { data, body } = parseFrontmatter(file, source);
  if (body === "") {
    throw new PostError(file, "the post has frontmatter but no body.");
  }
  validateMarkdown(file, body);

  const declaredMinutes = data.readingMinutes ? Number(data.readingMinutes) : undefined;
  if (declaredMinutes !== undefined && (!Number.isFinite(declaredMinutes) || declaredMinutes <= 0)) {
    throw new PostError(file, `\`readingMinutes: ${data.readingMinutes}\` must be a number of minutes, or omitted.`);
  }

  const draft = data.draft ? !["false", "no", "0"].includes(data.draft.toLowerCase()) : false;

  return {
    slug,
    title: data.title!,
    summary: data.summary!,
    date: validateDate(file, data.date!),
    category: validateCategory(file, data.category!),
    readingMinutes: declaredMinutes ?? estimateReadingMinutes(body),
    draft,
    markdown: body,
    file: `posts/${file}`,
  };
}

/**
 * Reads the folder. In development the files are re-read on every call so a
 * new post or an edit shows up on refresh; in a build the result is cached,
 * because the files cannot change mid-build and parsing them repeatedly is
 * just work.
 */
let cache: Post[] | null = null;

function readAll(): Post[] {
  if (cache && process.env.NODE_ENV === "production") return cache;

  const directory = path.join(process.cwd(), POSTS_DIR);

  let names: string[];
  try {
    names = readdirSync(directory);
  } catch {
    throw new Error(
      `Could not read the posts folder at ${directory}. Posts are Markdown files in ` +
        `${POSTS_DIR}/ at the root of the website — if the folder was deleted, restore it ` +
        "with at least one .md file in it.",
    );
  }

  const posts = names
    .filter((name) => name.endsWith(".md") && !name.startsWith("_") && !IGNORED.has(name))
    .map((name) => parsePost(name, readFileSync(path.join(directory, name), "utf8")))
    .sort((a, b) => (a.date < b.date ? 1 : a.date > b.date ? -1 : a.slug.localeCompare(b.slug)));

  const slugs = new Set<string>();
  for (const post of posts) {
    if (slugs.has(post.slug)) throw new PostError(post.file, `two posts claim the address /blog/${post.slug}.`);
    slugs.add(post.slug);
  }

  cache = posts;
  return posts;
}

/** Published posts, newest first. */
export function allPosts(): Post[] {
  return readAll().filter((post) => !post.draft);
}

/** The slugs the blog builds a page for. Drafts are left out entirely. */
export function postSlugs(): string[] {
  return allPosts().map((post) => post.slug);
}

export function postBySlug(slug: string): Post | undefined {
  return allPosts().find((post) => post.slug === slug);
}

/**
 * Parsed Markdown tokens for a post.
 *
 * Exported so the renderer stays a presentation component and the parsing stays
 * here, next to the validation that guarantees it is safe to render.
 */
export function postTokens(markdown: string): Token[] {
  return marked.lexer(markdown, { gfm: true });
}
