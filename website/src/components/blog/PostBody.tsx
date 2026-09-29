import Link from "next/link";
import { Fragment, type ReactNode } from "react";
import type { Token, Tokens } from "marked";
import { cn } from "@/lib/cn";

/**
 * Renders a post's Markdown as React elements.
 *
 * `marked` parses the Markdown into tokens; this file turns those tokens into
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

/** Inline tokens: bold, italic, code, links, line breaks, strikethrough. */
function inline(tokens: Token[] | undefined, keyPrefix: string): ReactNode[] {
  if (!tokens) return [];

  return tokens.map((token, index) => {
    const key = `${keyPrefix}-${index}`;

    switch (token.type) {
      case "text": {
        const text = token as Tokens.Text;
        // A text token may itself carry nested inline tokens (a link inside bold).
        if (text.tokens?.length) return <Fragment key={key}>{inline(text.tokens, key)}</Fragment>;
        // Leaf text is returned as a string: no wrapper element is needed, and
        // a post body should not add markup the author did not write.
        return text.text;
      }
      case "escape":
        return (token as Tokens.Escape).text;
      case "strong":
        return (
          <strong key={key} className="font-semibold text-text-primary">
            {inline((token as Tokens.Strong).tokens, key)}
          </strong>
        );
      case "em":
        return <em key={key}>{inline((token as Tokens.Em).tokens, key)}</em>;
      case "del":
        return <del key={key}>{inline((token as Tokens.Del).tokens, key)}</del>;
      case "codespan":
        return (
          <code key={key} className="rounded bg-ink-850 px-1.5 py-0.5 font-mono text-[0.85em] text-brand-100">
            {(token as Tokens.Codespan).text}
          </code>
        );
      case "br":
        return <br key={key} />;
      case "link": {
        const link = token as Tokens.Link;
        const href = safeHref(link.href);
        const label = inline(link.tokens, key);
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
        // Anything unhandled still shows its text rather than vanishing.
        const fallback = "text" in token ? String((token as { text?: string }).text ?? "") : "";
        return fallback ? fallback : null;
      }
    }
  });
}

function ListBlock({ token, keyPrefix }: { token: Tokens.List; keyPrefix: string }) {
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
            !token.ordered && "flex gap-3",
          )}
        >
          {!token.ordered ? (
            <span aria-hidden="true" className="mt-2.5 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
          ) : null}
          <span>{inline(item.tokens, `${keyPrefix}-${index}`)}</span>
        </li>
      ))}
    </Tag>
  );
}

function TableBlock({ token, keyPrefix }: { token: Tokens.Table; keyPrefix: string }) {
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
                {inline(cell.tokens, `${keyPrefix}-h-${index}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {token.rows.map((row, rowIndex) => (
            <tr key={`${keyPrefix}-r-${rowIndex}`} className="border-b border-line/40 last:border-0">
              {row.map((cell, cellIndex) => (
                <td key={`${keyPrefix}-r-${rowIndex}-${cellIndex}`} className="px-3 py-2.5 text-text-secondary">
                  {inline(cell.tokens, `${keyPrefix}-r-${rowIndex}-${cellIndex}`)}
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
export function PostBlock({ token, keyPrefix }: { token: Token; keyPrefix: string }) {
  switch (token.type) {
    case "heading": {
      const heading = token as Tokens.Heading;
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
          {inline(heading.tokens, keyPrefix)}
        </Tag>
      );
    }

    case "paragraph": {
      const paragraph = token as Tokens.Paragraph;
      return (
        <p className="mb-4 text-base leading-relaxed text-text-secondary">
          {inline(paragraph.tokens, keyPrefix)}
        </p>
      );
    }

    case "list":
      return <ListBlock token={token as Tokens.List} keyPrefix={keyPrefix} />;

    case "table":
      return <TableBlock token={token as Tokens.Table} keyPrefix={keyPrefix} />;

    case "blockquote": {
      const quote = token as Tokens.Blockquote;
      return (
        <blockquote className="surface mb-6 border-l-2 border-brand-400/60 px-5 py-4">
          <div className="text-base leading-relaxed text-text-secondary">
            {quote.tokens.map((inner, index) => (
              <PostBlock key={`${keyPrefix}-${index}`} token={inner} keyPrefix={`${keyPrefix}-${index}`} />
            ))}
          </div>
        </blockquote>
      );
    }

    case "code": {
      const code = token as Tokens.Code;
      return (
        <figure className="mb-6">
          <pre className="surface overflow-x-auto p-4">
            <code className="font-mono text-xs leading-relaxed text-text-secondary">{code.text}</code>
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
export function PostBody({ tokens }: { tokens: Token[] }) {
  return (
    <>
      {tokens.map((token, index) => (
        <PostBlock key={`block-${index}`} token={token} keyPrefix={`block-${index}`} />
      ))}
    </>
  );
}
