using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Inquiry
{
    public sealed class InquiryEditorViewModel : ViewModelBase
    {
        private readonly IInquiryWorkflowService _workflow;
        public InquiryRecord Record { get; }
        public ObservableCollection<string> Priorities { get; } = new() { "Urgent", "High", "Normal", "Low" };
        public ObservableCollection<string> Salesmen { get; } = new();
        public ObservableCollection<string> Customers { get; } = new();
        public DateTime ReceivedDate
        {
            get => Record.ReceivedAt == default ? DateTime.Today : Record.ReceivedAt.Date;
            set => Record.ReceivedAt = value.Date.Add(
                Record.ReceivedAt == default ? DateTime.Now.TimeOfDay : Record.ReceivedAt.TimeOfDay);
        }
        public ICommand SaveCommand { get; }
        public ICommand ChangeStatusCommand { get; }
        public ICommand CancelCommand { get; }
        public event Action<bool>? CloseRequested;

        public InquiryEditorViewModel(IInquiryWorkflowService workflow, InquiryRecord? source = null)
        {
            _workflow = workflow;
            Record = source ?? new InquiryRecord
            {
                InquiryReference = "Customer Inquiry", Status = InquiryStatuses.Received,
                Priority = "Normal", ReceivedAt = DateTime.Now
            };
            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            ChangeStatusCommand = new RelayCommand(async p =>
            {
                if (Record.Id > 0 && p is string next)
                {
                    try
                    {
                        await _workflow.ChangeStatusAsync(Record.Id, Record.Status, next);
                        Record.Status = InquiryStatuses.Normalize(next);
                        OnPropertyChanged(nameof(Record)); StatusText = $"Status changed to {Record.Status}.";
                    }
                    catch (Exception ex) { StatusText = ex.Message; }
                }
            }, _ => Record.Id > 0);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
            _ = LoadMastersAsync();
        }

        private async Task LoadMastersAsync()
        {
            if (App.MasterData == null) return;
            foreach (var x in await App.MasterData.GetAllClientsAsync()) Customers.Add(x.CustomerName);
            foreach (var x in await App.MasterData.GetAllSalesmenAsync()) Salesmen.Add(x.Name);
        }

        private async Task SaveAsync()
        {
            try
            {
                StatusText = "Saving…";
                if (Record.Id == 0) await _workflow.CreateAsync(Record);
                else await _workflow.UpdateAsync(Record);
                StatusText = "Saved.";
                CloseRequested?.Invoke(true);
            }
            catch (Exception ex) { StatusText = ex.Message; }
        }

        private string _statusText = "";
        public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }
    }
}
