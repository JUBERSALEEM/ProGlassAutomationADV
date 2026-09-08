# Behaviors

<!-- pg-nav -->
> **[Repository root](../README.md)** · [All documents](../docs/README.md) · [Application workflow](../docs/WORKFLOW.md) · [Design system](../docs/DESIGN_SYSTEM.md)
<!-- pg-nav -->
Global reusable **attached behaviors** that enhance WPF controls with polished UX
enhancements. These are non-visual add-ons you can attach to any matching control
via XAML without subclassing or code-behind.

## Folder Purpose

This folder holds cross-cutting behaviors used across multiple modules
(Sheet Store, Proforma Invoice, Job Order, etc.). Keep behaviors here
that are **not specific to a single feature**.

## Available Behaviors

### TextBoxBehaviors.SelectAllOnFocus

Auto-selects all text when a TextBox receives focus (via mouse click or
Tab key). Users can immediately overwrite the existing value without
manually deleting it first.

**When to use:**
- Numeric input fields (prices, quantities, percentages)
- ID or code fields users will retype
- Any field where "clear + retype" is the common editing pattern

**Usage — Per TextBox:**

    xmlns:behaviors="clr-namespace:ProGlassAutomation.Behaviors"

    <TextBox Text="{Binding Price}"
             behaviors:TextBoxBehaviors.SelectAllOnFocus="True"/>

**Usage — In a Style (applies to all matching TextBoxes):**

    <Style x:Key="DarkInputStyle" TargetType="TextBox">
        <Setter Property="behaviors:TextBoxBehaviors.SelectAllOnFocus" Value="True"/>
        <!-- other setters -->
    </Style>

**How it works internally:**
- Hooks GotKeyboardFocus (Tab-in scenarios) → SelectAll()
- Hooks GotFocus (edge cases) → SelectAll()
- Hooks PreviewMouseLeftButtonDown → if TextBox doesn't yet have focus,
  intercepts the click, gives focus (triggering SelectAll), and swallows
  the click so the caret doesn't reposition mid-word.

## Design Principles

1. **Stateless** — No dependency on external services or DI containers.
2. **Safe to toggle** — Handlers detach before re-attaching, no memory leaks.
3. **XAML-friendly** — All properties are DependencyProperties for binding
   and style-based application.
4. **Namespace isolated** — All behaviors live under `ProGlassAutomation.Behaviors`
   to avoid conflicts with third-party libraries.

## Adding a New Behavior

Follow this template pattern (based on `TextBoxBehaviors.SelectAllOnFocus`):

    public static readonly DependencyProperty YourBehaviorProperty =
        DependencyProperty.RegisterAttached(
            "YourBehavior",
            typeof(bool),
            typeof(YourBehaviorClass),
            new UIPropertyMetadata(false, OnYourBehaviorChanged));

    public static bool GetYourBehavior(DependencyObject obj)
        => (bool)obj.GetValue(YourBehaviorProperty);

    public static void SetYourBehavior(DependencyObject obj, bool value)
        => obj.SetValue(YourBehaviorProperty, value);

    private static void OnYourBehaviorChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TargetControlType control) return;

        // Always detach first to prevent double-hooking
        control.SomeEvent -= HandlerMethod;

        if ((bool)e.NewValue)
            control.SomeEvent += HandlerMethod;
    }

    private static void HandlerMethod(object sender, EventArgs e)
    {
        // Your behavior logic here
    }

## Planned Behaviors (Future)

- `NumericTextBoxBehavior` — restrict TextBox input to numeric characters only
- `MaxLengthTextBoxBehavior` — visual warning when approaching max length
- `WatermarkBehavior` — themed placeholder text without breaking bindings
- `FocusOnLoadBehavior` — auto-focus a specific control when view loads
- `TripleClickSelectAllBehavior` — triple-click selects entire text

## Related Folders

- `Converters/` — value converters for XAML binding transformations
- `Themes/` — theme dictionaries (LightTheme, DarkTheme)
- `Commands/` — RelayCommand and command infrastructure

## Status

| Behavior | Status | Since |
|----------|--------|-------|
| SelectAllOnFocus | ✅ Complete | Sub-Phase 6.4h |

<!-- pg-nav -->
---

*This README documents `Behaviors/`. For the end-to-end business chain see [`docs/WORKFLOW.md`](../docs/WORKFLOW.md); for every other document see [`docs/README.md`](../docs/README.md).*
<!-- pg-nav -->
