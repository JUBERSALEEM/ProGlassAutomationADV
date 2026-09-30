/**
 * "Do not translate this" — for machine data, and only for machine data.
 *
 * The site ships its own fifteen languages, and it also accepts that a visitor
 * reading a language this build has not finished will use the browser's
 * translation. Those two facts have one consequence worth writing down: what a
 * browser translator must never touch is the same thing this site's own
 * extractor refuses to treat as language.
 *
 * That line is already drawn in the repository — `scripts/i18n-extract.mjs`
 * excludes identifiers, file formats, proper nouns and bare numbers, because
 * they are the same string in every language. A spec name, a document serial, a
 * rack code, a batch number and the product's own wordmark are data: they are
 * things the office typed and the floor reads back, and a translator that
 * renders `SGU 6 mm Clear FT` as prose has corrupted a label somebody will cut
 * glass against.
 *
 * So the rule is mechanical rather than aesthetic: **prose goes through `t()`
 * and stays translatable; data is wrapped here.** Both browsers and the site's
 * own pipeline read the same attribute — Chrome, Edge and Safari honour
 * `translate="no"`, and the `notranslate` class is set alongside it because
 * some engines look for the class rather than the attribute.
 */

import type { ElementType, ReactNode } from "react";
import { cn } from "@/lib/cn";

export function NoTranslate({
  children,
  as: Tag = "span",
  className,
}: {
  children: ReactNode;
  /** The element to render — a `span`, `p` or `td` keeps the surrounding layout. */
  as?: ElementType;
  className?: string;
}) {
  return (
    <Tag translate="no" data-notranslate="" className={cn("notranslate", className)}>
      {children}
    </Tag>
  );
}
