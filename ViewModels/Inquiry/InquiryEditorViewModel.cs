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
        private readonly InquiryAttachmentService _attachments;
        public InquiryRecord Record { get; }
        public ObservableCollection<InquiryAttachment> AttachmentItems { get; } = new();
        private InquiryAttachment? _selectedAttachment;
        public InquiryAttachment? SelectedAttachment { get => _selectedAttachment; set { _selectedAttachment = value; OnPropertyChanged(); } }
        public Func<string[]?>? SelectFilesRequested { get; set; }
        public Func<InquiryAttachment, bool>? ConfirmDeleteRequested { get; set; }
        public Action<InquiryAttachment>? PreviewRequested { get; set; }
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
        public ICommand AddAttachmentCommand { get; }
        public ICommand DeleteAttachmentCommand { get; }
        public ICommand PreviewAttachmentCommand { get; }
        public ICommand CancelCommand { get; }
        public event Action<bool>? CloseRequested;

        public InquiryEditorViewModel(IInquiryWorkflowService workflow,
            InquiryAttachmentService attachments, InquiryRecord? source = null)
        {
            _workflow = workflow;
            _attachments = attachments;
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
            AddAttachmentCommand = new RelayCommand(async _ => await AddAttachmentsAsync());
            DeleteAttachmentCommand = new RelayCommand(async p =>
            {
                if (p is InquiryAttachment file && ConfirmDeleteRequested?.Invoke(file) == true)
                {
                    await _attachments.DeleteAsync(file);
                    AttachmentItems.Remove(file);
                }
            });
            PreviewAttachmentCommand = new RelayCommand(p =>
            {
                if (p is InquiryAttachment file) PreviewRequested?.Invoke(file);
            });
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
            _ = LoadMastersAsync();
            _ = LoadAttachmentsAsync();
        }

        private async Task LoadMastersAsync()
        {
            if (App.MasterData == null) return;
            foreach (var x in await App.MasterData.GetAllClientsAsync()) Customers.Add(x.CustomerName);
            foreach (var x in await App.MasterData.GetAllSalesmenAsync()) Salesmen.Add(x.Name);
        }

        private async Task LoadAttachmentsAsync()
        {
            if (Record.Id <= 0) return;
            AttachmentItems.Clear();
            foreach (var x in await App.Inquiries.GetAttachmentsAsync(Record.Id)) AttachmentItems.Add(x);
        }

        private async Task AddAttachmentsAsync()
        {
            if (Record.Id <= 0) { StatusText = "Save the Inquiry before adding attachments."; return; }
            var files = SelectFilesRequested?.Invoke();
            if (files == null) return;
            try
            {
                foreach (var path in files) AttachmentItems.Add(await _attachments.AddAsync(Record.Id, path));
                StatusText = $"{files.Length} attachment(s) added.";
            }
            catch (Exception ex) { StatusText = "Attachment failed: " + ex.Message; }
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
