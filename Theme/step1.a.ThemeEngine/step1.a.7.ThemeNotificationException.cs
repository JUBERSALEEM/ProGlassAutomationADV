namespace ProGlassAutomation.Theming;

/// <summary>Activation succeeded; a consumer refresh failed, rather than selection being rejected.</summary>
public sealed class ThemeNotificationException : InvalidOperationException
{
    public ThemeNotificationException(string message, Exception inner) : base(message, inner) { }
}
