# ProGlass Automation — First Run

What to expect the very first time you launch ProGlass Automation.

## What you need

- The **Setup.exe** or **portable zip** from your vendor (see `INSTALL.md`).
- Optionally: a **licence file** (`.lic`) emailed to you by your vendor. The welcome view will accept it directly.

## What happens on first launch

The app's startup sequence is:

1. **Configuration load** — your user preferences (`config.json`).
2. **Database initialization** — the eleven SQLite files in `%AppData%\ProGlassAutomation\`. On a fresh install, all of them are created empty.
3. **Licence check** — the welcome view appears. Pick one of the three options (below).
4. **Splash + Terms** — the terms-and-conditions gate. You must accept to proceed.
5. **Login** — the seeded admin user (`admin`) is created on the first run. The default password is empty; you are forced to set one immediately.
6. **Main window** — you are in. The default landing module is the Dashboard.

## The Welcome view

The welcome view has three buttons:

### 1. ACTIVATE LICENSE FILE…

Click this if your vendor emailed you a `something.lic` file. The standard file picker opens. Pick the file. The app verifies the signature in under 50 ms; if it passes, you are in.

If you do not have a `.lic` file yet, your vendor will send one. Do not run the app on production data without a valid licence.

### 2. START 30-DAY TRIAL

Click this to evaluate the app for 30 days. The app generates a local trial payload, signs it with the developer's private key (which is on your machine — your vendor installed the right version), and writes it to the standard licence location.

A trial is bound to the machine that started it (`maxActivations = 1`). It cannot be transferred to a different machine. It expires automatically 30 days from first activation.

### 3. CONTINUE READ-ONLY

Click this to evaluate the UI without a licence. The app boots in read-only mode — you can browse every screen, but **you cannot save any data, print, or export**.

You can leave read-only mode at any time by activating a licence from **Settings → Data → Database Health → ACTIVATE LICENSE**.

## Setting the admin password

On the first run, the app forces you to set a password for the seeded `admin` user. Choose a strong one — the password must be at least 8 characters. This is the only way to recover the system if the user list is ever corrupted, so **write it down** in a safe place.

After the first run, additional users are created from **Settings → Users** (or via the User Center module on the shell rail). The seeded `admin` is intentionally a backdoor and should be disabled once you have a real administrator account.

## First-day checklist

After the welcome view + admin password + first dashboard view:

- [ ] **About → License** — confirm your edition, customer, expiry, and that the machine fingerprint is correct.
- [ ] **Settings → Appearance** — pick the theme (Dark or Light), the accent colour, the UI density (Compact is the default).
- [ ] **Settings → Company** — fill in your company name, TRN, address, logo, and representative. These appear on every PDF you print.
- [ ] **Settings → Master Data → Customers** — add at least one customer. The first customer is needed before you can create a Proforma Invoice.
- [ ] **Settings → Master Data → Salesmen** — add the salespeople whose names will appear on invoices.
- [ ] **Settings → Master Data → Glass types** — define the glass you sell (clear, tinted, low-E, mirror). The SQM price is set here.
- [ ] **Settings → Backup** — click **CREATE BACKUP NOW** so you have a clean baseline before any data entry.

## First-week checklist

Once you have a few days of data:

- [ ] **Settings → AI → Enable Google Gemini (optional)** — paste your Gemini API key. The AI Assistant uses only your business summary (with your consent) and answers glass-industry questions.
- [ ] **Run a real inquiry through to a delivery note** — Inquiry → Proforma Invoice → Job Order → Delivery Note → Tax Invoice → Statement. The full chain takes about 10 minutes once you have data in master records. If anything looks wrong, see `TROUBLESHOOTING.md`.
- [ ] **Settings → Backup** — verify the schedule (default: every app exit). Run a manual backup before the end of the day.
- [ ] **Settings → Updates** — check that the OTA update channel is reachable. A real customer install is usually wired to the vendor's GitHub release feed.

## Common first-day questions

**Q: I clicked START TRIAL but the button does nothing.**
A: The trial signer needs the developer's private key, which is on the machine that built the installer. If the installer was built with the `LicenseGenerator` tool's private key baked in, the trial works. If the installer was built with a different signing key, the trial button returns an error dialog. Contact your vendor.

**Q: I activated a licence but the welcome view still shows.**
A: Click ACTIVATE LICENSE FILE… again, and pick the same file. The second attempt is the silent retry after the file copy.

**Q: Where did the admin password I just typed go?**
A: It is stored in `audit.db` (the `AppUsers` table), hashed with PBKDF2-SHA256 + per-user salt. The plain text is never written anywhere.

**Q: How do I find my licence error ID when I report a problem?**
A: Open the welcome view, click ACTIVATE LICENSE FILE… — the error card at the top shows the precise reason. The `ErrorId` in the corner is what support needs.

**Q: Can I install ProGlass on a Mac?**
A: No, the WPF shell is Windows-only. The data is plain SQLite + JSON; a future Linux or macOS build would only need a new UI layer. Talk to your vendor.

**Q: My first launch is slow / hangs at "Loading…".**
A: On first run, the app creates eleven SQLite files. This takes 2-10 seconds depending on disk. If the splash spins for more than 30 seconds, the database is locked by another copy of the app — open Task Manager and kill any `ProGlassAutomation.exe` that is hung.
