"use client";

/**
 * `next/link`, except it keeps the reader in their language.
 *
 * Every internal link on the site goes through this. A page written in Japanese
 * links to `/ja-JP/pricing`, not to `/pricing` — and the component, not the
 * author of each link, is what guarantees it. The alternative, remembering to
 * prefix by hand, is a habit, and habits break: the first link that forgets
 * drops the reader into English mid-sentence.
 *
 * The prefix itself is applied by `hrefIn`, which is the one function on the
 * site that knows English has no prefix and every other language does.
 */

import NextLink from "next/link";
import type { ComponentProps } from "react";
import { useI18n } from "@/i18n/provider";

type LinkProps = ComponentProps<typeof NextLink>;

/**
 * Anything with a scheme is not a page on this site and must not be localized.
 *
 * `mailto:`, `tel:`, `https:` — `hrefIn` treats every string as a path and
 * prefixes it, so a `mailto:` link rendered through this component came out as
 * `/mailto:…`, which is a 404 with the developer's own email address in the
 * address bar. The bug survived because the two links that hit it were both
 * buttons, and a button that goes nowhere looks like a button whose click was
 * not noticed. The site's own crawler found it: `scripts/audit-site.py` checks
 * every internal link and reported a 400 for that exact URL.
 *
 * Only a root-relative path is ours to localize. Everything else is somebody
 * else's address.
 */
function isInternalPath(href: unknown): href is string {
  return typeof href === "string" && href.startsWith("/") && !href.startsWith("//");
}

export function LocaleLink({ href, ...rest }: LinkProps) {
  const { href: localize } = useI18n();
  const target = isInternalPath(href) ? localize(href) : href;
  return <NextLink href={target} {...rest} />;
}

export default LocaleLink;
