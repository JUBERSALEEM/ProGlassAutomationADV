# Phase C12: staff PI WPF Steps 4–6 parity and workflow snapshot

## Scope

This changes only the staff `ProformaWorkspace`. It does not alter the customer technical-spec intake or make technical intake priced. Step 4 cost/margin inputs and Step 6 charges are staff-only revision state.

## Website behavior added

- **Step 4:** exposes the four WPF build families (SGU, DGU, LAM, DGU+LAM), editable pane/interlayer/spacer/processing/yield/margin inputs, generated specification names, and the WPF rate formulas. “Apply” hands the name and estimate to the selected Step 5 group. Existing operator-owned references and line rates are retained; builder-owned rates use the Step 5 `Sqm1 >= 4` surcharge rule.
- **Step 5:** remains the editable cut list and rate editor, now visibly presents SQM1/total, SQM2/total, LM1, and LM2. Existing row, group, copy/reorder, saved-spec reuse/replace, and rate-setting behavior remains available. Line reference/rate ownership is revision-persisted so a subsequent Step 4 handoff does not overwrite manual edits.
- **Step 6:** adds predefined Other Charges, per-spec metric/target selection, polish subtype/custom quantity, advanced/manual modes, wastage rows, custom charges, and a separate Step 6 total. Advanced suggestions are based on the applied Step 4 generated name/build. Wastage uses the selected panel area and manual rate; sheet quantity is recorded but is not a live stock decrement.
- **Save/reopen:** Step 4 inputs, Step 5 ownership markers, Step 6 charge rows and selections are saved in `inquiry_proforma_workflow_states`, keyed to the proforma revision. The v3 RPC delegates the existing v2 line/rate save and writes the extra snapshot atomically. Idempotent retries do not overwrite the original snapshot. If C12 is absent, the API falls back to the existing v2/v1 behavior and the UI warns that Step 4/6 state was not persisted.
- **Document:** the saved Step 6 calculation is shown as a separate amount, not folded into the existing database glass-line total. The website still does not compute discounts, VAT, or the desktop grand total.

## Migration and security

`supabase-inquiry-proforma-phaseC12-workflow-state.sql` is additive and should be applied after Phase C8 and C11. It grants reads only to enabled inquiry administrators/auditors through the existing role policy; direct client writes are revoked. The security-definer v3 RPC requires `inquiry_admin`, bounds the JSON payload, calls the existing v2 save function, and pins the snapshot to that save's revision.

**The migration has not been applied to Production Supabase.** No Production database action was performed in this task.

## Explicit remaining gaps

1. The WPF obtains colours, sheet sizes, and selling prices from Sheet Store; the website does not yet connect to Sheet Store. Corresponding values are editable staff inputs and must be verified by the operator. Inventory/sheet pricing is not inferred or silently substituted.
2. WPF Step 6 can consume optimization-run and Sheet Store data for wastage formulas/quantity. The website records manual sheet quantity and rate only; it does not decrement stock or import live optimizer results.
3. DGU+LAM has three glass colours/panes in Step 4, while the current Step 5 line schema stores only W1/H1 and W2/H2. Step 6 uses the second panel area for the additional hybrid pane; parity for per-pane dimensions needs an additive Step 5 data-model/schema change.
4. The existing revision header and line table still compute/store the glass-line subtotal only. Step 6 stays a saved staff snapshot and separate displayed subtotal until a reviewed, revision-safe invoice-total schema change is approved.
5. The current UI does not provide WPF's full optimization/inventory tooling or every Step 6 secondary action. These are not represented as completed parity.
