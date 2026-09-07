# PDF Logo / Letterhead Senior Audit — 2026-09-07

## Scope
Company logo rendering and PDF letterhead behavior were audited across Proforma Invoice, Proforma Dossier, Job Order, Delivery Note, Tax Invoice, and Account Statement.

## Root causes found
1. **Renderer gate was too strict**: the shared PDF chrome only treated a logo as available when `LogoLayout != None`. Any existing company/profile/snapshot with a valid logo path but `LogoLayout = None` rendered as text-only.
2. **Top-panel logo was not truly top-panel**: the previous masthead capped the logo to a small 28 mm width, so landscape/wide logos could not work as a professional full-width brand panel.
3. **File-path-only snapshots were fragile**: saved Proforma/Delivery/Statement paths worked only while the original logo file remained accessible. Downstream documents could lose the logo if the path moved.
4. **Company/PI selection defaults allowed logo + None layout**: importing/selecting a logo did not always force a printable layout, allowing a valid image to be saved in a non-rendering state.

## Fix implemented
- Central `PdfChrome` now renders a valid logo even for legacy records where the layout is `None`.
- Wide/landscape logo dimensions auto-promote to the top-panel masthead so long logos are not squeezed into the left portrait slot.
- Logo path resolution is more defensive and supports direct paths, relative paths, application directory, current directory, local app data, and pictures-folder filename fallback.
- Top-panel layout now uses a centered full-width/landscape masthead chip with page-safe width/height caps and document/company metadata integrated into the same professional header.
- Company Center and PI Step 1 now default to `LeftPortrait` when a logo is selected while layout is still `None`; clearing the logo returns to text-only.
- Proforma snapshots now embed `LogoBase64` so PI exports and downstream Job Order / Delivery Note / Tax Invoice can survive a moved/deleted source image.
- Delivery Note and Statement snapshots/layouts now carry the embedded logo payload.
- Tax Invoice import now preserves the embedded PI logo into its saved/exported PDF state.

## Affected document paths
- Proforma Invoice: shared letterhead with path + embedded logo fallback.
- Proforma Dossier: shared letterhead with path + embedded logo fallback.
- Job Order: uses PI company snapshot logo path/base64/layout.
- Delivery Note: uses job-order reference snapshot logo path/base64/layout.
- Tax Invoice: uses imported/saved logo path/base64/layout.
- Account Statement: direct QuestPDF document but now uses the shared `PdfChrome.Letterhead` with path/base64/layout.

## Validation notes
- The change is centralized to avoid duplicating header code per document.
- Existing `None`, `LeftPortrait`, and `TopPanel` enum values are preserved for data compatibility.
- Build could not be executed in this sandbox because the .NET SDK is not installed; syntax/consistency checks were run separately before commit.
