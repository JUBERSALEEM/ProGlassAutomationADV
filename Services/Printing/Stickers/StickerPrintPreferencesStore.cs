using System;
using System.IO;
using System.Text.Json;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    /// <summary>
    /// Keeps printer-specific stock choices on this Windows user profile. Sticker
    /// stock/calibration is workstation-local and must not move the shared data root.
    /// </summary>
    public static class StickerPrintPreferencesStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static string SettingsPath
        {
            get
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(local))
                    local = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(local))
                    local = AppContext.BaseDirectory;
                return Path.Combine(local, "ProGlassAutomation", "StickerPrinter", "preferences.json");
            }
        }

        public static StickerPrintPreferences Load()
        {
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path)) return new StickerPrintPreferences();
                var loaded = JsonSerializer.Deserialize<StickerPrintPreferences>(
                    File.ReadAllText(path), JsonOptions) ?? new StickerPrintPreferences();
                if (!double.IsFinite(loaded.LabelWidthMm) || loaded.LabelWidthMm < 15 || loaded.LabelWidthMm > 500)
                    loaded.LabelWidthMm = 100;
                if (!double.IsFinite(loaded.LabelHeightMm) || loaded.LabelHeightMm < 15 || loaded.LabelHeightMm > 500)
                    loaded.LabelHeightMm = 75;
                if (!double.IsFinite(loaded.MarginMm) || loaded.MarginMm < 0 || loaded.MarginMm > 25
                    || loaded.MarginMm * 2 >= Math.Min(loaded.LabelWidthMm, loaded.LabelHeightMm))
                    loaded.MarginMm = 2.5;
                if (!double.IsFinite(loaded.FontSizePt) || loaded.FontSizePt < 5 || loaded.FontSizePt > 30)
                    loaded.FontSizePt = 9;
                return loaded;
            }
            catch (Exception ex)
            {
                AppLogService.Warning("[StickerPrinter] Could not load local preferences: " + ex.Message);
                return new StickerPrintPreferences();
            }
        }

        public static bool TrySave(StickerPrintPreferences preferences, out string error)
        {
            error = string.Empty;
            if (preferences == null)
            {
                error = "No sticker preferences were supplied.";
                return false;
            }

            try
            {
                string path = SettingsPath;
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonSerializer.Serialize(preferences, JsonOptions));
                return true;
            }
            catch (Exception ex)
            {
                error = "Could not save sticker preferences: " + ex.Message;
                AppLogService.Warning("[StickerPrinter] " + error);
                return false;
            }
        }
    }
}
