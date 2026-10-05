
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.JobOrder.Common.JobPriority;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Documents;
using ProGlassAutomation.Services.JobOrder.Export;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.ProformaInvoice.Attachments;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Views.JobOrder.SavedJobOrdersList.ProformaPicker;
using ProGlassAutomation.Views.JobOrder.SavedJobOrdersList;
namespace ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList
{
    public partial class SavedJobOrdersListViewModel : ViewModelBase, IDisposable
    {
    }
}
