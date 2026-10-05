# Senior engineering audit — 19 Aug 2026

Full-repository review for security, memory/resource leaks, concurrency
and correctness. 659 C# files, ~126k LOC.

Every item below was traced to the specific line that causes it, and the
fix is applied in this branch. Findings are ordered by severity, not by
how easy they were to find.

**Verification note:** the project targets `net8.0-windows` with WPF, and
this sandbox has neither a Windows target pack nor network access to
NuGet/dotnet, so `dotnet build` cannot run here. Every change was
therefore reviewed by hand against the surrounding code, checked for
brace/using balance, and kept deliberately local. **The branch has not
been compiled — please build once before merging.**

---

## CRITICAL

### C-1 Arbitrary program execution from an attachment row — `S-9`
`Services/DeliveryNote/Attachments/AttachmentService.cs`
`Services/ProformaInvoice/Attachments/ProformaAttachmentService.cs`

`OpenInDefaultApp` called `Process.Start` with `UseShellExecute = true`
on a path taken straight from the database / manifest, with only an
existence check. For `.pdf` that opens a viewer; for `.exe`, `.bat`,
`.cmd`, `.ps1`, `.js`, `.lnk` or `.scr` **it executes the file** with the
signed-in user's privileges.

Upload validates extensions, but this method is public and is fed
`row.FilePath` read back out of storage. A row from an older build, a
restored backup, a hand-edited SQLite file, or any future caller that
forgets to validate is enough. The attachment list is the one place in
the app where a click runs a file path that is not app-controlled.

**Fix:** the image/PDF allow-list is now re-applied at the moment of
launch — the only place a caller cannot bypass — and anything else is
refused with a clear message.

### C-2 ffmpeg pipe deadlock hangs export and orphans processes — `P-1`
`Agent/Demo/Video/FfmpegMp4Composer.cs` (3 call sites)
`Agent/Demo/DemoPackageComposer.cs`

`RedirectStandardOutput`/`RedirectStandardError` were set and **neither
pipe was ever read** before `WaitForExitAsync`. A redirected pipe has a
fixed OS buffer (~4 KB). Once full, the child blocks on its next write
permanently, because nothing is draining it. ffmpeg prints a banner,
per-stream metadata, then a progress line *per frame*, so it overruns
that buffer within the first second of any real encode.

This is a true deadlock, not slowness: ffmpeg waits for us to read, we
wait for ffmpeg to exit. The await never completes, temp frames are never
cleaned up, and ffmpeg outlives the app. It only appeared to work on
clips short enough to fit in one buffer — exactly what a manual test uses.

**Fix:** both pipes are drained concurrently *before* awaiting exit;
cancellation now kills the process tree instead of leaking it; and
ffmpeg's stderr is logged on failure instead of being discarded (a failed
encode previously returned a bare `null` with no explanation anywhere).

The same bug in `TryReadDurationSeconds` — reading stdout while stderr
filled — made the 5 s `WaitForExit` time out on any file that emits
warnings. Also fixed.

---

## HIGH

### H-1 Audit-trail write failures invisible in Release — `R-1 recurrence`
`Services/Audit/AuditService.cs`

`WriteEntryAsync` reported failure only through `Debug.WriteLine`, which
is `[Conditional("DEBUG")]` — the compiler **removes it from the shipped
build**, leaving `catch (Exception ex) { }`. A locked, full or read-only
`audit.db` meant activity was silently not recorded while the app carried
on as if it had been.

For a compliance trail whose entire value is being trustworthy after the
fact, that is the one failure that must never be silent. Now routed to
`AppLogService` (which writes in every configuration) naming the section
and action that went unrecorded. Still non-throwing.

Note: 69 other `catch { Debug.WriteLine(...); }` blocks share this
"empty in Release" property. The audit path is fixed here because it is
the one with compliance consequences; the rest are catalogued in F-1
below rather than silently rewritten en masse.

### H-2 `HttpClient` per provider instance — socket exhaustion
`Services/AI/AIService/{Gemini,Groq,Cohere}AiService.cs`

Each provider allocated its own `HttpClient` (Gemini: two) in its
constructor and never disposed it — and these are **not** startup
singletons. `CreateForProvider` runs on every "Test connection" press
(three separate commands) and `CreateActive` on every provider switch.

An undisposed `HttpClient` keeps its handler, connection pool and sockets
until finalization. Repeated allocation is the textbook cause of socket
exhaustion (sockets stranded in `TIME_WAIT`) and of stale DNS after a
network change, because DNS is cached per handler.

**Fix:** one `static readonly` client per provider. Safe here because the
API key travels on each `HttpRequestMessage`, never on
`DefaultRequestHeaders`, so nothing is shared between callers.

### H-3 Audit timestamps double-shifted by timezone — `T-1`
`Services/Audit/VersionHistoryService.cs`, `Services/Audit/RecordMetadataService.cs`

Timestamps are written as `DateTime.UtcNow.ToString("o")` and were read
back with a bare `DateTime.Parse()`. That overload uses the **current
culture** and returns `Kind = Local`, so a UTC reading was reinterpreted
as local. Every consumer then calls `.ToLocalTime()` on it
(`SectionActivityViewModel`, `SystemCenterViewModel`, `VersionsDialog`,
`ActivityPanel`) — shifting it a second time.

On UTC+4, this app's home market, a save at 09:00 displays as **17:00**.
Version history and "created/updated by" stamps were wrong by one UTC
offset on every read. A silently wrong audit timestamp is worse than none.

**Fix:** shared `AuditTimestamp.ParseUtc` using `InvariantCulture` +
`RoundtripKind | AdjustToUniversal`, so the single `.ToLocalTime()` the
UI already performs is now correct. Also removes the dependency on the
operator's regional settings.

---

## MEDIUM — leaks and lifetime

### M-1 Diagnostics log panel leaked permanently
`ViewModels/Diagnostics/AppLogPanelViewModel.cs`

Subscribed to the **static** `AppLogService.LogWritten` with an anonymous
lambda, which cannot be detached. A static event strongly roots every
subscriber for the process lifetime, so each `DiagnosticsViewModel`
construction leaked another panel — and every leaked copy kept appending
to its own list on every log line written anywhere in the app. The leak
grows on its own after the view is gone.

Separately, the backing `_all` list was **unbounded**: seeded with 500
entries but never trimmed, so a long session with a chatty log retained
every entry and made each filter change progressively slower.

**Fix:** named handler, `IDisposable`, and both collections capped at 500.

### M-2 Dashboard pages never detach from `ThemeManager`
`ViewModels/Dashboard/DashboardPageBase.cs`

The base constructor subscribes to static `ThemeManager.ThemeChanged`
with no unsubscribe, so all seven dashboard pages — and their KPI models,
chart bars and feed collections — are rooted for the session, and every
dead page still runs `OnThemeChanged` on each theme toggle.

Bounded today only because `DashboardViewModel` is built once. That is
precisely the assumption `DashboardFilterService` was written to avoid
depending on (see its own header), and it stops holding the moment a page
is created per navigation.

**Fix:** `IDisposable` with a `DisposeCore` hook for sub-pages.

### M-3 `ConnectivityService` leaked an OS-level hook
`Services/AI/AIConnectivity/ConnectivityService.cs`

Attached a lambda capturing `this` to the static
`NetworkChange.NetworkAvailabilityChanged` and never detached it, rooting
the service and its timer via a CLR-owned static. `StopMonitoring()` only
stopped the timer, so a "stopped" service still woke on every network
transition and still raised `ConnectivityChanged` to its subscribers.

Also fixed here:
- **Stopped/disposed services kept reporting** — both paths are now gated.
- **`DispatcherTimer` bound to the constructing thread.** It is now bound
  explicitly to the application dispatcher; if this type were ever
  constructed off the UI thread the timer would simply never fire, with
  no exception — connectivity would freeze at its initial value.

Disposal is wired into `App.OnExit`.

---

## MEDIUM — concurrency and untrusted input

### C-4 Presence service: races and stale identity
`Services/Network/UserPresenceService.cs`

- `_username` / `_displayName` are written by the UI thread on sign-in and
  user switch, and read by two timer callbacks plus the receive loop.
  Without `volatile` the JIT may hoist the read, so a worker can keep
  broadcasting the **previous user's identity** after a switch. Now
  `volatile`.
- `_disposed` had the same visibility problem while acting as the receive
  loop's termination condition. Now `volatile`.
- `Start` and `Dispose` could interleave and strand a live socket owned by
  nothing. Both now take a lifecycle lock; `Start` after `Dispose` is a
  no-op; in-flight timer callbacks re-check the flag (including inside
  `_senderLock`) so they cannot resurrect a socket that was just closed.
- The listener was `Close()`d, not disposed, leaving the handle to the
  finalizer — a restart could still hit "address already in use". Now
  disposed, which also cleanly unblocks the pending `ReceiveAsync`.
- The catch-all in the receive loop could spin at 100% CPU on a
  persistently failing socket; it now backs off and re-checks `_disposed`.

### S-10 Unauthenticated UDP input was unbounded
`Services/Network/UserPresenceService.cs`

Every peer field arrives in a broadcast UDP datagram from anyone on the
LAN — no handshake, no signature, so the contents are attacker-controlled
by definition. Two limits were missing:

1. **Unbounded peer table.** The dictionary key is built from the sender's
   own user/machine strings, so one host spoofing a new identity per
   packet added entries without limit; they are only evicted after 45 s of
   silence. A trivial flood grew the table and the bound Users list until
   the process ran out of memory.
2. **Unbounded string lengths.** A datagram could push megabyte display
   names straight into a WPF `ItemsControl`.

**Fix:** 4 KB datagram cap, 64-char field clamp, 256-peer ceiling that
refuses *new* identities when full while still letting known peers refresh
— so a flood cannot evict real colleagues. Presence is cosmetic, so
discarding hostile input is always the right trade.

---

## Verified as already correct

Worth recording, because these are the usual suspects and the code
already handles them properly:

- **SQL injection** — every value-carrying query is parameterised
  (`AddWithValue`). The only interpolated SQL is DDL over hard-coded
  internal table/column identifiers, never user input.
- **Password storage** — PBKDF2-HMAC-SHA256, 210k iterations, per-hash
  salt, self-describing format, `FixedTimeEquals`, and a correct silent
  migration off legacy salted SHA-256.
- **CSV formula injection** — properly handled in `CsvExportEngine`, with
  a well-reasoned narrowing of the escape set to keep negative currency
  intact.
- **Update download origin** — HTTPS + exact-host allow-list, correctly
  avoiding the `EndsWith(".github.com")` trap, plus a truncation check.
- **Secrets at rest** — DPAPI `CurrentUser` with app entropy; keys are
  masked out of `config.json` on save and migrated from legacy plain text.
- **Path traversal in attachments** — `SanitizeName` correctly handles
  `..`, separators, all-dot segments and trailing dots/spaces.

---

## Not changed — recommended follow-ups

- **F-1 — 69 `catch { Debug.WriteLine(...) }` blocks** compile to empty
  catches in Release, so those failures are undiagnosable in the field.
  The audit-trail one is fixed (H-1); the rest want a mechanical sweep to
  `AppLogService`, which is a large diff best done as its own reviewed
  change rather than folded into a security fix.
- **`SectionActivityViewModel` is `IDisposable` but never disposed** by
  its four owners (`DeliveryNote`, `JobOrder`, `TaxInvoice`,
  `Diagnostics`). Currently harmless — those owners are themselves
  session-long singletons — but the cleanup exists and is not called, so
  it will become a real leak the moment they become per-navigation. Needs
  an owner-lifetime decision, not a one-line edit.
- **`DefaultAdminPassword = "admin"`** with no forced rotation. There is a
  deliberate, documented reason (no sign-in recovery path offline) and a
  detection helper, but shipping a known default remains a real risk worth
  revisiting with a first-run change-password gate.
- **`.Result` on already-completed `Task.WhenAll` results** in two
  dashboard/list VMs is safe as written, but `await`-ing each task would
  remove the deadlock footgun if anyone later reorders those lines.
