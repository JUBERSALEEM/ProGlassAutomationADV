import Link from "next/link";
import { Fragment, type ReactNode } from "react";
import type {
  InlineToken,
  ListItem,
  TableCell,
  Token,
} from "@/lib/markdown";
import { cn } from "@/lib/cn";
import type { Translator } from "@/i18n/core";

/**
 * Renders a post's Markdown as React elements.
 *
 * `@/lib/markdown` parses the Markdown into tokens; this file turns them into
 * the same typography the rest of the site uses — headings, paragraphs, lists,
 * quotes, code and tables all come out wearing the site's own classes rather
 * than being dumped in as an HTML string.
 *
 * That choice is deliberate and it is the reason `dangerouslySetInnerHTML`
 * does not appear on the blog at all: text tokens become React children, which
 * React escapes, so there is no path from a post's content to executing script
 * on a page. The loader has already rejected raw HTML and images, so nothing
 * arrives here that cannot be rendered faithfully.
 *
 * Headings start at the second level. A post's title is the page's `h1`, so a
 * `#` or `##` in the body both render as an `h2` — an author cannot accidentally
 * create a second `h1` and break the page's outline.
 */

/** Only these protocols may become a link. Anything else renders as plain text. */
function safeHref(href: string | null | undefined): string | null {
  if (!href) return null;
  const trimmed = href.trim();
  if (/^(\/|#|mailto:)/i.test(trimmed)) return trimmed;
  if (/^https?:\/\//i.test(trimmed)) return trimmed;
  return null;
}

/**
 * Inline tokens: bold, italic, code, links, line breaks, strikethrough.
 *
 * Accepts block tokens as well, because a list item's children are blocks: an
 * item that is a single paragraph renders as the item's own text, and anything
 * else — a nested list, a code fence — is passed through the block renderer.
 */
function inline(
  tokens: (InlineToken | Token)[] | undefined,
  keyPrefix: string,
  t: Translator,
): ReactNode[] {
  if (!tokens) return [];

  return tokens.map((token, index) => {
    const key = `${keyPrefix}-${index}`;

    switch (token.type) {
      case "text": {
        const text = token as { text: string; tokens?: InlineToken[] };
        // A text token may itself carry nested inline tokens (a link inside bold).
        if (text.tokens?.length) return <Fragment key={key}>{inline(text.tokens, key, t)}</Fragment>;
        /*
         * Leaf text goes through the dictionary. The extractor walks the same
         * token tree with the same tokenizer, so every run rendered here has a
         * key — a paragraph split by a code span or a bold phrase arrives as two
         * runs, and both are keys rather than one sentence no dictionary can
         * reach.
         *
         * A run with nothing to translate (`" "`, `"."`, a number) comes back
         * unchanged: the dictionary has no entry for it and `t` returns what it
         * was given.
         */
        return t(text.text);
      }
      case "escape":
        return (token as { text: string }).text;
      case "strong":
        return (
          <strong key={key} className="font-semibold text-text-primary">
            {inline((token as { tokens: InlineToken[] }).tokens, key, t)}
          </strong>
        );
      case "em":
        return <em key={key}>{inline((token as { tokens: InlineToken[] }).tokens, key, t)}</em>;
      case "del":
        return <del key={key}>{inline((token as { tokens: InlineToken[] }).tokens, key, t)}</del>;
      case "codespan":
        return (
          <code key={key} className="rounded bg-ink-850 px-1.5 py-0.5 font-mono text-[0.85em] text-brand-100">
            {(token as { text: string }).text}
          </code>
        );
      case "br":
        return <br key={key} />;
      case "link": {
        const link = token as { href: string; tokens: InlineToken[] };
        const href = safeHref(link.href);
        const label = inline(link.tokens, key, t);
        if (!href) return <span key={key}>{label}</span>;

        const external = /^https?:\/\//i.test(href);
        if (external) {
          return (
            <a
              key={key}
              href={href}
              target="_blank"
              rel="noopener noreferrer"
              className="link-underline text-brand-200"
            >
              {label}
            </a>
          );
        }
        return (
          <Link key={key} href={href} className="link-underline text-brand-200">
            {label}
          </Link>
        );
      }
      default: {
        // A block token in an inline position: unwrap its inline children.
        if ("tokens" in token && Array.isArray((token as { tokens?: unknown }).tokens)) {
          return (
            <Fragment key={key}>{inline((token as { tokens: InlineToken[] }).tokens, key, t)}</Fragment>
          );
        }
        // Anything else unhandled still shows its text rather than vanishing.
        const fallback = "text" in token ? String((token as { text?: string }).text ?? "") : "";
        return fallback ? t(fallback) : null;
      }
    }
  });
}

function ListBlock({
  t,
  token,
  keyPrefix,
}: {
  /** The page's translator — this component renders on the server. */
  t: Translator;
  token: { ordered: boolean; items: ListItem[] }; keyPrefix: string
}) {
  const Tag = token.ordered ? "ol" : "ul";
  return (
    <Tag
      className={cn(
        "mb-5 space-y-3 pl-0",
        token.ordered && "list-decimal space-y-2 pl-6 text-text-secondary",
      )}
    >
      {token.items.map((item, index) => (
        <li
          key={`${keyPrefix}-${index}`}
          className={cn(
            "text-base leading-relaxed text-text-secondary",
            !token.ordered && "flex flex-wrap gap-3",
          )}
        >
          {!token.ordered ? (
            <span aria-hidden="true" className="mt-2.5 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
          ) : null}
          {/*
            The item's own sentences in the bullet's line, and anything that is
            not a paragraph — a nested list, a code fence — after it. A `<span>`
            cannot legally contain a `<ul>`, so the two are siblings rather than
            nested.
          */}
          <span className={cn(!token.ordered && "min-w-0 flex-1")}>
            {inline(
              item.tokens.filter((inner): inner is Extract<Token, { type: "paragraph" }> => inner.type === "paragraph"),
              `${keyPrefix}-${index}`,
              t,
            )}
          </span>
          {item.tokens
            .filter((inner) => inner.type !== "paragraph")
            .map((inner, innerIndex) => (
              <PostBlock
      t={t}
                key={`${keyPrefix}-${index}-${innerIndex}`}
                token={inner}
                keyPrefix={`${keyPrefix}-${index}-${innerIndex}`}
              />
            ))}
        </li>
      ))}
    </Tag>
  );
}

function TableBlock({
  t,
  token,
  keyPrefix,
}: {
  /** The page's translator — this component renders on the server. */
  t: Translator;
  token: { header: TableCell[]; rows: TableCell[][] }; keyPrefix: string
}) {
  return (
    <div className="mb-6 overflow-x-auto">
      <table className="w-full min-w-[34rem] border-collapse text-sm">
        <thead>
          <tr className="border-b border-line/60 text-left">
            {token.header.map((cell, index) => (
              <th
                key={`${keyPrefix}-h-${index}`}
                scope="col"
                className="px-3 py-2.5 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium"
              >
                {inline(cell.tokens, `${keyPrefix}-h-${index}`, t)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {token.rows.map((row, rowIndex) => (
            <tr key={`${keyPrefix}-r-${rowIndex}`} className="border-b border-line/40 last:border-0">
              {row.map((cell, cellIndex) => (
                <td key={`${keyPrefix}-r-${rowIndex}-${cellIndex}`} className="px-3 py-2.5 text-text-secondary">
                  {inline(cell.tokens, `${keyPrefix}-r-${rowIndex}-${cellIndex}`, t)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** One top-level block. Exported so the page can reveal each block in turn. */
export function PostBlock({
  t,
  token,
  keyPrefix,
}: {
  /** The page's translator — this component renders on the server. */
  t: Translator;
  token: Token; keyPrefix: string
}) {
  switch (token.type) {
    case "heading": {
      const heading = token as { depth: number; tokens: InlineToken[] };
      // depth 1 and 2 both land on h2 — see the note at the top of this file.
      const level = Math.min(4, Math.max(2, heading.depth + 1));
      const Tag = `h${level}` as "h2" | "h3" | "h4";
      return (
        <Tag
          className={cn(
            "mb-4 font-semibold text-text-primary",
            level === 2 && "text-xl sm:text-2xl",
            level === 3 && "text-lg",
            level === 4 && "text-base",
          )}
        >
          {inline(heading.tokens, keyPrefix, t)}
        </Tag>
      );
    }

    case "paragraph": {
      const paragraph = token as { tokens: InlineToken[] };
      return (
        <p className="mb-4 text-base leading-relaxed text-text-secondary">
          {inline(paragraph.tokens, keyPrefix, t)}
        </p>
      );
    }

    case "list":
      return <ListBlock t={t} token={token as { ordered: boolean; items: ListItem[] }} keyPrefix={keyPrefix} />;

    case "table":
      return (
        <TableBlock
          t={t}
          token={token as { header: TableCell[]; rows: TableCell[][] }}
          keyPrefix={keyPrefix}
        />
      );

    case "blockquote": {
      const quote = token as { tokens: Token[] };
      return (
        <blockquote className="surface mb-6 border-l-2 border-brand-400/60 px-5 py-4">
          <div className="text-base leading-relaxed text-text-secondary">
            {quote.tokens.map((inner, index) => (
              <PostBlock
      t={t} key={`${keyPrefix}-${index}`} token={inner} keyPrefix={`${keyPrefix}-${index}`} />
            ))}
          </div>
        </blockquote>
      );
    }

    case "code": {
      const code = token as { lang: string | null; text: string };
      return (
        <figure className="mb-6">
          <pre className="surface overflow-x-auto p-4">
            <code className="font-mono text-xs leading-relaxed text-text-secondary">{t(code.text)}</code>
          </pre>
          {code.lang ? (
            <figcaption className="mono-label mt-2">{code.lang}</figcaption>
          ) : null}
        </figure>
      );
    }

    case "hr":
      return <hr className="my-8 border-line/60" />;

    case "space":
      return null;

    default:
      return null;
  }
}

/** The whole body, for callers that do not need per-block wrapping. */
export function PostBody({
  t,
  tokens,
}: {
  /** The page's translator — this component renders on the server. */
  t: Translator;
  tokens: Token[]
}) {
  return (
    <>
      {tokens.map((token, index) => (
        <PostBlock
      t={t} key={`block-${index}`} token={token} keyPrefix={`block-${index}`} />
      ))}
    </>
  );
}
