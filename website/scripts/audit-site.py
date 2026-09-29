#!/usr/bin/env python3
"""Crawl the built site with curl and report broken links, missing metadata,
missing image files and headings, plus a few content-safety checks.

Usage: python3 scripts/audit-site.py [base-url]
"""
import html as html_module
import os
import re
import subprocess
import sys
import tempfile
import urllib.parse

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:3000"

PAGES = [
    "/", "/features", "/optimization", "/how-it-works", "/screenshots", "/pricing",
    "/about", "/developer", "/contact", "/resources", "/blog",
    "/blog/why-the-price-basis-is-stamped", "/blog/guillotine-versus-free-nesting",
    "/blog/why-delivery-balance-is-hard",
    "/legal/privacy", "/legal/terms", "/legal/license",
]

# Words that must never appear in customer-facing copy.
BANNED = [
    r"\bsaas\b", r"\b(we are|our) multi-tenant\b", r"\btenant dashboard\b", r"\bstart (your )?free trial\b",
    r"\bsubscription plan\b", r"\bcloud workspace\b", r"\b10x\b", r"\b99\.9%",
    r"\btrusted by \d", r"\blorem ipsum\b", r"revolutionary platform", r"best software ever",
    r"world-class", r"cutting-edge", r"game[- ]chang",
]


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
    titles, descriptions = [], []

    for page in PAGES:
        code, html = fetch(page)
        if code != "200":
            failures.append(f"page {page} returned {code}")
            continue

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

        for href in re.findall(r'href="([^"]+)"', html):
            if href.startswith(("mailto:", "#", "data:")):
                continue
            if href.startswith("http"):
                if "proglassautomation.com" in href:
                    links.add(urllib.parse.urlparse(href).path or "/")
                continue
            if href.startswith("/"):
                links.add(href.split("#")[0] or "/")

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

    print(f"pages: {len(PAGES)}   internal links: {len(links)}   images: {len(images)}")
    print("\nlongest titles (target <= 62):")
    for page, length, text in sorted(titles, key=lambda row: -row[1])[:6]:
        flag = "!" if length > 62 else " "
        print(f" {flag} {length:3d}  {page}: {text}")
    print("\nlongest descriptions (target <= 160):")
    for page, length in sorted(descriptions, key=lambda row: -row[1])[:6]:
        flag = "!" if length > 160 else " "
        print(f" {flag} {length:3d}  {page}")

    print(f"\nFAILURES: {len(failures)}")
    for item in failures:
        print("  -", item)
    print(f"WARNINGS: {len(warnings)}")
    for item in warnings:
        print("  -", item)
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
