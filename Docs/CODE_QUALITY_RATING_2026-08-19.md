# Code quality rating — ProGlassAutomation v2.0.0
**Date:** 19 Aug 2026 · **Scope:** 659 C# files, 126,439 LOC, 147 XAML · **Reviewer perspective:** senior/staff .NET

## Overall: **6.8 / 10** — *Good, above the WPF-line-of-business average, held back by infrastructure rather than by craft*

This is not typical internal-tooling code. Someone here understands
security, threading and domain modelling at a genuinely senior level. The
weaknesses are almost entirely **structural and process** — no CI, no DI,
no analyzers, thin tests on the money paths — not "this developer writes
bad code."

Read that number as: *strong engineer, under-invested engineering system.*

---

## Scorecard

| # | Dimension | Rating | One-line verdict |
|---|-----------|--------|------------------|
| 1 | Security posture | **8.5** | Genuinely excellent, ahead of most commercial products |
| 2 | Domain correctness | **8.0** | `decimal` discipline, real calculation tests |
| 3 | Readability & intent | **8.5** | Best-in-class comments; explains *why*, not *what* |
| 4 | Consistency | **8.0** | Strong shared patterns, applied uniformly |
| 5 | Error handling | **4.0** | The single worst area — silently empty in Release |
| 6 | Architecture / coupling | **5.0** | No DI; 311 static service calls; God-ViewModels |
| 7 | Concurrency & async | **5.5** | Good `ConfigureAwait` habit, but 55 raw `async void` |
| 8 | Resource & lifetime mgmt | **5.5** | Fixed the worst; 10 undetachable static subs remain |
| 9 | Test coverage & shape | **4.5** | 363 tests, but ~0 on the largest business files |
| 10 | Build/CI/tooling | **2.5** | No CI wired, no analyzers, no `.editorconfig` |

*(Weighted toward 1–5 for a financial/ERP product; 6.8 is the weighted mean.)*

---

## What is genuinely excellent

### Security — 8.5/10
I went looking for the standard catastrophes and the code had already
handled them, correctly and for the right stated reasons:

- **PBKDF2-HMAC-SHA256, 210k iterations**, per-hash salt, self-describing
  format, `CryptographicOperations.FixedTimeEquals`, and a *correct*
  silent migration off legacy salted SHA-256. This is textbook-perfect.
  Most in-house apps in this category are still on unsalted MD5.
- **SQL:** 25 parameterised bindings; the only 3 interpolated
  `CommandText` strings are DDL over hard-coded internal identifiers.
- **Update origin:** HTTPS + *exact-host* allow-list that explicitly
  avoids the `EndsWith(".github.com")` bypass — a subtlety that catches
  experienced people.
- **CSV injection** hardened, with a documented, deliberate narrowing of
  the escape set to avoid mangling negative currency.
- **DPAPI** secret vault with app entropy, key masking on save, legacy
  migration.
- **Agent sandbox** — consent, data boundary, integrity, network
  allow-list, redaction, `OperationGuard.Demand` at 38 call sites.

That is a mature, layered posture. The two holes I found (attachment
`Process.Start` RCE, unbounded UDP peer table) were *gaps in otherwise
well-built defences*, not absence of defence.

### Comment quality — 8.5/10
24,498 comment lines against 126k LOC (~19%), and critically they explain
**rationale**, not mechanics. The `csproj` comment on why `Tests/**` is
excluded, `DashboardFilterService`'s note on not depending on singleton
lifetime, the CSV escape-set reasoning — this is documentation that
survives refactoring and prevents a future maintainer from "fixing" a
deliberate decision. Rare and valuable.

Corroborating signal: **1** TODO/HACK/FIXME in 126k LOC and only **2**
`#pragma warning disable`. The codebase is not papered over.

### Domain modelling — 8.0/10
910 `decimal` usages and **zero** `double`/`float` money fields — the
mistake that quietly destroys invoicing systems, avoided throughout.
Nullable reference types are **enabled** and respected: only 23 `!`
suppressions across the whole repo, which means the annotations are
carrying real information rather than being bulldozed.

---

## What drags the score down

### 1. Error handling — 4.0/10 · *the highest-value fix available*

```
318  Debug.WriteLine calls
 89  literally empty catch blocks
```

`Debug.WriteLine` is `[Conditional("DEBUG")]`. **The compiler deletes it
from the shipped build.** So the real production shape is:

> **~400 code paths that swallow exceptions and leave no trace.**

This is the defect that will cost the most support hours over this app's
life. When a user says "the invoice didn't save," there is frequently no
artefact anywhere to explain why. I fixed the audit-trail instance
because it had compliance consequences; the rest is a mechanical sweep to
`AppLogService` and it is the **single best ROI change** in the repo.

### 2. No engineering infrastructure — 2.5/10

| Check | Status |
|---|---|
| `.github/workflows/` | **absent** — `Docs/CI/build.yml` exists but is an unwired template |
| `TreatWarningsAsErrors` | not set |
| `EnableNETAnalyzers` / `AnalysisLevel` | not set |
| `.editorconfig` | **absent** |
| `Directory.Build.props` | absent |

CI YAML sitting in `Docs/` and never wired to `.github/` means **363
tests exist and nothing runs them**. Combined with no analyzers, this is
why defects like the undrained ffmpeg pipes survive: nothing mechanical
is watching. Moving one file and adding three properties would raise this
dimension from 2.5 to ~7.

### 3. No dependency injection — architecture 5.0/10

```
  0  IServiceCollection / AddSingleton / ServiceProvider
311  static XService.Method(...) call sites
 38  direct `new XService(...)`
```

Services are reached statically or newed in place. Consequences visible
in the code: the `HttpClient` leak (H-2) existed *because* provider
services are constructed ad-hoc rather than owned by a container; and the
untested 2,183-line ViewModels are untestable largely because their
dependencies can't be substituted. This is the root cause behind several
symptoms scored elsewhere.

### 4. God-ViewModels — coupling

Distribution is actually healthy overall — **median file 108 LOC**, mean
191 — but the tail is severe:

| File | LOC |
|---|---|
| `DeliveryNoteDetailViewModel.cs` | 2,183 *(201 members)* |
| `TaxInvoiceDetailViewModel.cs` | 2,130 |
| `StatementGeneratorViewModel.cs` | 1,950 |
| `SettingsViewModel.cs` | 1,853 |
| `SpecGroup.cs` / `StatementWorkflowEngine.cs` | 1,700 each |

12 files over 1,000 LOC; 56 over 500. A 2,183-line class with 201 members
has no meaningful unit boundary. Note the *good* signal alongside it:
10,552 LOC of `.xaml.cs` code-behind across 147 views (~72 lines each) —
MVVM separation is being respected; the logic is in ViewModels, there is
just too much of it per ViewModel.

### 5. Async discipline — 5.5/10

```
1660  await
 427  ConfigureAwait   (good habit, ~26% of awaits — reasonable for UI code)
  68  async void
  55  async void NOT event-handler-shaped   ← unhandled-exception crashes
  39  .Result / .Wait()                     ← deadlock surface
```

An exception in `async void` outside an event handler cannot be caught by
the caller and **terminates the process**. `AsyncUtility.SafeFireAndForget`
already exists in this repo — the pattern is understood, just not applied
in 55 places.

### 6. Event lifetimes — 5.5/10

```
382  +=
 84  -=      (ratio ~4.5:1)
 16  subscriptions to the 12 known static events
  5  matching unsubscribes
 10  lambda subscriptions to static events — physically undetachable
```

The 10 undetachable ones are the real issue (`App.xaml.cs:447`,
`ViewportFitBehavior.cs:61`, `VisualThemeEngine.cs:112`,
`AiChatViewModel.cs:308`, `DashboardViewModel.cs:143`,
`MainViewModel.cs:725`, `SettingsViewModel.cs:283,285`,
`SystemCenterViewModel.cs:80`, `UserCenterViewModel.cs:124`). Each roots
its subscriber for the process lifetime. **Currently bounded** because
these owners are session-long singletons — but that is luck, not design,
and it stops holding the day any becomes per-navigation.

### 7. Test shape — 4.5/10 · *not the count, the placement*

363 assertions across 40 files is a respectable number. The problem:

> **All 40 test files live in `Tests/Agent/`. Zero test files target the
> five largest business ViewModels.**

The suite is real and thoughtful where it exists — `PasswordHasherTests`,
`UpdateOriginSecurityTests`, `SecurityBoundaryTests`, `OperationGuardTests`,
`DguLamPricingTests`, `InvoiceTotalsCalculatorTests`, `SqmCalculatorTests`,
`ProfitLossIntegrityTests`. Security and pricing maths *are* covered, which
is the right instinct.

But `DeliveryNoteDetailViewModel` (2,183 LOC), `TaxInvoiceDetailViewModel`
(2,130) and `StatementWorkflowEngine` (1,700) — the code that decides what
a customer is billed — have no direct tests. And with no CI, even the
existing 363 don't gate anything.

---

## Priority order (highest value first)

1. **Wire CI** — move `Docs/CI/build.yml` → `.github/workflows/`. One file
   move makes 363 existing tests start protecting you. *Hours.*
2. **Enable analyzers + `.editorconfig`**, `EnableNETAnalyzers`,
   `AnalysisLevel=latest`. CA2007/CA1063/VSTHRD100 would have flagged
   several findings in this audit mechanically. *Hours.*
3. **Sweep the ~400 silent catches** to `AppLogService`. Largest reduction
   in future support cost. *A few days, mechanical, low risk.*
4. **Convert 55 non-handler `async void`** to the existing
   `SafeFireAndForget`. Removes a class of hard crash. *1–2 days.*
5. **Detach the 10 static-event lambdas.** *1 day.*
6. **Introduce DI** (`Microsoft.Extensions.DependencyInjection`) at the
   composition root. Unblocks testing the big ViewModels. *Weeks — but it
   is the precondition for items 7.*
7. **Decompose the 12 files >1,000 LOC**, starting with the two Detail
   ViewModels, and add tests to the billing paths as you split.

---

## Honest summary

Rated against a **shipping commercial ERP product**, 6.8 is a fair,
respectable score — most WPF line-of-business codebases I have reviewed
sit at 4–5, and they do not have PBKDF2 at 210k iterations, an agent
security sandbox, or comments that explain their own trade-offs.

The gap between the **craft** (8–8.5 on security, readability, domain
modelling) and the **system** (2.5 on CI/tooling) is the story here. This
codebase was written by someone who knows what good looks like, working
without the automated safety net that would catch the mechanical mistakes
— which is exactly the profile of the bugs I found: not conceptual errors,
but the specific things a human misses and a machine never does (an
undrained pipe, a `Conditional` attribute, a `Kind=Local` parse).

**Fix the infrastructure first.** Items 1–2 cost hours and would have
caught a meaningful share of this audit's findings automatically.
