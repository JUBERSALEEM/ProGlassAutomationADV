import { cn } from "@/lib/cn";
import { site } from "@/lib/site";
import type { Translator } from "@/i18n/core";
import { buildEnvironmentKey, buildStamp, buildStampLine } from "@/lib/buildStamp";

/**
 * The build stamp — the top panel and the footer both print one.
 *
 * Why it exists at all: this site is deployed from one repository to more than
 * one host, and each host keeps its own build of the same branch. Nothing on
 * the page said which of them a visitor was looking at, so two builds minutes
 * apart — a preview and the live site — looked identical while being different
 * code. A date, a time in UTC and the revision settle it in one glance, and the
 * hover text adds the branch and where the build ran.
 *
 * Two shapes, because the two places have different amounts of room:
 *
 *   `BuildStampPill`  a three-line card for the top panel — the site's name, the
 *                     date and the time — narrow enough to sit beside the
 *                     settings controls without crowding them. The name is
 *                     inside the card because that is what the card answers:
 *                     *which* build, of *which* site, as at *when*;
 *   `BuildStampRow`   a labelled line for the mobile panel and the footer,
 *                     where the revision and both zones can be shown in full.
 *
 * The time is Gulf Standard Time, the clock the panel is read on, and the
 * tooltip carries UTC beside it, because comparing two hosts is done in UTC and
 * reading a badge should not need the reader to do the arithmetic.
 *
 * Both are pure functions of props: the values are constants baked into the
 * bundle at build time (see `lib/buildStamp.ts`), so the server and the browser
 * render exactly the same text and there is nothing to hydrate differently.
 */

/**
 * The hover text: the instant in both zones, the revision, the branch, where it
 * ran.
 *
 * The UTC instant is spelled out here rather than left implied, because this
 * string is what `scripts/audit-site.py` reads to hold a page against its own
 * `/api/health` — and a page and its endpoint are compared in UTC, whatever the
 * panel prints.
 */
function stampTitle(t: Translator): string {
  return [
    t.template`Updated ${buildStamp.lineUae}`,
    buildStamp.utcInstant ? t.template`UTC ${buildStamp.utcInstant}` : null,
    buildStamp.revision ? t.template`revision ${buildStamp.revision}` : null,
    buildStamp.ref,
    t(buildEnvironmentKey),
  ]
    .filter(Boolean)
    .join(" · ");
}

export function BuildStampPill({
  t,
  className,
}: {
  /** The translator — the hover text is a sentence like any other. */
  t: Translator;
  className?: string;
}) {
  return (
    <span
      /*
       * `title` rather than a popover: this is a fact a visitor checks once,
       * and the platform tooltip costs nothing to show and nothing to dismiss.
       */
      title={stampTitle(t)}
      className={cn(
        "select-none rounded-card border border-line-strong/60 bg-ink-900/50 px-2 py-1 text-center font-mono text-[0.59375rem] font-semibold leading-[1.35] tracking-tight text-text-muted",
        className,
      )}
    >
      <span className="block whitespace-nowrap text-text-faint">{site.name}</span>
      <span className="block whitespace-nowrap">{buildStamp.dateUae}</span>
      <span className="block whitespace-nowrap text-text-faint">{buildStamp.timeUae}</span>
    </span>
  );
}

export function BuildStampChip({
  t,
  className,
}: {
  t: Translator;
  className?: string;
}) {
  return (
    <span
      title={stampTitle(t)}
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border border-line/60 bg-ink-900/60 px-2.5 py-1 font-mono text-[0.6rem] leading-none text-text-faint",
        className,
      )}
    >
      {/*
       * The sync mark: two arrows chasing. It is the one fact a phone reader
       * asked the bar for — when did this build ship — reduced to a glyph so
       * the number beside it reads as a build time, not a clock.
       */}
      <svg
        aria-hidden="true"
        viewBox="0 0 16 16"
        className="h-3 w-3 shrink-0 text-brand-400"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        <path d="M12.5 6.5A5 5 0 0 0 4.9 4.3M3.5 9.5a5 5 0 0 0 7.6 2.2" />
        <path d="M4.6 1.8v3h3M11.4 14.2v-3h-3" />
      </svg>
      <span className="whitespace-nowrap">{buildStamp.timeUae}</span>
    </span>
  );
}

export function BuildStampRow({
  t,
  className,
}: {
  /** The translator — the label and the hover text are sentences like any other. */
  t: Translator;
  className?: string;
}) {
  return (
    <p
      title={stampTitle(t)}
      className={cn(
        "flex flex-wrap items-baseline gap-x-2 gap-y-1 text-[0.75rem] font-medium text-text-faint",
        className,
      )}
    >
      <span className="mono-label">{t("Updated")}</span>
      <span className="font-mono font-semibold text-text-muted">{buildStampLine}</span>
      {/* The UTC clock, faint, for the one reader who compares this line with a
          deploy log — the panel itself is read in Gulf Standard Time. */}
      <span className="font-mono text-text-faint">{buildStamp.time}</span>
      <span className="text-text-faint">{t(buildEnvironmentKey)}</span>
    </p>
  );
}
