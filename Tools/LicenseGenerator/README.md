# ProGlass Automation — License Key Generator

A **vendor-only** WPF tool that issues signed ProGlass
Automation **license keys**. **Never** shipped to customers.

The tool is deliberately simple: generate a signing key once,
then produce a single **copy/paste license key string** for each
customer. The customer pastes it into **Settings → License →
paste a license key → Apply** and it activates instantly. **No
`.lic` file, no path, no browsing** — just one string.

End users never run this tool. The 30-day **trial** is
auto-issued by the app itself; a **paid** license is a key the
vendor generates here and sends over.

## What it does

1. **GENERATE KEY PAIR** — creates an RSA-2048 signing key once.
   - Private key → `%AppData%\ProGlassAutomation\license-keys.json`
     (kept on this machine, never shared).
2. **INSTALL PUBLIC KEY INTO APP** — one-click stamps the public
   half into `Services/Licensing/LicensePublicKey.cs` so the app
   verifies keys from this tool. Do this once, then rebuild the
   app. No manual copy-paste, no key mismatch.
3. **For each customer:** pick an **Edition**, set **Valid for
   (days)**, optionally enter a **Customer ID** / **Company**, and
   optionally check **Bind to a specific computer** to bind the key
   to one machine's hardware ID. Click **GENERATE LICENSE KEY**.
4. The key string appears — click **COPY KEY** and send it to the
   customer. **SAVE AS .LIC FILE** is an optional fallback for
   customers who still prefer a file.

## How the key activates

The key is a base64-encoded, RSA-2048-PSS-signed license envelope.
When the customer pastes it, the app decodes it and verifies the
signature against the embedded public key — then activates
instantly. Because it is signed, the key cannot be tampered with.

## Optional hardware binding

- For a **portable** key (runs on any machine up to MaxActivations),
  leave **Bind to a specific computer** unchecked.
- For a **machine-bound** key: check **Bind to a specific computer**,
  use the customer's hardware ID (from their app's About page) or
  **USE THIS PC** to fill this machine's own ID. The key then only
  runs on that exact machine.

## Build

From the repo root:

```
dotnet build Tools/LicenseGenerator/ProGlassAutomation.LicenseGenerator.csproj -c Release
```

Output: `Tools\LicenseGenerator\bin\Release\net8.0-windows\ProGlassAutomation.LicenseGenerator.exe`.

## Security notes

- RSA-2048, PSS padding, SHA-256 (industry standard).
- The private key never leaves this machine; only the public half is
  embedded in the app.
- The tool refuses to generate a key when its signing key does not
  match the app's embedded public key (the "signature does not
  match" cause), so you cannot accidentally produce an invalid key.
- **Back up `license-keys.json`** — if you lose the private key you
  cannot re-issue or renew existing keys.
