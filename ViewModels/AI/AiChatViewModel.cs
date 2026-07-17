using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIService;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.AI
{
    public class AiChatViewModel : ViewModelBase
    {
        private readonly IAiService _ai;

        private string _currentInput;
        private bool _isBusy;
        private bool _isOpen;

        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();

        public string CurrentInput
        {
            get => _currentInput;
            set { _currentInput = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSend)); }
        }

        public bool IsOpen
        {
            get => _isOpen;
            set { _isOpen = value; OnPropertyChanged(); }
        }

        public bool HasMessages => Messages.Count > 0;

        public bool CanSend => !IsBusy;

        public ICommand SendCommand { get; }
        public ICommand ToggleChatCommand { get; }
        public ICommand ClearChatCommand { get; }
        public ICommand CloseChatCommand { get; }

        public AiChatViewModel(IAiService ai)
        {
            _ai = ai;

            SendCommand = new RelayCommand(async _ => await SendAsync(), _ => CanSend);
            ToggleChatCommand = new RelayCommand(_ => IsOpen = !IsOpen);
            CloseChatCommand = new RelayCommand(_ => IsOpen = false);
            ClearChatCommand = new RelayCommand(_ =>
            {
                Messages.Clear();
                OnPropertyChanged(nameof(HasMessages));
            });

            Messages.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasMessages));
        }

        private async Task SendAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentInput)) return;

            // Guard: AI not configured
            if (_ai == null || !_ai.IsEnabled || !_ai.IsConfigured)
            {
                Messages.Add(new ChatMessage
                {
                    Text = "AI is not enabled. Please open Settings and enable AI + set your Gemini API key.",
                    IsUser = false,
                    IsError = true
                });
                return;
            }

            // Add user message
            var userMsg = CurrentInput.Trim();
            Messages.Add(new ChatMessage { Text = userMsg, IsUser = true });
            CurrentInput = string.Empty;

            IsBusy = true;

            // System prompt gives Gemini context about the app
            const string systemPrompt =
                "You are the ProGlass Automation AI Assistant — a helpful expert in glass " +
                "production workflows, invoicing, and manufacturing. Give concise, practical " +
                "answers. When appropriate, use short bullet points. If the question is about " +
                "the app itself (Dashboard, Proforma Invoice, Job Order, Cutting, Tempering, " +
                "Delivery Note, Tax Invoice, etc.), guide the user based on typical glass " +
                "production workflows.";

            var response = await _ai.AskAsync(systemPrompt, userMsg);

            IsBusy = false;

            if (response.Success)
            {
                Messages.Add(new ChatMessage
                {
                    Text = response.Text?.Trim() ?? "(empty response)",
                    IsUser = false
                });
            }
            else
            {
                Messages.Add(new ChatMessage
                {
                    Text = "Error: " + (response.ErrorMessage ?? "Unknown error"),
                    IsUser = false,
                    IsError = true
                });
            }
        }
    }
}