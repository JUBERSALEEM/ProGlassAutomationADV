# Translation

Everything a visitor reads goes through one function. This directory is that
function, its dictionaries, and the vocabulary it inherits from the desktop
application.

```
src/i18n/
  locales.ts        the fifteen languages, their names, their scripts, the country map
  core.ts           the pure half: lookup, placeholders, URLs, hreflang
  server.ts         the server half: read the request, stack the dictionaries
  provider.tsx      the client half: `useT()` and `useI18n()`
  app-strings.ts    the application's own vocabulary, per language
  app-strings/      one JSON per language, generated from Themes/Languages/*.xaml
  dict/             the site's own dictionary, one file per language
  dict/keys.ts      every sentence the site can render — generated
```

## The three places a sentence can come from

A sentence is looked up in this order, and the first hit wins:

1. **The site's dictionary** — `dict/<locale>.ts`, written for this website.
2. **The application's language file** — `Themes/Languages/<code>.xaml`, indexed
   by its English value. This is where a term the software already names comes
   from, and it is why the site cannot disagree with the product about what a
   proforma or a delivery note is called.
3. **The English sentence itself.** A sentence nobody has translated yet shows
   in English rather than as a key name or a blank. The failure mode is a page in
   mixed language, never a broken page.

`en-US` is empty and stays empty: the key *is* the English sentence.

## The key is the English sentence

There are no invented identifiers (`nav.product.features`). The key is the
sentence itself, which has three consequences worth the trade:

- a translator sees the sentence in context, not a name;
- `src/data/*.ts` stays English and readable — a new row of data needs no
  translation work to render;
- the extractor can find every sentence by reading the source, so the key list
  cannot drift from what the site renders.

Placeholders are `{name}`, and `fill()` substitutes them:

```ts
t("Desktop software · v{version} · No account required", { version: site.version })
```

## The pipeline

| command | what it does |
| --- | --- |
| `npm run i18n:app` | rebuild `app-strings/` from the application's XAML files |
| `npm run i18n:import-app` | fill every language from the application's own translations |
| `npm run i18n:keys` | scan the source, write `dict/keys.ts`, write the work plan |
| `npm run i18n:audit` | coverage per language, and where each language's entries came from |
| `npm run i18n:gaps` | the same, plus `/tmp/proglass-i18n-gaps.json` for batching |
| `npm run i18n:rendered` | fetch every page and report English that is still on it |
| `npm run i18n:wire` | run the page-level codemod: translator, metadata, LocaleLink, prose attributes |
| `npm run i18n:blocks` | wire the mixed sentences the line-based passes cannot see |
| `npm run i18n:wire-text` | wrap raw JSX text children that are prose (headings, labels, buttons) |
| `npm run i18n:wire-fields` | wrap the prose fields a component reads straight into JSX |

`i18n:rendered` is the one that tells the truth about the *page* rather than
about the dictionary: it separates text the site does not know about (a wiring
gap — no dictionary can fix it) from text it knows and has not translated yet (a
translation gap). Run it against a server:

```bash
npx next dev -p 3200 -H 0.0.0.0 &
node scripts/i18n-rendered-gaps.mjs --locale ar-AE --dump /tmp/ar.json
```

## Translating

A batch is a tab-separated file, `English<TAB>translation`, one per line:

```bash
node scripts/i18n-apply.mjs ar-AE /tmp/i18n/ar.tsv
```

or by key number once the numbers are known:

```bash
node scripts/i18n-apply.mjs ar-AE /tmp/i18n/ar-index.tsv --index
```

The script refuses to lose anything: existing entries are kept, a key the site
never renders is reported and skipped, a blank value is rejected, and an index
batch is refused outright when the key list has been regenerated since the work
plan was written — numbers shift when a sentence is added, and a batch written
against the old numbering would land on its neighbours.

Two more rules for anyone writing a batch:

- **Take the term from the application.** `Themes/Languages/<code>.xaml` has the
  product's own word for a proforma, a job order, a stage and a base rate.
- **Leave names alone.** A customer, a person, a supplier, a font family, a file
  path and a format name are the same string in every language; they are
  excluded from the key list rather than translated.

## Why a page can still read English, measured

"The language is set to French and the page is still English" has four causes,
and they are not the same problem. `i18n-rendered-gaps.mjs` separates them by
reading the built pages and sorting every text node that reads as English:

```bash
node scripts/i18n-rendered-gaps.mjs --locale fr-FR            # counts + worst offenders
node scripts/i18n-rendered-gaps.mjs --locale fr-FR --dump /tmp/fr.json
```

1. **Key, not translated** — a sentence the site knows about and this language
   has not covered yet. This is volume, and it is the only one of the four that
   a dictionary can fix.
2. **Not extracted** — prose that no pass ever turned into a key, so no
   dictionary could hold it however complete it was. Round 25 found three of
   these at once, each of which had been keeping whole sections English in all
   fifteen languages: quoted values held in page and component literals (the
   step list on the home page, the paragraph arrays on the legal pages); a code
   filter that rejected any span containing a semicolon or the word "document."
   and any string beginning with a digit ("9-step wizard with company and client
   cards", "0.5 m² minimum billable area per panel"); and table values rendered
   without `t()`. Fixed by two extractor passes (`i18n-extract.mjs`) and
   `i18n-wire-fields.mjs`. 2,554 → 2,903 keys.
3. **Not wired** — a text node no `t()` call reaches: `<p className="eyebrow">
   Built up, not guessed at</p>` renders as plain text, not as a key. 81 of
   them, concentrated in eyebrows, card headings, link labels and buttons —
   which is why the complaint was about *headings*. Fixed by
   `i18n-wire-text.mjs`, a narrow codemod that edits source and is documented
   with its guards.
4. **Several keys side by side** — markup glued two known strings together. Not
   a gap; the audit lists them separately so the list means what it says.

The distinction matters more than the numbers: causes 2 and 3 are invisible to
a coverage percentage. Arabic read **100 %** of the dictionary and still
rendered 919 English text nodes, because 188 of them were translated strings
the component printed without asking for the translation, and the rest were
strings that had never become keys. After round 25: **573** nodes, **102**
strings, of which the remainder are endonyms (`Bahasa Indonesia`), seeded log
lines and a few destructured map labels.

## Filling two thousand sentences per language

Three tools, in order, none of them clever:

**1. The application's own wording, where it exists.**

```bash
node scripts/i18n-memory-fill.mjs --all                # writes <locale>-mem.tsv per language
node --experimental-strip-types scripts/i18n-apply.mjs fr-FR /tmp/i18n/mt/fr-FR-mem.tsv
```

`Themes/Languages/*.xaml` holds 2,750 strings per language, paired by key with
the English file. A website key that is byte-for-byte (or case-and-punctuation-
equal to) an English string in that file takes the application's own
translation; a key that is several app strings joined by ` · `, ` — `, ` / ` or
`, ` is composed from them, and refused unless every part resolves. Nothing is
invented: this pass writes only what the software already says, and it is the
reason a document name on the site is the document name in the product. It
yields about 45 keys per language today; it will yield more every time the site
takes another word from the application.

**2. Machine translation for the rest, through the same applier.**

```bash
node scripts/i18n-machine-fill.mjs --all                 # needs internet
node scripts/i18n-machine-fill.mjs --locale de-DE --dry  # batch without sending
```

It masks the product's own terms and every `{v0}` placeholder before the text
leaves, and restores them afterwards, so `Delivery Note` cannot come back as
"Anweisungsgenerator". A row lands only if every placeholder survived and the
answer is in the right script; the rest are counted and skipped, never pasted.

**3. If the machine running this has no internet** — the sandbox this was built
in does not — the same script runs in two halves that a person drives:

```bash
node scripts/i18n-machine-fill.mjs --split --locale de-DE --fresh --max-url 7000 --max-source 6000
node scripts/i18n-machine-fill.mjs --print --locale de-DE --chunk 3    # one GET URL
# … open that URL anywhere (browser, phone, another machine) and paste the JSON array back …
node scripts/i18n-machine-fill.mjs --ingest --locale de-DE --chunk 3 < response.json
node --experimental-strip-types scripts/i18n-apply.mjs de-DE /tmp/i18n/mt/de-DE-c3.tsv
```

The queue is frozen once and cut into request-sized chunks, so chunks may be
fetched in any order; a chunk already applied prints `done`. Guard rails worth
knowing: the answer's string count must equal the chunk's row count (a
truncated answer is refused rather than misaligned), a fresh split deletes the
previous chunk files so a stale URL cannot be answered, and the applier keeps a
key's own leading space (`" — clamped"` is a key).

## Browser translation: what it is for, and what it is not for

Chrome translates a whole page in about a second and it costs nothing, so the
honest question is why this site ships 2,554 sentences per language at all. The
answer is not "we did it first" — it is that the two tools answer different
questions, and this site has one question that a browser translator gets wrong
every time.

**What the browser is good at.** A visitor reading Indonesian, Turkish or
Japanese meets a site whose dictionaries are 12 % filled. Chrome will render
the remaining paragraphs competently, in place, for free, and it will do it for
a language nobody has thought to fill. That is a real service and this site does
not fight it: the document carries a correct `lang` and `dir`, which is exactly
what makes the browser offer the translation, and the language menu says so out
loud instead of letting the reader assume fifteen finished languages.

**What the browser must not be asked to do here.**

1. **Trade vocabulary.** A machine translator has never seen a proforma, an SGU
   air space, a Z/W trim or an edge-deletion table. It renders them plausibly
   and wrongly, and the reader is an estimator who will recognise the wrong word
   immediately. The product's own words live in `Themes/Languages/*.xaml`, and
   the site is built so the two cannot drift.
2. **Numbers as prose.** Machine translation rewrites `34,762.19` as
   `34.762,19`, `40 pcs` as `40 pieces`, `3210 × 2250` as something with a
   different multiplication sign, and it will happily translate a unit. Every
   figure on this site is a real one from the seeded documents; a translated
   number is a false statement about the product.
3. **The half-page problem.** The optimizer, the specification grid and the P&L
   tabs recompute their text in the browser. Chrome translates the DOM it can
   see, so a run, a tab switch or a rule tick inside a translated page inserts
   English into a translated page — the exact "partially translated" state this
   round exists to remove.
4. **Attributes and metadata.** `aria-label`, `title`, `placeholder`, the
   per-page `<meta name="description">` and the JSON-LD are translated
   unreliably or not at all, and they are the parts a screen reader and a search
   engine read.
5. **Its own vocabulary.** A language list written in the languages themselves
   is destroyed by translating it, and a brand name translated is a brand name
   lost.

**So the policy is: ship the sentences, mark the data, cooperate with the
browser for the rest.** Concretely, in this repository:

- the language menu states each language's real coverage, computed from the
  dictionary in the build (`src/i18n/coverage.ts`) rather than typed into a
  table, and tells the reader that the browser can translate the remainder;
- the menu and the brand mark are marked `translate="no"` (with the
  `notranslate` class, because engines differ on which they read);
- anything that is data rather than prose — a spec name, a serial, a module
  key, a rack code — goes through `<NoTranslate>`, which is the same line
  `scripts/i18n-extract.mjs` already draws when it refuses to treat identifiers
  and proper nouns as language. Prose stays translatable; data does not.

A third-party translation widget was considered and rejected for the same
reason: it would put a second, ungoverned dictionary in front of the reader,
break the 4,560 `hreflang` links that already tell search engines about
fifteen real versions, and hide the gap instead of closing it.

## What is not language

The extractor excludes, deliberately and visibly:

- identifiers and code (`AppConfig.FontStyleName = Italic`),
- file formats and paths (`DXF · .OPT · .ISO`, `Views/SheetStore/`),
- font stacks, CSS values, SVG path data,
- proper nouns (customers, people, suppliers, brand names),
- a bare placeholder or a number with a unit.

Anything else that reads as a sentence is a key. That is the whole rule, and it
is why the coverage figure means something: 100 % on a language means a reader
of that language finds no English sentence on the site — not that a list has no
holes.

## Where the languages stand

Run `npm run i18n:audit`. Coverage is honest per language:

| language | entries | English still on 16 pages | notes |
| --- | --- | --- | --- |
| ar-AE | 2,553 / 2,553 | 919 nodes | 100 % — the reference language |
| de-DE | 1,402 / 2,553 | 843 nodes | 55 % — the most complete of the filled ones |
| ur-PK · hi-IN · bn-BD · ru-RU | 364 / 2,553 each | ~2,244 nodes | app vocabulary + the highest-frequency band |
| fr-FR | 357 / 2,553 | 1,783 nodes | same |
| the rest | 297 / 2,553 each | ~2,394 nodes | the app-memory pass only |

An English page renders 2,680 English text nodes across the same sixteen pages,
so the node column is directly comparable: it counts what a visitor still reads
in English, not what a dictionary contains.

The site is built so the remaining work is data entry through this pipeline
rather than code: adding a language is a file, and filling one is a batch. The
one-command path for a machine with internet is
`node scripts/i18n-machine-fill.mjs --all`; the sandbox this repository was
developed in has no egress, which is why the hand-driven `--split` / `--print` /
`--ingest` half exists at all.
