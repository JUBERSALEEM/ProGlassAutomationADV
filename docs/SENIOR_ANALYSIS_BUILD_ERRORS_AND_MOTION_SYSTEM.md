# Senior Analysis — Build Errors (MC3000 / CS0200 / CS0191 / CS0103) and the Global Motion System

**Date:** 2026-09-02
**Build:** 2.0.0 (2026.09.02) · .NET 8.0.30 · Windows 10.0.26200
**Branch:** `arena/01a06070-proglassautomationadv`

---

## 0. Executive summary

Four compiler errors, of which **three were caused by my own previous
commits**. One was a pure cascade. All are fixed, and in each case the
*detection gap* that let them through has been closed too — because the
real failure here was not the bugs, it was shipping refactors validated
only by static text analysis in an environment with no compiler.

| Error | Count | Cause | Mine? |
|---|---|---|---|
| MC3000 | 1 | XML comment placed *inside* a start tag | **Yes** (`8bef2a4`) |
| CS0103 | 1 | Cascade — no `.g.cs` emitted after MC3000 | No (collateral) |
| CS0200 | **155** | Get-only auto-props assigned from a method | **Yes** (`a8bd6a0`) |
| CS0191 | 1 | `readonly` field assigned from a method | **Yes** (`a8bd6a0`) |

The reported build output listed only 11 CS0200 errors. That was
truncated by the IDE. The true scope was **155 properties across 26
files**, found by scanning rather than by trusting the list.

---

## 1. MC3000 — malformed XAML

### What happened

In `8bef2a4` I added an explanatory comment for the agent-panel
visibility fallback. I placed it **between attributes**, inside the
`<Border ...>` start tag:

```xml
<Border ...
        Effect="{DynamicResource ShadowElevated}"
        <!-- FallbackValue/TargetNullValue are load-bearing... -->
        Visibility="{Binding IsAgentChatOpen, ...}">
```

XML comments are **nodes**, not trivia. They may appear between
elements, never between attributes of a tag. The XAML compiler reported:

```
MC3000  'Name cannot begin with the '<' character, hexadecimal value 0x3C.
        Line 1120, position 21.' XML is not valid.
```

### Why CS0103 followed

`WorkflowWarningDialog.xaml.cs:32` reported
`The name 'InitializeComponent' does not exist in the current context`.
That file was never touched by any of these commits (`git log` confirms
it last changed in `a509840`, the branch point).

`InitializeComponent()` is generated into `obj/.../*.g.cs` by the XAML
compilation pass. **When that pass fails, it emits nothing for any
file** — so every code-behind in the project loses its generated
partial. CS0103 was collateral, and it disappeared the moment MC3000
was fixed. Chasing it directly would have been wasted effort.

**Rule:** when MC3000 and CS0103 appear together, fix the XAML first and
rebuild before reading any CS0103.

### The detection gap — and the fix

`tools/xaml_validate.py` passed this file cleanly. Reviewing it revealed
why: **it never actually parsed the XML.** All eight rules (R1/R2/R3/
M1/M2/E1/D1/C1) were regex-based resource-resolution checks. A file
could be structurally invalid and still score clean.

Added **rule X0**: run `xml.etree.ElementTree.parse` on every `.xaml`.
It had to be inserted *before* the `if not violations: sys.exit(0)`
early-return — my first two attempts placed it after, and it silently
never ran. That is exactly the kind of mistake that motivates fault
injection rather than assuming a check works:

```
Fault injection test
  bug reintroduced → EXIT=1, "X0 (FATAL) MainWindow.xaml:1120
                              malformed XML: not well-formed
                              (invalid token): line 1120, column 20"
  bug removed      → EXIT=0
```

The validator now reports the **same line number MSBuild does**.

This rule immediately paid for itself: while writing the motion styles
in this same session, I used a `-----` divider inside an XML comment.
A `--` sequence is illegal inside XML comments, and X0 caught it
instantly.

---

## 2. CS0200 — the real flaw in the `InitializeCore` refactor

### What happened

`a8bd6a0` fixed the ERR-633EEB two-phase-constructor defect by
**extracting** each parameterless constructor's body into a private
`InitializeCore()` and calling it from the parameterized constructor.
That solved the null-command crashes correctly.

But C# permits assigning a **get-only auto-property** (`{ get; }`) only
from a **constructor of the declaring type**. Moving that code into a
*method* made all 155 such assignments illegal:

```
CS0200: Property or indexer 'DataGridViewModel.SpecGroups'
        cannot be assigned to -- it is read only
```

### Why no static check caught it

This is the important part. Every check I had passed:

- brace/paren balance — unchanged
- body byte-identical — verified char by char
- declaration order — explicitly tested (this was the *v1* defect)
- BOM/encoding — preserved
- exactly one call site and one definition — verified
- null-dereference scan — clean

None of them encode the C# language rule *"get-only auto-properties are
assignable only from a constructor."* **Structural correctness is not
semantic correctness.** Only a compiler knows this, and there is no
compiler in this environment (no .NET SDK, no network — verified).

### The fix, and why this one

Four options were considered:

1. **Revert to constructor-based init** — reintroduces ERR-633EEB, the
   exact crash the user hit at Settings → Appearance. Rejected.
2. **Make `InitializeCore` a constructor** — impossible; a type cannot
   have two parameterless constructors.
3. **Convert properties to backing fields** — a large, invasive diff
   across 155 members, high regression risk.
4. **`{ get; }` → `{ get; private set; }`** — chosen.

Option 4 is minimal and preserves the public contract exactly: the
property remains **publicly read-only**, so no binding, no consumer and
no interface changes. Only the declaring class gains the ability to
assign — which is precisely what `InitializeCore` needs.

**Verification.** Line-by-line diff across all 26 files: **zero
unexpected edits, zero line-count change.** The only textual difference
in the entire commit is `{ get; }` → `{ get; private set; }`.

---

## 3. CS0191 — the same root cause, different member kind

Having found CS0200 by scanning rather than by reading the error list, I
asked what *else* only a compiler enforces about assignment context.
`readonly` **fields** have the identical restriction — assignable only
in a declaration or a constructor.

Scanning the 29 `InitializeCore` files found one:

```
ViewModels/Reports/SalesReportViewModel.cs:154
    _reportingService = _reporting ?? new ReportingDataService();
```

This error was **not in the reported build output** — the list was
truncated before reaching it. Fixed by dropping `readonly`, with a
comment recording that the field is still assigned exactly once on the
single construction path. A rescan confirms zero remaining CS0191 and
zero `static readonly` cases.

---

## 4. Global motion system — "bouncy" hover everywhere

### The request

The Settings nav rail feels alive; the rest of the app feels static.
Make that motion available app-wide, on cards and hover states.

### What the rail was actually doing

Worth stating plainly, because it shapes the design: **the rail had no
animation at all.** `SettingsNavButtonStyle` used only static
`IsMouseOver` / `IsPressed` / `Tag=Active` triggers swapping brushes.
The perceived "bounce" came from the glass material ramp plus the
accent bar — not motion.

So this is not "copy the rail's animation." There was none to copy. It
is designing the motion the rail *implies*, and making it shared.

### Why an attached behavior, not styles

The obvious approach — a `ScaleTransform` plus two `Storyboard`s plus
four triggers per `ControlTemplate` — would mean dozens of
near-identical XAML blocks that drift apart over time. It also requires
rewriting every template, which is exactly the kind of broad change that
has already burned this project twice.

`Behaviors/BounceHoverBehavior.cs` instead exposes one attached
property. Opt in with a single line, on **any** `UIElement` — `Border`,
`Button`, `Card`, `ListBoxItem` — with **no template rewrite**:

```xml
<Border behaviors:BounceHover.IsEnabled="True" .../>
```

It animates `RenderTransform`, which every `UIElement` has.

### Engineering details that matter

- **BackEase overshoot** (`Amplitude = 0.45`, `EaseOut`) on enter/leave
  is what reads as "bouncy"; press uses a fast `QuadraticEase` dip so
  clicks feel physical rather than springy.
- **`RenderTransformOrigin` forced to (0.5, 0.5)** — otherwise the
  element scales from its top-left corner and visibly drifts.
- **Never hijacks an existing transform.** If an element already has a
  non-identity, non-`ScaleTransform` `RenderTransform`, the behavior
  declines to animate rather than silently breaking that layout.
  Injected transforms are tracked via a private attached property so we
  only ever animate our own.
- **Respects the performance tier.** `PerformanceProfileService.TargetFps`
  (verified public static) gates the effect; below 60 fps no handlers
  are attached at all. A probe failure defaults to *enabled* — a broken
  probe must never disable the UI.
- **No leaks.** Handlers are always detached before attaching, because
  re-entrant style application can raise the callback more than once for
  the same element. This matches the Phase-4 named-handler convention
  used elsewhere in this codebase.
- **Reset on hide.** Collapsing an element mid-animation would otherwise
  leave it frozen at hover scale.

### Where it is applied

Opt-in, deliberately — not blanket-applied:

| Surface | Hover scale | Why |
|---|---|---|
| `SidebarNavItem` (shell) | 1.03 | Highest-traffic surface in the app |
| `SidebarSubItem` | 1.02 | Dense list, smaller travel |
| `SettingsNavButtonStyle` | 1.035 | The rail that inspired this |
| `InteractiveCardStyle` | 1.03 | General clickable card |
| `InteractiveElevatedCardStyle` | 1.03 | Already shadowed at rest |
| `InteractiveStatCardStyle` | 1.02 | Dashboard tiles in dense grids |
| `InteractiveListRowCardStyle` | 1.008 | Repeats many times down a page |
| `SoftCardInteractiveStyle` | 1.02 | Clickable variant of `SoftCardStyle` |

**Why opt-in and not global.** `SoftCardStyle` alone backs **38** static
section containers. A section header that springs under the pointer
reads as a bug, not polish. Motion must signal *affordance* — "this
responds to clicks" — so applying it to decorative surfaces would
actively degrade the UI. Hence separate `Interactive*` variants rather
than changing the base styles.

Scale values are tuned inversely to repetition: the more often an
element appears, the smaller its travel.

---

## 5. Honest status

`xaml_validate` is CLEAN (587 keys), the ERR-633EEB guard reports 0
violations, and every edit was verified line-by-line with encodings and
brace balance checked.

**But there is still no compiler in this environment.** The three
compiler errors in this report are direct evidence of that limitation.
I have closed the specific gaps that produced them — a real XML parse
(X0, fault-injection tested), plus explicit scans for CS0200 and CS0191
assignment-context violations. Those scans now cover the classes of
error that actually occurred.

They cannot cover the classes that have not occurred yet. **The build
is the source of truth.** Please rebuild and send any remaining errors.
