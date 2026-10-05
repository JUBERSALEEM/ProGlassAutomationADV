using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.SheetStore;
using ProGlassAutomation.ViewModels.SheetStore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Live Sheet Store: filter, select, persist buy/use through the same
    /// repository the Buy/Use dialogs use. Dialogs are not opened.
    /// </summary>
    public sealed class SheetStoreWorkflow : IDemoWorkflowRunner
    {
        public string ModuleId => "SheetStore";
        public string OutputFileName => "SheetStore_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            ["filtered", "selected", "stock_moved", "sheet_added"];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.Navigate("SheetStore")) return events;

            await WpfUiDriver.WaitReadyAsync(500, ct).ConfigureAwait(false);
            var store = await ResolveAsync(ct).ConfigureAwait(false);
            if (store == null) return events;

            await DemoWalk.TryCommandAsync(store.RefreshCommand, null, ct).ConfigureAwait(false);
            await DemoWalk.WaitUntilAsync(() => store.Sheets.Count > 0, 8000, ct)
                .ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "opened_list",
                "Sheet Store list",
                $"This is Sheet Store — the warehouse of raw glass sheets. Each row is one type of glass we keep in stock: a specific category like Clear or Reflective, a specific thickness like 5 or 6 or 8 millimetres, a specific colour, and a specific sheet size. The three key columns are Stock — how many sheets we bought — Used — how many the cutter has consumed on jobs — and Balance — the difference, which is what is physically still on the rack. Right now there are {store.Sheets.Count} sheet types on file totalling {store.TotalStock} sheets. This is the very first screen the storekeeper checks every morning to know what to reorder.",
                $"sheets={store.Sheets.Count} stock={store.TotalStock}",
                DemoWalk.BeatMs, ct).ConfigureAwait(false);

            bool filtered = await WpfUiDriver.OnUiAsync(() =>
            {
                if (store.ThicknessOptions.Contains("6mm"))
                    store.SelectedThickness = "6mm";
                if (store.ColorOptions.Contains("Clear"))
                    store.SelectedColor = "Clear";
                store.SearchText = "Clear";
                return store.HasActiveFilters;
            }, ct).ConfigureAwait(false);

            await WpfUiDriver.DwellAsync(800, ct).ConfigureAwait(false);
            if (filtered)
            {
                await DemoWalk.PresentAsync(events, "filtered",
                    "Filter 6 millimetre Clear",
                    $"You can slice the warehouse the way an operator thinks. Here we filtered on 6 millimetre Clear because that is the most common glass for shopfronts and interior partitions, and instantly only {store.Sheets.Count} rows are left — every one of them 6 millimetre Clear from a different supplier or a different sheet size. The Balance column tells the estimator on the spot whether we can start a job today or whether he has to wait for the next purchase.",
                    $"rows={store.Sheets.Count} thickness={store.SelectedThickness}",
                    DemoWalk.HoldMs, ct).ConfigureAwait(false);
            }

            var pick = await WpfUiDriver.OnUiAsync(() =>
            {
                var sheet = store.Sheets.FirstOrDefault(s =>
                    s.ThicknessMm == 6 && s.Color == "Clear")
                    ?? store.Sheets.FirstOrDefault();
                store.SelectedSheet = sheet;
                return sheet;
            }, ct).ConfigureAwait(false);

            if (pick != null)
            {
                await DemoWalk.PresentAsync(events, "selected",
                    "Select a sheet",
                    $"Clicking a row opens the sheet's detail. This one is {pick.Category} {pick.ThicknessDisplay} in {pick.Color}, sheet size {pick.SizeDisplay}. We currently have {pick.Stock} sheets in Stock, {pick.Used} have been consumed on jobs, so the Balance actually on the rack is {pick.Balance}. The side panel also shows the supplier we usually buy this from, the purchase price per square metre and the selling price we quote to customers — the two numbers the estimator uses to price a new quotation.",
                    $"id={pick.Id} stock={pick.Stock} used={pick.Used} balance={pick.Balance}",
                    DemoWalk.HoldMs, ct).ConfigureAwait(false);
            }

            if (pick != null && pick.Id > 0)
            {
                var repo = new SheetStoreRepository();
                int before = pick.Stock;
                try
                {
                    await repo.BuyStockAsync(pick.Id, 2).ConfigureAwait(false);
                    if (pick.Balance + 2 > 0)
                        await repo.UseStockAsync(pick.Id, 1).ConfigureAwait(false);
                }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SheetStoreWorkflow] RunAsync: persist must not kill the walk: " + ex.Message); }

                await DemoWalk.TryCommandAsync(store.ClearFiltersCommand, null, ct)
                    .ConfigureAwait(false);
                await DemoWalk.TryCommandAsync(store.RefreshCommand, null, ct)
                    .ConfigureAwait(false);
                await WpfUiDriver.DwellAsync(700, ct).ConfigureAwait(false);

                var after = await WpfUiDriver.OnUiAsync(() =>
                    store.Sheets.FirstOrDefault(s => s.Id == pick.Id), ct)
                    .ConfigureAwait(false);

                if (after != null && after.Stock != before)
                {
                    await DemoWalk.PresentAsync(events, "stock_moved",
                        "Buy two, use one",
                        $"Now watch a real stock movement. The storekeeper bought two more sheets from the supplier and issued one to the cutting floor for a job. Stock went from {before} to {after.Stock}, Used incremented to {after.Used}, and Balance recalculated to {after.Balance}. This is exactly what happens when he clicks the Buy and Use dialog boxes in production — we are calling the same save path, not a demo shortcut, so the audit log will show these as normal operator activity.",
                        $"id={after.Id} stock {before}→{after.Stock} used={after.Used}",
                        DemoWalk.HoldMs, ct).ConfigureAwait(false);
                }
            }

            var added = await AddShowcaseSheetAsync(store, ct).ConfigureAwait(false);
            if (added != null)
            {
                await WpfUiDriver.OnUiAsync(() =>
                {
                    store.SearchText = added.Category;
                    store.SelectedSheet = store.Sheets.FirstOrDefault(s => s.Id == added.Id);
                    return true;
                }, ct).ConfigureAwait(false);

                await DemoWalk.PresentAsync(events, "sheet_added",
                    "New laminated sheet",
                    $"And here is a brand new item added to the warehouse. When the supplier introduces a new specification — in this case a {added.Category} {added.ThicknessDisplay} {added.Color} sheet from {added.Supplier} — the storekeeper adds it here once, sets the opening stock at {added.Stock} sheets, and from that moment on the estimator can pick it in any quotation. There is no separate 'master data' screen — the storekeeper is the owner of his own catalogue.",
                    $"id={added.Id} {added.Category} {added.ThicknessDisplay}",
                    DemoWalk.HoldMs, ct).ConfigureAwait(false);
            }

            await DemoWalk.TryCommandAsync(store.ClearFiltersCommand, null, ct)
                .ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(700, ct).ConfigureAwait(false);
            return events;
        }

        private static async Task<GlassSheet?> AddShowcaseSheetAsync(
            SheetStoreViewModel store, CancellationToken ct)
        {
            try
            {
                var repo = new SheetStoreRepository();
                var all = await repo.GetAllAsync().ConfigureAwait(false);
                var existing = all.FirstOrDefault(s =>
                    s.Category == "Laminated" && s.ThicknessMm == 8
                    && s.Color == "Clear" && s.WidthMm == 2140 && s.HeightMm == 1530);
                if (existing != null) return existing;

                var added = await repo.AddAsync(new GlassSheet
                {
                    Category = "Laminated",
                    ThicknessMm = 8,
                    Color = "Clear",
                    WidthMm = 2140,
                    HeightMm = 1530,
                    Supplier = "Guardian Glass",
                    PurchasePricePerSqm = 165m,
                    SellPricePerSqm = 240m,
                    Stock = 12,
                    Used = 0
                }).ConfigureAwait(false);

                await DemoWalk.TryCommandAsync(store.RefreshCommand, null, ct)
                    .ConfigureAwait(false);
                await DemoWalk.WaitUntilAsync(
                    () => store.Sheets.Any(s => s.Id == added.Id), 6000, ct)
                    .ConfigureAwait(false);
                return added;
            }
            catch { return null; }
        }

        private static Task<SheetStoreViewModel?> ResolveAsync(CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var main = System.Windows.Application.Current?.MainWindow?.DataContext
                    as ViewModels.MainViewModel;
                return main?.SheetStoreVM;
            }, ct);
    }
}
