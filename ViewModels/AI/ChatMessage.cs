using System;

namespace ProGlassAutomation.ViewModels.AI
{
    public class ChatMessage : ViewModelBase
    {
        public string Text { get; set; }
        public bool IsUser { get; set; }
        public bool IsError { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // For binding — computed labels
        public string SenderLabel => IsUser ? "You" : "ProGlass AI";
        public string TimeLabel => Timestamp.ToString("h:mm tt");
    }
}