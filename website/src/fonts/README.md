# The typefaces

Six files, four families, all served from this origin. Nothing here is fetched
from a font host at runtime, which is what lets `/legal/privacy` say plainly
that visiting a page does not call a font service, and what lets the theme
studio switch family without a network round trip.

They are **vendored, not hand-drawn**: each file is copied byte-for-byte out of
the `@fontsource-variable/*` package named against it in `package.json`, so the
provenance of every file is a version number. The packages are dependencies for
that reason — they are the record of where these bytes came from and what
licence they carry.

| File | Package (devDependency) | Face |
|---|---|---|
| `Inter-Variable-latin.woff2` | `@fontsource-variable/inter` | Inter, weight axis |
| `JetBrainsMono-Variable-latin.woff2` | `@fontsource-variable/jetbrains-mono` | JetBrains Mono, weight axis |
| `SourceSerif4-Variable-latin.woff2` | `@fontsource-variable/source-serif-4` | Source Serif 4, upright |
| `SourceSerif4-Variable-latin-italic.woff2` | `@fontsource-variable/source-serif-4` | Source Serif 4, italic |
| `IBMPlexSans-Variable-latin.woff2` | `@fontsource-variable/ibm-plex-sans` | IBM Plex Sans, upright |
| `IBMPlexSans-Variable-latin-italic.woff2` | `@fontsource-variable/ibm-plex-sans` | IBM Plex Sans, italic |

## Re-vendoring after a version bump

Run from `website/`, after `npm install`:

```bash
cp node_modules/@fontsource-variable/inter/files/inter-latin-wght-normal.woff2 \
   src/fonts/Inter-Variable-latin.woff2
cp node_modules/@fontsource-variable/jetbrains-mono/files/jetbrains-mono-latin-wght-normal.woff2 \
   src/fonts/JetBrainsMono-Variable-latin.woff2
cp node_modules/@fontsource-variable/source-serif-4/files/source-serif-4-latin-wght-normal.woff2 \
   src/fonts/SourceSerif4-Variable-latin.woff2
cp node_modules/@fontsource-variable/source-serif-4/files/source-serif-4-latin-wght-italic.woff2 \
   src/fonts/SourceSerif4-Variable-latin-italic.woff2
cp node_modules/@fontsource-variable/ibm-plex-sans/files/ibm-plex-sans-latin-wght-normal.woff2 \
   src/fonts/IBMPlexSans-Variable-latin.woff2
cp node_modules/@fontsource-variable/ibm-plex-sans/files/ibm-plex-sans-latin-wght-italic.woff2 \
   src/fonts/IBMPlexSans-Variable-latin-italic.woff2
```

Only the **latin** subsets are vendored. The packages also ship Cyrillic, Greek
and Vietnamese subsets; the application carries fifteen locales, but this website
is written in English, so the other subsets would be dead weight in every
visitor's cache.

## Where each one is used

`src/app/layout.tsx` declares them with `next/font/local`, which self-hosts and
preloads them and hands each one a CSS variable:

- `--font-inter` — the body face, and the studio's default.
- `--font-mono` — every figure, size and identifier on the site. Fixed, not a
  choice: a measurement should line up under the one above it.
- `--font-serif`, `--font-plex` — the two alternative body faces offered by the
  theme and colour panel.

Each of the three switchable faces carries its **own italic**, which is the
point of vendoring the italic files rather than letting the browser slant the
upright: the panel's specimen shows bold, bold italic and underline, and a
synthesised oblique is not the same typeface.

## Licence

All four families are under the SIL Open Font License 1.1, which permits
commercial use, modification and redistribution as part of a website. Authors:

- Inter — Rasmus Andersson
- JetBrains Mono — JetBrains
- Source Serif 4 — Frank Grießhammer, Adobe
- IBM Plex Sans — IBM

This is also stated on the site itself, at `/legal/license`, because a visitor
reading a licence page is entitled to know what else shipped with the page.
