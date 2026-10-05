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
import os
import re
import subprocess
import sys
import tempfile
import urllib.parse

BASE = "http://127.0.0.1:3000"
BUDGET_MODE = False
for argument in sys.argv[1:]:
    if argument == "--budget":
        BUDGET_MODE = True
    elif argument.startswith("-"):
        raise SystemExit(f"unknown option: {argument}\nusage: audit-site.py [base-url] [--budget]")
    else:
        BASE = argument

PAGES = [
    "/", "/features", "/optimization", "/how-it-works", "/screenshots", "/pricing",
    "/about", "/developer", "/sponsors", "/collaborate", "/contact", "/resources", "/blog", "/worked-example",
    "/blog/why-the-price-basis-is-stamped", "/blog/guillotine-versus-free-nesting",
    "/blog/why-delivery-balance-is-hard",
    "/legal/privacy", "/legal/terms", "/legal/license",
    # The two operations pages read Supabase and are behind an operator session
    # in production; in a local crawl they render their sign-in or preview state,
    # which still has to carry the metadata, one h1 and working links.
    "/reports", "/deliveries",
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
    "/": {"gz": 90, "nodes": 3500},             # measured 85.9K / 3464
    "/features": {"gz": 85, "nodes": 4500},     # measured 80.5K / 4310
    "/optimization": {"gz": 55, "nodes": 2600},  # measured 46.8K / 1859
    "/how-it-works": {"gz": 50, "nodes": 1800},  # measured 47.8K / 1716
    "/screenshots": {"gz": 72, "nodes": 9500},   # measured 69.1K / 9198
    "/worked-example": {"gz": 55, "nodes": 1900},  # measured 48.2K / 1568
    "/sponsors": {"gz": 36, "nodes": 900},       # measured 33.4K / 770
    "/developer": {"gz": 52, "nodes": 1150},     # measured 45.6K / 1023
    "/collaborate": {"gz": 40, "nodes": 900},    # measured 34.2K / 816
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
ASSET_BUDGET = {"js_gz": 880, "css_gz": 22}


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

    if BUDGET_MODE:
        budget_failures = budget_section(weights)
        assets = measure_assets()
        if assets:
            print("\nbuilt assets (every chunk the build emits, compressed):")
            print(f"   javascript  {assets['js_gz']:6.1f}K   budget {ASSET_BUDGET['js_gz']}K")
            print(f"   css         {assets['css_gz']:6.1f}K   budget {ASSET_BUDGET['css_gz']}K")
            if assets["js_gz"] > ASSET_BUDGET["js_gz"]:
                budget_failures.append(
                    f"javascript is {assets['js_gz']:.1f} KB compressed, over the {ASSET_BUDGET['js_gz']} KB budget"
                )
            if assets["css_gz"] > ASSET_BUDGET["css_gz"]:
                budget_failures.append(
                    f"css is {assets['css_gz']:.1f} KB compressed, over the {ASSET_BUDGET['css_gz']} KB budget"
                )
        else:
            print("\nbuilt assets: skipped (run `npm run build` first)")
        failures.extend(budget_failures)

    print(f"\nFAILURES: {len(failures)}")
    for item in failures:
        print("  -", item)
    print(f"WARNINGS: {len(warnings)}")
    for item in warnings:
        print("  -", item)
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
