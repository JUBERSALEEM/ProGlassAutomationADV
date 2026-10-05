// ══════════════════════════════════════════════════════════════════════
//  FILE: Tests/Agent/SheetStoreDialogAndDropdownTests.cs
//
//  Three regressions/defects, pinned so they cannot come back quietly:
//
//  1. The Sheet Store's dropdowns stopped accepting typed values. A style that
//     pins a custom ControlTemplate REPLACES WPF's, and WPF's default template
//     is the one carrying PART_EditableTextBox — so a combo can render, open its
//     list, and still be impossible to type into, with no error anywhere.
//     Thirteen fields bind Text= and are meant to accept a category, colour,
//     supplier or size the store has never seen.
//
//  2. Step 4's "Open Sheet Store — Add Sheet" only navigated, so the operator
//     landed on the module's list and nothing they asked for appeared.
//
//  3. The sheet workspace was a modal dialog with WindowState="Maximized" — a
//     screen takeover — and the dialog family disagreed with itself about button
//     widths: the window's own SAVE was narrower than the BUY/USE actions beside
//     it, and the −/+ steppers were 58px in one dialog and 50px in another.
//
//  These are XAML and window-level facts, so most of them are structural. The
//  ones that can be arithmetic are: a bounded window must not be bounded below
//  what already fit, and every button role must agree on one width.
//
//  There is no dotnet SDK here, so these run on the developer's machine.
// ══════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class SheetStoreDialogAndDropdownTests
    {
        private const string DetailWindow = "Views/SheetStore/SheetDetailWindow.xaml";
        private const string AddEditSheet = "Views/SheetStore/AddEditSheetView.xaml";
        private const string BuyDialog = "Views/SheetStore/SheetStoreBuy/BuyDialogView.xaml";
        private const string UseDialog = "Views/SheetStore/SheetStoreUse/UseDialogView.xaml";
        private const string HistoryView = "Views/SheetStore/SheetStoreHistory/HistoryView.xaml";
        private const string StoreView = "Views/SheetStore/SheetStoreView.xaml";
        private const string GlobalDesignSystem = "Themes/Styles/GlobalDesignSystem.xaml";
        private const string ComboTemplates = "Themes/Styles/ComboTemplates.xaml";
        private const string StoreEditorVm = "ViewModels/SheetStore/SheetStoreViewModel.Editor.cs";
        private const string ShellVm = "ViewModels/MainViewModel.Part1.cs";

        /// <summary>The six fields that must accept a value the store has never seen.</summary>
        private static readonly string[] OpenVocabulary =
        {
            "Category", "ThicknessText", "Color", "WidthText", "HeightText", "Supplier"
        };

        private static string Root()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Views", "SheetStore")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }

        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));

        private static string Slice(string text, string start, string end)
        {
            int i = text.IndexOf(start, StringComparison.Ordinal);
            Assert.True(i >= 0, "start marker not found: " + start);
            int j = text.IndexOf(end, i + start.Length, StringComparison.Ordinal);
            Assert.True(j > i, "end marker not found after start: " + end);
            return text.Substring(i, j + end.Length - i);
        }

        /// <summary>
        /// Every ComboBox start tag in a view. Handles both the self-closing form
        /// and the one with ComboBoxItem children, whose first "/>" belongs to the
        /// child rather than to the combo.
        /// </summary>
        private static List<string> ComboTags(string view)
        {
            var tags = new List<string>();
            int i = 0;
            while ((i = view.IndexOf("<ComboBox", i, StringComparison.Ordinal)) >= 0)
            {
                int selfClose = view.IndexOf("/>", i, StringComparison.Ordinal);
                int open = view.IndexOf('>', i);
                Assert.True(open > i, "malformed ComboBox tag");
                int stop = selfClose >= 0 && selfClose < open ? selfClose + 2 : open + 1;
                tags.Add(view.Substring(i, stop - i));
                i = stop;
            }

            return tags;
        }

        private static string BoundText(string tag) =>
            Regex.Match(tag, @"\bText=""\{Binding ([A-Za-z0-9_.]+)").Groups[1].Value;

        private static string BoundSelection(string tag) =>
            Regex.Match(tag, @"\bSelected(?:Item|Value)=""\{Binding ([A-Za-z0-9_.]+)").Groups[1].Value;

        private static bool IsEditable(string tag) =>
            tag.Contains("IsEditable=\"True\"")
            || tag.Contains("ModernEditableComboBoxStyle");

        private static int? MinWidthOf(string tag)
        {
            Match m = Regex.Match(tag, @"\bMinWidth=""([0-9]+)""");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
        }

        private static List<string> ButtonTags(string view) =>
            Regex.Matches(view, "<Button\\b(?:(?!/>|</Button>).)*", RegexOptions.Singleline)
                .Cast<Match>()
                .Select(m => m.Value)
                .ToList();

        // ══ 1. the dropdown regression ════════════════════════════════════

        [Fact]
        public void A_style_that_pins_a_template_must_swap_it_when_the_combo_is_editable()
        {
            // The root cause, and the reason it was invisible: a custom Template
            // REPLACES WPF's, and WPF's is the one that carries
            // PART_EditableTextBox. So pinning the non-editable template makes
            // IsEditable="True" render a box that cannot be typed into — no
            // exception, no binding error, just a dead field.
            string styles = Read(GlobalDesignSystem);
            string input = Slice(styles, "<Style x:Key=\"Input.ComboBox\"", "</Style>");

            Assert.Contains("<Trigger Property=\"IsEditable\" Value=\"True\">", input);
            Assert.Contains("Themed.EditableComboBoxTemplate", input);
        }

        [Fact]
        public void Only_one_of_the_two_themed_templates_can_be_typed_into()
        {
            string templates = Read(ComboTemplates);

            string editable = Slice(templates,
                "x:Key=\"Themed.EditableComboBoxTemplate\"", "</ControlTemplate>");
            string picker = Slice(templates,
                "x:Key=\"Themed.ComboBoxTemplate\"", "</ControlTemplate>");

            // The part the ComboBox contract looks for by name.
            Assert.Contains("PART_EditableTextBox", editable);
            Assert.DoesNotContain("PART_EditableTextBox", picker);

            // And the picker is not crippled by the swap: it keeps its own
            // button and popup, so non-editable combos are untouched.
            Assert.Contains("PART_Button", editable);
            Assert.Contains("Popup", picker);
        }

        [Fact]
        public void Every_open_vocabulary_field_in_the_sheet_store_can_be_typed_into()
        {
            var checked_ = 0;
            foreach (string file in new[] { AddEditSheet, DetailWindow })
            {
                string view = Read(file);
                foreach (string tag in ComboTags(view))
                {
                    string bound = BoundText(tag);
                    if (bound.Length == 0 || !OpenVocabulary.Contains(bound)) continue;

                    checked_++;
                    Assert.True(IsEditable(tag),
                        $"{Path.GetFileName(file)} binds Text={bound} but is not editable — " +
                        "a value the store has never seen cannot be entered.");
                }
            }

            // 6 in the add/edit form and 6 in the detail window. If this drops,
            // a field went missing rather than a field becoming read-only.
            Assert.Equal(12, checked_);
        }

        [Fact]
        public void The_add_sheet_form_can_take_a_category_and_unit_it_has_never_seen()
        {
            string view = Read(StoreView);
            foreach (string bound in new[] { "Category", "Uom" })
            {
                string tag = ComboTags(view).Single(t => BoundText(t) == bound);

                // Editable sibling of the picker style: identical setters, so the
                // chrome does not change — only the ability to type.
                Assert.Contains("ModernEditableComboBoxStyle", tag);
                Assert.Contains("UpdateSourceTrigger=PropertyChanged", tag);
            }
        }

        [Fact]
        public void Enumerated_pickers_stay_pickers()
        {
            // The other side of the same decision. A pane position, a history
            // action and a sort order are closed sets: typing into them cannot
            // produce a valid value, it can only produce a silent no-match or a
            // corrupted row. Manual entry was restored where a new value means
            // something, and deliberately not where it does not.
            string detail = Read(DetailWindow);
            string pane = ComboTags(detail).Single(t => BoundText(t) == "PanePosition");
            Assert.False(IsEditable(pane), "pane position is Outer or Inner; it must not be typeable");

            string history = ComboTags(Read(HistoryView)).Single(t => BoundSelection(t) == "FilterAction");
            Assert.False(IsEditable(history), "history action is a closed set");

            string sort = ComboTags(Read(StoreView)).Single(t => BoundSelection(t) == "SelectedSort");
            Assert.False(IsEditable(sort), "sort order is a closed set");
        }

        // ══ 2. Step 4 → add sheet ═════════════════════════════════════════

        [Fact]
        public void Step_4_arrives_with_the_add_sheet_workspace_already_open()
        {
            string shell = Read(ShellVm);
            string handler = Slice(shell,
                "ProformaInvoiceVM.Step4VM.NavigateToSheetStoreRequested += (_, _) =>", "};");

            int navigate = handler.IndexOf("NavigateFromAgent(\"SheetStore\")", StringComparison.Ordinal);
            int open = handler.IndexOf("SheetStoreVM.OpenAddSheetWorkspace()", StringComparison.Ordinal);

            // Navigate first: it applies the license gate and creates the module
            // VM. Opening the workspace first would either be gated out or run
            // against a VM that is not the one now on screen.
            Assert.True(navigate >= 0, "the handler must navigate to the module");
            Assert.True(open > navigate, "and it must open the add-sheet workspace after navigating");
        }

        [Fact]
        public void The_add_sheet_entry_point_cannot_land_on_consumables()
        {
            string vm = Read(StoreEditorVm);
            string method = Slice(vm, "public void OpenAddSheetWorkspace()", "OpenSheetWorkspace(null);");

            // The module's own ADD NEW branches on whichever view the operator
            // last left open, so on Consumables it adds a consumable. A caller
            // asking for "add sheet" always means glass.
            Assert.Contains("if (IsConsumablesView) StockKind = \"Glass\";", method);

            // And it goes through the same private path as the module's own
            // button — one add-sheet flow, not two that can drift.
            Assert.Contains("OpenSheetWorkspace(null);", vm);
        }

        // ══ 3. the dialog's shape and its buttons ═════════════════════════

        [Fact]
        public void The_sheet_workspace_is_a_dialog_rather_than_a_screen_takeover()
        {
            string view = Read(DetailWindow);
            string window = Slice(view, "<Window x:Class=", ">");

            Assert.DoesNotContain("WindowState=\"Maximized\"", window);
            Assert.Contains("WindowStartupLocation=\"CenterOwner\"", window);
            Assert.Contains("ResizeMode=\"CanResizeWithGrip\"", window);
        }

        [Fact]
        public void Bounding_the_window_cannot_make_it_smaller_than_what_already_fit()
        {
            string window = Slice(Read(DetailWindow), "<Window x:Class=", ">");

            int width = Attr(window, "Width");
            int height = Attr(window, "Height");
            int minWidth = Attr(window, "MinWidth");
            int minHeight = Attr(window, "MinHeight");

            // The minimums are the promise the layout was built against; they
            // were deliberately left untouched. A default smaller than its own
            // minimum would open clipped.
            Assert.True(width >= minWidth, $"Width {width} < MinWidth {minWidth}");
            Assert.True(height >= minHeight, $"Height {height} < MinHeight {minHeight}");

            // Never below what the layout was already built against.
            Assert.True(minWidth >= 980, "MinWidth was lowered below what already fit");
            Assert.True(minHeight >= 640, "MinHeight was lowered below what already fit");
        }

        [Fact]
        public void The_dialog_family_agrees_on_button_widths_per_role()
        {
            var steppers = new List<int>();
            var primaries = new List<int>();

            foreach (string file in new[] { DetailWindow, BuyDialog, UseDialog })
            {
                foreach (string tag in ButtonTags(Read(file)))
                {
                    int? min = MinWidthOf(tag);
                    if (min == null) continue;

                    string content = Regex.Match(tag, "Content=\"([^\"]*)\"").Groups[1].Value;
                    if (content == "−" || content == "+") steppers.Add(min.Value);
                    else if (tag.Contains("PrimaryPillButton")) primaries.Add(min.Value);
                }
            }

            // −/+ sat at 58 in the workspace and 50 in the buy and use dialogs:
            // the same control, three widths apart, in dialogs opened from the
            // same button.
            Assert.NotEmpty(steppers);
            Assert.True(steppers.Distinct().Count() == 1,
                "steppers disagree: " + string.Join(",", steppers.Distinct()));

            // And the workspace's own SAVE was narrower than the RECORD PURCHASE
            // and RECORD USAGE actions sitting beside it.
            Assert.NotEmpty(primaries);
            Assert.True(primaries.Distinct().Count() == 1,
                "primary actions disagree: " + string.Join(",", primaries.Distinct()));
        }

        [Fact]
        public void No_dialog_button_overrides_the_height_its_style_already_sets()
        {
            // The pill styles set Height from a density token. A local Height
            // smaller than the style's padding is what clipped Step 6's ADD
            // button in 14 of the 15 languages this app ships, so the dialog
            // family is not allowed to reintroduce it.
            foreach (string file in new[] { DetailWindow, AddEditSheet, BuyDialog, UseDialog })
            {
                foreach (string tag in ButtonTags(Read(file)))
                {
                    // \s so MinHeight/MaxHeight do not trip a test about Height.
                    Assert.DoesNotMatch(@"\sHeight=""", tag);
                }
            }
        }

        [Fact]
        public void Dialog_shapes_come_from_tokens_rather_than_literals()
        {
            // Verified rather than assumed while redesigning: every corner radius
            // in this family already came from a token, so there was no shape
            // drift to fix. This keeps it that way.
            foreach (string file in new[] { DetailWindow, AddEditSheet, BuyDialog, UseDialog })
            {
                string view = Read(file);
                Assert.DoesNotMatch("CornerRadius=\"[0-9]", view);
            }
        }

        private static int Attr(string tag, string name)
        {
            Match m = Regex.Match(tag, "\\b" + name + "=\"([0-9]+)\"");
            Assert.True(m.Success, name + " is not set on the window");
            return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }
}
