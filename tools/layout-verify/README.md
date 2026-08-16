# Layout verification harness

Static XAML analysers used because this environment has **no dotnet SDK**,
so nothing can be compiled here. They are a substitute for a build, not a
replacement — **a real `dotnet build` on Windows remains the authority.**

Run from the repository root:

```bash
python3 tools/layout-verify/xaml_wellformed.py    $(find . -name "*.xaml" -not -path "./.git/*" | sort)
python3 tools/layout-verify/overlap_scan.py       $(find . -name "*.xaml" -not -path "./.git/*" | sort)
python3 tools/layout-verify/scrollviewer_scan.py  $(find . -name "*.xaml" -not -path "./.git/*" | sort)
python3 tools/layout-verify/dockpanel_scan.py     Views/Settings/Sections/*.xaml
python3 tools/layout-verify/text_overflow_scan.py Views/Settings/Sections/*.xaml
python3 tools/layout-verify/width_budget.py 539   Views/Settings/Sections/*.xaml
python3 tools/layout-verify/binding_check.py  <view.xaml> <vm.cs...>
python3 tools/layout-verify/resource_check.py . <view.xaml...>
python3 tools/layout-verify/brace_scan.py     <file.cs...>
```

## What each one catches

| Script | Fault class |
|--------|-------------|
| `xaml_wellformed.py` | malformed XML |
| `overlap_scan.py` | two children in the same Grid cell; missing `Grid.Row`/`Grid.Column` |
| `scrollviewer_scan.py` | **ScrollViewer measuring content at infinite width, making `TextWrapping`/`WrapPanel` inert** |
| `dockpanel_scan.py` | `Dock` ignored on the last child under `LastChildFill=True`; implicit `Dock=Left` |
| `text_overflow_scan.py` | long literal text that cannot wrap |
| `width_budget.py` | rows whose fixed widths exceed the available viewport |
| `binding_check.py` | binding paths with no matching VM member |
| `resource_check.py` | unresolved `StaticResource`/`DynamicResource` keys |
| `brace_scan.py` | unbalanced braces in `.cs` |

## Known accepted findings (verified by-design, do not "fix")

- `MainWindow.xaml` — `Grid`/`Track` share a cell: that is the `Track` part
  of a Slider `ControlTemplate`.
- `PrintPreviewView.xaml` — horizontal scrolling is intentional; it previews
  a fixed-width page.
- `OtherChargesView.xaml` — the flagged `ScrollViewer` is `PART_ContentHost`
  inside a `TextBox` template.
- `resource_check.py` flags resource-like prose inside XML comments.
