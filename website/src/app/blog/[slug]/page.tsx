import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Container, Section } from "@/components/ui/Container";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { posts, postBySlug } from "@/data/resources";
import { pageMetadata } from "@/lib/metadata";
import { site } from "@/lib/site";

export function generateStaticParams() {
  return posts.map((post) => ({ slug: post.slug }));
}

export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> {
  const { slug } = await params;
  const post = postBySlug(slug);
  if (!post) {
    return pageMetadata({
      title: "Note not found",
      description: "The requested note could not be found.",
      path: `/blog/${slug}`,
    });
  }
  return {
    ...pageMetadata({
      title: post.title,
      description: post.summary,
      path: `/blog/${post.slug}`,
      type: "article",
      publishedTime: post.date,
      keywords: [post.category, "glass software", "ProGlass Automation"],
    }),
  };
}

/**
 * Article structured data. The author is the developer named in the
 * application itself; no editorial entity is invented for the blog.
 */
function articleSchema(post: ReturnType<typeof postBySlug>) {
  if (!post) return null;
  return {
    "@context": "https://schema.org",
    "@type": "Article",
    headline: post.title,
    description: post.summary,
    datePublished: post.date,
    author: { "@type": "Person", name: site.developerName },
    publisher: { "@type": "Organization", name: site.legalName, url: site.url },
    mainEntityOfPage: `${site.url}/blog/${post.slug}`,
  };
}

export default async function BlogPostPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const post = postBySlug(slug);
  if (!post) notFound();

  const others = posts.filter((p) => p.slug !== post.slug);
  const schema = articleSchema(post);

  return (
    <>
      {schema ? (
        <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(schema) }} />
      ) : null}

      <article>
        <header className="relative overflow-hidden border-b border-line/60 pb-12 pt-12 sm:pb-14 sm:pt-16">
          <div aria-hidden="true" className="pointer-events-none absolute inset-0">
            <div className="absolute -top-28 left-[12%] h-72 w-72 rounded-full bg-brand-500/10 blur-[110px]" />
          </div>
          <Container className="relative">
            <nav aria-label="Breadcrumb" className="mb-6">
              <ol className="flex flex-wrap items-center gap-2 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                <li>
                  <Link href="/" className="transition-colors hover:text-brand-300">
                    Home
                  </Link>
                </li>
                <li aria-hidden="true">/</li>
                <li>
                  <Link href="/blog" className="transition-colors hover:text-brand-300">
                    Blog
                  </Link>
                </li>
              </ol>
            </nav>

            <div className="max-w-3xl">
              <div className="flex flex-wrap items-center gap-3">
                <Badge tone="accent">{post.category}</Badge>
                <time dateTime={post.date} className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                  {new Date(post.date).toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric" })}
                </time>
                <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                  {post.readingMinutes} min read
                </span>
              </div>
              <h1 className="mt-5 text-[1.9rem] leading-[1.12] sm:text-[2.4rem]">{post.title}</h1>
              <p className="lede mt-5">{post.summary}</p>
              <p className="mt-6 text-xs text-text-faint font-medium">
                Written by{" "}
                <Link href="/developer" className="link-underline">
                  {site.developerName}
                </Link>
                , developer of {site.name}.
              </p>
            </div>
          </Container>
        </header>

        <Section aria-label="Article body">
          <Container>
            <div className="flow measure">
              {post.body.map((block, index) => (
                <Reveal key={block.heading ?? index} delay={index * 40} className="mb-9">
                  {block.heading ? (
                    <h2 className="mb-4 text-xl font-semibold text-text-primary sm:text-2xl">{block.heading}</h2>
                  ) : null}
                  {block.paragraphs.map((paragraph) => (
                    <p key={paragraph} className="mb-4 text-base leading-relaxed text-text-secondary">
                      {paragraph}
                    </p>
                  ))}
                  {block.list ? (
                    <ul className="mt-5 space-y-3">
                      {block.list.map((item) => (
                        <li key={item} className="flex gap-3 text-base leading-relaxed text-text-secondary">
                          <span aria-hidden="true" className="mt-2.5 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                          {item}
                        </li>
                      ))}
                    </ul>
                  ) : null}
                </Reveal>
              ))}
            </div>

            <div className="mx-auto mt-12 max-w-prose border-t border-line/60 pt-8">
              <p className="text-sm leading-relaxed text-text-muted">
                If this raised a question about how the software handles something specific, ask it directly — you are
                writing to the person who builds it.
              </p>
              <div className="mt-5 flex flex-wrap gap-3">
                <ButtonLink href="/contact" size="md" trailing={<ArrowRight />}>
                  Ask a question
                </ButtonLink>
                <ButtonLink href="/resources" variant="secondary" size="md">
                  Reference guides
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
              More notes
            </h2>
            <ul className="mt-8 grid gap-5 lg:grid-cols-2">
              {others.map((other, index) => (
                <Reveal key={other.slug} as="li" delay={index * 60}>
                  <Link
                    href={`/blog/${other.slug}`}
                    className="surface group flex h-full flex-col p-6 transition-colors duration-300 hover:border-brand-400/40"
                  >
                    <div className="flex items-center gap-3">
                      <Badge>{other.category}</Badge>
                      <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                        {other.readingMinutes} min
                      </span>
                    </div>
                    <h3 className="mt-4 text-base font-semibold text-text-primary transition-colors group-hover:text-brand-200">
                      {other.title}
                    </h3>
                    <p className="mt-2 flex-1 text-sm leading-relaxed text-text-muted">{other.summary}</p>
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
