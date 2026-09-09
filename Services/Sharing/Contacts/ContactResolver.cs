// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Sharing/Contacts/ContactResolver.cs
// PROJECT: ProGlassAutomation
// MODULE: Sharing Center — contact resolution (Stage 1)
//
// FROM = the ProGlass user: logged-in AppUser first (name + role +
// contact fields), falling back to the document's prepared-by snapshot
// (older docs, or a share opened by someone other than the preparer).
//
// TO = resolved from ERP data, never typed, except through the explicit
// manual fallback. Default preference order:
//     Customer Attention → Customer → Company Representative → Salesman
// The composer pre-selects the first candidate that can satisfy the
// chosen provider (phone for WhatsApp, email for Email) and always offers
// "Change contact".
//
// LIVE LOOKUPS, NO DUPLICATION. When the context carries only a customer
// NAME (reports), the resolver fills phone/attention from MasterData
// (FindClientByNameAsync). Nothing is copied into a sharing database —
// there is none, by design.
//
// Never throws: resolution failure yields fewer candidates, never a crash.
// The composer shows "no contact on file" with the manual fallback when
// the ERP genuinely knows nobody for the document.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Audit;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Sharing.Contacts
{
    /// <summary>FROM identity: the ProGlass user behind the share.</summary>
    public sealed class ShareSender
    {
        public string Name { get; }
        public string Role { get; }
        public string Mobile { get; }
        public string Email { get; }

        /// <summary>True when from the live user, false when from snapshot.</summary>
        public bool IsLiveUser { get; }

        public ShareSender(
            string name, string role = "", string mobile = "",
            string email = "", bool isLiveUser = false)
        {
            Name = (name ?? "").Trim();
            Role = (role ?? "").Trim();
            Mobile = (mobile ?? "").Trim();
            Email = (email ?? "").Trim();
            IsLiveUser = isLiveUser;
        }

        /// <summary>"Juber Saleem · Estimator" (role omitted when blank).</summary>
        public string DisplayLine =>
            string.IsNullOrWhiteSpace(Role) ? Name : $"{Name} · {Role}";
    }

    public interface IContactResolver
    {
        /// <summary>FROM for this share (live user → prepared-by fallback).</summary>
        ShareSender ResolveSender(ShareContext context);

        /// <summary>
        /// TO candidates in preference order, gaps filled from MasterData.
        /// </summary>
        Task<List<ShareRecipient>> ResolveRecipientsAsync(ShareContext context);
    }

    public sealed class ContactResolver : IContactResolver
    {
        public ShareSender ResolveSender(ShareContext context)
        {
            try
            {
                AppUser? live = AuditService.CurrentUser;
                if (live != null && !string.IsNullOrWhiteSpace(live.DisplayName))
                {
                    return new ShareSender(
                        live.DisplayName, live.Role,
                        live.Mobile, live.Email, isLiveUser: true);
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Sharing.Resolver] live user read failed: {ex.Message}");
            }

            // Fallback: whoever prepared the document (snapshot survives
            // user switches, deletions and older databases).
            return new ShareSender(
                string.IsNullOrWhiteSpace(context.PreparedByName)
                    ? "ProGlass" : context.PreparedByName,
                context.PreparedByRole,
                context.PreparedByMobile,
                context.PreparedByEmail,
                isLiveUser: false);
        }

        public async Task<List<ShareRecipient>> ResolveRecipientsAsync(
            ShareContext context)
        {
            var result = new List<ShareRecipient>();
            try
            {
                string customer = (context.CustomerName ?? "").Trim();
                string attention = (context.AttentionName ?? "").Trim();
                string phone = (context.CustomerPhone ?? "").Trim();

                // Gap-fill from MasterData when the context only names the
                // customer (reports) — a live read, never a copy.
                if (!string.IsNullOrWhiteSpace(customer)
                    && (string.IsNullOrWhiteSpace(attention)
                        || string.IsNullOrWhiteSpace(phone)))
                {
                    try
                    {
                        var client = App.MasterData == null ? null
                            : await App.MasterData
                                .FindClientByNameAsync(customer)
                                .ConfigureAwait(false);
                        if (client != null)
                        {
                            if (string.IsNullOrWhiteSpace(attention))
                                attention = (client.AttentionExecutiveName ?? "").Trim();
                            if (string.IsNullOrWhiteSpace(phone))
                                phone = (client.ClientContactPhone ?? "").Trim();
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Debug(
                            $"[Sharing.Resolver] client lookup failed: {ex.Message}");
                    }
                }

                // 1. Customer attention (name + customer phone).
                if (!string.IsNullOrWhiteSpace(attention)
                    && !string.IsNullOrWhiteSpace(customer))
                {
                    result.Add(new ShareRecipient(
                        ShareRecipientKind.CustomerAttention,
                        attention, customer, phone));
                }
                // 2. Customer primary.
                if (!string.IsNullOrWhiteSpace(customer))
                {
                    result.Add(new ShareRecipient(
                        ShareRecipientKind.Customer,
                        customer, customer, phone));
                }

                // 3. Own-company representative (internal forwarding).
                string rep = (context.CompanyRepresentative ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(rep))
                {
                    result.Add(new ShareRecipient(
                        ShareRecipientKind.CompanyRepresentative,
                        rep, context.CompanyName ?? "",
                        context.CompanyRepMobile ?? "",
                        context.CompanyEmail ?? ""));
                }

                // 4. Salesman on the document (name only — the ERP stores no
                // salesman phone; offered for completeness, skipped by
                // phone/email providers unless the operator supplies one).
                string salesman = (context.SalesmanName ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(salesman))
                {
                    result.Add(new ShareRecipient(
                        ShareRecipientKind.Salesman, salesman, customer));
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Sharing.Resolver] resolve failed: {ex.Message}");
            }

            return result;
        }
    }
}
