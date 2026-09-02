# Deep Audit & Phase Plan — Stability, Responsiveness, Quality

**Date:** 2026-09-02 → ongoing
**Method:** reading actual source files end to end, not grep summaries.
**Standing goal (yours):** every module ≥ 9/10, no freezes, no unrelated
errors, consistently smooth — matching the Specifications module, which
you identified as the one that already feels right.

---

## 0. The recording freeze — I was wrong twice, here is the proof

I have claimed a fix twice and you were right both times that it was not
fixed. The reason is a factual error I carried into both commits.

My slice-B commit message said:

> *"it runs entirely on the CALLING thread against the OS, so it can be
> driven from a background thread and never touches the WPF dispatcher."*

**That statement is false.** Microsoft's documentation for `PrintWindow`:

> *"This is a **blocking or synchronous** function and might not return
> immediately… **The application that owns the window referenced by hWnd
> processes the PrintWindow call** and renders the image… The application
> receives a **WM_PRINT** message… Calling this function from a thread
> that manages interaction with the user interface could make the
> application appear to be unresponsive."*

### What that means for us, concretely

`PrintWindow(hwnd, …)` is not a pixel read. It **posts `WM_PRINT` to the
thread that owns the HWND and blocks until that thread renders it.**

We pass our **own** `MainWindow`. Its owner is our **own UI thread**.

So the "off-thread" capture loop was doing this 30 times a second:

```
bg thread → PrintWindow → SendMessage(WM_PRINT) → UI THREAD renders
                                                   whole window
bg thread ←──────────── blocks until done ─────────┘
```

The UI thread was still doing every full-window render — I had merely
moved the *request* off-thread. That is why:

- module switching crawls
- dialogs stop responding
- **STOP does not register** (its click has to queue behind the
  `WM_PRINT` backlog)
- force-close is required

It is *architecturally the same defect* as `RenderTargetBitmap`, dressed
in different API calls. Slice B replaced the symptom, not the cause.

### Why my verification never caught it

Every check I ran was structural — brace balance, symbol resolution,
namespace reachability, BOM. **None of them can detect "this Win32 call
has a hidden cross-thread dependency."** That required reading the API
contract, which I did not do until now.

**Correction to my process:** for any Win32/interop call I add, I will
read the official contract for threading and blocking behaviour *before*
committing, not after a bug report.

---

## 1. The real fix for capture

There is exactly one Windows API that captures a window **without**
involving the owning thread: **Windows.Graphics.Capture** (WGC), backed
by DWM and Direct3D. It is what OBS, Teams and Game Bar use.

| Approach | Owner thread involved? | Verdict |
|---|---|---|
| `RenderTargetBitmap` | Yes — runs *on* it | Freezes (original) |
| `PrintWindow` | Yes — `WM_PRINT` blocks it | **Freezes (current)** |
| `BitBlt` from window DC | Partially; also misses DWM content | Not viable |
| **Windows.Graphics.Capture** | **No** — DWM/GPU side | **Correct** |

WGC needs `CsWinRT` interop and a D3D11 device. It is a real
dependency, and I cannot compile or test it here. That is a genuine
risk I will state plainly rather than hide.

**Therefore Phase 1 does not attempt WGC blind.** It makes the current
recorder *honest and harmless* first — because a feature that freezes
the whole application is worse than a feature that is temporarily
unavailable.

---

## 2. Phase plan

Ordered by **risk to the user**, not by how interesting the work is.
Each phase: read → fix → cross-check → commit → push → re-read.

### Phase 1 — Stop the freeze (CRITICAL)
Recording currently locks the app. Until WGC lands, capture must not be
able to hang the UI thread.

- Move `PrintWindow` behind a **watchdog**: if a single capture exceeds
  a frame budget, back off the capture rate automatically.
- **Adaptive frame rate**: on sustained overrun, halve fps rather than
  queueing more blocking calls into the UI thread.
- Hard ceiling on capture cost: skip a slot entirely rather than let
  `WM_PRINT` pile up.
- Make STOP genuinely instant — cancel *before* any further capture.

### Phase 2 — Windows.Graphics.Capture backend
The permanent fix. Behind an interface, with Phase 1's throttled
`PrintWindow` as automatic fallback so no machine loses recording.

### Phase 3 — Dispatcher-hygiene audit (whole app)
Find every `Dispatcher.Invoke` / `.Wait()` / `.Result` / `Thread.Sleep`
on the UI thread. These are the classic sources of "randomly
unresponsive". Read each, fix the blocking ones.

### Phase 4 — Startup & module-switch performance
You said only Specifications feels smooth. Find out *why* — then apply
that pattern to the rest. Likely causes elsewhere: synchronous DB work
in constructors, unvirtualised lists, per-item effects.

### Phase 5 — List virtualisation & rendering cost
Large grids without virtualisation are a top cause of slow module
switching in WPF.

### Phase 6 — Async correctness sweep
`async void`, unawaited tasks, fire-and-forget without error handling.

### Phase 7 — Resource & handle leaks
`IDisposable` not disposed, event handlers never detached, timers never
stopped.

### Phase 8 — Consistent motion/theme polish
Apply the Specifications module's styling and animation conventions
app-wide, at a constant frame budget.

---

## 3. Standing rules I am adopting

1. **Read the actual file** before changing it — no more grep-only edits.
2. **Read the API contract** for any interop, before committing.
3. **Never leave a broken path reachable** as a "fallback". Slice B's
   silent fallbacks were how the frozen path stayed alive.
4. **Fail loudly, never degrade into a hang.** An error message is
   always better than a force-close.
5. **State what is unverified.** No compiler here means some things can
   only be confirmed on your machine; I will say which.

---

## 4. Progress log

| Phase | Status | Commit | Outcome |
|---|---|---|---|
| Audit + plan | ✅ | `7b5f6ec` | Root cause proven from the Win32 contract |
| 1 — Stop the freeze | ✅ | `7b5f6ec` | Adaptive fps + unconditional yield + sync STOP |
| 3a — Sync-over-async | ✅ | `58ef86f` | Attachment open no longer blocks on file I/O |
| 4 — Lazy modules | ✅ | `604d8a5` | 18 of 28 VMs now build on first use |
| 2 — WGC backend | next | — | Permanent capture fix |
| 3b — Dispatcher.Invoke review | pending | — | 33 blocking call sites to read |
| 5 — Virtualisation | pending | — | |
| 6 — Async correctness | pending | — | |
| 7 — Leaks | pending | — | |
| 8 — Motion/theme polish | pending | — | |

### Phase 4 finding worth remembering

I assumed the smooth/slow difference would be **virtualisation**. It was
not — Specifications has **zero** virtualisation while the modules that
feel slow have plenty (DeliveryNote 26, TaxInvoice 24). Measuring beat
assuming.

The real cause was **28 view models constructed eagerly at startup**,
most of which start database loads in their constructors
(InquiryListViewModel alone had 24). Nothing could paint until that
storm cleared.

### Phase 4 near-miss

My first attempt lazified 25 VMs. Caught before commit: the constructor
wires cross-module callbacks (`InquiryVM.OpenProformaForRowRequested`
etc.), which touches those properties and would have instantiated them
anyway. Reverted, re-derived the safe set mechanically by counting `X.`
member access per property, landed 18.
