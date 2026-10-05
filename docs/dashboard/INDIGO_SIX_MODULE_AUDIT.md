# Indigo Studio — six-module dashboard audit and implementation

Date: 2026-09-22. Source baseline: `1c09c117a1c3ab9376c26b401038f50c821742eb` (published Indigo Overview).

This is a production WPF redesign, not an image/HTML preview. Analysis is based on the actual XAML, view models, row models, metric services, commands and chart controls. Native rendering has not been inspected in this environment; no pixel-perfect or runtime-performance claim is made.

## 1. Per-page inventory and decisions

| Dashboard | Existing data contract retained | Original binding occurrences / distinct paths | Indigo layout |
|---|---|---:|---|
| Sheet Store | 4 KPIs: sheet types, selling-price inventory value, available SQM, low/out-of-stock; category bars and category area mix | 12 / 8 | Four full-width responsive KPI cards; category bars beside stock-area mix |
| Proformas | 4 KPIs: total, approved, sent/pending, draft; status counts, status mix, top-five customer VAT-inclusive value | 16 / 9 | Four KPI cards; categorical status bar/mix cards; full-width customer ranking |
| Job Orders | 4 KPIs: total, production, completed, overdue; status and priority counts, status mix, original recent feed | 19 / 11 | Four KPI cards; responsive analytics pairs and bounded, wheel-scrollable document feed |
| Deliveries | 4 KPIs: total notes, delivered today, pending pieces, rework/returns; status/type counts, status mix, original recent feed | 19 / 11 | Same bento grammar, with explicit note/piece distinction rather than combining units |
| Tax Invoices | **5** KPIs: invoiced MTD, invoice VAT MTD, overdue amount, pending count, paid/part-paid count; overdue count/amount, aging mix and bracket rows, recent invoices, top customers, six-month invoiced-value trend | 63 / 34 | All five KPIs moved out of the cramped three-column strip into a full-width responsive band; aging/overdue pair, area trend/recent pair, customer/bracket pair |
| Statements | 4 KPIs: generated statements, invoiced, outstanding, overdue; saved-balance values, overdue summary, every recent-row field, attachments and PDF/share actions | 35 / 25 | Full-width KPI band; independent balance counters/overdue card; wide, responsive document rows with functional actions |

Total: **164 original binding attribute occurrences**, **25 KPI tiles** across the six pages. This does not count the Overview's separate 17 fields, which remain unchanged.

The checked-in contract file contains only binding expressions, not customer data or sample totals. It preserves multiplicity, formatting, command parameters, relative-ancestor lookup and enabled states—not just the first name in a binding.

## 2. Senior review findings

### Layout and presentation issues addressed

1. **Legacy visual system:** all six roots used `ErpPageRootStyle` and the Pg/Meridian grammar. Added explicitly scoped Indigo resources based on the approved Overview palette and removed those dependencies from these six bodies. Application theme dictionaries and non-dashboard module screens are not restyled.
2. **Tax KPI congestion:** five metrics shared a narrow subsection alongside aging and two summary cards. Give the KPI collection a full-width band and allow rows to grow. No `Take`, filtering or fixed KPI-band height is introduced.
3. **Rigid peer grids:** fixed two/three-column sections could squeeze charts, cards and document actions. `IndigoDashboardPanel` measures actual children, uses DIP width thresholds, stacks when the narrower column cannot fit, and preserves all children through resizing.
4. **Document row collisions:** Statement fields and right-hand actions shared a constrained three-column row. Reflow the data/actions and wrap the row's field groups while retaining every command and `HasPdf` enabled state. Long trimmed fields gain full-text tooltips.
5. **Missing keyboard equivalent:** tax recent-invoice rows supported a mouse binding only. Preserve it and add focus, a visible focus state, and Enter/Space using the same command and row parameter.
6. **Feed scroll inconsistency:** Jobs and Deliveries had unbounded item hosts, while other detail lists already scrolled. Add bounded inner scrolling and smart wheel bubbling without reducing the source collections. Existing Tax/Statement scrolling remains.
7. **Chart theme leakage:** the donut legend used `Application.Current.TryFindResource`, bypassing page colours. Change only its legend lookup to instance `TryFindResource`; page-local chart palettes now work, while the existing application fallback still serves other callers. No public chart API or data calculation changes.
8. **False fresh-state appearance:** a permanently green LIVE chip and visible old content could coexist with load failure. Use Snapshot wording, preserve the refresh timestamp, hide badges during loading/error, and hide real content under an error/loading parent gate. The error band remains visible.

### Financial/data semantics

- **Sheet Store:** stock value is at selling price, not cost. Category bars/mix represent available area.
- **Proformas:** VAT-inclusive quote values and approvals are not collected revenue. No conversion forecast, growth percentage or invented action is added.
- **Job Orders/Deliveries:** status, priority and type are categorical views, so they remain **bar charts**, not area/time-series curves. Delivery-note counts and piece balances remain different units.
- **Tax:** the service requests six trend buckets; the old fixed 12-month caption was inaccurate. Retain `RevenueChartTitle`, remove the fixed claim and render the time series as an Indigo area chart. The stored VAT sum is invoice VAT by issue month, not evidence of tax cash received. Paid MTD counts paid/part-paid invoices **issued** that month. Presentation labels/footnotes clarify this without recalculation.
- **Statements:** the existing `BalanceMixSlices` uses non-overdue outstanding as a raw slice, but displays total outstanding including overdue. That makes the partition and its label disagree. Retain the same collection and formatted values as **independent balance counters**, not a percentage donut. Explain that overdue is included in outstanding and collected is inferred from saved snapshot totals. Statement aggregates are not a deduplicated cash ledger; no claim to the contrary is introduced.

### Existing state defects corrected

- **Statements → empty refresh:** the zero-statements branch clears scalar totals and rebuilds the balance mix, but did not call `RebuildKpiCards()`. It can leave the previous four KPI values in the collection after the last statement is removed. The implementation now rebuilds that same collection after the existing zero assignments.
- **Startup service unavailable:** Sheet Store, Job Orders and Statements logged and returned without setting an error when required services were absent. The implementation now uses their existing `HasError` and `ErrorMessage` properties so the new failure gate does not present missing data as a successful empty result. Delivery Notes already has this error handling; Proforma/Tax exceptions already reach the base error handler.

No query, metric formula, date scope, feed cap, identifier, command implementation, repository, schema, setting, serialization or optimization-engine algorithm needs to change for these corrections.

## 3. Implementation boundaries

- `Views/Dashboard/IndigoDashboardResources.xaml`: shared module styles, responsive KPI template compatible with **both** KPI model types, skeletons, typography, alerts and page-scoped chart brush aliases. Merges the existing Overview palette instead of maintaining a second set of hex values.
- `Views/Dashboard/IndigoDashboardPanel.cs`: layout only. No VM references, DB calls, data replacement, timers or global theme subscriptions.
- Six existing module XAML files: layout and view styling; original identity, localization keys (except misleading captions explicitly corrected), bindings and actual row actions preserved.
- `Controls/DonutChart/DonutChartControl.xaml.cs`: local resource lookup for two legend text brushes only; data and drawing logic unchanged.
- Three view models: only the documented missing-service error states and Statement empty KPI refresh.
- No application-level theme replacement, new NuGet dependency, mockup generator, fictional business data, optimizer modification or change to Overview's approved layout.

## 4. Verification and release gate

Automated source checks are separate from native WPF evidence:

- `python3 scripts/verify-indigo-modules.py`: binding occurrence/command contracts, all 25 KPI construction sites and collection hosts, responsive/scoped layout, loading/error gates, full feed scrolling, chart semantics, XAML property assignment structure and chart colour lookup.
- `python3 scripts/verify-indigo-overview.py`: the existing Overview regression checks.
- `python3 scripts/verify-dashboard.py`: XAML well-formedness, resource keys and VM/nested-model binding heads.
- `python3 scripts/xaml-xref-audit.py`: forward resource references/duplicate-key audit.
- `python3 scripts/audit-xaml.py --quiet`: existing repository-wide hygiene debt must be compared, not silently called a pass.
- Native `IndigoDashboardLayoutTests`: STA tests for five-item layout at wide/narrow widths and back, weighted stacking, collapsed/empty children, parameter validation and BAML resource-dictionary loading. **Not executed here without a Windows/.NET toolchain.**

Before accepting a release on Windows:

```powershell
dotnet build ProGlassAutomation.csproj -c Release
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release --filter "FullyQualifiedName~Dashboard"
```

Also run the full regression suite before any release claim. Inspect all seven dashboards at 100/125/150/200% scaling, narrow and wide content widths, Light/Dark/global theme switches, empty/populated data, startup service failure, refresh failure, and long names/large amounts. Check chart hover/keyboard values, Tax invoice mouse/Enter/Space navigation, Statement PDF/share permissions, and nested-to-outer wheel scrolling. Delete the last saved statement and refresh to confirm all four KPI values reset. No screenshot/rendering or performance certification can be inferred from source checks.


### Results actually executed

| Verification | Result |
|---|---|
| Six-module source-contract tests | **PASS — 18 tests**, including all 164 baseline binding occurrences, their template scopes, original action endpoints and 25 KPI construction sites |
| Existing Overview source regression tests | **PASS — 12 tests** |
| Dashboard audit | **PASS — 339/339 XAML files well-formed**, zero unresolved static/dynamic dashboard resources, zero unresolved binding heads and zero style/target-type mismatches |
| Resource cross-reference audit | **PASS — 0 forward references, 0 reported duplicate keys** |
| Changed C# delimiter checks | PASS for all eight checked files; this is **not compilation** |
| `git diff --check` | PASS |
| Services, models, project file, App.xaml, Overview and dashboard shell compared with baseline | Unchanged |
| Repository-wide XAML hygiene | **FAIL — same 234 pre-existing violations**, no increase in failing violations; more shared legacy theme resources are now unused warnings |
| Native Windows build / xUnit / visual and DPI verification | **NOT EXECUTED — TOOLCHAIN UNAVAILABLE** |

Publication authorized explicitly by the user on 2026-09-22, with native validation still unavailable. The dashboard implementation, tests and this audit are being committed and pushed on the session branch; no mockups or image assets are included. No build, release, full-regression or performance certification is claimed.


## Continuation: context-aware binding verification

The continuation re-read the **committed pre-redesign source** at `1c09c11`, not the rewritten views, to extend the contract with each binding's page/data-template scope, each collection's row-template selection, each page's `x:Class`, and each action's label, tooltip, command, parameter and enabled state.

The stricter checks confirm:

- All 164 original binding occurrences remain in the correct page or row-template context, with the documented converter and Statement balance presentation mappings.
- Collection hosts still select the appropriate KPI/feed/invoice/customer/aging/statement templates.
- Commands are checked as complete endpoints: the same button or mouse action retains its own command, label, parameter, ancestor lookup and enabled state. Merely finding those properties elsewhere does not satisfy the check.
- No replacement `DataContext` was introduced. Page identities remain unchanged.
- No fixed height, maximum height or clipping flag was added to the KPI hosts or their ancestors.
- A negative-control test deliberately swaps two binding contexts without changing global binding counts; the context-aware checker detects it.

A layout edge case was also corrected: during unbounded-width measurement, weighted panels now reserve enough width for **both** columns' minimum sizes. The previous equal-column fallback could squeeze a 4:1 panel's smaller column despite unlimited available width. Native test cases cover both 4:1 and 1:4 layouts. These native cases are added but remain **unexecuted**; source inspection and delimiter checks are not runtime layout proof.

Current portable verification totals **30 passing tests**: 18 six-module checks plus 12 existing Overview checks. Native Windows compilation, xUnit execution, binding-error tracing, rendering and DPI review remain pending. During the Windows review, inspect the binding trace output while loading each dashboard and using its row actions; source-preservation checks cannot certify runtime binding resolution.
