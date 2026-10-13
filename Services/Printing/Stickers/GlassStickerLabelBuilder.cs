using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Production;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    /// <summary>
    /// Maps only confirmed source data into print-ready sticker rows. The Step 5
    /// path delegates pane expansion to ProductionDistributionEngine so sticker
    /// routing cannot drift from the factory's SGU/DGU/LAM/DGU+LAM rules.
    /// </summary>
    public static class GlassStickerLabelBuilder
    {
        public static IReadOnlyList<GlassStickerLabel> FromStep5(
            IReadOnlyList<SpecGroupSnapshot>? specGroups,
            ProformaInvoiceSnapshot? sourceInvoice)
        {
            if (specGroups == null || specGroups.Count == 0)
                return Array.Empty<GlassStickerLabel>();

            var invoice = new ProformaInvoiceSnapshot
            {
                Company = sourceInvoice?.Company ?? new CompanySnapshot(),
                Client = sourceInvoice?.Client ?? new ClientSnapshot(),
                Project = sourceInvoice?.Project ?? new ProjectSnapshot(),
                SpecGroups = specGroups.Where(g => g != null).ToList()
            };

            string orderId = invoice.Company?.ProformaId?.Trim() ?? string.Empty;
            var plan = ProductionDistributionEngine.Distribute(orderId, invoice);
            var groupsByNumber = invoice.SpecGroups
                .Where(g => g != null)
                .GroupBy(g => g.GroupNumber)
                .ToDictionary(g => g.Key, g => g.First());
            var output = new List<GlassStickerLabel>();
            var company = StickerCompanyProfile.FromSnapshot(invoice.Company);
            string reference = BuildReference(invoice);

            foreach (var item in plan.Items.Where(i => i != null
                         && i.Stage == ProductionStage.Cutting
                         && i.WidthMm > 0 && i.HeightMm > 0 && i.Qty > 0))
            {
                if (!groupsByNumber.TryGetValue(item.SpecGroupNumber, out var group))
                    continue;

                string buildType = ProductionDistributionEngine.ResolveBuildType(group, invoice);
                var occurrence = ProductionDistributionEngine
                    .ResolvePaneOccurrences(buildType, group)
                    .FirstOrDefault(o => string.Equals(
                        o.Position, item.Position, StringComparison.OrdinalIgnoreCase));

                string color = occurrence?.Pane?.Color ?? ColorForPosition(group, item.Position);
                string glassName = !string.IsNullOrWhiteSpace(occurrence?.Composition)
                    ? occurrence!.Composition
                    : !string.IsNullOrWhiteSpace(item.SpecName)
                        ? item.SpecName
                        : !string.IsNullOrWhiteSpace(group.SelectedSpecName)
                            ? group.SelectedSpecName
                            : item.GlassRef;
                glassName = ComposeGlassName(glassName, color, occurrence?.ThicknessMm ?? 0);

                output.Add(new GlassStickerLabel
                {
                    Company = company,
                    OrderNumber = orderId,
                    ProformaNumber = orderId,
                    CustomerCompanyName = invoice.Client?.CustomerName?.Trim() ?? string.Empty,
                    GlassName = glassName,
                    GlassReference = item.GlassRef ?? string.Empty,
                    WidthMm = item.WidthMm,
                    HeightMm = item.HeightMm,
                    Quantity = item.Qty,
                    Reference = reference,
                    PanePosition = item.Position ?? string.Empty,
                    Source = "Proforma Invoice Step 5",
                    SpecGroupNumber = item.SpecGroupNumber,
                    SerialNumber = item.SerialNumber
                });
            }

            return output;
        }

        public static IReadOnlyList<GlassStickerLabel> FromOptimization(
            IEnumerable<OptimizationStickerSourceRow>? rows)
        {
            if (rows == null) return Array.Empty<GlassStickerLabel>();

            return rows
                .Where(r => r != null && r.WidthMm > 0 && r.HeightMm > 0 && r.Quantity > 0)
                .Select(r => new GlassStickerLabel
                {
                    Company = r.Company ?? new StickerCompanyProfile(),
                    OrderNumber = !string.IsNullOrWhiteSpace(r.OrderNumber)
                        ? r.OrderNumber.Trim()
                        : !string.IsNullOrWhiteSpace(r.JobOrderNumber)
                            ? r.JobOrderNumber.Trim()
                            : (r.ProformaNumber ?? string.Empty).Trim(),
                    JobOrderNumber = (r.JobOrderNumber ?? string.Empty).Trim(),
                    ProformaNumber = (r.ProformaNumber ?? string.Empty).Trim(),
                    CustomerCompanyName = (r.CustomerCompanyName ?? string.Empty).Trim(),
                    GlassName = ComposeGlassName(r.GlassName, string.Empty, 0),
                    GlassReference = (r.GlassReference ?? string.Empty).Trim(),
                    WidthMm = r.WidthMm,
                    HeightMm = r.HeightMm,
                    Quantity = r.Quantity,
                    Reference = (r.Reference ?? string.Empty).Trim(),
                    PanePosition = (r.PanePosition ?? string.Empty).Trim(),
                    Source = string.IsNullOrWhiteSpace(r.Source)
                        ? "Optimization pane list" : r.Source.Trim(),
                    SpecGroupNumber = r.SpecGroupNumber,
                    SerialNumber = r.SerialNumber
                })
                .ToList();
        }

        /// <summary>Combines only source-provided reference values, removing duplicates.</summary>
        public static string BuildReference(ProformaInvoiceSnapshot? invoice)
        {
            if (invoice == null) return string.Empty;
            var values = new[]
            {
                invoice.Project?.ProjectReference,
                invoice.Project?.ProjectContractNo,
                invoice.Project?.LpoNumber,
                invoice.Client?.CustomerReferenceCode
            }
            .Select(v => (v ?? string.Empty).Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

            return string.Join(" / ", values);
        }

        /// <summary>
        /// Adds pane thickness/color only when the composition does not already
        /// contain them; it never assigns a thickness or color that the source
        /// did not provide.
        /// </summary>
        public static string ComposeGlassName(string? composition, string? color, int thicknessMm)
        {
            string name = (composition ?? string.Empty).Trim();
            string paneColor = (color ?? string.Empty).Trim();

            if (thicknessMm > 0 && !Regex.IsMatch(name,
                    @"(^|\s)" + thicknessMm.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + @"\s*mm(?=\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                name = string.IsNullOrWhiteSpace(name)
                    ? thicknessMm.ToString(System.Globalization.CultureInfo.InvariantCulture) + "mm"
                    : thicknessMm.ToString(System.Globalization.CultureInfo.InvariantCulture) + "mm " + name;
            }

            if (!string.IsNullOrWhiteSpace(paneColor)
                && !name.Contains(paneColor, StringComparison.OrdinalIgnoreCase))
                name = string.IsNullOrWhiteSpace(name) ? paneColor : name + " " + paneColor;

            return name.Trim();
        }

        private static string ColorForPosition(SpecGroupSnapshot group, string? position)
        {
            if (string.Equals(position, "Lam Inner", StringComparison.OrdinalIgnoreCase))
                return !string.IsNullOrWhiteSpace(group.ThirdGlassColor)
                    ? group.ThirdGlassColor : group.InnerGlassColor;
            if (string.Equals(position, "Inner", StringComparison.OrdinalIgnoreCase))
                return group.InnerGlassColor;
            return group.OuterGlassColor;
        }
    }
}
