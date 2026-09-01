// ═══════════════════════════════════════════════════════════════════════
// FILE: AiChatViewModel.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.AI.AIContext;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIConnectivity;

namespace ProGlassAutomation.ViewModels.AI
{
        public partial class AiChatViewModel : ViewModelBase
    {
            private static void RememberExchange(
            string userMsg, string aiText, EntityReference? entityRef)
        {
            _ = ProGlassAutomation.Services.AI.AIMemory
                .AiMemoryService.RememberAsync(
                    userMsg, aiText, entityRef?.EntityId ?? "")
                .ContinueWith(t =>
                {
                    if (t.Exception != null)
                        AppLogService.Debug(
                            "[AiChatVM] Memory save failed: " +
                            t.Exception.GetBaseException().Message);
                }, TaskScheduler.Default);
        }

        /// <summary>v3 JARVIS: friendly first-open greeting.</summary>
        private void AddGreeting()
        {
            var screen = AiScreenContext.Describe();

            var text =
                "👋 Hello! I'm ProGlass AI — your factory-floor assistant.\n\n" +
                "Ask me anything: glass types, pricing math, SQM/VAT " +
                "calculations, your invoices, job orders, stock levels, " +
                "delivery status…\n\n" +
                (string.IsNullOrEmpty(screen)
                    ? ""
                    : "I can see which screen you're on, so you can ask " +
                      "\"help me with this screen\" anytime.\n\n") +
                "🎤 Tap the mic to talk — I'll answer instantly.";

            Messages.Add(new ChatMessage
            {
                Text = text,
                IsUser = false,
                ProviderName = ActiveProviderName,
                ProviderIcon = ActiveProviderIcon
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  ENTITY DETECTION — Tier 2 RAG
        // ══════════════════════════════════════════════════════════════

        private static EntityReference? DetectEntityReference(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return null;

            var ti = _tiPattern.Match(msg);
            if (ti.Success)
                return new EntityReference("TI",
                    ti.Groups[1].Value.ToUpperInvariant());

            var pi = _piPattern.Match(msg);
            if (pi.Success)
                return new EntityReference("PI",
                    pi.Groups[1].Value.ToUpperInvariant());

            var jo = _joPattern.Match(msg);
            if (jo.Success)
                return new EntityReference("JO",
                    jo.Groups[1].Value.ToUpperInvariant());

            var dn = _dnPattern.Match(msg);
            if (dn.Success)
                return new EntityReference("DN",
                    dn.Groups[1].Value.ToUpperInvariant());

            return null;
        }

        private readonly struct EntityReference
        {
            public string EntityType { get; }
            public string EntityId { get; }

            public EntityReference(string entityType, string entityId)
            {
                EntityType = entityType;
                EntityId = entityId;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Static App event handler (named, auditable). Refreshes the
        /// provider identity shown in the chat header.
        /// </summary>
        private void OnAiProviderChanged(object? sender, EventArgs e)
        {
            RunOnUi(() =>
            {
                OnPropertyChanged(nameof(ActiveProviderName));
                OnPropertyChanged(nameof(ActiveProviderIcon));
                OnPropertyChanged(nameof(ActiveProviderSubtitle));
            });
        }

        private static void RunOnUi(Action action)
        {
            if (action == null) return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.InvokeAsync(action);
        }

        private void AddError(string text, string providerName = null)
        {
            Messages.Add(new ChatMessage
            {
                Text = text,
                IsUser = false,
                IsError = true,
                ProviderName = providerName ?? string.Empty
            });
        }

        private void StopAllVoice()
        {
            if (_stt != null && _stt.IsListening)
            {
                _stt.StopListening();
                IsListening = false;
            }
            _tts?.Stop();
            MicAudioLevel = 0f;
            IsProcessingVoice = false;
        }

        // ══════════════════════════════════════════════════════════════
        //  SYSTEM PROMPT
        // ══════════════════════════════════════════════════════════════

        private static string BuildSystemPrompt()
        {
            // v3 GLOBAL INTEGRATION: the assistant knows which screen
            // the user is looking at (updated by MainViewModel on every
            // navigation) — JARVIS-style contextual awareness.
            var screen = AiScreenContext.Describe();

            return
            "You are ProGlass AI — an expert assistant for glass " +
            "manufacturing in the UAE. You help users of the " +
            "ProGlass Automation ERP system.\n\n" +

            "YOUR KNOWLEDGE AREAS:\n" +
            "• Glass types: clear, tinted, frosted, tempered, " +
            "laminated, insulated (DGU/IGU), low-E coated, " +
            "reflective, patterned\n" +
            "• Processing: cutting, holes/cutouts, polish, " +
            "edge deletion, mitring, tempering, pairing, " +
            "doubling/DGU assembly\n" +
            "• UAE market: AED currency, 5% VAT, FTA compliance, " +
            "delivery logistics\n" +
            "• App modules: Dashboard, Sheet Store, Proforma Invoice, " +
            "Job Order, Delivery Note, Tax Invoice, " +
            "Sales Report/Statements, AI Assistant\n" +
            "• Document chain: Proforma Invoice → Job Order → " +
            "Delivery Note → Tax Invoice → Account Statement\n\n" +

            "DOCUMENT ID FORMATS:\n" +
            "• PI-YYYY-NNN    = Proforma Invoice\n" +
            "• JOB-YYYY-NNN  = Job Order\n" +
            "• DN-YYYY-NNN-NN = Delivery Note\n" +
            "• MDN-YYYY-NNN  = Master Delivery Note\n" +
            "• INV-YYYY-NNN or INV-YYYY-NNN-A = Tax Invoice\n" +
            "• STMT-YYYY-NNN = Account Statement\n\n" +

            "STATUS VALUES:\n" +
            "• Proforma Invoice: Draft | Approved | Sent\n" +
            "• Job Order: Draft | Released | InProduction | " +
            "Completed | Delivered\n" +
            "• Delivery Note: Draft | Dispatched | Delivered | " +
            "Signed | Received\n" +
            "• Tax Invoice: Draft | Issued | Paid | PartiallyPaid | " +
            "Overdue | Cancelled | Void\n\n" +

            "GUIDELINES:\n" +
            "• Give concise practical answers (bullet points preferred)\n" +
            "• Use LIVE DATA and entity detail sections if present\n" +
            "• Answer with REAL numbers when data is provided\n" +
            "• If a record is not in the data, say so honestly\n" +
            "• Show calculations for SQM, VAT, pricing formulas\n" +
            "• Keep responses under 300 words unless asked for detail\n" +
            "• Format monetary values as AED with commas\n" +
            "• Be professional but friendly — factory floor assistant\n" +
            "• Voice responses: under 100 words, no markdown, " +
            "conversational tone" +
            (string.IsNullOrEmpty(screen)
                ? ""
                : "\n\n" + screen +
                  " If the user asks about \"this screen\" or " +
                  "\"this module\", they mean the module named above — " +
                  "guide them through it.");
        }
    }
}
