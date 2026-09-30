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

Run `npm run i18n:audit`. Coverage is honest per language, and the site is built
so the remaining work is data entry through this pipeline rather than code:
adding a language is a file, and filling one is a batch.
