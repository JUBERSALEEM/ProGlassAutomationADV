import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { notFound } from "next/navigation";
import { Container, Section } from "@/components/ui/Container";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { PostBlock } from "@/components/blog/PostBody";
import { allPosts, postBySlug, postSlugs, postTokens } from "@/lib/posts";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { site } from "@/lib/site";
import { translatorFor } from "@/i18n/server";

/**
 * One page per Markdown file in `posts/`. Drafts are not in this list, so a
 * draft is not built at all — it cannot be reached, guessed or indexed.
 */
export function generateStaticParams() {
  return postSlugs().map((slug) => ({ slug }));
}

/**
 * Addresses not in that list do not exist.
 *
 * Without this, Next renders the route on demand for any slug and `notFound()`
 * inside the page renders the 404 *page* with a 200 *status* — a soft 404, which
 * search engines index and which reported success for a draft's address. With
 * it, an unpublished or mistyped address is a real 404, and the route is never
 * executed at request time at all: the posts folder is read at build time only.
 */
export const dynamicParams = false;

type PageProps = { params: Promise<{ locale: string; slug: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  const { slug } = await params;
  const post = postBySlug(slug);
  if (!post) {
    return pageMetadata({
      locale: t.locale,
      title: t("Note not found"),
      description: t("The requested note could not be found."),
      path: `/blog/${slug}`,
    });
  }
  return {
    ...pageMetadata({
      locale: t.locale,
      title: t(post.title),
      description: t(post.summary),
      path: `/blog/${post.slug}`,
      type: "article",
      publishedTime: post.date,
      keywords: [t(post.category), t("glass software"), t("ProGlass Automation")],
      ogEyebrow: t(post.category),
    }),
  };
}

export default async function BlogPostPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  const { slug } = await params;
  const post = postBySlug(slug);
  if (!post) notFound();

  const others = allPosts().filter((entry) => entry.slug !== post.slug);
  const tokens = postTokens(post.markdown);

  return (
    <>
      {/*
        One graph for the article, its WebPage and its breadcrumb trail, all
        linked to the shared Organization / Person / WebSite nodes the layout
        defines. See lib/schema.ts.
      */}
      <RouteSchema
      locale={locale}
        path={`/blog/${post.slug}`}
        title={t(post.title)}
        description={t(post.summary)}
        type="WebPage"
        article={{
          slug: post.slug,
          title: t(post.title),
          summary: t(post.summary),
          date: post.date,
          category: t(post.category),
        }}
      />

      <article>
        <header className="relative overflow-hidden border-b border-line/60 pb-12 pt-12 sm:pb-14 sm:pt-16">
          <div aria-hidden="true" className="glow-layer pointer-events-none absolute inset-0">
            <div className="absolute -top-28 left-[12%] h-72 w-72 rounded-full bg-brand-500/10 blur-[110px]" />
          </div>
          <Container className="relative">
            <nav aria-label={t("Breadcrumb")} className="mb-6">
              <ol className="flex flex-wrap items-center gap-2 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                <li>
                  <Link href="/" className="transition-colors hover:text-brand-300">
          {t("Home")}
        </Link>
                </li>
                <li aria-hidden="true">/</li>
                <li>
                  <Link href="/blog" className="transition-colors hover:text-brand-300">
          {t("Blog")}
        </Link>
                </li>
              </ol>
            </nav>

            <div className="max-w-3xl">
              <div className="flex flex-wrap items-center gap-3">
                <Badge tone="accent">{t(post.category)}</Badge>
                <time dateTime={post.date} className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                  {new Date(post.date).toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric" })}
                </time>
                <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                  {post.readingMinutes} min read
                </span>
              </div>
              <h1 className="mt-5 text-[1.9rem] leading-[1.12] sm:text-[2.4rem]">{t(post.title)}</h1>
              <p className="lede mt-5">{t(post.summary)}</p>
              <p className="mt-6 text-xs text-text-faint font-medium">
                Written by{" "}
                <Link href="/developer" className="link-underline">
                  {site.developerName}
                </Link>
                , developer of {t(site.name)}.
              </p>
            </div>
          </Container>
        </header>

        <Section aria-label={t("Article body")}>
          <Container>
            <div className="flow measure">
              {/*
                The Markdown is parsed into tokens by the site's own tokenizer and rendered here
                as React elements — never as an HTML string, so there is no
                injection path from a post's content into the page.
              */}
              {tokens.map((token, index) => (
                <Reveal key={index} delay={Math.min(index, 6) * 40} className="mb-9">
                  <PostBlock
      t={t} token={token} keyPrefix={`block-${index}`} />
                </Reveal>
              ))}
            </div>

            <div className="mx-auto mt-12 max-w-prose border-t border-line/60 pt-8">
              <p className="text-sm leading-relaxed text-text-muted">{t("If this raised a question about how the software handles something specific, ask it directly — you are writing to the person who builds it.")}</p>
              <div className="mt-5 flex flex-wrap gap-3">
                <ButtonLink href="/contact" size="md" trailing={<ArrowRight />}>
          {t("Ask a question")}
        </ButtonLink>
                <ButtonLink href="/resources" variant="secondary" size="md">
          {t("Reference guides")}
        </ButtonLink>
              </div>
            </div>
          </Container>
        </Section>
      </article>

      {/* ── More notes ───────────────────────────────────────────── */}
      {others.length > 0 ? (
        <Section tone="raised" aria-labelledby="more-heading">
          <Container>
            <h2 id="more-heading" className="text-xl font-semibold text-text-primary sm:text-2xl">
          {t("More notes")}
        </h2>
            <ul className="mt-8 grid gap-5 lg:grid-cols-2">
              {others.map((other, index) => (
                <Reveal key={other.slug} as="li" delay={index * 60}>
                  <Link
                    href={`/blog/${other.slug}`}
                    className="surface group flex h-full flex-col p-6 transition-colors duration-300 hover:border-brand-400/40"
                  >
                    <div className="flex items-center gap-3">
                      <Badge>{t(other.category)}</Badge>
                      <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                        {other.readingMinutes} min
                      </span>
                    </div>
                    <h3 className="mt-4 text-base font-semibold text-text-primary transition-colors group-hover:text-brand-200">
                      {t(other.title)}
                    </h3>
                    <p className="mt-2 flex-1 text-sm leading-relaxed text-text-muted">{t(other.summary)}</p>
                    <span className="link-underline mt-4 text-xs">
                      Read the note <ArrowRight />
                    </span>
                  </Link>
                </Reveal>
              ))}
            </ul>
          </Container>
        </Section>
      ) : null}
    </>
  );
}
