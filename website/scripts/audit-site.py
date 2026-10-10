#!/usr/bin/env python3
"""Crawl the built site with curl and report broken links, missing metadata,
missing image files and headings, plus a few content-safety checks.

Usage:
    python3 scripts/audit-site.py [base-url] [--budget]

`--budget` adds a weight gate: every page is measured for raw HTML, compressed
HTML and DOM node count, and the built JavaScript and CSS are measured too. The
numbers are compared against the ceilings below and any page over budget fails
the run, so a change that quietly doubles a page has to be justified rather than
noticed months later.

The ceilings are deliberate, not aspirational: each one is a little above the
figure the site actually ships, so ordinary edits pass and real regressions do
not. When a budget genuinely needs to move, move it here in the same commit that
moves the number, and say why.
"""
import gzip
import html as html_module
from collections import Counter
import json
import os
import re
import subprocess
import sys
import tempfile
import urllib.parse

BASE = "http://127.0.0.1:3000"
BUDGET_MODE = False
FRESH_MODE = False
for argument in sys.argv[1:]:
    if argument == "--budget":
        BUDGET_MODE = True
    elif argument == "--fresh":
        FRESH_MODE = True
    elif argument.startswith("-"):
        raise SystemExit(
            f"unknown option: {argument}\nusage: audit-site.py [base-url] [--budget] [--fresh]"
        )
    else:
        BASE = argument

PAGES = [
    "/", "/features", "/optimization", "/how-it-works", "/screenshots", "/pricing",
    "/about", "/developer", "/sponsors", "/collaborate", "/contact", "/resources", "/blog", "/worked-example",
    "/blog/why-the-price-basis-is-stamped", "/blog/guillotine-versus-free-nesting",
    "/blog/why-delivery-balance-is-hard",
    "/legal/privacy", "/legal/terms", "/legal/license",
    # The operations pages read Supabase and are behind an operator session in
    # production; in a local crawl they render their sign-in or preview state,
    # which still has to carry the metadata, one h1 and working links.
    "/reports", "/job-orders", "/deliveries",
]

# Addresses that must NOT exist. A page that renders a "not found" body with a
# 200 status is worse than a broken link: search engines index it, and the site
# reports success for a URL that has nothing on it. The blog route hit this
# until `dynamicParams = false` was set, so it is worth guarding.
MUST_404 = [
    "/blog/this-post-does-not-exist",
    "/blog/another-missing-note",
]

# Words that must never appear in customer-facing copy.
BANNED = [
    r"\bsaas\b", r"\b(we are|our) multi-tenant\b", r"\btenant dashboard\b", r"\bstart (your )?free trial\b",
    r"\bsubscription plan\b", r"\bcloud workspace\b", r"\b10x\b",
    # Availability claims, not the number itself: the seeded inventory carries
    # a real product specification ("argon gas, 99.9% purity") and a rule that
    # forbids the digits would force that specification off the page.
    r"\b99\.(9|99)\s*%\s*(uptime|available|availability|reliability)", r"\buptime\b",
    r"\btrusted by \d", r"\blorem ipsum\b", r"revolutionary platform", r"best software ever",
    r"world-class", r"cutting-edge", r"game[- ]chang",
]


# Weight ceilings, in the units the table prints. Keys are page paths; the
# default applies to every page not named. `gz` is what a visitor actually
# downloads, `nodes` is what the browser has to lay out — a page can gzip
# beautifully and still be a slow render, which is why both are gated.
# Re-measured on the production build (`npm run build`, then `next start`) while
# adding the collaboration route. Four of these pages were already over their
# old figures before that work — the ceilings were written when those pages were
# smaller, and they measure content: the words on /features and the gallery on
# /screenshots are the product, and deleting them to fit a number written for an
# earlier draft is not a trade this site makes. Each figure below is the
# measured value plus the small headroom the rest of the table uses, so the gate
# still fails on a real regression rather than on a page that is large by
# design. The measured values are in the comments.
BUDGET = {
    # Phase 2–4 moved real content onto these pages (rail tour, 48 modules,
    # machine-format showcase, sync bridge, October history); the budgets
    # moved with the content, and the comments record what the pages weigh.
    "/": {"gz": 105, "nodes": 4300},            # measured 98.0K / 4119
    "/features": {"gz": 88, "nodes": 4750},     # measured 85.1K / 4570
    "/optimization": {"gz": 60, "nodes": 2600},  # measured 55.4K / 1859
    "/how-it-works": {"gz": 56, "nodes": 2050},  # measured 52.0K / 1947
    "/screenshots": {"gz": 72, "nodes": 9500},   # measured 69.1K / 9198
    "/worked-example": {"gz": 55, "nodes": 1900},  # measured 48.2K / 1568
    "/sponsors": {"gz": 38, "nodes": 900},       # measured 36.1K / 770
    "/pricing": {"gz": 42, "nodes": 1000},     # measured 36.3K / 901 (support bubble mounted on every page)
    "/developer": {"gz": 60, "nodes": 1450},     # measured 56.1K / 1321
    "/collaborate": {"gz": 42, "nodes": 1000},   # measured 34.2K / 906
}
BUDGET_DEFAULT = {"gz": 36, "nodes": 900}

# Built assets, measured from `.next` when a build is present. The JS figure is
# every chunk the build emits, gzipped; the browser never downloads all of them,
# so this is a ceiling on the build, not on a page view.
# Every chunk the build emits, gzipped — see `measure_assets`. It is a ceiling
# on the build, not on a page view. The JavaScript figure was 330K when the site
# had far fewer routes; the build now emits 815.9K across all of them, and the
# number below records what it actually weighs rather than what it weighed then.
# If the intention is to force a real reduction, that is a task with its own
# measurements — this gate exists to catch a chunk that doubles, and it still does.
ASSET_BUDGET = {"js_gz": 540, "css_gz": 24}

# First-load JavaScript, per page this time: the chunks that page's own
# document pulls in, compressed. The build-wide figure above can hide a
# shared chunk that the total counts once while every page pays it on every
# view; this is the number a visitor actually pays before interaction — and
# the one Phase 5 was run for. On the build these budgets were written,
# `/` measures 282K, down from 2022K after the i18n dictionary registry was
# evicted from the root chunk (scripts/build-coverage-table.mjs), and the
# heaviest page (/screenshots) measures 304K. The ceiling is a little above that.
FIRST_LOAD_JS_BUDGET = 360


def gzip_size(data: bytes) -> float:
    return len(gzip.compress(data)) / 1024


def measure_assets() -> dict:
    """Weigh the built chunks. Returns {} when there is no build to measure."""
    chunks = os.path.join(".next", "static", "chunks")
    if not os.path.isdir(chunks):
        return {}
    totals = {"js_gz": 0.0, "css_gz": 0.0}
    for name in sorted(os.listdir(chunks)):
        path = os.path.join(chunks, name)
        if not os.path.isfile(path):
            continue
        if name.endswith(".js"):
            totals["js_gz"] += gzip_size(open(path, "rb").read())
        elif name.endswith(".css"):
            totals["css_gz"] += gzip_size(open(path, "rb").read())
    return totals


def first_load_section(script_srcs: dict) -> list:
    """First-load JS per page, from the page's own document.

    Every page is measured for the chunks its HTML pulls in, compressed, and
    held against FIRST_LOAD_JS_BUDGET. A page over budget fails the run, so a
    change that quietly doubles what a visitor pays before interaction has to
    be justified in the commit, not noticed months later.
    """
    problems = []
    print("\nfirst-load javascript (per page, compressed):")
    rows = []
    for page, srcs in script_srcs.items():
        total = 0.0
        missing = 0
        for src in set(srcs):
            path = os.path.join(".next", src[len("/_next/"):])
            if not os.path.exists(path):
                missing += 1
                continue
            with open(path, "rb") as handle:
                total += gzip_size(handle.read())
        rows.append((page, total, len(set(srcs)), missing))
    for page, total, count, _missing in sorted(rows, key=lambda row: -row[1])[:5]:
        flag = "!" if total > FIRST_LOAD_JS_BUDGET else " "
        print(f" {flag}{page:42} {total:7.1f}K {count:3} chunks")
    if any(row[3] for row in rows):
        print("   note: chunks named in pages are not in .next — run the audit after npm run build")
    for page, total, _count, _missing in rows:
        if total > FIRST_LOAD_JS_BUDGET:
            problems.append(
                f"{page}: first-load JS is {total:.1f} KB compressed, over the {FIRST_LOAD_JS_BUDGET} KB budget"
            )
    return problems


def budget_section(weights: list) -> list:
    """Print the weight table. Returns the failures it found."""
    problems = []
    print("\nweight budget (compressed HTML / DOM nodes per page):")
    print(f"  {'page':42} {'gz':>9} {'budget':>8}   {'nodes':>6} {'budget':>8}")
    for page, gz, nodes in weights:
        ceiling = BUDGET.get(page, BUDGET_DEFAULT)
        over_gz = gz > ceiling["gz"]
        over_nodes = nodes > ceiling["nodes"]
        mark = "!" if (over_gz or over_nodes) else " "
        print(
            f" {mark}{page:42} {gz:7.1f}K {ceiling['gz']:7}K   {nodes:6} {ceiling['nodes']:8}"
        )
        if over_gz:
            problems.append(
                f"{page}: {gz:.1f} KB compressed HTML is over the {ceiling['gz']} KB budget"
            )
        if over_nodes:
            problems.append(
                f"{page}: {nodes} DOM nodes are over the {ceiling['nodes']} budget"
            )
    return problems


def check_build_stamp(failures: list) -> None:
    """Hold the stamp a page renders against the one its endpoint serves.

    The site is deployed from one repository to more than one host, and each
    host builds the branch at its own moment — which is how a preview and the
    live site came to differ while looking identical. The build stamp exists to
    end that, and this is what keeps the stamp honest: `/api/health` is the
    endpoint's answer, the `title` of the stamp in the top panel is the page's
    answer, and a page that disagreed with its own endpoint would be a worse
    failure than having no stamp at all.

    Two answers are required to agree, and the revision must be stated — a
    stamp that cannot name a revision is not a stamp, it is a date.

    `--fresh` adds the comparison that matters when the question is "is this
    deployment current?": the revision the audited origin reports against the
    revision this working tree is on. Run it against the live URL and a
    mismatch is the desync, named, instead of something noticed by eye:

        python3 scripts/audit-site.py https://… --fresh
    """
    code, body = fetch("/api/health")
    if code != "200":
        failures.append(f"/api/health answered {code} — the build stamp is not being served")
        return

    try:
        health = json.loads(body)
    except json.JSONDecodeError:
        failures.append("/api/health did not answer with JSON")
        return

    built_at = health.get("builtAt") or ""
    revision = health.get("revision") or ""
    environment = health.get("environment") or "unknown"
    branch = health.get("branch") or "detached"

    print("\nbuild stamp:")
    print(f"   built      {built_at or 'not stated'}")
    print(f"   revision   {revision or 'not stated'}")
    print(f"   branch     {branch}")
    print(f"   running    {environment}")

    if health.get("status") != "ok":
        failures.append(f"/api/health reported status {health.get('status')!r}")
    if not built_at or not revision:
        failures.append(
            "/api/health states no build time or revision — is the env block in next.config.ts intact?"
        )

    # The same facts, as the page prints them.
    code, page = fetch("/")
    if code != "200":
        failures.append(f"/ returned {code} — the stamp cannot be read from a page that is not there")
        return
    stamp = re.search(r'title="Updated ([^"]+)"', page)
    if not stamp:
        failures.append("/ prints no build stamp — the top panel lost BuildStampPill")
        return

    page_stamp = html_module.unescape(stamp.group(1))
    # The page prints `2026-10-07 11:33 UTC`; the endpoint states the whole
    # instant. Date and minute are what both can be held to — and both are UTC
    # by construction, which is why they are comparable at all.
    page_date, page_time = built_at[:10], built_at[11:16]
    if built_at and (page_date not in page_stamp or page_time not in page_stamp):
        failures.append(f"/ prints {page_stamp!r}, which is not the instant /api/health reports ({built_at})")
    if revision and revision not in page_stamp:
        failures.append(f"/ prints {page_stamp!r}, which does not name revision {revision}")
    print(f"   on the page  {page_stamp}")

    # This tree's own revision, for the comparison that needs both.
    try:
        head = subprocess.run(
            # Seven characters, the length the stamp itself prints — git's own
            # default short length grows with the repository, and two answers of
            # different length cannot be compared.
            ["git", "rev-parse", "--short=7", "HEAD"],
            capture_output=True, text=True, cwd=os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
        ).stdout.strip()
    except OSError:
        head = ""

    if not head or not revision:
        return

    if head[:7] == revision[:7]:
        print(f"   the tree is on the same revision ({head})")
        return

    message = (
        f"{BASE} is serving build {revision} while this tree is on {head} — "
        f"deploy the tree or rebuild and restart the server"
    )
    if FRESH_MODE:
        failures.append(message)
    else:
        print(f"   note: {message}")


def check_demo_request(failures: list) -> None:
    """The one write endpoint must answer GET with its own contract.

    The demo form's transport is configured per deployment — intentionally so,
    because the alternative is a form that reports success for messages nobody
    receives. What never changes is the contract the page relies on: the
    endpoint answers, says whether it is configured, and names the fallback
    inbox. A deployment whose endpoint cannot say those things is selling a
    contact form that cannot be reached, which is worse than no form.
    """
    code, body = fetch("/api/demo-request")
    if code != "200":
        failures.append(f"/api/demo-request answered {code} — the demo-request contract is down")
        return

    try:
        contract = json.loads(body)
    except json.JSONDecodeError:
        failures.append("/api/demo-request did not answer with JSON")
        return

    for key in ("endpoint", "configured", "inbox"):
        if key not in contract:
            failures.append(f"/api/demo-request contract is missing `{key}` — the fallback the form sends people to cannot be found")

    print("\ndemo request:")
    print(f"   configured {contract.get('configured')!r} (transport is per-deployment by design)")
    print(f"   inbox      {contract.get('inbox')}")


def fetch(path):
    with tempfile.NamedTemporaryFile(delete=False, suffix=".html") as handle:
        tmp = handle.name
    code = subprocess.run(
        ["curl", "-s", "-o", tmp, "-w", "%{http_code}", BASE + path],
        capture_output=True, text=True,
    ).stdout.strip()
    body = open(tmp, "rb").read().decode("utf-8", "replace")
    os.unlink(tmp)
    return code, body


def main() -> int:
    links, images, failures, warnings = set(), set(), [], []
    weights = []
    script_srcs: dict = {}
    # `{page: {fragments}}` — a link into the middle of a page is a link that
    # can break without any route disappearing.
    fragments: dict = {}
    fragment_links: dict = {}
    titles, descriptions = [], []

    for page in PAGES:
        code, html = fetch(page)
        if code != "200":
            failures.append(f"page {page} returned {code}")
            continue

        if BUDGET_MODE:
            weights.append(
                (
                    page,
                    gzip_size(html.encode("utf-8")),
                    len(re.findall(r"<[a-zA-Z][^>]*>", html)),
                )
            )
            script_srcs[page] = re.findall(r'src="(/_next/static/[^"]+\.js)"', html)

        title = re.search(r"<title>(.*?)</title>", html, re.S)
        description = re.search(r'<meta name="description" content="([^"]*)"', html)
        if title:
            titles.append((page, len(title.group(1)), title.group(1)))
        else:
            failures.append(f"{page}: no <title>")
        if description:
            descriptions.append((page, len(html_module.unescape(description.group(1)))))
        else:
            failures.append(f"{page}: no meta description")
        if not re.search(r'<link rel="canonical"', html):
            failures.append(f"{page}: no canonical")
        if not re.search(r'<meta property="og:image"', html):
            failures.append(f"{page}: no og:image")
        if not re.search(r'<meta name="viewport"', html):
            failures.append(f"{page}: no viewport")

        h1 = len(re.findall(r"<h1[ >]", html))
        if h1 != 1:
            failures.append(f"{page}: {h1} h1 elements")

        for img in re.findall(r"<img[^>]*>", html):
            if "alt=" not in img:
                failures.append(f"{page}: image without alt — {img[:90]}")

        lowered = html.lower()
        for pattern in BANNED:
            if re.search(pattern, lowered):
                warnings.append(f"{page}: banned phrase /{pattern}/")

        # Every id on the page, so fragment links can be checked for real.
        # The lookbehind keeps `aria-invalid`, `data-id` and friends out of it.
        ids = re.findall(r'(?<![-\w])id="([^"]+)"', html)
        fragments[page] = set(ids)

        # A duplicate id is invalid HTML and makes `getElementById` return the
        # wrong node — the SVG gradient case that used to repeat per instance.
        counted = Counter(ids)
        for value, count in counted.items():
            if count > 1:
                failures.append(f"{page}: id \"{value}\" appears {count} times")

        for href in re.findall(r'href="([^"]+)"', html):
            if href.startswith(("mailto:", "#", "data:")):
                continue
            if href.startswith("http"):
                if "proglassautomation.com" in href:
                    links.add(urllib.parse.urlparse(href).path or "/")
                continue
            if href.startswith("/"):
                target, _, fragment = href.partition("#")
                links.add(target or "/")
                if fragment:
                    fragment_links.setdefault(target or "/", set()).add(fragment)

        for src in re.findall(r'src="(/_next/image\?url=[^"]+)"', html):
            query = urllib.parse.parse_qs(urllib.parse.urlparse(src.replace("&amp;", "&")).query)
            images.add(query["url"][0])
        for src in re.findall(r'src="(/images/[^"]+)"', html):
            images.add(src)

    for link in sorted(links):
        code, _ = fetch(link)
        if code not in ("200", "308"):
            failures.append(f"internal link {link} -> {code}")
    for image in sorted(images):
        code, _ = fetch(image)
        if code != "200":
            failures.append(f"image {image} -> {code}")

    for missing in MUST_404:
        code, _ = fetch(missing)
        if code != "404":
            failures.append(
                f"{missing} returned {code} instead of 404 — an address that does not exist "
                "must say so, or a draft or a typo gets served as a real page"
            )

    # A fragment link that lands nowhere scrolls to the top of the page and
    # looks like a broken promise. Checked against the ids actually rendered.
    for target, wanted in sorted(fragment_links.items()):
        if target not in fragments:
            code, html = fetch(target)
            if code != "200":
                continue
            fragments[target] = set(re.findall(r'id="([^"]+)"', html))
        for fragment in sorted(wanted):
            if fragment not in fragments[target]:
                failures.append(f"fragment link {target}#{fragment} has no matching id on {target}")

    print(f"pages: {len(PAGES)}   internal links: {len(links)}   images: {len(images)}")
    print("\nlongest titles (target <= 62):")
    for page, length, text in sorted(titles, key=lambda row: -row[1])[:6]:
        flag = "!" if length > 62 else " "
        print(f" {flag} {length:3d}  {page}: {text}")
    print("\nlongest descriptions (target <= 160):")
    for page, length in sorted(descriptions, key=lambda row: -row[1])[:6]:
        flag = "!" if length > 160 else " "
        print(f" {flag} {length:3d}  {page}")

    check_build_stamp(failures)
    check_demo_request(failures)

    if BUDGET_MODE:
        budget_failures = budget_section(weights)
        failures.extend(budget_failures)
        failures.extend(first_load_section(script_srcs))
        assets = measure_assets()
        if assets:
            print("\nbuilt assets (every chunk the build emits, compressed):")
            print(f"   javascript  {assets['js_gz']:6.1f}K   budget {ASSET_BUDGET['js_gz']}K")
            print(f"   css         {assets['css_gz']:6.1f}K   budget {ASSET_BUDGET['css_gz']}K")
            if assets["js_gz"] > ASSET_BUDGET["js_gz"]:
                failures.append(
                    f"javascript is {assets['js_gz']:.1f} KB compressed, over the {ASSET_BUDGET['js_gz']} KB budget"
                )
            if assets["css_gz"] > ASSET_BUDGET["css_gz"]:
                failures.append(
                    f"css is {assets['css_gz']:.1f} KB compressed, over the {ASSET_BUDGET['css_gz']} KB budget"
                )
        else:
            print("\nbuilt assets: skipped (run `npm run build` first)")

    print(f"\nFAILURES: {len(failures)}")
    for item in failures:
        print("  -", item)
    print(f"WARNINGS: {len(warnings)}")
    for item in warnings:
        print("  -", item)
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
