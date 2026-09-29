import type { Metadata } from "next";
import Link from "next/link";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { posts } from "@/data/resources";
import { pageMetadata } from "@/lib/metadata";

export const metadata: Metadata = pageMetadata({
  title: "Blog — notes on glass software and the industry",
  description:
    "Notes from building ProGlass Automation: why quoted figures carry a price basis, guillotine versus free nesting, and why delivery balances are hard.",
  path: "/blog",
  keywords: ["glass industry blog", "glass software engineering", "cutting optimization article", "ERP design notes"],
});

export default function BlogPage() {
  const [lead, ...rest] = posts;

  return (
    <>
      <PageHero
        eyebrow="Blog"
        title="Notes on glass software and the work behind it."
        lead="Short pieces about the decisions inside a glass-business application: what to store, what to recompute, and which constraints are real. Written to be useful whether or not you use ProGlass."
        banner="writing"
        breadcrumb={[
          { label: "Home", href: "/" },
          { label: "Blog", href: "/blog" },
        ]}
        meta={[
          { label: "Notes", value: `${posts.length} published` },
          { label: "Categories", value: "Industry · Engineering · Product" },
          { label: "Written by", value: "The developer" },
          { label: "Also", value: "Guides under Resources" },
        ]}
        actions={
          <>
            <ButtonLink href="/resources" size="lg" trailing={<ArrowRight />}>
              Reference guides
            </ButtonLink>
            <ButtonLink href="/contact" variant="secondary" size="lg">
              Request a Demo
            </ButtonLink>
          </>
        }
      />

      {/* ── Lead article ─────────────────────────────────────────── */}
      {lead ? (
        <Section aria-labelledby="lead-heading">
          <Container>
            <SectionHeading id="lead-heading" eyebrow="Latest" title="Start here." />
            <Reveal delay={80} className="mt-8">
              <Link
                href={`/blog/${lead.slug}`}
                className="plate group block p-6 transition-colors duration-300 hover:border-brand-400/40 sm:p-8"
              >
                <div className="flex flex-wrap items-center gap-3">
                  <Badge tone="accent">{lead.category}</Badge>
                  <time dateTime={lead.date} className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
                    {new Date(lead.date).toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric" })}
                  </time>
                  <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
                    {lead.readingMinutes} min read
                  </span>
                </div>
                <h2 className="mt-5 text-2xl leading-tight transition-colors group-hover:text-brand-200 sm:text-3xl">
                  {lead.title}
                </h2>
                <p className="lede mt-4 max-w-3xl">{lead.summary}</p>
                <span className="link-underline mt-6 text-sm">
                  Read the note <ArrowRight />
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
            eyebrow="Archive"
            title="All notes."
            lead="The blog architecture is in place for regular writing: each note is a data entry with its own page, sitemap entry and metadata. New notes are added rather than retro-fitted."
          />

          <ul className="mt-10 grid gap-5 lg:grid-cols-2">
            {rest.map((post, index) => (
              <Reveal key={post.slug} as="li" delay={index * 60}>
                <Link
                  href={`/blog/${post.slug}`}
                  className="surface group flex h-full flex-col p-6 transition-colors duration-300 hover:border-brand-400/40"
                >
                  <div className="flex flex-wrap items-center gap-3">
                    <Badge>{post.category}</Badge>
                    <time dateTime={post.date} className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
                      {new Date(post.date).toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" })}
                    </time>
                  </div>
                  <h3 className="mt-4 text-lg font-semibold text-text-primary transition-colors group-hover:text-brand-200">
                    {post.title}
                  </h3>
                  <p className="mt-3 flex-1 text-sm leading-relaxed text-text-muted">{post.summary}</p>
                  <div className="mt-5 flex items-center justify-between border-t border-line/60 pt-4">
                    <span className="link-underline text-xs">
                      Read the note <ArrowRight />
                    </span>
                    <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
                      {post.readingMinutes} min
                    </span>
                  </div>
                </Link>
              </Reveal>
            ))}
          </ul>

          <Reveal delay={120} className="mt-8">
            <p className="text-xs leading-relaxed text-text-faint">
              More notes will be published here as work continues. There is no schedule to promise and no editorial
              calendar to keep — each note goes up when there is something worth saying.
            </p>
          </Reveal>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section aria-label="Explore the product">
        <Container>
          <Reveal>
            <div className="plate flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="text-2xl leading-tight sm:text-3xl">Reading about it is not the same as seeing it.</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">
                  The application is the argument. A demonstration will take you through the chain on a job you
                  recognise.
                </p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
                  Request a Demo
                </ButtonLink>
                <ButtonLink href="/screenshots" variant="secondary" size="lg">
                  See screenshots
                </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
  );
}
