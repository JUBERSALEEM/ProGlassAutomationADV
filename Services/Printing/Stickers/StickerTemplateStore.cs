using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    /// <summary>
    /// Reads and writes portable .pgsticker layouts. Files contain only layout
    /// metadata and field keys; invoice/customer data is never exported with a template.
    /// </summary>
    public static class StickerTemplateStore
    {
        public const string FileExtension = ".pgsticker";
        public const string FileDialogFilter = "ProGlass sticker template (*.pgsticker)|*.pgsticker|JSON template (*.json)|*.json";
        private const long MaximumFileBytes = 1_000_000;

        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
        private static readonly Regex FieldTokenRegex = new(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string LibraryDirectory
        {
            get
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(local))
                    local = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(local)) local = AppContext.BaseDirectory;
                return Path.Combine(local, "ProGlassAutomation", "StickerPrinter", "Templates");
            }
        }

        public static IReadOnlyList<(StickerLayoutTemplate Template, string Path)> LoadLibrary()
        {
            var result = new List<(StickerLayoutTemplate Template, string Path)>();
            try
            {
                if (!Directory.Exists(LibraryDirectory)) return result;
                foreach (string path in Directory.EnumerateFiles(LibraryDirectory, "*" + FileExtension)
                             .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    if (TryLoadFile(path, out var template, out string error) && template != null)
                        result.Add((template, path));
                    else
                        AppLogService.Warning("[StickerTemplate] Skipped template '" + path + "': " + error);
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning("[StickerTemplate] Could not scan the local template library: " + ex.Message);
            }
            return result;
        }

        public static bool TryLoadFile(string path, out StickerLayoutTemplate? template, out string error)
        {
            template = null;
            error = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    error = "Choose an existing .pgsticker template file.";
                    return false;
                }
                var file = new FileInfo(path);
                if (file.Length <= 0 || file.Length > MaximumFileBytes)
                {
                    error = "Sticker template files must be between 1 byte and 1 MB.";
                    return false;
                }

                var parsed = JsonSerializer.Deserialize<StickerLayoutTemplate>(
                    File.ReadAllText(path), JsonOptions);
                if (parsed == null)
                {
                    error = "The template file is empty or is not a ProGlass sticker template.";
                    return false;
                }
                if (!TryValidate(parsed, out error)) return false;
                template = parsed;
                return true;
            }
            catch (JsonException ex)
            {
                error = "The template file is not valid ProGlass template JSON: " + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = "Could not read the sticker template: " + ex.Message;
                return false;
            }
        }

        public static bool TrySaveFile(string path, StickerLayoutTemplate template, out string error)
        {
            error = string.Empty;
            if (!TryValidate(template, out error)) return false;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Choose a file path for the sticker template.";
                return false;
            }

            try
            {
                string finalPath = EnsureExtension(path);
                string? directory = Path.GetDirectoryName(finalPath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                var copy = template.DeepClone();
                copy.FileFormat = StickerLayoutTemplate.CurrentFileFormat;
                copy.SchemaVersion = StickerLayoutTemplate.CurrentSchemaVersion;
                File.WriteAllText(finalPath, JsonSerializer.Serialize(copy, JsonOptions));
                return true;
            }
            catch (Exception ex)
            {
                error = "Could not save the sticker template: " + ex.Message;
                AppLogService.Warning("[StickerTemplate] " + error);
                return false;
            }
        }

        public static bool TrySaveToLibrary(StickerLayoutTemplate template, out string path, out string error)
        {
            path = string.Empty;
            error = string.Empty;
            if (template == null)
            {
                error = "No sticker template was supplied.";
                return false;
            }

            string safeName = SanitizeFileName(template.Name ?? string.Empty);
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "Custom sticker";

            try
            {
                Directory.CreateDirectory(LibraryDirectory);
                string candidate = Path.Combine(LibraryDirectory, safeName + FileExtension);
                int suffix = 2;
                while (File.Exists(candidate))
                {
                    // Re-saving the same saved layout updates its existing file.
                    // A different layout with the same display name gets its own
                    // file instead of silently overwriting somebody else's work.
                    if (TryLoadFile(candidate, out var existing, out _)
                        && existing != null
                        && !string.IsNullOrWhiteSpace(template.TemplateId)
                        && string.Equals(existing.TemplateId, template.TemplateId,
                            StringComparison.OrdinalIgnoreCase))
                        break;

                    candidate = Path.Combine(LibraryDirectory,
                        safeName + " (" + suffix.ToString(CultureInfo.InvariantCulture) + ")" + FileExtension);
                    suffix++;
                    if (suffix > 1000)
                    {
                        error = "Could not find an unused filename in the local sticker template library.";
                        return false;
                    }
                }

                if (!TrySaveFile(candidate, template, out error)) return false;
                path = candidate;
                return true;
            }
            catch (Exception ex)
            {
                error = "Could not save the sticker template to the local library: " + ex.Message;
                AppLogService.Warning("[StickerTemplate] " + error);
                return false;
            }
        }

        public static bool TryValidate(StickerLayoutTemplate? template, out string error)
        {
            error = string.Empty;
            if (template == null)
            {
                error = "No sticker template was supplied.";
                return false;
            }
            if (!string.Equals(template.FileFormat, StickerLayoutTemplate.CurrentFileFormat,
                    StringComparison.Ordinal))
            {
                error = "This is not a ProGlass sticker template file.";
                return false;
            }
            if (template.SchemaVersion != StickerLayoutTemplate.CurrentSchemaVersion)
            {
                error = "This sticker template version is not supported by this application.";
                return false;
            }
            if (!Enum.IsDefined(typeof(StickerTemplateKind), template.Kind))
            {
                error = "The template uses an unknown style family.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(template.Name) || template.Name.Length > 100)
            {
                error = "Template name must contain 1 to 100 characters.";
                return false;
            }
            if (!IsFiniteInRange(template.LabelWidthMm, 15, 500)
                || !IsFiniteInRange(template.LabelHeightMm,
                    StickerPrinterSpecifications.MinimumTemplateLengthMm,
                    StickerPrinterSpecifications.MaximumTemplateLengthMm))
            {
                error = "Template width must be between 15 and 500 mm, and label length must be between 12.7 and 991 mm.";
                return false;
            }
            if (!IsFiniteInRange(template.MarginMm, 0, 25)
                || template.MarginMm * 2 >= Math.Min(template.LabelWidthMm, template.LabelHeightMm))
            {
                error = "Template margin must leave a printable area.";
                return false;
            }
            if (!IsFiniteInRange(template.DefaultFontSizePt, 3, 72))
            {
                error = "Default font size must be between 3 and 72 pt.";
                return false;
            }
            if (template.Elements == null || template.Elements.Count > 100)
            {
                error = "A template can contain at most 100 elements.";
                return false;
            }
            if (!IsColor(template.Background))
            {
                error = "Template background must be a #RRGGBB or #AARRGGBB color.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var element in template.Elements)
            {
                if (element == null || string.IsNullOrWhiteSpace(element.Id) || !ids.Add(element.Id))
                {
                    error = "Every template element must have a unique id.";
                    return false;
                }
                if (!Enum.IsDefined(typeof(StickerTemplateElementKind), element.Kind))
                {
                    error = "A template element has an unknown object type.";
                    return false;
                }
                if (element.Name == null || element.Text == null || element.Prefix == null || element.Suffix == null
                    || element.Name.Length > 100 || element.Text.Length > 2000
                    || element.Prefix.Length > 200 || element.Suffix.Length > 200)
                {
                    error = "Element names, text and prefixes must stay within the supported length.";
                    return false;
                }
                if (element.Kind == StickerTemplateElementKind.Text
                    && !string.IsNullOrWhiteSpace(element.FieldKey)
                    && !StickerTemplateFieldCatalog.Contains(element.FieldKey))
                {
                    error = "The template contains an unknown data field: " + element.FieldKey;
                    return false;
                }
                foreach (Match token in FieldTokenRegex.Matches(element.Text))
                {
                    string key = token.Groups[1].Value;
                    if (!StickerTemplateFieldCatalog.Contains(key))
                    {
                        error = "The template text contains an unknown data token: " + key;
                        return false;
                    }
                }
                if (!IsFiniteInRange(element.Xmm, -500, 1000)
                    || !IsFiniteInRange(element.Ymm, -500, 1000)
                    || !IsFiniteInRange(element.WidthMm, 0.5, 500)
                    || !IsFiniteInRange(element.HeightMm, 0.5, 500)
                    || !IsFiniteInRange(element.FontSizePt, 3, 72)
                    || !IsFiniteInRange(element.BorderWidthMm, 0, 10)
                    || !IsFiniteInRange(element.PaddingMm, 0, 20)
                    || !IsFiniteInRange(element.RotationDegrees, -360, 360)
                    || !IsFiniteInRange(element.Opacity, 0, 1))
                {
                    error = "An element has an invalid position, size, font, rotation or opacity.";
                    return false;
                }
                if (!IsColor(element.Foreground) || !IsColor(element.Background)
                    || !IsColor(element.BorderColor))
                {
                    error = "Element colors must be #RRGGBB or #AARRGGBB values.";
                    return false;
                }
            }
            return true;
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        private static string EnsureExtension(string path)
        {
            if (string.Equals(Path.GetExtension(path), FileExtension, StringComparison.OrdinalIgnoreCase))
                return path;
            return path + FileExtension;
        }

        private static string SanitizeFileName(string name)
        {
            string safe = Regex.Replace(name.Trim(), "[<>:\"/\\|?*\\x00-\\x1F]", "_");
            return safe.Trim(' ', '.');
        }

        private static bool IsFiniteInRange(double value, double minimum, double maximum) =>
            double.IsFinite(value) && value >= minimum && value <= maximum;

        private static bool IsColor(string value) =>
            !string.IsNullOrWhiteSpace(value)
            && Regex.IsMatch(value, @"^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$");
    }
}
