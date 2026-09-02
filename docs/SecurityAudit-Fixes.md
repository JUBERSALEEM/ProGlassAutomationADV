# Security audit — fixes applied

Companion to `Docs/SecurityAudit.md`. One section per change: what changed,
why, which files, and what could break.

---

## C-1 — Remote code execution via the OTA updater  (CRITICAL)

**Files:** `Services/Updates/UpdateService.cs`, `Tests/Agent/UpdateOriginSecurityTests.cs`

**What was wrong.** `CheckForUpdateAsync` reads `browser_download_url` out of the GitHub
API JSON. `DownloadUpdateAsync` fetched that URL verbatim, and `InstallAndRestart`
passed the resulting file to `Process.Start(..., UseShellExecute = true)`. Nothing
verified where the URL pointed. Tampering with the API response — a hostile proxy,
DNS interception, a replaced release asset, a compromised account — would cause the
app to download and **execute an arbitrary binary with the user's privileges**.

**What changed.**
1. `IsAllowedDownloadUrl(url)` — requires an absolute **HTTPS** URL whose host is an
   **exact match** against a five-entry GitHub allowlist.
2. `DownloadUpdateAsync` calls it *before the first byte is fetched*; refusal is
   logged, audited (`UPDATE_DOWNLOAD_BLOCKED`) and reported to the user.
3. `InstallAndRestart` additionally refuses to launch anything outside the user
   profile directory (defence in depth).

**Why an allowlist and not a hash.** A hash taken from the same JSON as the URL proves
nothing — an attacker who can edit one can edit the other. Genuine authenticity needs
an out-of-band trust anchor: Authenticode signature verification, which is what the
**Velopack** path already does. This fix does what can be done correctly at this layer
and the code says so explicitly rather than implying more assurance than it delivers.

**Why exact host matching.** `EndsWith(".github.com")` accepts `evil-github.com`;
`Contains("github.com")` accepts `attacker.net/github.com/x`. Both are pinned as
failing tests so a future "simplification" breaks the build.

**Breakage risk:** none for genuine releases — all five real asset hosts are allowlisted
and covered by passing tests.

---

## C-2 — Unhandled startup crash  (CRITICAL)

**File:** `App.xaml.cs`

**What was wrong.** `Application_Startup` is `async void`. An exception thrown after the
first `await` does **not** reach `DispatcherUnhandledException` — it is posted to the
synchronisation context and terminates the process. `DatabaseInitializer.Initialize()`
and nine parallel DB initialisations ran with no try/catch. A corrupt, locked or
read-only SQLite file therefore produced a bare OS crash at launch: no log, no error ID,
nothing the user or support could act on.

**What changed.** The original body moved **unchanged** into `StartupCoreAsync()`.
`Application_Startup` now awaits it inside a try/catch and routes any failure to
`HandleFatalStartupFailure`, which logs, writes the crash report, shows the same
professional dialog used elsewhere, and exits with code 1. The nested `catch` inside
that handler covers the case where logging *itself* fails (disk full, permissions).

**Breakage risk:** none — no startup logic was reordered, added or removed.

---

## X-2 / X-3 — Missing global exception handlers  (HIGH)

**File:** `App.xaml.cs`

Only `DispatcherUnhandledException` was registered. Added:

- **`AppDomain.CurrentDomain.UnhandledException`** — background-thread crashes
  (`Task.Run` workers, timer callbacks) previously killed the process with no log line.
  Cannot mark the exception handled (the CLR is already tearing down) but guarantees the
  crash is recorded.
- **`TaskScheduler.UnobservedTaskException`** — a faulted fire-and-forget task was
  completely invisible; a failed background save looked like success. Now logged and
  marked observed.

Both handlers are wrapped in their own try/catch: a last-chance handler must never throw.

---

## C-3 — CSV formula injection  (HIGH, exploits the recipient)

**File:** `Services/Export/Csv/CsvExportEngine.cs`

**What was wrong.** CsvHelper ships with injection protection **disabled**
(`InjectionOptions.None`) and it was never enabled. Exported cells carry externally
supplied free text — customer names, project names, driver names, remarks. Excel and
Sheets execute any cell starting with `=`, `+`, `-`, `@` as a formula. A customer saved as
`=cmd|'/c calc'!A0` becomes live code **on the accountant's machine** when the export is
opened.

**What changed.** A single shared `SafeCsvConfig` with `InjectionOptions.Escape`.

**Two deliberate deviations from the library defaults, both to protect data fidelity:**

- **`Escape`, not `Strip`.** Strip deletes the character, silently corrupting real data —
  a project genuinely called `-Phase 2` would export as `Phase 2`. Escape prefixes the
  OWASP-recommended apostrophe and preserves content. Accounting exports must never be
  silently altered.
- **Injection set narrowed to `= @ +`.** CsvHelper's default set includes `-`, and it
  applies the rule to numeric fields too, so `-1234.56` exports as `'-1234.56` and lands
  in Excel as *text* rather than a negative number (JoshClose/CsvHelper issue #2126).
  This export feeds financial reconciliation; that would trade a security bug for a
  correctness bug. `=`, `@` and `+` are the formula triggers and are never the first
  character of a number this app emits, so escaping them is lossless.

**Breakage risk:** cells legitimately beginning with `=`, `@` or `+` gain a leading
apostrophe. No such field exists in the three current layouts (Delivery Note, Job Order,
Proforma Invoice).

**Also:** `CsvConfiguration`/`InjectionOptions` live in `CsvHelper.Configuration`, not
`CsvHelper`. That using was added, and both type names were added to
`tools/layout-verify/using_check.py` so the omission is caught automatically in future
(verified: removing the using makes the gate fail).

---

## P-2 — Gemini API key in the URL query string  (HIGH, privacy)

**File:** `Services/AI/AIService/GeminiAiService.cs`

The key was appended as `?key=<secret>` on both the request and streaming endpoints.
Query strings are the most commonly captured part of an HTTP request: proxy logs,
corporate TLS-inspection appliances, crash dumps, and any future logging of the URL.
Both call sites now send `x-goog-api-key` as a header and the URL carries no credential.
This also makes Gemini consistent with the Groq and Cohere services, which already used
`Authorization: Bearer`.

**Verified:** no occurrence of `key=` with a credential remains in the file.
**Breakage risk:** none — `x-goog-api-key` is Google's documented equivalent.

---

## M-1 — `SectionActivityViewModel` had no disposal path  (MEDIUM)

**File:** `ViewModels/Shared/SectionActivityViewModel.cs`

Subscribes to the **static** `AuditService.ActivityLogged`, never unsubscribed, and did
not implement `IDisposable` — so a caller had no way to release an instance at all.
Now implements `IDisposable` with an idempotent `Dispose()`.

**Deliberately honest scoping.** I first recorded this as an active leak because the type
is created at 12 sites. Tracing all 12 owners showed each is a `MainViewModel` property
initializer — created **once per process**. Navigation in this app swaps content inside
long-lived ViewModels rather than recreating them, so instances do not currently
accumulate. This fix removes a *latent* hazard, not measured growth, and
`Docs/SecurityAudit.md` says exactly that instead of claiming 16 leaks were fixed.

---

## Not changed, and why

- **The other 15 static-event subscriptions.** All belong to app-lifetime singletons.
  Adding teardown that never runs would be churn without benefit.
- **The 22 multi-line `catch { }` blocks.** Real debt, but each needs individual judgement
  about what to log; a blanket edit across 22 unrelated call sites is exactly the kind of
  sweeping change the brief prohibits. Listed as remaining risk.
- **No package upgrades.** Compatibility cannot be verified without a build.
- **QuestPDF licence** — flagged for a commercial decision, library untouched.
