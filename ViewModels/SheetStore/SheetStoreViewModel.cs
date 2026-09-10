using Microsoft.Win32;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.SheetStore;
using ProGlassAutomation.Services.SheetStore.SheetStoreCsv;
using ProGlassAutomation.Services.SheetStore.SheetStoreExcel;
using ProGlassAutomation.Services.SheetStore.SheetStorePdf;
using ProGlassAutomation.Services.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Views.SheetStore;
using ProGlassAutomation.Views.SheetStore.SheetStoreBuy;
using ProGlassAutomation.Views.SheetStore.SheetStoreCsv;
using ProGlassAutomation.Views.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Views.SheetStore.SheetStoreUse;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreBuy;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreCsv;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreHistory;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreUse;
using ProGlassAutomation.ViewModels.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.SheetStore
{
    public partial class SheetStoreViewModel : ViewModelBase,
        ProGlassAutomation.Agent.Context.IAgentContextAware
    {
        /// <summary>
        /// Raised after a LEDGER MUTATION (add / edit / buy / use / CSV
        /// import) has been persisted. Cross-module consumers (Step 4
        /// pricing panels) refresh their Sheet Store reads so a return
        /// from the store shows new colours/prices without reopening the
        /// wizard. Never raised on plain navigation/loads.
        /// </summary>
        public event EventHandler? StockLedgerChanged;
        internal void RaiseStockLedgerChanged()
            => StockLedgerChanged?.Invoke(this, EventArgs.Empty);
    }
}