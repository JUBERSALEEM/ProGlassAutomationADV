using System.Windows.Input;

namespace ProGlassAutomation.ThemeStudio;

public sealed class DelegateCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => execute();
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

public sealed class DelegateCommand<T>(Action<T> execute, Func<T, bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter)
    {
        if (canExecute == null) return true;
        if (parameter is T typed) return canExecute(typed);
        // Handle null for reference types / nullable value types
        if (parameter == null)
        {
            if (default(T) == null) return canExecute(default!);
            return false;
        }
        try { return canExecute((T)parameter); } catch { return false; }
    }
    public void Execute(object? parameter)
    {
        if (parameter is T typed) { execute(typed); return; }
        if (parameter == null && default(T) == null) { execute(default!); return; }
        execute((T)parameter!);
    }
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
