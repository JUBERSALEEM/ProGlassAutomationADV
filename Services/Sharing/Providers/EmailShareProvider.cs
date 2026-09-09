// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Sharing/Providers/EmailShareProvider.cs
// PROJECT: ProGlassAutomation
// MODULE: Sharing Center — email provider (Stage 1)
//
// LOCAL HANDOFF ONLY. Opens the OS default mail client with To/Subject/
// Body prefilled via mailto:. No SMTP, no credentials, no server, no paid
// API — the operator's own mail app sends.
//
// WHAT IT CANNOT DO (same honesty rule as WhatsApp)
// ──────────────
// mailto: carries NO attachment parameter. The provider stages the
// artifact(s), reveals the first in Explorer beside the compose window,
// and reports ExternalAppOpened — the draft was opened, nothing was sent.
// Large-file warning: most SMTP servers refuse >20–25 MB; the provider
// warns past the configured threshold but still opens the draft (the
// operator's server decides, not us).
//
// EMAIL AVAILABILITY REALITY. The ERP stores almost no emails (no customer
// email field; company email exists on SavedCompany). The composer
// therefore enables Email only when the chosen recipient HAS an address,
// and the manual fallback exists precisely for this provider.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Sharing.Providers
{
    public sealed class EmailShareProvider : ISharingProvider
    {
        public string Id => SharingProviderIds.Email;
        public string DisplayName => "Email";

        public ProviderCapabilities Capabilities { get; } = new()
        {
            SupportsText = true,
            SupportsSubject = true,
            RequiresRecipient = true,
            RequiresPhone = false,
            RequiresEmail = true,
            AttachesFile = false, // mailto: has no attachment parameter
            SupportsMultipleAttachments = true, // staged + listed; user attaches
            CompletesInProcess = false
        };

        /// <summary>Warn past this total (bytes); typical SMTP ceiling.</summary>
        private readonly long _largeTotalBytes;

        public EmailShareProvider(long largeTotalBytes = 20L * 1024 * 1024)
        {
            _largeTotalBytes = largeTotalBytes > 0 ? largeTotalBytes
                : 20L * 1024 * 1024;
        }

        public string Validate(ShareExecutionRequest request)
        {
            if (request == null) return "Nothing to share.";
            if (request.Artifacts == null || request.Artifacts.Count == 0)
                return "No file was generated to share.";
            if (request.CancellationToken.IsCancellationRequested)
                return "Share cancelled.";

            if (request.Recipient == null)
                return "Choose who should receive this document.";
            if (!IsPlausibleEmail(request.Recipient.Email))
                return string.IsNullOrWhiteSpace(request.Recipient.Email)
                    ? $"No email address on file for {request.Recipient.Name}. " +
                      "Pick another contact or enter one manually."
                    : $"The email address for {request.Recipient.Name} does not look valid.";

            return "";
        }

        public async Task<ShareOutcome> ShareAsync(ShareExecutionRequest request)
        {
            try
            {
                string invalid = Validate(request);
                if (invalid.Length > 0)
                    return ShareOutcome.Failed(Id, invalid, "validation: " + invalid);
                if (request.CancellationToken.IsCancellationRequested)
                    return ShareOutcome.Cancelled(Id);

                string folder = ShareTempFiles.CreateShareFolder();
                if (folder.Length == 0)
                    return ShareOutcome.Failed(Id,
                        "Could not prepare the attachment (temporary folder unavailable).",
                        "temp folder unavailable");

                var staged = request.Artifacts
                    .Select(a => ShareTempFiles.StageCopy(a, folder))
                    .Where(p => p.Length > 0)
                    .ToList();
                if (staged.Count == 0)
                    return ShareOutcome.Failed(Id,
                        "Could not prepare the attachment.",
                        "stage copy failed");

                if (request.CancellationToken.IsCancellationRequested)
                    return ShareOutcome.Cancelled(Id);

                string to = request.Recipient!.Email.Trim();
                string subject = string.IsNullOrWhiteSpace(request.Subject)
                    ? $"{request.Context.DisplayTitle}".Trim()
                    : request.Subject.Trim();
                // The address stays literal (standard mailto: form); only the
                // query parts are escaped.
                string url = "mailto:" + to
                    + "?subject=" + Uri.EscapeDataString(subject)
                    + "&body=" + Uri.EscapeDataString(request.Message ?? "");

                var launched = await Task.Run(() =>
                    ProcessStartGuard.TryOpenUrl(url)).ConfigureAwait(false);
                if (!launched.IsSuccess)
                    return ShareOutcome.Failed(Id,
                        "Email could not be opened. " +
                        (launched.Error.Length > 0 ? launched.Error
                            : "Is a default mail app configured?"),
                        "launch failed: " + launched.Error);

                try
                {
                    ProcessStartGuard.TryOpenExplorerSelect(staged[0]);
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[Sharing.Email] reveal failed: {ex.Message}");
                }

                string files = string.Join(", ",
                    request.Artifacts.Select(a => a.FileName));
                long total = request.Artifacts.Sum(a => a.FileSizeBytes);
                string followUp =
                    $"Attach {files} from the opened folder into the email — " +
                    "your mail app was opened with a draft, nothing was sent automatically.";
                if (total > _largeTotalBytes)
                    followUp += $" Note: the attachment totals {ShareableArtifact.FormatSize(total)}, " +
                                "which many mail servers refuse — consider sharing a smaller file.";

                return ShareOutcome.ExternalAppOpened(
                    Id,
                    $"Opened email draft to {request.Recipient.Name}.",
                    followUp,
                    $"to {request.Recipient.AuditIdentity}; files: {files}");
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Sharing.Email] failed: {ex.Message}");
                return ShareOutcome.Failed(Id,
                    $"Email share failed: {ex.Message}", "exception");
            }
        }

        /// <summary>
        /// Plausibility only (one @, dot after @, no spaces) — the mail app
        /// is the real validator; we only stop obvious garbage.
        /// </summary>
        public static bool IsPlausibleEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            string e = email.Trim();
            if (e.Contains(' ', StringComparison.Ordinal)) return false;
            int at = e.IndexOf('@');
            if (at <= 0 || at != e.LastIndexOf('@')) return false;
            int dot = e.IndexOf('.', at);
            return dot > at + 1 && dot < e.Length - 1;
        }
    }
}
