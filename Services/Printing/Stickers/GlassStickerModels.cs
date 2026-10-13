using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    /// <summary>Company branding frozen from the invoice/job that owns a label.</summary>
    public sealed class StickerCompanyProfile
    {
        public string CompanyName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Telephone { get; set; } = string.Empty;
        public string RepresentativeMobile { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string LogoPath { get; set; } = string.Empty;
        public string LogoBase64 { get; set; } = string.Empty;

        public static StickerCompanyProfile FromSnapshot(
            ProGlassAutomation.Models.ProformaInvoice.SavedInvoice.CompanySnapshot? snapshot)
        {
            if (snapshot == null) return new StickerCompanyProfile();
            return new StickerCompanyProfile
            {
                CompanyName = snapshot.CompanyName ?? string.Empty,
                Address = snapshot.CompanyLocation ?? string.Empty,
                Telephone = snapshot.CompanyTelephone ?? string.Empty,
                RepresentativeMobile = snapshot.RepresentativeMobile ?? string.Empty,
                Website = snapshot.CompanyWebsite ?? string.Empty,
                Email = snapshot.CompanyEmail ?? string.Empty,
                LogoPath = snapshot.LogoPath ?? string.Empty,
                LogoBase64 = snapshot.LogoBase64 ?? string.Empty
            };
        }
    }

    /// <summary>One normalized physical pane source row shown in the preview list.</summary>
    public sealed class GlassStickerLabel
    {
        public StickerCompanyProfile Company { get; set; } = new();
        public string OrderNumber { get; set; } = string.Empty;
        public string JobOrderNumber { get; set; } = string.Empty;
        public string ProformaNumber { get; set; } = string.Empty;
        public string CustomerCompanyName { get; set; } = string.Empty;
        public string GlassName { get; set; } = string.Empty;
        public string GlassType { get; set; } = string.Empty;
        public string GlassReference { get; set; } = string.Empty;
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int Quantity { get; set; }
        /// <summary>Quantity shown on this physical label (one or aggregate row qty).</summary>
        public int PrintedQuantity { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string PanePosition { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        public string SizeText => string.Format(
            CultureInfo.InvariantCulture, "{0} × {1} mm", WidthMm, HeightMm);
        public string OrderDisplay => string.IsNullOrWhiteSpace(JobOrderNumber)
            ? (OrderNumber ?? string.Empty)
            : string.IsNullOrWhiteSpace(ProformaNumber)
                ? JobOrderNumber
                : JobOrderNumber + " / " + ProformaNumber;
        public int QuantityToDisplay => PrintedQuantity > 0 ? PrintedQuantity : Quantity;
    }

    /// <summary>
    /// Plain input contract for Optimization's current pane list. The view model
    /// supplies provenance already attached to each pane; this layer does not
    /// reach into Optimization UI state or query the database.
    /// </summary>
    public sealed class OptimizationStickerSourceRow
    {
        public StickerCompanyProfile Company { get; set; } = new();
        public string OrderNumber { get; set; } = string.Empty;
        public string JobOrderNumber { get; set; } = string.Empty;
        public string ProformaNumber { get; set; } = string.Empty;
        public string CustomerCompanyName { get; set; } = string.Empty;
        public string GlassName { get; set; } = string.Empty;
        public string GlassType { get; set; } = string.Empty;
        public string GlassReference { get; set; } = string.Empty;
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int Quantity { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string PanePosition { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }
    }

    /// <summary>Workstation-local stock/layout preferences for the Phase 1 printer.</summary>
    public sealed class StickerPrintPreferences
    {
        public double LabelWidthMm { get; set; } = 100;
        public double LabelHeightMm { get; set; } = 75;
        public double MarginMm { get; set; } = 2.5;
        public double FontSizePt { get; set; } = 9;
        public string LastTemplateName { get; set; } = string.Empty;
        public StickerLayoutTemplate? LastTemplate { get; set; }
        [JsonIgnore]
        public bool HasPersistedSettings { get; set; }
        public bool OneLabelPerPiece { get; set; } = true;
        public bool ShowLogo { get; set; } = true;
        public bool ShowCompanyName { get; set; } = true;
        public bool ShowAddress { get; set; } = true;
        public bool ShowTelephone { get; set; } = true;
        public bool ShowMobile { get; set; } = false;
        public bool ShowWebsite { get; set; } = true;
        public bool ShowEmail { get; set; } = true;
        public bool ShowOrder { get; set; } = true;
        public bool ShowCustomer { get; set; } = true;
        public bool ShowGlassName { get; set; } = true;
        public bool ShowSize { get; set; } = true;
        public bool ShowQuantity { get; set; } = true;
        public bool ShowReference { get; set; } = true;
        public bool ShowPanePosition { get; set; } = false;

        public StickerPrintPreferences Copy()
        {
            var copy = (StickerPrintPreferences)MemberwiseClone();
            copy.LastTemplate = LastTemplate?.DeepClone();
            return copy;
        }
    }
}
