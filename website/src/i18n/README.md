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
| `npm run i18n:wire` | run the codemods that put text through the translator |
| `npm run i18n:blocks` | wire the mixed sentences the line-based passes cannot see |

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

"The language is set to French and the page is still English" has exactly three
causes, and they are not the same problem. `i18n-rendered-gaps.mjs` separates
them by reading the built pages and sorting every text node that reads as
English:

```bash
node scripts/i18n-rendered-gaps.mjs --locale fr-FR            # counts + worst offenders
node scripts/i18n-rendered-gaps.mjs --locale fr-FR --dump /tmp/fr.json
```

1. **Key, not translated** — a sentence the site knows about and this language
   has not covered yet. On 16 French pages: **1,148 distinct sentences**. This
   is the whole job, and it is volume: 2,552 sentences per language.
2. **Not a key** — text no `t()` ever sees, so no dictionary could translate it
   even if it were perfect. On the same run: **194 fragments** (an array item
   printed as `{item}`, a template literal, a paragraph split across an inline
   element). These are wiring defects, reported per file; the ones found so far
   are fixed.
3. **Several keys side by side** — markup glued two known strings together. Not
   a gap; the audit lists them separately so the list means what it says.

Numbers are per language, and they move: `de-DE` currently renders **479**
untranslated sentences and **196** fragments, `fr-FR` 1,148 and 194.

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
