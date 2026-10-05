/**
 * Markdown, parsed by this repository.
 *
 * The blog used to parse Markdown with the `marked` package. It was the site's
 * only runtime dependency beyond Next and React, and it was the cause of the
 * one build error that kept coming back:
 *
 *     Module not found: Can't resolve 'marked'
 *
 * That error is not a code fault — it is what you get when a project is built
 * without its dependencies installed (`npx next build` instead of
 * `npm run build`). But it is an unhelpful way to learn that, and the package
 * was buying very little: the blog renders eleven block and inline constructs,
 * and `PostBody` already owned the rendering of every one of them.
 *
 * So the parsing lives here now. Nothing is installed, nothing can go missing,
 * and a build of this site depends on Next and React and nothing else.
 *
 * ── What this supports ──────────────────────────────────────────────────────
 *
 *   block    ATX headings (`#` … `######`), paragraphs, bullet and numbered
 *            lists (including nesting), block quotes, fenced code (``` and ~~~),
 *            thematic breaks, and GFM pipe tables
 *   inline   bold (`**` / `__`), italic (`*` / `_`), strikethrough (`~~`),
 *            inline code, links, hard line breaks, and backslash escapes
 *
 * ── What it deliberately does not ──────────────────────────────────────────
 *
 *   · raw HTML — the loader already rejects it, and this parser escapes it if it
 *     ever arrives: there is no path from post content to markup;
 *   · images — rejected by the loader (see `posts.ts`), because a post cannot
 *     reference an asset the site does not ship;
 *   · reference links, footnotes, task lists, setext headings, indented code
 *     blocks, nested block quotes. None appear in a post, and each would be
 *     guessed at rather than implemented properly. They render as the plain text
 *     they are, rather than as something clever and wrong.
 *
 * The token shapes are the ones `PostBody` reads — the same shapes `marked`
 * produced — so the renderer did not have to change when this replaced it.
 */

/* ══════════════════════════════════════════════════════════════════════════
 * Token shapes
 * ════════════════════════════════════════════════════════════════════════ */

export type InlineToken =
  | { type: "text"; text: string; tokens?: InlineToken[] }
  | { type: "escape"; text: string }
  | { type: "strong"; tokens: InlineToken[] }
  | { type: "em"; tokens: InlineToken[] }
  | { type: "del"; tokens: InlineToken[] }
  | { type: "codespan"; text: string }
  | { type: "br" }
  | { type: "link"; href: string; title?: string | null; tokens: InlineToken[] };

export type TableCell = { text: string; tokens: InlineToken[] };

export type Token =
  | { type: "heading"; depth: number; text: string; tokens: InlineToken[] }
  | { type: "paragraph"; text: string; tokens: InlineToken[] }
  | { type: "list"; ordered: boolean; start: number; items: ListItem[] }
  | { type: "blockquote"; tokens: Token[] }
  | { type: "code"; lang: string | null; text: string }
  | { type: "table"; header: TableCell[]; align: (string | null)[]; rows: TableCell[][] }
  | { type: "hr" }
  | { type: "space" };

export type ListItem = { type: "list_item"; tokens: Token[] };

/* ══════════════════════════════════════════════════════════════════════════
 * Inline syntax
 * ════════════════════════════════════════════════════════════════════════ */

/** Characters a backslash may escape, per CommonMark. */
const ESCAPABLE = "\\`*_{}[]()#+-.!|~>";

/**
 * Inline markers, longest first, so `**` is tried before `*`.
 *
 * A small table beats a chain of string comparisons: the order is the priority,
 * and it is visible in one place.
 */
const MARKERS: { marker: string; type: "strong" | "em" | "del" }[] = [
  { marker: "**", type: "strong" },
  { marker: "__", type: "strong" },
  { marker: "~~", type: "del" },
  { marker: "*", type: "em" },
  { marker: "_", type: "em" },
];

/** The text inside a link: `[label](href)` or `[label](<href> "title")`. */
function parseLink(source: string, index: number): { token: InlineToken; next: number } | null {
  if (source[index] !== "[") return null;

  // Find the closing bracket, allowing one level of nesting in the label.
  let depth = 0;
  let close = -1;
  for (let i = index; i < source.length; i += 1) {
    const character = source[i];
    if (character === "\\") {
      i += 1;
      continue;
    }
    if (character === "[") depth += 1;
    else if (character === "]") {
      depth -= 1;
      if (depth === 0) {
        close = i;
        break;
      }
    }
  }
  if (close === -1 || source[close + 1] !== "(") return null;

  // The destination runs to the matching parenthesis.
  let destinationEnd = -1;
  let destinationDepth = 1;
  for (let i = close + 2; i < source.length; i += 1) {
    const character = source[i];
    if (character === "\\") {
      i += 1;
      continue;
    }
    if (character === "(") destinationDepth += 1;
    else if (character === ")") {
      destinationDepth -= 1;
      if (destinationDepth === 0) {
        destinationEnd = i;
        break;
      }
    }
  }
  if (destinationEnd === -1) return null;

  const label = source.slice(index + 1, close);
  const destination = source.slice(close + 2, destinationEnd).trim();
  if (!destination) return null;

  // `href "title"` — the title is optional and is not rendered, but it is
  // parsed so it does not end up inside the address.
  const titleMatch = /^(\S+?)(?:\s+["'(](.*?)["')])?$/.exec(destination);
  const href = titleMatch?.[1] ?? destination;
  const title = titleMatch?.[2] ?? null;

  return {
    token: { type: "link", href: unescape(href), title, tokens: parseInline(label) },
    next: destinationEnd + 1,
  };
}

/** Remove backslash escapes from a run of text. */
function unescape(value: string): string {
  return value.replace(/\\(.)/g, (match, character: string) =>
    ESCAPABLE.includes(character) ? character : match,
  );
}

/**
 * Parse inline Markdown into tokens.
 *
 * Linear rather than clever: walk the string once and emit a leaf when a
 * construct ends. Emphasis is matched to its *closing* marker, so `2 * 3 * 4`
 * does not become italic — the marker must be followed by non-space content,
 * which is the same rule CommonMark uses to decide a delimiter is real.
 */
export function parseInline(source: string): InlineToken[] {
  const tokens: InlineToken[] = [];
  let buffer = "";

  const flush = () => {
    if (buffer.length === 0) return;
    tokens.push({ type: "text", text: buffer });
    buffer = "";
  };

  let index = 0;
  while (index < source.length) {
    const character = source[index]!;

    // Backslash escape: the next character is literal.
    if (character === "\\" && index + 1 < source.length) {
      const next = source[index + 1]!;
      if (next === "\n") {
        flush();
        tokens.push({ type: "br" });
        index += 2;
        continue;
      }
      if (ESCAPABLE.includes(next)) {
        flush();
        tokens.push({ type: "escape", text: next });
        index += 2;
        continue;
      }
      buffer += character;
      index += 1;
      continue;
    }

    // Inline code: everything to the matching run of backticks, verbatim.
    if (character === "`") {
      let fence = 0;
      while (source[index + fence] === "`") fence += 1;
      const open = "`".repeat(fence);
      const close = source.indexOf(open, index + fence);
      if (close !== -1) {
        flush();
        tokens.push({ type: "codespan", text: source.slice(index + fence, close).trim() });
        index = close + fence;
        continue;
      }
    }

    // Link.
    if (character === "[") {
      const link = parseLink(source, index);
      if (link) {
        flush();
        tokens.push(link.token);
        index = link.next;
        continue;
      }
    }

    // Hard break: two or more spaces at the end of a line.
    if (character === "\n") {
      if (/ {2,}$/.test(buffer)) {
        buffer = buffer.replace(/ +$/, "");
        flush();
        tokens.push({ type: "br" });
      } else {
        // A soft break inside a paragraph is a space, which is what a reader
        // expects from a wrapped line in a plain-text file.
        buffer = `${buffer.replace(/ +$/, "")} `;
      }
      index += 1;
      continue;
    }

    // Emphasis, strikethrough.
    const marker = MARKERS.find((entry) => source.startsWith(entry.marker, index));
    if (marker) {
      const markerLength = marker.marker.length;
      const contentStart = index + markerLength;
      // A delimiter must be followed by content, and its closer preceded by
      // content — otherwise `*` used as a bullet or a footnote star is a star.
      if (contentStart < source.length && !/\s/.test(source[contentStart]!)) {
        const close = findClosing(source, contentStart, marker.marker);
        if (close !== -1) {
          flush();
          tokens.push({
            type: marker.type,
            tokens: parseInline(source.slice(contentStart, close)),
          } as InlineToken);
          index = close + markerLength;
          continue;
        }
      }
    }

    buffer += character;
    index += 1;
  }

  flush();
  return mergeAdjacentText(tokens);
}

/**
 * The closing delimiter for an emphasis run.
 *
 * Underscores are not allowed inside a word (`snake_case_name` is one word), so
 * an `_` delimiter must be preceded by whitespace or punctuation at its close.
 * Asterisks have no such restriction. Both rules exist because getting them
 * wrong mangles ordinary prose.
 */
function findClosing(source: string, from: number, marker: string): number {
  const underscore = marker.startsWith("_");
  for (let index = from; index < source.length; index += 1) {
    if (source[index] === "\\") {
      index += 1;
      continue;
    }
    if (!source.startsWith(marker, index)) continue;
    if (source[index - 1] === " " || source[index - 1] === "\n") continue;
    if (underscore && index > 0 && /[\w]/.test(source[index - 1]!)) continue;
    if (underscore && /[\w]/.test(source[index + marker.length] ?? "")) continue;
    return index;
  }
  return -1;
}

/** Join neighbouring text leaves, so the renderer does not emit empty spans. */
function mergeAdjacentText(tokens: InlineToken[]): InlineToken[] {
  const merged: InlineToken[] = [];
  for (const token of tokens) {
    const previous = merged[merged.length - 1];
    if (token.type === "text" && previous?.type === "text") {
      previous.text += token.text;
      continue;
    }
    merged.push(token);
  }
  return merged.filter((token) => token.type !== "text" || token.text.length > 0);
}

/* ══════════════════════════════════════════════════════════════════════════
 * Block syntax
 * ════════════════════════════════════════════════════════════════════════ */

const HEADING = /^(#{1,6})[ \t]+(.*)$/;
const FENCE = /^ {0,3}(`{3,}|~{3,})[ \t]*([^`]*)$/;
const BULLET = /^ {0,3}[-*+][ \t]+(.*)$/;
const ORDERED = /^ {0,3}(\d{1,9})[.)][ \t]+(.*)$/;
const QUOTE = /^ {0,3}>[ \t]?(.*)$/;
const THEMATIC = /^ {0,3}((\*[ \t]*){3,}|(-[ \t]*){3,}|(_[ \t]*){3,})$/;
const TABLE_DIVIDER = /^ {0,3}\|?[ \t]*:?-{3,}:?[ \t]*(\|[ \t]*:?-{3,}:?[ \t]*)*\|?[ \t]*$/;

/** How far a line is indented, in spaces (a tab counts as four). */
function indentOf(line: string): number {
  let width = 0;
  for (const character of line) {
    if (character === " ") width += 1;
    else if (character === "\t") width += 4;
    else break;
  }
  return width;
}

/** Split a table row into cell sources, tolerating a missing outer pipe. */
function splitRow(line: string): string[] {
  const trimmed = line.trim().replace(/^\|/, "").replace(/\|$/, "");
  const cells: string[] = [];
  let current = "";
  for (let index = 0; index < trimmed.length; index += 1) {
    const character = trimmed[index]!;
    if (character === "\\" && trimmed[index + 1] === "|") {
      current += "|";
      index += 1;
      continue;
    }
    if (character === "|") {
      cells.push(current.trim());
      current = "";
      continue;
    }
    current += character;
  }
  cells.push(current.trim());
  return cells;
}

function cell(source: string): TableCell {
  return { text: source, tokens: parseInline(source) };
}

/**
 * Parse Markdown into block tokens.
 *
 * Line-based, with one look-behind for tables (a header row is only a header
 * once the divider under it confirms it) and one for lists (an item continues
 * until a line is neither a new item nor indented under the current one).
 */
export function tokenize(markdown: string): Token[] {
  const tokens: Token[] = [];
  const lines = markdown.replace(/\r\n?/g, "\n").split("\n");
  let index = 0;

  const push = (token: Token) => {
    // A blank line is one space token, never two in a row.
    if (token.type === "space" && tokens[tokens.length - 1]?.type === "space") return;
    tokens.push(token);
  };

  while (index < lines.length) {
    const line = lines[index]!;

    if (line.trim() === "") {
      push({ type: "space" });
      index += 1;
      continue;
    }

    // ── Fenced code ─────────────────────────────────────────────────────
    const fence = FENCE.exec(line);
    if (fence && !line.trim().startsWith("````")) {
      const marker = fence[1]!;
      const lang = fence[2]?.trim() || null;
      const body: string[] = [];
      index += 1;
      while (index < lines.length && !new RegExp(`^ {0,3}${marker[0]}{${marker.length},}[ \\t]*$`).test(lines[index]!)) {
        body.push(lines[index]!);
        index += 1;
      }
      index += 1; // consume the closing fence (or run off the end)
      push({ type: "code", lang, text: body.join("\n") });
      continue;
    }

    // ── Thematic break ──────────────────────────────────────────────────
    if (THEMATIC.test(line)) {
      push({ type: "hr" });
      index += 1;
      continue;
    }

    // ── Heading ─────────────────────────────────────────────────────────
    const heading = HEADING.exec(line);
    if (heading) {
      const text = heading[2]!.replace(/[ \t]+#+[ \t]*$/, "");
      push({ type: "heading", depth: heading[1]!.length, text, tokens: parseInline(text) });
      index += 1;
      continue;
    }

    // ── Table ───────────────────────────────────────────────────────────
    const next = lines[index + 1];
    if (line.includes("|") && next !== undefined && TABLE_DIVIDER.test(next) && next.includes("-")) {
      const header = splitRow(line);
      const align = splitRow(next).map((cell) => {
        const left = cell.startsWith(":");
        const right = cell.endsWith(":");
        if (left && right) return "center";
        if (right) return "right";
        if (left) return "left";
        return null;
      });
      const rows: TableCell[][] = [];
      index += 2;
      while (index < lines.length && lines[index]!.includes("|") && lines[index]!.trim() !== "") {
        rows.push(splitRow(lines[index]!).map(cell));
        index += 1;
      }
      push({ type: "table", header: header.map(cell), align, rows });
      continue;
    }

    // ── Block quote ─────────────────────────────────────────────────────
    if (QUOTE.test(line)) {
      const inner: string[] = [];
      while (index < lines.length && QUOTE.test(lines[index]!)) {
        inner.push(QUOTE.exec(lines[index]!)![1]!);
        index += 1;
      }
      push({ type: "blockquote", tokens: tokenize(inner.join("\n")) });
      continue;
    }

    // ── List ────────────────────────────────────────────────────────────
    const bullet = BULLET.exec(line);
    const ordered = ORDERED.exec(line);
    if (bullet || ordered) {
      const isOrdered = Boolean(ordered);
      const start = ordered ? Number(ordered[1]) : 1;
      const items: ListItem[] = [];

      while (index < lines.length) {
        const itemLine = lines[index]!;
        const itemBullet = BULLET.exec(itemLine);
        const itemOrdered = ORDERED.exec(itemLine);
        const matches = isOrdered ? itemOrdered : itemBullet;
        if (!matches) break;

        const contentIndent = indentOf(itemLine) + (matches[0].length - matches[1]!.length);
        const body: string[] = [matches[1]!];
        index += 1;

        // Continuation: further lines indented under this item, including a
        // nested list, until a blank line followed by a new item or the end.
        while (index < lines.length) {
          const candidate = lines[index]!;
          if (candidate.trim() === "") {
            // A blank line may end the item or sit inside it; look ahead.
            const following = lines[index + 1];
            if (following !== undefined && following.trim() !== "" && indentOf(following) >= contentIndent) {
              body.push("");
              index += 1;
              continue;
            }
            break;
          }
          if (indentOf(candidate) >= contentIndent) {
            body.push(candidate.slice(Math.min(contentIndent, indentOf(candidate))));
            index += 1;
            continue;
          }
          break;
        }

        items.push({ type: "list_item", tokens: tokenize(body.join("\n")) });
        // Skip the blank line between items.
        if (lines[index]?.trim() === "" && (isOrdered ? ORDERED.test(lines[index + 1] ?? "") : BULLET.test(lines[index + 1] ?? ""))) {
          index += 1;
        }
      }

      push({ type: "list", ordered: isOrdered, start, items });
      continue;
    }

    // ── Paragraph ───────────────────────────────────────────────────────
    const paragraph: string[] = [];
    while (index < lines.length) {
      const candidate = lines[index]!;
      if (candidate.trim() === "") break;
      if (HEADING.test(candidate) || FENCE.test(candidate) || THEMATIC.test(candidate)) break;
      if (QUOTE.test(candidate)) break;
      if (BULLET.test(candidate) || ORDERED.test(candidate)) break;
      if (
        candidate.includes("|") &&
        TABLE_DIVIDER.test(lines[index + 1] ?? "") &&
        (lines[index + 1] ?? "").includes("-")
      ) {
        break;
      }
      // Only the leading indent is stripped: a line's trailing spaces are the
      // hard-break marker, and trimming them would silently join two lines the
      // author deliberately separated.
      paragraph.push(candidate.replace(/^ {0,3}/, ""));
      index += 1;
    }

    if (paragraph.length === 0) {
      // Defensive: a line that matched no rule and could not start a paragraph.
      index += 1;
      continue;
    }

    const text = paragraph.join("\n");
    push({ type: "paragraph", text, tokens: parseInline(text) });
  }

  return tokens;
}
