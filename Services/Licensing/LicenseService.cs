using System.ComponentModel;
using System.IO;
using ProGlass.Licensing;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Services.Licensing;

/// <summary>
/// Runtime licensing state for ProGlass Automation.
///
/// 2026 WORKFLOW SIMPLIFICATION — the licensing model now has three
/// cooperating tracks, all behind the same secured verification:
///
///   1. SIMPLE LICENSE KEY (new, primary) — the vendor generates a signed
///      .proglasslicense key with the License Generator and sends the
///      single-line PGKEY1 text block to the customer, who pastes it in
///      Settings → License. No request, no reference, no PC-ID round-trip.
///      InstallKeyText / InstallKeyFile verify the vendor RSA-2048-PSS
///      signature, keyId, product, dates, version policy and (optional)
///      machine binding before storing the key DPAPI-protected.
///
///   2. OFFLINE ACTIVATION REQUEST (kept for advanced cases) — the
///      customer exports a request bound to this PC's fingerprint and the
///      vendor returns a signed response. The old "vendor license
///      reference" requirement is GONE: a request can be generated with
///      no reference at all.
///
///   3. AUTOMATIC TRIAL (restored) — on a fresh install the app issues a
///      30-day evaluation trial through the tamper-evident TrialLedger
///      (see AutoTrialManager). A valid signed license ALWAYS wins; when
///      a key/response is removed or expires while the machine's trial
///      window is still open, the app falls back to the trial. When the
///      window has passed the app runs in the existing read-only mode.
///
/// Public surface is intentionally kept stable for the rest of the app:
/// - Current
/// - Initialize / Reload
/// - InstallFile / InstallText / InstallKeyText / InstallKeyFile
/// - FeatureAllowed
/// - IsReadOnlyMode
/// </summary>
public static partial class LicenseService
{
}
