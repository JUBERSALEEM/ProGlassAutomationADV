# SaaS customer intake and WPF proforma handoff — senior analysis

**Scope:** the public SaaS welcome/header, customer inquiry and PI-request entry flow, Supabase persistence, and the Windows inbox boundary. This is an intake/handoff design, not a claim that the website can safely reproduce the desktop's local pricing engine.

## Repository/version evidence

The checked-out repository's `ProGlassAutomation.csproj` is version **2.0.11**. The local and available remote refs inspected in this checkout do not include a branch named 2.0.15; the repository's website history describes 2.0.15 as a development line. The WPF module was therefore inspected from the source present in this checkout (`Views/ProformaInvoice/README.md`, `Views/ProformaInvoice/ProformaInvoiceView.xaml`, `ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs`, and its step view models), not from a separate 2.0.15 branch.

## WPF PI workflow findings

The desktop describes a nine-step PI wizard:

1. Company legal entity, registration, location, representative, bank details, and logo.
2. Customer profile, TRN/billing data, deduplication and registration.
3. Project identity, contract reference, and site mapping.
4. Glass build/pricing estimator (SGU, DGU, LAM, DGU+LAM), driven by desktop sheet-store and production-rate data.
5. Specification/BOQ grid, with inquiry BOQ conversion and line ownership rules.
6. Other charges, wastage, and custom charges.
7. Invoice summary/print preview, discounts and UAE VAT.
8. Invoice attachments.
9. Internal profit/loss and price-basis snapshot.

The SaaS intake now combines the company, customer/contact, and project questions in one guided form flow. It does not expose the desktop's company master data, customer directory, sheet costs, production rates, margin/P&L, or invoice numbering to a public browser. Those are authoritative desktop/internal data and need an authenticated service contract before they can be used on the web. Step 8 is not part of the PI-request experience; the separately labelled optional project drawings/BOQ upload belongs to the inquiry handoff, not to an issued PI. The customer flow does not issue or confirm a PI.

## Existing Supabase → Windows handoff contract

The existing portal's intended path is:

1. A customer signs in with Supabase Auth.
2. The browser writes a staged customer submission through the inquiry-submission APIs and private Storage bucket.
3. Finalization marks the submission `submitted`. For `inquiry_type = 'proforma_invoice'`, the submitted event carries `approval_required: true`.
4. The authenticated Windows inbox bridge (`website/scripts/inquiry-inbox-bridge.mjs`) plans the delivery, downloads the private objects, verifies byte count and SHA-256, writes a versioned `manifest.json` plus `READY` and private document files into the isolated inbox, then acknowledges the delivery.

The manifest carries the submission fields (company/contact/TRN/phone, project/location, inquiry type, description and routing metadata) along with document names, hashes, and paths. This is the existing WPF inbox route to reuse for a PI request; the new UI deliberately sets the type to `proforma_invoice` rather than creating a parallel PI payload.

**Boundary to be explicit about:** this browser flow does not write to WPF SQLite, and the bridge documentation explicitly says it writes to an isolated inbox rather than the authoritative desktop data root. A website submission reaching the Windows inbox is not by itself proof that a new row has been inserted into the desktop `Inquiries` table. If the required handoff means automatic desktop record creation (rather than the existing inbox delivery), that needs a separately reviewed WPF inbox consumer/importer and idempotency contract. The UI must not promise a direct database write that is not implemented.

## SaaS UX changes in this iteration

- The header owns Login, Sign up, Chat, appearance controls, and a Create inquiry dropdown.
- The dropdown exposes General inquiry and Request a PI. Both use the existing authenticated customer portal; PI mode locks the type to `proforma_invoice`.
- Customer/company/project prompts run on one page, one field at a time, with progress, back/next arrow controls, Enter-to-advance, auto-focus on the next prompt, and contextual right-side examples. It is not a navigation to three separate pages.
- Supporting files remain optional project-intake documents. They are not the desktop PI attachment step.
- PI request submission confirms that a request was sent to the ProGlass inbox. There is no PI approval/confirmation screen or customer-side invoice generation step.
- Appearance studio, light/system/dark selection, palette selection, language, account, community chat, private inquiry chat, and the Vercel Speed Insights integration remain available.

## Configuration and release caveat

The local checkout has no `website/.env.local`; the sign-in screen's "Supabase authentication is not configured" state is caused by missing local `NEXT_PUBLIC_SUPABASE_URL` and `NEXT_PUBLIC_SUPABASE_ANON_KEY`, not by removal of the Supabase client or API code. Do not place service-role credentials in the browser or repository. Vercel authentication and live submission require the public URL/key and the already-provisioned schema/storage/bridge settings in the relevant deployment environment.
