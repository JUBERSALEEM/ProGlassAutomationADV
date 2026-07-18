# Proforma Invoice Module

Enterprise-grade 7-step wizard for building professional glass proforma invoices.

## Wizard Steps
1. **Company Details** — TRN, location, billing, proforma ID, dates, status
2. **Project** — Reference numbers, PO, contract no, site/delivery location
3. **Other References** — Clients, salesman, attention, billing address, contact
4. **Glass Type & Specs** — SGU/DGU/LAM estimator, materials, treatments, pricing
5. **Data Grid** — Multi-spec row configurations, outer/inner glass, paste Excel
6. **Other Charges** — Supplementary charges (argon, silicon, polish, wastages)
7. **Print Preview** — Review, terms, VAT calculation, PDF export

## Current Status
- ✅ **Step 0** — Empty shell + navigation wired
- 🚧 **Step 1** — Wizard sidebar (7 vertical steps) — next
- ⏳ **Step 2+** — Progress bar, step content, per-step VMs

## Folder Structure (Planned)

### Models

    Models/ProformaInvoice/
        ProformaInvoice.cs              (main entity)
        ProformaInvoiceItem.cs          (grid row items)
        ProformaSpecGroup.cs            (spec groups)
        ProformaOtherCharge.cs          (other charges)
        CompanyProfile/
            CompanyProfile.cs           (saved company profiles)
        ClientProfile/
            ClientProfile.cs            (saved client profiles)
        GlassCatalog/
            GlassCatalogEntry.cs        (Guardian/AGC catalog)

### Services

    Services/ProformaInvoice/
        IProformaRepository.cs          (main — flat)
        ProformaRepository.cs           (main — flat)
        ProformaPdf/                    (PDF generation)
        ProformaExcel/                  (Excel paste/parse)
        ProformaPricing/                (SGU/DGU/LAM formulas)
        ProformaCharges/                (auto-calc charges)
        CompanyProfile/                 (company profile CRUD)
        ClientProfile/                  (client profile CRUD)

### ViewModels

    ViewModels/ProformaInvoice/
        ProformaInvoiceViewModel.cs     (main wizard coordinator)
        Step1_CompanySetup/
        Step2_ProjectSpecs/
        Step3_ClientProfile/
        Step4_PricingEstimator/
        Step5_DataGrid/
        Step6_OtherCharges/
        Step7_PrintPreview/

### Views

    Views/ProformaInvoice/
        ProformaInvoiceView.xaml        (wizard shell)
        Step1_CompanySetup/
        Step2_ProjectSpecs/
        Step3_ClientProfile/
        Step4_PricingEstimator/
        Step5_DataGrid/
        Step6_OtherCharges/
        Step7_PrintPreview/

## Dashboard Contract
`ProformaInvoiceViewModel` exposes these properties so `DashboardViewModel` bindings work:
- `Invoices` — ObservableCollection (empty until save/load is implemented)
- `TotalSales` — decimal (currently 0)
- `InvoiceCount` — int (currently 0)
- `PendingCount` — int (currently 0)
- `HasInvoices` — bool (currently false)

## Business Rules
- Currency: AED (UAE Dirham)
- Proforma ID format: PI-{YEAR}-{SEQ} (e.g., PI-2026-001)
- Pricing: SQM-based (Width × Height / 1,000,000 × Rate)
- VAT: 5% UAE standard
- Glass types: SGU (Single Glass Unit), DGU (Double Glass Unit), LAM (Laminated)
- Spec example: "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear Annealed Glass"
- Other charges: Argon Gas, Silicon Bed, Overlap, Polish, Mitring, Cutouts, Holes, Wastages

## Build Phases
| Phase | Description | Status |
|-------|-------------|--------|
| Phase 0 | Empty shell + navigation | ✅ Done |
| Phase 1 | Wizard sidebar + step navigation | 🚧 Next |
| Phase 2 | Step 1 — Company Setup | ⏳ |
| Phase 3 | Step 2 — Project Specs | ⏳ |
| Phase 4 | Step 3 — Client Profile | ⏳ |
| Phase 5 | Step 4 — Smart Pricing Estimator | ⏳ |
| Phase 6 | Step 5 — Data Grid | ⏳ |
| Phase 7 | Step 6 — Other Charges | ⏳ |
| Phase 8 | Step 7 — Print Preview + PDF | ⏳ |
| Phase 9 | Save/Load + History | ⏳ |