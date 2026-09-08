# Senior Analysis — ProGlass Automation ADV

**Date:** 2026-09-02
**Branch:** `arena/01a05d56-proglassautomationadv`
**HEAD:** `57dcfbd`
**Scope:** build-integrity cross-check of the Phase 3.8 god-file split, publish-readiness audit, and applied senior fixes.

---

## 1. Did the 3.8 refactor break the clean build?

**Verdict: no structural breakage — verified statically with full parity. Four test-side breakages were found and fixed.**

### 1.1 Structural invariants (all PASS, whole tree)
| Check | Scope | Result |
|---|---|---|
| Brace balance (tokenizer-aware: strings/comments ignored) | 1098 app `.cs` files | **0 failures** |
| `#region`/`#if`–`#endregion`/`#endif` balance | every `.cs` | **0 failures** |
| Member census vs `git show HEAD:` (methods, props, fields, consts, events, indexers, operators, explicit impls) | 24 split families | **0 lost / 0 dup** |
| Partial-family class declarations identical across parts | 24 families | **consistent** |
| Part files contain only their partial class (no stray top-level types) | 49 part files | **clean** |
| Namespace in part == namespace in main | 49 part files | **clean** |
| UTF-8 / no mojibake / no BOM damage | 49 part files | **clean** |
| XAML untouched since 3.7 (`git diff 751f292..HEAD -- '*.xaml'`) | — | **zero files** |
| Compile wiring: SDK default globbing includes `*.PartN.cs`; no csproj edit needed; parts not under excluded dirs | csproj ×4 | **PASS** |
| Class-name collision census (namespace + partial-aware) | whole tree | **no real collisions** |

### 1.2 Breakages the split DID cause — and fixed
1. **Source-assertion tests read only the skeleton main file.** The 3.8 split moved member bodies into `.PartN.cs`, so tests asserting on source text of split files started asserting on ~50-line skeletons. **19 call sites across 10 test files** now read the whole family via `Tests/TestSupport/SourceFamily.cs` (or per-file `ReadFamily` helpers). Files fixed: `LiquidGlassThemeSourceTests`, `AboutModuleAuditTests`, `OfflineLicensingHardeningSourceTests`, `OtherChargesAdvancedSourceTests`, `ProformaStep4PersistenceSourceTests`, `DeliveryNoteModuleAuditTests`, `DeliveryNoteWorkflowTests`, `DocumentListStatusTests`, `JobOrderModuleAuditTests`, `InquiryPiIntegrationTests` (commits `93190df`, `57dcfbd`).
2. **SilentCatchAuditTests (S-5 gate) broke.** Its allowlist is keyed on `"X.cs"` substrings; empty catches that moved into `LicenseService.Part1/2.cs` (5) and `InquiryListViewModel.Part1/2.cs` (4, incl. the Dispose trio) became disallowed → the Roslyn regression gate would fail the build. Fixed by normalizing `.PartN.cs → .cs` before allowlist matching (family-scoped allowlisting, `642b9ac`). Also found and allowlisted a pre-existing gap: `LicenseActivatedDialog.cs:45` (defensive remaining-days arithmetic — a crafted payload can carry `DateTime.MaxValue`, and `MaxValue - UtcNow` overflows).

### 1.3 Honest boundary
No .NET SDK in this sandbox — every check above is static. The remaining runtime-only verification (actual `dotnet build` + `dotnet test` on Windows) is listed in §4.

---

## 2. Rating

**Overall: 8.6 / 10 — production-close for a single-developer offline-first desktop app.**

| Dimension | Score | Evidence |
|---|:---:|---|
| Build integrity (static) | 9.5/10 | 1098 files balanced; member parity 24/24 families; zero XAML drift |
| Architecture | 9/10 | Service/DI core, `IDialogService`, `IDatabaseServices`, repository layer, partial-family organization after 3.8; VMs own flows |
| Licensing | 8.5/10 | RSA-2048-PSS, machine fingerprint, trial ledger, **52 `ReadOnlyGate.Demand` write guards** (this session), offline activation; placeholder public key is the one red |
| Security | 8.5/10 | S-1..S-5 closed; update signature verification; DPAPI vault; silent-catch regression gate (now family-aware) |
| Test discipline | 8/10 | 92 commits, ~21k LOC of tests, source-level regression gates (hardening tests); **suite unexecuted in sandbox** — biggest single risk |
| Data integrity | 8.5/10 | 11 SQLite contexts, backup/restore, schema migrations, retry wrappers, audit trail incl. every update-state transition (fixed this session) |
| Localization | 9/10 | 15 languages, key sets identical, all 2064 dotted refs resolve; 2 regressions found+fixed this session |
| Publish pipeline | 8/10 | Velopack manifest, publish profile, release workflow, docs; code-signing secrets + delta-upgrade VM run pending |
| Maintainability | 9/10 | No app `.cs` > 903 lines (was 16 files > 1000); 64 XAML handler attrs / 34 files, all documented by-design keeps |

**Phase status:** 1, 2.1–2.6, 3.1–3.5, 3.7, 3.8 ✅ · 3.6 ⏸ (37 `x:Name` contract, build-gated) · 5.1 ⏸ (AppStartupPipeline, build-gated) · 5.2 ⏳ (OS-dialog facade — deliberate defer, see §5) · 6 ⏸ (CommunityToolkit, build-gated).

---

## 3. Applied this session (7 commits)

| Commit | Fix |
|---|---|
| `de1e376`, `7bcd88c` | 3.8 wave 1 + wave 2 — 23 god files split (52 parts), all census-verified |
| `476cc45` | Localization regressions: LoginPanel sign-in button, InvoiceApproval LPO label → `LocalizationService.T(...)` |
| `93190df`, `57dcfbd` | Test family reads (19 call sites, 10 files) |
| `662206e` | **Publish-blocker closed:** read-only write-gate sweep — 34 new `ReadOnlyGate.Demand` sites across 8 repositories (Inquiry, JobOrder, Statement, SheetStore, MasterData ×9 ops, Specifications, AI memory, SampleData) + 8 new hardening source tests |
| `f2fe96a` | UpdateService: 3 missing terminal-transition audit entries |
| `642b9ac` | Silent-catch allowlist family normalization + LicenseActivatedDialog entry |
| `cc7aaad`, `f3f8083`, `2bd1899` | Docs: audit, balance sheet, RELEASE_GATE line-ref corrections |

---

## 4. Publish checklist — what is left, in order

1. **Run the real build + tests** on Windows: `dotnet build ProGlassAutomation.csproj -c Release` then `dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release` (unblocks the sandbox-static boundary; expects green — all gates are static-verified).
2. **Activate CI** — grant the GitHub App `workflows` scope; `cp docs/ci/build-test.yml .github/workflows/build-test.yml`.
3. **Secrets** — repo admin adds `CODESIGN_PFX_BASE64` + `CODESIGN_PFX_PASSWORD` before the first customer tag.
4. **Public key** — paste the real RSA-2048 public key into `LicensePublicKey.cs` (placeholder today).
5. **E2E walk-through** — run `docs/E2E_WALKTHROUGH.md` (30 steps) on a clean Windows VM.
6. **Delta-upgrade smoke test** — 2.0.0 → 2.0.1 on a clean VM.
7. **AI consent smoke test** — chat refuses before Settings opt-in (code verified; run once).
8. **Known reds (out of scope, documented):** first-run user-creation/backup/preferences flow, online/offline activation UX, remote crash-send, CodeQL, 1k–100k load fixtures.

---

## 5. Senior decisions recorded

- **5.1 AppStartupPipeline / 3.6 MainWindow / 6 CommunityToolkit: deferred until a build exists.** Rewiring init order, the 37 `x:Name` field contract, or a mass `[ObservableProperty]` migration blind would be the kind of change that *looks* fine and breaks at runtime — the opposite of publish discipline. They are the correct next phase after step 1 of §4.
- **5.2 OS-dialog facade: deliberately deferred.** 15 call sites with bespoke filter/title/initial-dir configs; zero runtime tests; mechanical churn with no user-visible gain before the first release. Revisit after launch.
- **`SheetHistoryRepository.LogAsync`, `InquiryLinkSync`, `SqliteResilience` are intentionally ungated** (journal semantics / transitively gated / infra wrapper) — documented in the hardening test.
- **`App.xaml.cs:283` `.Wait(1500)`** on exit is a bounded, commented, try/caught audit flush — deliberate.
- **`AttachmentService` sync-over-async (`GetAwaiter().GetResult()`)** is deadlock-free (only await uses `ConfigureAwait(false)`; no UI-context capture) — noted, not churned.
