using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Documents;
using ProGlassAutomation.Services.DeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Views.DeliveryNote.DeliveryNoteList.JobOrderPicker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.JobOrder.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList
{
    /// <summary>
    /// ViewModel for the Delivery Note list landing page.
    ///
    /// 6 tabs: All | Prepared | Delivered | Pending Qty | Rework | Returned
    ///
    /// CHAT 19 CHANGES:
    ///   • Master DN Tab 6 REMOVED — Master DN is internal only
    ///   • MasterDnItems / MasterDnCount removed
    ///   • OnOpenMasterDnRequested removed
    ///   • OpenMasterDnCommand removed
    ///   • GetMasterDnsAsync no longer called in LoadAsync
    ///   • TypeFilterOptions keeps "Dummy" removed (Master DN internal)
    ///   • DeleteAsync guards against Master DN deletion (safety)
    /// </summary>
    public partial class DeliveryNoteListViewModel : ViewModelBase, System.IDisposable
    {
    }
}