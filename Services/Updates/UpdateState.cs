// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Updates/UpdateState.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — OTA update lifecycle states (spec §7)
//
// The full OTA experience is driven by this state machine. The UI binds
// to UpdateService.State / StateDetail / DownloadPercent through
// SettingsViewModel; Velopack (download/apply/restart provider) plugs
// into the SAME states later without any UI change.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Services.Updates
{
    /// <summary>OTA update lifecycle states.</summary>
    public enum UpdateState
    {
        /// <summary>Nothing happening yet.</summary>
        Idle,

        /// <summary>Contacting the release server.</summary>
        Checking,

        /// <summary>Running the newest version.</summary>
        UpToDate,

        /// <summary>A newer release exists and can be downloaded.</summary>
        UpdateAvailable,

        /// <summary>Download in progress (see DownloadPercent).</summary>
        Downloading,

        /// <summary>Download finished; ready to install.</summary>
        DownloadCompleted,

        /// <summary>Applying the update (Velopack provider phase).</summary>
        Installing,

        /// <summary>Something went wrong — StateDetail carries the reason.</summary>
        Failed,

        /// <summary>No internet connection.</summary>
        Offline,

        /// <summary>User cancelled the operation.</summary>
        Cancelled
    }
}
