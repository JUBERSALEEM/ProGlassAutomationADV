import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { allPosts } from "@/lib/posts";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { site } from "@/lib/site";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("Blog — notes on glass software"),
  description: t("Notes from building ProGlass Automation: why quoted figures carry a price basis, guillotine versus free nesting, and why delivery balances are hard."),
  path: "/blog",
  keywords: ["glass industry blog", "glass software engineering", "cutting optimization article", t("ERP design notes")],
  ogEyebrow: t("Notes"),
  });
}

export default async function BlogPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  const posts = allPosts();
  const [lead, ...rest] = posts;

  return (
    <PageOverlay locale={t.locale} route="blog">

    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/blog"
      title={t("Blog — notes on glass software")}
      description={t("Notes from building ProGlass Automation: why quoted figures carry a price basis, guillotine versus free nesting, and why delivery balances are hard.")}
      type="CollectionPage"
      items={posts.map((post) => ({ name: post.title, url: `${site.url}/blog/${post.slug}` }))}
    />
      <PageHero
      t={t}
        eyebrow={t("Blog")}
        title={t("Notes on glass software and the work behind it.")}
        lead={t("Short pieces about the decisions inside a glass-business application: what to store, what to recompute, and which constraints are real. Written to be useful whether or not you use ProGlass.")}
        banner="writing"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("Blog"), href: "/blog" },
        ]}
        meta={[
          { label: t("Notes"), value: t("{count} published", { count: posts.length }) },
          { label: t("Categories"), value: t("Industry · Engineering · Product") },
          { label: t("Written by"), value: t("The developer") },
          { label: t("Also"), value: t("Guides under Resources") },
        ]}
        actions={
          <>
            <ButtonLink href="/resources" size="lg" trailing={<ArrowRight />}>
          {t("Reference guides")}
        </ButtonLink>
            <ButtonLink href="/contact" variant="secondary" size="lg">
          {t("Request a Demo")}
        </ButtonLink>
          </>
        }
      />

      {/* ── Lead article ─────────────────────────────────────────── */}
      {lead ? (
        <Section aria-labelledby="lead-heading">
          <Container>
            <SectionHeading id="lead-heading" eyebrow={t("Latest")} title={t("Start here.")} />
            <Reveal delay={80} className="mt-8">
              <Link
                href={`/blog/${lead.slug}`}
                className="plate group block p-6 transition-colors duration-300 hover:border-brand-400/40 sm:p-8"
              >
                <div className="flex flex-wrap items-center gap-3">
                  <Badge tone="accent">{t(lead.category)}</Badge>
                  <time dateTime={lead.date} className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                    {new Date(lead.date).toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric" })}
                  </time>
                  <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                    {t("{minutes} min read", { minutes: lead.readingMinutes })}
                  </span>
                </div>
                <h2 className="mt-5 text-2xl leading-tight transition-colors group-hover:text-brand-200 sm:text-3xl">
                  {t(lead.title)}
                </h2>
                <p className="lede mt-4 max-w-3xl">{t(lead.summary)}</p>
                <span className="link-underline mt-6 text-sm">
                  {t("Read the note")} <ArrowRight />
                </span>
              </Link>
            </Reveal>
          </Container>
        </Section>
      ) : null}

      {/* ── Archive ──────────────────────────────────────────────── */}
      <Section tone="raised" aria-labelledby="archive-heading">
        <Container>
          <SectionHeading
            id="archive-heading"
            eyebrow={t("Archive")}
            title={t("All notes.")}
            lead={t("The blog architecture is in place for regular writing: each note is a data entry with its own page, sitemap entry and metadata. New notes are added rather than retro-fitted.")}
          />

          <ul className="mt-10 grid gap-5 lg:grid-cols-2">
            {rest.map((post, index) => (
              <Reveal key={post.slug} as="li" delay={index * 60}>
                <Link
                  href={`/blog/${post.slug}`}
                  className="surface group flex h-full flex-col p-6 transition-colors duration-300 hover:border-brand-400/40"
                >
                  <div className="flex flex-wrap items-center gap-3">
                    <Badge>{t(post.category)}</Badge>
                    <time dateTime={post.date} className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                      {new Date(post.date).toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" })}
                    </time>
                  </div>
                  <h3 className="mt-4 text-lg font-semibold text-text-primary transition-colors group-hover:text-brand-200">
                    {t(post.title)}
                  </h3>
                  <p className="mt-3 flex-1 text-sm leading-relaxed text-text-muted">{t(post.summary)}</p>
                  <div className="mt-5 flex items-center justify-between border-t border-line/60 pt-4">
                    <span className="link-underline text-xs">
                      {t("Read the note")} <ArrowRight />
                    </span>
                    <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                      {t("{minutes} min", { minutes: post.readingMinutes })}
                    </span>
                  </div>
                </Link>
              </Reveal>
            ))}
          </ul>

          <Reveal delay={120} className="mt-8">
            <p className="text-xs leading-relaxed text-text-faint font-medium">
          {t("More notes will be published here as work continues. There is no schedule to promise and no editorial calendar to keep — each note goes up when there is something worth saying.")}
        </p>
          </Reveal>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section aria-label={t("Explore the product")}>
        <Container>
          <Reveal>
            <div className="glass-primary flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="display-2">{t("Reading about it is not the same as seeing it.")}</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">
          {t("The application is the argument. A demonstration will take you through the chain on a job you recognise.")}
        </p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request a Demo")}
        </ButtonLink>
                <ButtonLink href="/screenshots" variant="secondary" size="lg">
          {t("See screenshots")}
        </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
    </PageOverlay>
  );
}
