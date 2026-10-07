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
    builtAt: buildStamp.builtAt,
  });
}
