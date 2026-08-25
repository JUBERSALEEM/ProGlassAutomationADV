# Module Feature Toggles — Job Order / Delivery Note / Tax Invoice (Aug 2026)

**Branch:** `arena/01a03473-proglassautomationadv`

---

## 1. What you asked for

> "Do a proper check in job order delivery note tax invoice there are so
> many extra option but we need only few can you update features option in
> module switch type so option can be visible as per module selection
> switch like it has only proforma invoice right now and make original
> module to auto adjust blank spacer so they look like they had only these
> option like auto compact fitting in original as per enable disable from
> setting features ... like current proforma invoice do a deep analysis so
> nothing break and auto adjust in main module."

Two things had to happen:

1. **Settings ► Features** already had toggles for **Proforma Invoice** and
   **Inquiry**. It needed the same per-module switch-style toggles for
   **Job Order**, **Delivery Note** and **Tax Invoice**.
2. When an operator turns a field **OFF**, the **actual module editor**
   must hide it **AND re-flow** so there is no leftover empty spacer —
   the screen should look like it only ever had those enabled fields.

---

## 2. What was added

### A. Settings ► Features — three new module sections
Each new section has a **Bulk Actions** card (Enable all / Hide all
optional / Reset defaults) plus a tile per optional field with an
iPhone-style toggle:

| Module | Toggleable optional fields | Always-visible (can't hide) |
|--------|----------------------------|-----------------------------|
| **Job Order** | Assigned To, Site Delivery Contact, Site Delivery Phone, Site Delivery Address, Drawing Reference, Cutting Instructions, Internal Notes | Status, Priority, Delivery Date |
| **Delivery Note** | Driver Name, Vehicle Number, FOC (Free), Receiver Name, Notes | Status |
| **Tax Invoice** | Due Date, Payment Terms, Discount %, IBAN, SWIFT | Issue Date, Bank Name, Account No |

Reliable DB / workflow fields are deliberately **not** listed so an
operator can never hide something the system needs to save.

### B. The module editors now auto-compact
Each optional field's `Visibility` binds to a feature-flag singleton and
the optional fields are hosted in a **WrapPanel** (instead of a fixed
star-column grid). When a field is toggled off:

- the field **collapses** (takes no space),
- the remaining fields **re-flow to fill the row**,
- no blank spacer remains.

This is the same mechanism the Proforma wizard already uses, replicated
across the three modules so the app is consistent.

### C. The full config / service stack (mirrors Proforma)
- `AppConfig` gained `JobOrderFields`, `DeliveryNoteFields`,
  `TaxInvoiceFields` (all default to TRUE = byte-identical upgrade).
- `ConfigService` hydrates the three new fields so old `config.json`
  files do not NRE.
- Three new singletons: `JobOrderFieldVisibility`,
  `DeliveryNoteFieldVisibility`, `TaxInvoiceFieldVisibility`.
- Three new view-models: `JobOrderFeaturesViewModel`,
  `DeliveryNoteFeaturesViewModel`, `TaxInvoiceFeaturesViewModel`, exposed
  on `SettingsViewModel` and bound from the Features page.

---

## 3. Verified (no `dotnet` build here)

- All modified XAML parses cleanly.
- No element sets the same property twice (the XLS0501/MC3024 guard).
- All existing module bindings preserved (Status/priority/delivery in JO,
  status/driver/vehicle/FOC/receiver/notes in DN, issue/due/terms/discount
  /IBAN/SWIFT in TI).
- All feature flags, singletons, view-models and config hydration balanced.
- Updated/added tests:
  - `Tests/Features/ModuleFieldVisibilityTests.cs` — the three new
    singletons default true, are singletons, and raise PropertyChanged on
    Refresh.
  - `Tests/Features/ModuleEditorVisibilityBindingsTests.cs` — each module
    editor actually binds its fields to the matching singleton.
  - `Tests/Settings/SettingsRedesignTests.cs` — Features page now asserts
    all **five** DataContext scopes and **47** field toggles.
  - `Tests/JobOrder/JobOrderProductionRedesignTests.cs` — production panel
    asserts the auto-compact WrapPanel + visibility bindings.

---

## 4. Notes for you (non-coder summary)

- Go to **Settings ► Features**. You'll now see five modules listed:
  **Proforma Invoice**, **Inquiry**, **Job Order**, **Delivery Note**,
  **Tax Invoice**.
- Switch any field OFF and the matching module editor instantly hides it
  AND tightens the layout (no empty gap).
- Use **Hide all optional** to collapse everything you don't fill in —
  the screen then only shows the fields you actually use.
- Nothing you turn off deletes data: if a field had a value it's kept on
  disk and reappears when you switch it back on.
