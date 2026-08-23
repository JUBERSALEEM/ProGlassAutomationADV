# ProGlass Automation — Troubleshooting

When something goes wrong, work through this checklist in order. Most problems are in the first three sections.

## 1. Read the error ID

Every crash, every validation failure, every licence problem has an `ErrorId` of the form `ERR-XXXXXX`. When you contact support, include this. The error ID maps to a precise line in the day's log file.

The error ID is shown:

- On the crash dialog after an unhandled exception.
- On the welcome view's error card when licence activation fails.
- In any `MessageBox` the app shows for a recoverable error.

## 2. Open the crash reports

The most useful diagnostic file is `%AppData%\ProGlassAutomation\logs\crash-latest.txt`. It contains:

- The exact exception type and message.
- The full stack trace.
- The OS + .NET version.
- The currently logged-in user.
- The licence status.
- The last 200 lines from the technical log (the most useful context — what the app was doing right before the crash).

The full timestamped history is in `crash-yyyymmdd_hhmmss.txt` files. The app's **About → Recent crash reports** card lists the last 10 with parsed error IDs; click **OPEN FOLDER** to see them in Explorer.

To send a report to support, click **OPEN CRASH FOLDER** in the crash dialog, then drag the file into an email.

## 3. Open the technical log

The technical log is `%AppData%\ProGlassAutomation\logs\app-yyyymmdd.log`. Open in Notepad. It contains every error and warning the app has logged today, with timestamps and correlation IDs.

A typical entry looks like:

```
2026-08-23 14:22:11.452  [Error  ]  (ERR-3F9A2C)  CORR-7b3e  user=admin  [Module=Database; Class=InquiryRepository; Operation=Add]  INSERT failed: UNIQUE constraint failed: Inquiries.InquiryNumber
  System.Data.SQLite.SQLiteException: constraint failed
     at System.Data.SQLite.SQLiteStatement.BindStep(...)
     at ProGlassAutomation.Services.Inquiry.InquiryRepository.Add(...)
```

The `Module.Class.Operation` triad tells you exactly which line of which class to look at. `CORR-7b3e` is a correlation id — every line in the same request has the same id, so you can trace the full flow.

The log is rolled daily and pruned after 14 days. Old logs are gone — if you need them, take a backup.

## 4. Common problems

### "The app won't start"

1. Open `%AppData%\ProGlassAutomation\logs\`. Is the latest log timestamped within the last few minutes? If not, the app is failing before `AppLogService.Initialize()` can run — the Windows Event Log (Application) is the only place to look.
2. Try **right-click → Run as administrator** on the EXE. Some users have the data folder under a profile the standard user cannot write to (rare; typically IT-managed).
3. Check the Event Viewer → Windows Logs → Application. Look for `.NET Runtime` errors.

### "I see a SQLite error: database is locked"

Another copy of the app is running. Open Task Manager and kill any `ProGlassAutomation.exe`. SQLite serialises writes; two instances on the same data folder will fight. **Note:** running on a network share is **not supported** — SQLite assumes local file locking and a network share breaks that. The app explicitly refuses to start on a UNC path with this error message.

### "I activated my licence but the welcome view still shows"

Re-open the welcome view (close and re-open the app) — the activation reload happens once at startup. Alternatively, click ACTIVATE LICENSE FILE… again and re-pick the same file; the second click is a silent retry.

### "I see 'Embedding public key is the placeholder'"

This is a developer-shipped build that forgot to paste the real public key into `LicensePublicKey.cs`. **Contact your vendor** — the fix is on their side, not yours. The licence file is fine; the app is just unable to verify it.

### "My attachment upload fails with 'file not found'"

The file picker is at `%AppData%\ProGlassAutomation\Inquiries\{id}\Attachments\`. If the path includes characters that are not valid in the underlying filesystem (rare, but happens with Asian characters on certain Windows versions), the upload silently fails. Try renaming the inquiry to ASCII-only and retry.

### "The Cutting Optimisation module says 'Upgrade required'"

Your licence's `features` list does not include the cutting-optimisation feature. Contact your vendor — they will re-issue a licence with `Cutting.*` (or `*` for the full edition) added to the features list. The licence file format supports it; no app upgrade is needed.

### "Print to PDF is blank / has missing fonts"

The PDF engine (QuestPDF) bundles its own fonts. If a custom report template references a font that is not installed on this machine, QuestPDF falls back to a substitute and the result looks wrong. The fix is to install the font on the user's machine, or to change the report template to use a bundled font. The bundled templates use **Segoe UI** (always present on Windows 10/11) and **Consolas** (always present); a custom template that uses a third-party font is the most likely culprit.

### "The app is slow on first open of the day"

The first open of the day runs schema migrations, opens eleven SQLite files, and warms the entity-framework cache. On a slow disk or an over-loaded machine, this can take 5-10 seconds. The splash screen is designed to mask this — if you see a long splash, that is the cause, not a problem. The second open of the day is instant.

### "Excel export is empty"

The export is in `.xlsx` (ClosedXML). If the export opens as a blank workbook, the source query returned no rows. Check the filter on the report you are exporting — likely the date range is wrong or the customer is mistyped.

### "The AI Assistant says 'no key configured'"

Go to Settings → AI → API Keys and paste your Gemini API key. The key is encrypted with Windows DPAPI and stored locally — it never leaves the machine except when sending a question to Google. **You need a key from Google AI Studio (free tier is fine for evaluation).**

## 5. If all else fails

1. Open `TROUBLESHOOTING.md` (this file) and `LICENSE.md` for context.
2. Click **About → License** in the app and screenshot it. The customer id, edition, expiry, and machine fingerprint are all on one screen.
3. Open the crash dialog and click **COPY ERROR ID**. Paste it in your support email.
4. Open `%AppData%\ProGlassAutomation\logs\` and zip the last 3 days' worth of `app-*.log` plus `crash-latest.txt`. Attach the zip to the email.

A support request that contains the error ID + a screenshot of the About → License card + the log zip can usually be diagnosed in 10 minutes by your vendor.

## 6. Privacy

- The log files do **not** contain passwords (they are PBKDF2-hashed in `audit.db`, never written in plain text anywhere).
- They do **not** contain API secrets (the app's only secret is the AI API key, which is DPAPI-encrypted on disk and never logged).
- They do **not** contain the licence private key (the app does not have one — only the vendor's `LicenseGenerator` tool has it).
- They **do** contain the customer / business data the user was looking at when the error happened. If you are worried about PII, redact those lines before sending.
