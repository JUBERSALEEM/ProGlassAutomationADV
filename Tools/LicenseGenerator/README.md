# ProGlass Automation — License Generator

A **vendor-only** WPF tool that issues signed ProGlass
Automation license files. **Never** shipped to customers.

End users do NOT use this tool, and never need to.
The 30-day **trial** is auto-issued by the app itself on
first launch (self-signed with an embedded key), and a
**paid** license is a `.lic` file the vendor signs here and
sends over; the customer just drops it in
`%AppData%\ProGlassAutomation\license.lic` (or pastes it) in
Settings → License. Only the *vendor/developer* runs this
tool, to generate the key pair once and to sign customer
licenses.

## What it does

1. **First run:** generates a fresh RSA-2048 key pair.
   - **Private key** → `%AppData%\ProGlassAutomation\license-keys.json`
   - **Public key** → displayed in the window. You copy it
     into `Services/Licensing/LicensePublicKey.cs` and
     rebuild the app.
2. **Every run:** the existing key pair is loaded. Fill in
   the customer details, click **SIGN LICENSE & SAVE**,
   pick where the `.lic` should be written (or accept the
   default in `Documents\ProGlassAutomation\Licenses\`).
3. **Reveal full public key** at any time: the REVEAL
   button in the KEY PAIR card shows the entire XML the
   developer must paste into the app.

## Where the keys live

| File | Purpose | Where |
|---|---|---|
| Private key | Signs new licenses | `%AppData%\ProGlassAutomation\license-keys.json` |
| Public key | Verifies licenses | Embedded in `Services/Licensing/LicensePublicKey.cs` (after you paste it) |
| Signed licenses (output) | Customer artifacts | `%USERPROFILE%\Documents\ProGlassAutomation\Licenses\` by default |

## First-time setup (developer)

1. Build the tool (see *Build* below).
2. Run `ProGlassAutomation.LicenseGenerator.exe`. The
   window opens. The KEY PAIR card shows "No key pair
   found."
3. Click **GENERATE NEW KEY PAIR**. A confirmation dialog
   warns that any old key (if present) will be replaced.
   The private key is saved to
   `%AppData%\ProGlassAutomation\license-keys.json`.
4. Click **WRITE PUBLIC KEY INTO APP**. The tool finds
   `Services/Licensing/LicensePublicKey.cs` in the repo and
   rewrites its `PublicKeyXml` to the matching public half —
   no manual copy-paste, no chance of a key mismatch. (If
   the tool is not run from a repo checkout it tells you to
   paste the key manually via COPY TO CLIPBOARD.)
5. Rebuild the main app. Future builds accept any license
   signed with the matching private key.

> **Why this matters:** "Activation failed: signature does not
> match the payload" is almost always a key mismatch — the
> public key embedded in the app does not match the private key
> the tool signs with. The one-click **WRITE PUBLIC KEY INTO APP**
> step makes that impossible. The tool also shows the error
> correctly if the app was rebuilt with a different public key.

## Issuing a customer license

1. Fill in the **Sign a license** card:
   - **Edition** — Trial / Basic / Business / Factory / Enterprise.
   - **Customer ID** — your internal code, e.g. `CUST-2026-042`.
   - **Company name** — `Acme Glass LLC` (used in the file
     name and in the welcome-view status display).
   - **License duration (days)** — 1 to 3650. 365 for a
     year, 30 for a trial.
   - **Max activations** — 1 for single-machine; >1 for
     a portable license that the customer can install on
     N devices.
   - **Features** — comma-separated allow-list. `*`
     grants every feature. `Inquiry.*` grants all Inquiry
     operations. Leave at the default for a full
     Business / Factory / Enterprise license.
   - **Machine fingerprint** — leave blank for a
     portable license. For a machine-bound license, click
     **USE THIS MACHINE** on your own box to fill the
     field, or paste the customer's fingerprint
     (they read it from the welcome view's diagnostic
     card or the About page).
   - **Notes** — free-form. Shown in the About page and
     written to the audit log on activation.
2. The **Output path** is auto-populated with
   `%USERPROFILE%\Documents\ProGlassAutomation\Licenses\<Company>-<date>.lic`.
   Click **BROWSE…** to choose a different location.
3. Click **SIGN LICENSE & SAVE**. A success message in
   the status bar confirms the LicenseId. Windows
   Explorer opens with the new `.lic` selected.
4. Email the `.lic` to the customer (or deliver by any
   out-of-band channel — WhatsApp, support ticket, etc.).

## Customer-side installation

The customer receives the `.lic` file. They have two ways
to install it:

1. **Manual**: drop the file in
   `%AppData%\ProGlassAutomation\license.lic`. Start the
   app — the welcome dialog skips; the app boots in full
   mode.
2. **First-run welcome**: launch the app, click
   **ACTIVATE LICENSE FILE…**, pick the `.lic`. The app
   activates and boots in full mode.

## Edition cheat-sheet

| Edition    | Max users | Includes |
|------------|-----------|----------|
| Trial      | 1         | Curated feature subset, 30-day expiry |
| Basic      | 1         | Inquiry, Proforma |
| Business   | 5         | All ERP, no cutting optimisation |
| Factory    | Unlimited | All ERP + cutting optimisation |
| Enterprise | Unlimited | All ERP + custom integrations |

The list above is the licence issuer's policy, not the
app's enforcement. The app only checks the `Edition`
field to decide which UI badge to show ("Trial",
"Business", etc.) and which `Features[]` are granted.
`Features[]` is the real gate — set it to a curated list
for a Trial, `*` for Enterprise.

## Machine-bound vs portable

* **Portable** (leave fingerprint blank): the customer
  may install on up to `MaxActivations` devices. The app
  records each device's fingerprint in
  `%AppData%\ProGlassAutomation\activations.json` and
  refuses the activation when the cap is hit.
* **Machine-bound** (paste a hex-16 fingerprint): the
  licence only runs on that exact machine. Get the
  fingerprint from the customer (they read it from the
  app's About page or run a small helper) and paste it
  into the form. The fingerprint is a SHA-256 prefix
  derived from CPU count, OS, arch, framework, volume
  serial and the first non-loopback NIC MAC address.

## What is NEVER committed

`.gitignore` excludes:

* `license-keys.json` — your private key
* `*.lic` — customer artifacts (rename to `*.sample.lic`
  to keep)
* `activations.json` — per-machine state

If your private key is ever pushed to a remote, treat it
as **compromised**: rotate the key pair, reissue every
active license under the new key, and notify affected
customers.

## Why a separate tool?

The signing private key must never enter the shipped
app's binary. A separate WPF project (a) keeps the tool
out of the install bundle, (b) has no EF Core / ClosedXML
/ NAudio / Whisper dependency graph, and (c) shares the
licensing primitives with the app via
`<Compile Include="..." Link="..." />` so the two stay in
lockstep.

The tool uses its own minimal dark theme (`Theme.xaml`)
because `pack://application:,,,/Themes/...` URIs only
resolve inside the owning assembly.

Like the main app, the tool shares these licensing source
files verbatim via `<Compile Include>` (they all compile
in both projects without a `ProjectReference`):
`LicensePayload.cs`, `LicenseSigner.cs`, `LicensePublicKey.cs`,
`MachineFingerprint.cs`, and `TrialKeyVault.cs` (`TrialKeyVault`
is referenced by `LicensePublicKey.cs` — without it the tool
fails to compile with CS0103).

## How it stays self-contained (and builds from the CLI)

The tool does **not** reference the main app project. To
keep the shared licensing source compiling without dragging
in the whole EF Core / ClosedXML / NAudio / Whisper graph:

* `System.Management` (WMI) is referenced directly as a
  NuGet package — `MachineFingerprint.cs` is shared verbatim
  and needs it. It also brings `Microsoft.Win32.Registry`
  transitively, which `ReadMachineGuid` uses.
* `AppLogService` is provided by a tiny self-contained
  stand-in (`AppLogServiceStub.cs`) in the tool's own
  namespace. It has the same method signatures as the app's
  logger but writes to its own log file under
  `%AppData%\ProGlassAutomation\logs\` and never throws. The
  real `AppLogService` still ships with the app — the stub is
  compiled **only** into this project, so the shared licensing
  files stay byte-for-byte identical on both sides.

You can build the tool entirely from a command prompt
(no Visual Studio required) with the single `dotnet build`
command below.

## Build

From the repo root:

```
dotnet build Tools/LicenseGenerator/ProGlassAutomation.LicenseGenerator.csproj -c Release
```

Output: `Tools\LicenseGenerator\bin\Release\net8.0-windows\ProGlassAutomation.LicenseGenerator.exe`.

## Run

Double-click the produced `.exe`, or:

```
dotnet Tools\LicenseGenerator\bin\Release\net8.0-windows\ProGlassAutomation.LicenseGenerator.dll
```

## What if I lose the private key?

You cannot reissue any license. The customers' existing
licenses will keep working (the public key is in the
app), but new customers cannot be onboarded until you
generate a fresh key pair, paste the new public key into
the app, and ship a new build. **Back up
`license-keys.json` somewhere safe.**
