// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Sharing/Providers/WhatsAppShareProvider.cs
// PROJECT: ProGlassAutomation
// MODULE: Sharing Center — WhatsApp provider (Stage 1)
//
// LEGITIMATE HANDOFF ONLY. This opens the device's WhatsApp (app or web,
// whichever the OS routes wa.me to) on the recipient's chat with the
// message prefilled. No scraping, no DOM automation, no session, no paid
// API, no third-party upload.
//
// WHAT IT CANNOT DO (stated so nobody "fixes" it later)
// ──────────────
// wa.me carries TEXT ONLY — no attachment parameter exists. The provider
// therefore stages the artifact(s) in the share temp folder, REVEALS the
// first in Explorer beside the chat, and tells the operator exactly that.
// The outcome is ExternalAppOpened — never "Sent". Delivery/read status
// is unknowable and never claimed.
//
// FROM/TO HONESTY. FROM is the ProGlass user identity shown in OUR
// composer for context; the WhatsApp account that actually sends is
// whichever is active on the device, and the UI says so.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Sharing.Contacts;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Sharing.Providers
{
    public sealed class WhatsAppShareProvider : ISharingProvider
    {
        public string Id => SharingProviderIds.WhatsApp;
        public string DisplayName => "WhatsApp";

        public ProviderCapabilities Capabilities { get; } = new()
        {
            SupportsText = true,
            SupportsSubject = false,
            RequiresRecipient = true,
            RequiresPhone = true,
            RequiresEmail = false,
            AttachesFile = false, // wa.me is text-only; file is revealed beside
            SupportsMultipleAttachments = true, // staged + listed; user attaches
            CompletesInProcess = false
        };

        private readonly string _defaultCountryCode;

        public WhatsAppShareProvider(string defaultCountryCode = "971")
        {
            _defaultCountryCode = string.IsNullOrWhiteSpace(defaultCountryCode)
                ? "971" : defaultCountryCode.Trim();
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
            if (string.IsNullOrWhiteSpace(request.Recipient.Phone))
                return $"No phone number on file for {request.Recipient.Name}. " +
                       "Pick another contact or enter one manually.";

            string digits = PhoneNumbers.NormalizeValidated(
                request.Recipient.Phone, _defaultCountryCode);
            if (digits.Length == 0)
                return $"The phone number for {request.Recipient.Name} is not usable. " +
                       "Check it and try again.";

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

                string digits = PhoneNumbers.NormalizeValidated(
                    request.Recipient!.Phone, _defaultCountryCode);

                // Stage copies (originals may live outside allowed roots;
                // the staged folder is always revealable).
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

                string text = request.Message ?? "";
                if (text.Length == 0)
                    text = $"Please find attached: {request.Artifacts[0].FileName}";
                string url = "https://wa.me/" + digits
                    + "?text=" + Uri.EscapeDataString(text);

                var launched = await Task.Run(() =>
                    ProcessStartGuard.TryOpenUrl(url)).ConfigureAwait(false);
                if (!launched.IsSuccess)
                    return ShareOutcome.Failed(Id,
                        "WhatsApp could not be opened. " +
                        (launched.Error.Length > 0 ? launched.Error
                            : "Is a browser or WhatsApp installed?"),
                        "launch failed: " + launched.Error);

                // Reveal the staged file(s) beside the chat so the operator
                // drags them in. Its failure degrades, never fails, the share.
                try
                {
                    ProcessStartGuard.TryOpenExplorerSelect(staged[0]);
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[Sharing.WhatsApp] reveal failed: {ex.Message}");
                }

                string files = string.Join(", ",
                    request.Artifacts.Select(a => a.FileName));
                return ShareOutcome.ExternalAppOpened(
                    Id,
                    $"Opened WhatsApp chat with {request.Recipient.Name}.",
                    $"Attach {files} from the opened folder into the chat — " +
                    "WhatsApp was opened with your message, nothing was sent automatically.",
                    $"to {request.Recipient.AuditIdentity}; files: {files}");
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Sharing.WhatsApp] failed: {ex.Message}");
                return ShareOutcome.Failed(Id,
                    $"WhatsApp share failed: {ex.Message}", "exception");
            }
        }
    }
}
