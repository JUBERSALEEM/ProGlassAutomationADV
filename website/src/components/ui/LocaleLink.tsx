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

export function LocaleLink({ href, ...rest }: LinkProps) {
  const { href: localize } = useI18n();
  const target = typeof href === "string" ? localize(href) : href;
  return <NextLink href={target} {...rest} />;
}

export default LocaleLink;
