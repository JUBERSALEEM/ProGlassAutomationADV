// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Security/Consent/AgentConsent.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE T)
//
// CONSENT as its own named module.
//
// Two separate switches, deliberately not one:
//   • LocalOnly            — the master switch. On by default.
//   • CloudConsentGiven    — an explicit, separate agreement.
//
// Turning Local-Only back ON clears consent. That matters: without it,
// a user who disabled privacy once, consented, then re-enabled privacy
// would silently be pre-approved the next time they disabled it. The
// clearing behaviour already lives in AgentConfigurationService; this
// module is the single place that READS the pair and answers the only
// question callers actually have.
//
// This is a thin, honest wrapper. It holds no state of its own so it can
// never disagree with the persisted settings file.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Configuration;
using System;
using System.Diagnostics;

namespace ProGlassAutomation.Agent.Security.Consent
{
    /// <summary>Answers "has the user agreed to cloud use, right now?"</summary>
    public static class AgentConsent
    {
        /// <summary>
        /// True only when Local-Only is OFF and consent has been given.
        /// Any failure reading settings returns false — the private
        /// posture is the safe one.
        /// </summary>
        public static bool CloudAllowed
        {
            get
            {
                try
                {
                    var s = AgentConfigurationService.Current;
                    return !s.LocalOnly && s.CloudConsentGiven;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AgentConsent] {ex.Message}");
                    return false;   // fail closed
                }
            }
        }

        /// <summary>True when the master privacy switch is on.</summary>
        public static bool LocalOnly
        {
            get
            {
                try { return AgentConfigurationService.Current.LocalOnly; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AgentConsent] {ex.Message}");
                    return true;    // fail closed
                }
            }
        }

        /// <summary>
        /// The reason cloud use is unavailable, or empty when it is
        /// allowed. Used so refusals name the actual blocker instead of a
        /// generic "not allowed".
        /// </summary>
        public static string BlockReason
        {
            get
            {
                if (LocalOnly)
                    return "Local-Only mode is on, so I cannot send anything to " +
                           "the internet. You can turn it off in " +
                           "Settings → AI & Agent.";

                if (!CloudAllowed)
                    return "Cloud use has not been consented to yet. Enable it in " +
                           "Settings → AI & Agent before I can use an online " +
                           "provider.";

                return "";
            }
        }
    }
}
