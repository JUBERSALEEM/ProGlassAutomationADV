import { buildStamp } from "@/lib/buildStamp";

/**
 * What this deployment is, as JSON.
 *
 * One purpose: to make "which build is live?" a question with a checkable
 * answer rather than an opinion. The site is deployed from one repository to
 * more than one host, and each host builds the same branch at a different
 * moment; `curl -s https://…/api/health` returns the revision and the instant
 * of the build that answered, and the same two facts are printed by the stamp
 * in the top panel, so a page and its endpoint can be compared instead of
 * trusted.
 *
 * A script does exactly that comparison — `scripts/audit-site.py` reads this
 * endpoint and holds it against the stamp rendered into the HTML (see
 * `check_build_stamp`). Run it against the live origin and against a preview
 * and the two answers say plainly whether they are the same build.
 *
 * The response is deliberately tiny and fixed: no timestamps of "now", no
 * counters, nothing that changes between two requests to the same deployment.
 * A health check that differs every time cannot be compared with anything.
 *
 * Both zones are reported because the panel prints one of them: the stamp reads
 * Gulf Standard Time, its tooltip and this endpoint carry UTC, and the pair is
 * what lets a person reading the site and a script reading the endpoint agree
 * without converting anything by hand.
 *
 * `force-static` is exact here rather than a cache hint. The values are
 * constants inlined at build time, so the answer is a property of the
 * deployment: prerendered once at build, served from the edge, identical for
 * every visitor, and replaced only by the next deploy. It also means the
 * endpoint costs nothing at runtime, which is the right price for something
 * that exists to be read.
 */
export const dynamic = "force-static";

export function GET() {
  return Response.json({
    status: "ok",
    version: buildStamp.version,
    revision: buildStamp.revision,
    branch: buildStamp.ref,
    environment: buildStamp.environment,
    /* The instant, twice: UTC because that is what two builds are compared by,
       and the same moment in the UAE's own offset because that is the clock the
       panel prints — so a curl and a screenshot can be read against each other
       without anyone doing arithmetic. */
    builtAt: buildStamp.builtAt,
    builtAtUae: buildStamp.builtAtUae,
  });
}
