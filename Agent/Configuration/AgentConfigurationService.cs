// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Configuration/AgentConfigurationService.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// agent-settings.json — the privacy posture of the agent.
//
// DEFAULTS MATTER MOST HERE:
//   LocalOnly = TRUE by default. A fresh install cannot talk to the
//   internet until the user turns Local-Only off AND separately gives
//   cloud consent. Two switches, not one, because "I enabled a feature"
//   should never silently mean "I agreed to send my data somewhere".
//
// Turning Local-Only back ON clears the consent flag: re-enabling cloud
// later requires consenting again, deliberately.
// ═══════════════════════════════════════════════════════════════════════

using System;
using ProGlassAutomation.Services.Logging;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ProGlassAutomation.Agent.Configuration
{
    /// <summary>Serialized agent settings.</summary>
    public sealed class AgentSettings
    {
        /// <summary>When true the agent never contacts the network.</summary>
        public bool LocalOnly { get; set; } = true;

        /// <summary>Explicit, separate consent to use a cloud provider.</summary>
        public bool CloudConsentGiven { get; set; }

        /// <summary>Maximum characters allowed in one outbound payload.</summary>
        public int ExternalPayloadCap { get; set; } = 4000;

        /// <summary>Selected cloud provider key: gemini | groq | cohere.</summary>
        public string CloudProvider { get; set; } = "gemini";

        /// <summary>
        /// Catalog id of the user-installed local model (see
        /// Agent/Models/LocalModelCatalog). Empty = never installed.
        /// </summary>
        public string LocalModelId { get; set; } = "";

        /// <summary>
        /// When true AND a local model is installed, the local chain uses
        /// the LLM before the curated knowledge base. Defaults false:
        /// knowledge-base answering keeps working on its own unless the
        /// user opts into the model.
        /// </summary>
        public bool LocalModelEnabled { get; set; }
    }

    /// <summary>Loads and saves agent-settings.json. Never throws.</summary>
    public static class AgentConfigurationService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static readonly object Gate = new();
        private static AgentSettings? _cached;

        /// <summary>%AppData%/ProGlassAutomation/Agent/agent-settings.json</summary>
        public static string SettingsFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation",
            "Agent",
            "agent-settings.json");

        /// <summary>
        /// Current settings. A missing or corrupt file yields the SAFE
        /// defaults (local-only, no consent) rather than an open posture.
        /// </summary>
        public static AgentSettings Current
        {
            get
            {
                lock (Gate)
                {
                    if (_cached != null) return _cached;

                    try
                    {
                        if (File.Exists(SettingsFilePath))
                        {
                            var json = File.ReadAllText(SettingsFilePath);
                            _cached = JsonSerializer.Deserialize<AgentSettings>(
                                json, JsonOptions) ?? new AgentSettings();
                        }
                        else
                        {
                            _cached = new AgentSettings();
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Debug(
                            $"[AgentConfig] Load failed, using safe defaults: {ex.Message}");
                        _cached = new AgentSettings();
                    }

                    return _cached;
                }
            }
        }

        /// <summary>Persists the current settings. Never throws.</summary>
        public static void Save()
        {
            lock (Gate)
            {
                try
                {
                    var folder = Path.GetDirectoryName(SettingsFilePath);
                    if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    File.WriteAllText(
                        SettingsFilePath,
                        JsonSerializer.Serialize(Current, JsonOptions));
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[AgentConfig] Save failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Switches Local-Only mode. Turning it ON also CLEARS cloud
        /// consent, so going back to cloud requires consenting again.
        /// The change is audited by the caller's audit service.
        /// </summary>
        public static void SetLocalOnly(bool localOnly)
        {
            lock (Gate)
            {
                var settings = Current;
                settings.LocalOnly = localOnly;

                if (localOnly)
                    settings.CloudConsentGiven = false;
            }

            Save();
        }

        /// <summary>Records explicit cloud consent.</summary>
        public static void SetCloudConsent(bool consent)
        {
            lock (Gate) Current.CloudConsentGiven = consent;
            Save();
        }

        /// <summary>Sets the preferred cloud provider key.</summary>
        public static void SetCloudProvider(string provider)
        {
            lock (Gate)
                Current.CloudProvider =
                    string.IsNullOrWhiteSpace(provider) ? "gemini" : provider.Trim();
            Save();
        }

        /// <summary>
        /// Records which local model is installed/active. Persisted like
        /// the privacy posture: the choice must survive a restart exactly
        /// as the user left it.
        /// </summary>
        public static void SetLocalModel(string modelId, bool enabled)
        {
            lock (Gate)
            {
                Current.LocalModelId = (modelId ?? "").Trim();
                Current.LocalModelEnabled = enabled;
            }
            Save();
        }

        /// <summary>Test seam — drops the cache so the file is re-read.</summary>
        public static void Invalidate()
        {
            lock (Gate) _cached = null;
        }
    }
}
