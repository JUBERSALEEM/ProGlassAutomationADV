using System.Text.Json;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.SguParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using Xunit;

namespace ProGlassAutomation.Tests.Agent;

public sealed class Step4LiveSyncRepairTests
{
    private static Step4PricingSnapshot Sgu(decimal rate, string color = "Clear") => new()
    {
        SelectedGlassTypeCode = "SGU", GeneratedSpecName = $"12mm {color} FT Glass",
        EstimatedRatePerSqm = rate,
        Sgu = new SguPricingPanelSnapshot
        {
            SelectedThickness = "12mm", GlassColor = color, SelectedTreatment = "FT Glass",
            SheetWidthMm = 3210, SheetHeightMm = 2250, BasePricePerSqm = 60
        }
    };

    [Fact]
    public void Repeated_rate_only_updates_do_not_claim_manual_ownership()
    {
        var group = new SpecGroup();
        group.ApplyStep4PricingSnapshot(Sgu(100));
        group.ApplyStep4RateSnapshot(Sgu(110));
        group.ApplyStep4RateSnapshot(Sgu(120));
        Assert.Equal(120m, group.BaseRate);
        Assert.False(group.HasManualStep5BaseRateOverride);
        Assert.Equal("SGU", group.SelectedGlassType);
        Assert.Equal("12mm Clear FT Glass", group.SelectedSpecName);
        group.BaseRate = 150;
        group.ApplyStep4RateSnapshot(Sgu(130));
        Assert.Equal(150m, group.BaseRate);
        Assert.Equal(130m, group.Step4EstimatedRatePerSqm);
        Assert.Contains("130.00", group.Step4RateTooltip);
    }

    [Fact]
    public void Same_rate_glass_change_updates_type_spec_sheets_and_provenance()
    {
        var group = new SpecGroup();
        group.ApplyStep4PricingSnapshot(Sgu(100));
        var dgu = new Step4PricingSnapshot
        {
            SelectedGlassTypeCode = "DGU", EstimatedRatePerSqm = 100,
            GeneratedSpecName = "10mm Bronze FT Glass + 12mm ASP + 6mm Clear FT Glass",
            Dgu = new DguPricingPanelSnapshot
            {
                OuterGlassColor = "Bronze", InnerGlassColor = "Clear",
                OuterThickness = "10mm", InnerThickness = "6mm",
                OuterSheetWidthMm = 3300, OuterSheetHeightMm = 2140,
                InnerSheetWidthMm = 3210, InnerSheetHeightMm = 2250,
                OuterBasePrice = 75, InnerBasePrice = 40
            }
        };
        group.ApplyStep4PricingSnapshot(dgu);
        Assert.Equal("DGU", group.SelectedGlassType);
        Assert.Equal(dgu.GeneratedSpecName, group.SelectedSpecName);
        Assert.Equal("Bronze", group.OuterGlassColor);
        Assert.Equal(3300, group.OuterSheetWidthMm);
        Assert.Equal(dgu.ComputeContentHash(), group.LastSyncedStep4ContentHash);
        Assert.Contains("75.00", group.Step4RateTooltip);
        Assert.DoesNotContain("AED", group.SheetSelectionSummary); // prices live in tooltip
        group.ApplyStep4PricingSnapshot(Sgu(100));
        Assert.Equal("SGU", group.SelectedGlassType);
        Assert.Equal(0, group.InnerSheetWidthMm);
        Assert.DoesNotContain("Inner sheet rate", group.Step4RateTooltip);
    }

    [Fact]
    public void Sgu_panel_extras_change_spec_and_survive_actual_section_capture_restore()
    {
        var pricing = new PricingEstimatorViewModel(null, null);
        pricing.SguVM.PanelExtra = "With Holes";
        pricing.SguVM.UseCustomPanelExtra = true;
        pricing.SguVM.CustomPanelExtra = "With Fanhole and Cutout As Per DWG";
        var captured = Assert.Single(pricing.CaptureSectionSnapshots());
        Assert.EndsWith(pricing.SguVM.CustomPanelExtra, captured.GeneratedSpecName);
        string fingerprint = captured.ComputeContentHash();
        var saved = JsonSerializer.Deserialize<Step4PricingSnapshot>(JsonSerializer.Serialize(captured))!;
        pricing.SguVM.UseCustomPanelExtra = false;
        pricing.SguVM.PanelExtra = "None";
        pricing.ApplyPricingSnapshot(saved);
        Assert.True(pricing.SguVM.UseCustomPanelExtra);
        Assert.Equal("With Holes", pricing.SguVM.PanelExtra);
        Assert.Equal(saved.GeneratedSpecName, pricing.SguVM.GeneratedSpecName);
        Assert.Equal(fingerprint, saved.ComputeContentHash());
        saved.Sgu.CustomPanelExtra = "With Cutout";
        Assert.NotEqual(fingerprint, saved.ComputeContentHash());
    }

    [Fact]
    public void Sgu_extras_do_not_duplicate_polish_or_offer_cavity_gas()
    {
        var vm = new SguParametersViewModel { SelectedPolishOption = "With Holes", PanelExtra = "With Holes" };
        Assert.Equal(1, vm.GeneratedSpecName.Split("With Holes").Length - 1);
        Assert.DoesNotContain(vm.PanelExtraOptions, value => value.Contains("Argon"));
    }
}
