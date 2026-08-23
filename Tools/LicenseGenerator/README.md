# ProGlass Automation — License Generator

A developer-only console tool that issues signed ProGlass Automation
license files. **Never** shipped to customers.

## What it does

1. **First run:** generates a fresh RSA-2048 key pair.
   - **Private key** → `%AppData%\ProGlassAutomation\license-keys.json`
   - **Public key** → printed to the console. You paste it into
     `Services/Licensing/LicensePublicKey.cs` and rebuild the app.

2. **Every run:** reads the existing key pair (if present) and lets
   you sign licenses through an interactive prompt.

## Where the keys live

| File | Purpose | Where |
|---|---|---|
| Private key | Signs new licenses | `%AppData%\ProGlassAutomation\license-keys.json` |
| Public key | Verifies licenses | Embedded in `Services/Licensing/LicensePublicKey.cs` (after you paste it) |
| Signed licenses | Output | `%USERPROFILE%\Documents\ProGlassAutomation\Licenses\` |

## First-time setup (developer)

```
> ProGlassAutomation.LicenseGenerator.exe

ProGlass Automation — License Generator
========================================

No signing key found. Generate a new RSA-2048 key pair? [Y/n] y
Generating key pair...
Key pair saved to: C:\Users\You\AppData\Roaming\ProGlassAutomation\license-keys.json

Paste this line into Services/Licensing/LicensePublicKey.cs:

    public const string PublicKeyXml =
        @"<RSAKeyValue><Modulus>...AQAB</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

After pasting, rebuild the app so the new key is embedded.
```

Open `Services/Licensing/LicensePublicKey.cs`, replace the placeholder
with the printed public key XML, save, and rebuild. Future builds of
the app will accept any license signed with the matching private key.

## Issuing a customer license

```
[S] Sign a license
[E] Re-export public key
[Q] Quit
> s

Edition (Trial|Basic|Business|Factory|Enterprise) [Business]: business
Customer ID (e.g. CUST-2026-001) [CUST-2026-001]: CUST-2026-042
Company name [Acme Glass LLC]: Acme Glass LLC
License duration in days [365]: 365
Max activations (1 for single-machine) [1]: 3
Features (comma-separated; * = all; *.<Module> = whole module) [...]: *
Machine fingerprint (hex16) or empty for portable: <Enter>
Notes (optional) [Issued by ...]:

License signed.
  LicenseId     : 0e3a...
  Edition       : Business
  Customer      : CUST-2026-042 (Acme Glass LLC)
  Expires       : 2027-08-23 10:42:13Z (in 365 days)
  Activations   : 3
  Features      : *
  Machine bound : no (portable)
  Notes         : Issued by You 2026-08-23

  Saved to      : C:\Users\You\Documents\ProGlassAutomation\Licenses\Acme Glass LLC-2026-08-23.lic

Send this file to the customer. They drop it into:
  %AppData%\ProGlassAutomation\license.lic
…or use the welcome view's 'Activate License' button.
```

The customer receives the `.lic` file. They place it in
`%AppData%\ProGlassAutomation\license.lic` (or use the welcome view's
**Activate License** button). The app verifies the signature on next
startup. No network connection is involved.

## Edition cheat-sheet

| Edition    | Max users | Includes |
|------------|-----------|----------|
| Trial      | 1         | Curated feature subset, 30-day expiry |
| Basic      | 1         | Inquiry, Proforma |
| Business   | 5         | All ERP, no cutting optimisation |
| Factory    | Unlimited | All ERP + cutting optimisation |
| Enterprise | Unlimited | All ERP + custom integrations |

The list above is the licence issuer's policy, not the app's
enforcement. The app only checks the `Edition` field to decide which
UI badge to show ("Trial", "Business", etc.) and which `Features[]`
are granted.

## Machine-bound vs portable

* **Portable** (leave fingerprint blank): the customer may install on
  up to `MaxActivations` devices. The app records each device's
  fingerprint in `%AppData%\ProGlassAutomation\activations.json`
  and refuses the activation when the cap is hit.
* **Machine-bound** (paste a hex-16 fingerprint): the licence only
  runs on that exact machine. Get the fingerprint with:

  ```
  > LicenseGenerator.exe --print-fingerprint
  abcd1234ef567890
  ```

  (or have the customer run the same command and send you the output).
  The app shows the current machine's fingerprint in the About page.

## What is NEVER committed

`.gitignore` excludes:

* `license-keys.json` — your private key
* `*.lic` — customer artifacts (rename to `*.sample.lic` to keep)
* `activations.json` — per-machine state

If your private key is ever pushed to a remote, treat it as
**compromised**: rotate the key pair, reissue every active license
under the new key, and notify affected customers.

## Why a separate tool?

The signing private key must never enter the shipped app's binary.
A separate console project (a) keeps the tool out of the install
bundle, (b) has no WPF / EF Core / ClosedXML dependency graph, and
(c) shares the licensing primitives with the app via
`<Compile Include="..." />` so the two stay in lockstep.

## What if I lose the private key?

You cannot reissue any license. The customers' existing licenses
will keep working (the public key is in the app), but new customers
cannot be onboarded until you generate a fresh key pair, paste the
new public key into the app, and ship a new build. **Back up
`license-keys.json` somewhere safe.**

## Build

The tool is part of the solution (or it would be if a `.sln` file
existed). From the repo root:

```
dotnet build Tools/LicenseGenerator/ProGlassAutomation.LicenseGenerator.csproj -c Release
```

The output goes to `Tools/LicenseGenerator/bin/Release/net8.0-windows/`.

## Run

```
dotnet Tools/LicenseGenerator/bin/Release/net8.0-windows/ProGlassAutomation.LicenseGenerator.dll
```

Or, on Windows, double-click the produced `.exe`.
