using System.Windows;

namespace ProGlassAutomation.Theming;

/// <summary>Checks a complete package without borrowing active application resources.</summary>
public static class ResourceValidation
{
    public static void Validate(ResourceDictionary resources, string context = "Theme package")
    {
        var errors = new List<string>();
        CheckUniqueKeys(resources, new HashSet<object>(), errors);
        foreach (var (key, type) in ThemeContract.Required)
        {
            if (!resources.Contains(key))
            {
                errors.Add($"Missing required token: {key}");
                continue;
            }
            var value = resources[key];
            if (value is null || !type.IsInstanceOfType(value))
                errors.Add($"{key} must be {type.Name}, not {value?.GetType().Name ?? "null"}.");
            else if (value is double number && (!double.IsFinite(number) || number < 0))
                errors.Add($"{key} must be a finite, nonnegative number.");
            else if (value is double opacity && key.EndsWith("Opacity", StringComparison.Ordinal) && opacity > 1)
                errors.Add($"{key} must be between zero and one.");
        }
        foreach (var (key, target) in ThemeContract.RequiredStyles)
        {
            if (!resources.Contains(key)) errors.Add($"Missing required style: {key}");
            else if (resources[key] is not Style style || style.TargetType != target)
                errors.Add($"Style '{key}' must target {target.Name}.");
        }
        if (errors.Count != 0)
            throw new InvalidOperationException(context + ":" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    // Duplicate keys across merged files are rejected, not silently resolved by order.
    private static void CheckUniqueKeys(ResourceDictionary resources, HashSet<object> seen, List<string> errors)
    {
        foreach (object key in resources.Keys)
            if (!seen.Add(key)) errors.Add($"Duplicate resource key in package: {key}");
        foreach (ResourceDictionary child in resources.MergedDictionaries)
            CheckUniqueKeys(child, seen, errors);
    }
}
