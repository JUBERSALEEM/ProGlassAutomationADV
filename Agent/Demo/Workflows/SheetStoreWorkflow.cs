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
                "Live Sheet Store. Stock, used and balance are the same fields the clerk reads every morning.",
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
                    $"Filter is 6 millimetre Clear. {store.Sheets.Count} rows remain. Available is stock minus used.",
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
                    $"{pick.Category} {pick.ThicknessDisplay} {pick.Color}, {pick.SizeDisplay}. Stock {pick.Stock}, used {pick.Used}, balance {pick.Balance}.",
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
                catch { /* persist must not kill the walk */ }

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
                        $"Same persist as the Buy and Use dialogs. Stock moved from {before} to {after.Stock}. Used {after.Used}, balance {after.Balance}.",
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
                    $"{added.Category} {added.ThicknessDisplay} {added.Color} from {added.Supplier} is now on the list. Opening stock {added.Stock} sheets.",
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
