using ProGlassAutomation.ViewModels;

namespace ProGlassAutomation.ViewModels.About
{
    public class AboutViewModel : ViewModelBase
    {
        public string AppName { get; } = "ProGlass Automation";
        public string AppTagline { get; } = "Software for Glass Production Workflow";
        public string Version { get; } = "v1.0.0";

        public string DeveloperName { get; } = "Juber Saleem";
        public string Country { get; } = "India";
        public string Email { get; } = "Jubersaleem@gmail.com";

        public string Copyright { get; } = "© 2025 ProGlass Automation. All rights reserved.";
    }
}