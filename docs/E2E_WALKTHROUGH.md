# ProGlass Automation — End-to-End Walk-Through

**Purpose.** The single most important amber item in
[`RELEASE_GATE.md`](RELEASE_GATE.md) is *"the runtime end-to-end
click-through of the Inquiry → PI → JO → DN → TI → STMT chain on a
clean install has not been done."* This document is the script a
tester (you, a contractor, or a future AI assistant) can follow
on a clean Windows VM to close that item. Every step has a
**Pass** criterion so a non-developer can run it and produce a
go/no-go answer.

**Time budget.** 60–90 minutes on a clean install with the seed
data loaded. Add 30 minutes if you also walk the licence / crash /
update paths.

**What you need.**
- A Windows 10/11 VM with no prior `%AppData%\ProGlassAutomation\`
  folder. (Or rename the existing one so the app boots into first-
  run mode.)
- A clean `Setup.exe` from the latest CI run, or `tools/
  build-installer.ps1` output.
- A `.lic` file from the LicenseGenerator tool (or pick "Continue
  Read-Only" if you only want the UI flow).
- A pen + the printed version of this checklist, or a fresh
  spreadsheet column for `Pass / Fail / Notes`.

---

## 0. Pre-flight (5 min)

1. **Clean profile.** Confirm `%AppData%\ProGlassAutomation\` is
   empty or absent. If the app has been run before on this VM,
   rename the folder to `%AppData%\ProGlassAutomation.previous\`
   — do **not** delete it until the walk-through is over.
2. **Install.** Run `Setup.exe`. Accept the default install
   path (`%ProgramFiles%\ProGlassAutomation`) and let it finish.
3. **Launch.** Start the app. **Expected:** the welcome dialog
   appears in the centre of the screen.

   **Pass:** welcome dialog visible, no `Operation` log entries
   with level `Error` or `Critical` in `%AppData%\ProGlassAutomation\logs\`.

4. **Activate / skip.** Either drop a `.lic` into
   `%AppData%\ProGlassAutomation\` and click **ACTIVATE** on the
   welcome, or click **CONTINUE READ-ONLY**.

   **Pass:** welcome dialog closes, the Splash view appears
   within 3 seconds, no log errors.

---

## 1. Sign-in (2 min)

5. **Terms gate.** Splash shows the terms panel first time.

   **Pass:** terms text is visible, both **Accept** and **Decline**
   are clickable. Click **Accept**.

6. **Login.** Splash shows the seeded credentials panel.

   **Pass:** `admin / admin` (or whatever the dev seed has) signs
   in. The Main Window appears within 3 seconds, no log errors.

---

## 2. Seed data (5 min, optional but recommended)

7. **Load sample data.** Open **Settings → Data & Backup** →
   **LOAD SAMPLE DATA**. The button is disabled if the main
   database is non-empty, which is the correct behaviour for a
   production install.

   **Pass:** 6 sheets, 3 proformas, 2 job orders, 2 delivery
   notes, 3 tax invoices are now in the system. The Dashboard
   numbers reflect them within 5 seconds.

---

## 3. Module walk-through (45 min, the heart of the gate)

The order is **Inquiry → Proforma Invoice → Job Order → Delivery
Note → Tax Invoice → Statement**. At every step, confirm the
**Pass** criterion before moving on. If a step fails, **stop** and
write down:
- The exact step number.
- The error message verbatim.
- The Error ID (visible in the **About → Recent crash reports**
  card, format `ERR-XXXXXX`).
- Whether the app is still usable or has frozen.

### 3.1 Inquiry (5 min)

8. **Create inquiry.** Dashboard → **Inquiry** → **NEW**. Fill
   customer (seeded: `Acme Glass`), project, glass spec
   (seeded: `6mm Clear Float`). Save.

   **Pass:** inquiry appears in the list with a generated number
   (`INQ-2026-…` or similar). The new inquiry row can be opened
   by double-click. The detail view loads without log errors.

9. **Edit + delete.** Open the inquiry, change the spec to
   `8mm Tinted Float`, save. Then click **DELETE** on the row
   and confirm.

   **Pass:** edit saves, delete prompts a confirmation dialog,
   confirming removes the row from the list, no log errors.

### 3.2 Proforma Invoice (8 min)

10. **Create from inquiry.** Open the inquiry from §3.1 (re-create
    it if you deleted it). Right-click → **CREATE PROFORMA**.

    **Pass:** a new PI is created and pre-fills customer, project
    and lines from the inquiry. PI number is generated, status
    starts at `Draft`.

11. **Add line + price.** Open the PI, add a second line
    (`5mm Reflective`, 12 sqm), manually set a unit price.
    Save. The PI total must equal `qty * price + tax` for each
    line, summed.

    **Pass:** line total and grand total match. The **PDF
    EXPORT** button produces a PDF at `%UserProfile%\Documents\
    ProGlass\Proformas\…`.

12. **Confirm.** Click **CONFIRM** on the PI. Status changes to
    `Confirmed`. **Pass:** the status badge updates, an audit
    entry is written to the **Logs & Activity** page.

13. **Lock.** Click **LOCK**. Status changes to `Locked`.

    **Pass:** the **EDIT** button on the toolbar is disabled
    after lock. The **EDIT** / **DELETE** commands in the right-
    click menu are greyed out. Opening the PI for read still
    works.

### 3.3 Job Order (8 min)

14. **Create from PI.** From the locked PI, right-click →
    **CREATE JOB ORDER**.

    **Pass:** a new JO is created and pre-fills customer, lines,
    and the PI reference. JO number generated.

15. **Production stage.** Open the JO, click **START CUTTING**.
    Status changes to `In Production`. The **Production %**
    progress bar appears.

    **Pass:** progress is 0%, status badge updated, no log errors.

16. **Mark complete.** Click **MARK COMPLETE**. Status changes
    to `Complete`.

    **Pass:** the **CREATE DELIVERY NOTE** action is now enabled
    in the right-click menu.

### 3.4 Delivery Note (8 min)

17. **Create from JO.** Right-click the completed JO →
    **CREATE DELIVERY NOTE**.

    **Pass:** a new DN is created and pre-fills from the JO. DN
    number generated, status `Draft`.

18. **Mark delivered.** Click **MARK DELIVERED**. Status
    changes to `Delivered`.

    **Pass:** the **CREATE TAX INVOICE** action is enabled.

### 3.5 Tax Invoice (8 min)

19. **Create from DN.** Right-click the delivered DN →
    **CREATE TAX INVOICE**.

    **Pass:** TI is created with customer, lines, tax from the
    DN. Status `Draft`.

20. **Post.** Click **POST**. Status changes to `Posted`.

    **Pass:** the **CREATE STATEMENT** action is enabled.

### 3.6 Statement (8 min)

21. **Create from TI.** Right-click the posted TI →
    **CREATE STATEMENT**. Or open the **Statement Generator**
    from the menu and pick the customer from the TI.

    **Pass:** the statement preview shows the TI as a row, with
    the correct running balance.

22. **Generate PDF.** Click **GENERATE PDF**.

    **Pass:** a PDF is produced at `%UserProfile%\Documents\
    ProGlass\Statements\…`. The PDF opens in the default viewer
    without a security warning.

---

## 4. Cross-cutting checks (10 min)

23. **Permission boundary.** While signed in as `admin`, open
    **Settings → Users** and add a user with the `Sales` role
    only. Sign out (the user switcher is on the title bar), sign
    in as that user. Try to **DELETE** the inquiry from §3.1.

    **Pass:** the **DELETE** action is not visible in the right-
    click menu, and a direct attempt via the keyboard shortcut
    (if any) produces a "Permission Denied" dialog, not a
    silent failure.

24. **Crash report path.** Trigger a controlled crash: in the
    **AI Assistant** panel, type
    `__throw new InvalidOperationException("e2e test")`
    and send. The agent should re-throw.

    **Pass:** the **Crash Report** dialog appears with the
    **RESTART**, **COPY ERROR ID**, **OPEN FOLDER**, **CLOSE**
    buttons. Clicking **COPY ERROR ID** places a string like
    `ERR-F24D2B` on the clipboard. Clicking **OPEN FOLDER**
    opens `%AppData%\ProGlassAutomation\logs\`.

25. **About → License.** Open the **About** page.

    **Pass:** the License card shows the edition, customer name,
    expiry date, and machine fingerprint. If a `.lic` was
    activated, the edition matches the payload; if running read-
    only, the badge says `Not Activated`.

26. **About → Recent crash reports.** Open the **About → Recent
    crash reports** card.

    **Pass:** the crash from §24 is listed as the most recent
    row with the right error ID. The **OPEN FOLDER** button
    opens the same folder as the dialog's.

27. **Settings → Data → Database Health.** Open **Settings →
    Data → Database Health** and click **REFRESH**.

    **Pass:** every database is listed with a non-zero row
    count, schema version `2.0.0.0`, and no **DRIFT** column
    entries. The license status row at the top shows the same
    status as the About card.

---

## 5. Shutdown + restart (5 min)

28. **Backup on close.** Close the app via the X button (not
    Alt+F4). Re-open it.

    **Pass:** a new backup set is created under `%AppData%\
    ProGlassAutomation\backups\automatic\` with a timestamp
    matching the close time. Visible from **Settings → Data &
    Backup** after a **REFRESH**.

29. **Clean exit.** Close the app again.

    **Pass:** no process is left in Task Manager, no `mscorlib`
    orphaned handle, no log entry at `CRITICAL` level on exit.

---

## 6. Result recording

Copy this table into a fresh sheet and fill in one row per step:

| Step | Pass / Fail | Error ID | Notes |
|------|-------------|----------|-------|
| 0.3 Launch | | | |
| 0.4 Activate | | | |
| 1.5 Terms | | | |
| 1.6 Login | | | |
| 2.7 Sample data | | | |
| 3.1.8 Inquiry create | | | |
| ... | | | |
| 5.29 Clean exit | | | |

**Overall verdict:**
- **All Pass** → the runtime end-to-end walk-through is green.
  Update `RELEASE_GATE.md` §10 with the date, the build number,
  and the tester's name.
- **Any Fail** → the gate stays amber for that step. The
  corresponding module's `Tests/` folder is the right place
  to add a regression test before the next fix.

---

## 7. After the walk-through

30. **Restore previous profile** (only if you backed it up in
    step 0.1): move `%AppData%\ProGlassAutomation.previous\`
    back to `%AppData%\ProGlassAutomation\`. Otherwise leave the
    test install in place; the next tester can re-use it.

31. **File the result.** Attach the filled-in table to a
    `Tests/E2E/2026-MM-DD-…md` file in the repo (the folder does
    not exist yet; create it on first use) so the walk-through
    result is version-controlled and reviewable.
