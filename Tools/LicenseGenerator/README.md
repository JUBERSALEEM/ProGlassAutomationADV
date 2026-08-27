# ProGlassAutomation.LicenseGenerator

Vendor-only WPF utility that issues the exact `.lic` envelope the current
ProGlass main application already accepts in **Settings / About → License**.

## What it generates

- **`.lic` JSON envelope** signed with the app-compatible RSA key
- **Base64 paste key** of that same JSON envelope

The app activation screen can accept either:
1. browse the `.lic` file
2. paste the base64 key

## Contract source of truth

This tool is deliberately linked to the same app-compatible licensing files the
main app uses today:

- `Services/Licensing/LicensePayload.cs`
- `Services/Licensing/LicenseSigner.cs`

That keeps the generator output in lockstep with the main software acceptance
path. No second implementation of the `.lic` format is maintained here.

## Trusted keys

- The generator signs with the embedded compatibility issuer key in
  `Tools/LicenseGenerator/AppCompatibleIssuerKeys.cs`.
- The main app trusts that public key as a **secondary production key** in
  `Services/Licensing/LicensePublicKey.cs`.
- The app still trusts the original legacy production key for older licenses.

## Edition presets

Default feature presets follow the app's current license model:

- **Trial** → `*`
- **Basic** → `Inquiry.*`, `Proforma.*`
- **Business** → all ERP workflow modules, no `Cutting.*`
- **Factory** → Business + `Cutting.*`
- **Enterprise** → `*`

## Intended workflow

1. Open the generator
2. Pick edition / days / activation model
3. Generate the license
4. Send either:
   - the `.lic` file, or
   - the copied paste key
5. In the app, the customer activates from the license screen

## Notes

- This is a **vendor/internal** tool, not customer-distributed software.
- It is intentionally app-compatible first; it does not use the newer
  `ProGlass.Licensing` contract library.
