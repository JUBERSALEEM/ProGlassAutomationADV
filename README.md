# 🪟 ProGlass Automation

> Purpose-built desktop software for **Glass Production Workflow** —
> built with WPF, C#, and clean MVVM architecture.

---

## 📸 Screenshots

> *(Add screenshots here after first run)*

| Dashboard (Light) | Dashboard (Dark) |
|---|---|
| ![light](docs/screenshots/dashboard-light.png) | ![dark](docs/screenshots/dashboard-dark.png) |

---

## 🧰 Tech Stack

| Technology | Details |
|---|---|
| **Language** | C# (.NET 8) |
| **UI Framework** | WPF (Windows Presentation Foundation) |
| **Pattern** | MVVM (Model-View-ViewModel) |
| **Theme System** | Dynamic ResourceDictionary switching |
| **IDE** | Visual Studio 2022 |
| **Platform** | Windows |

---

## ✨ Features

- 🪟 **Full-screen module host** — each module renders edge-to-edge
- 🍔 **Floating hamburger menu** (top-left) — toggles side navigation drawer
- 🔵 **Floating logo button** (top-right) — opens settings drawer
- ℹ️ **Info button** — opens About module
- 🌙 **Global Light / Dark theme** — switches instantly across all modules
- 🗂️ **MVVM navigation** — ViewModel-first via DataTemplates
- 🎨 **Card-style UI** — rounded corners, drop shadows, accent colors
- 📱 **Responsive layout** — scrollable, adapts to any window size

---

## 📦 Modules

### 🏠 Dashboard
- Welcome message and business overview
- Stat cards: Total Sales, Invoices, Customers, Pending Orders
- Recent Activity feed with timestamps

### 🧾 Proforma Invoice
- Invoice details form (Invoice No., Date, Customer, GSTIN, Contact)
- Line items table with Description, Qty, Rate, Amount
- GST calculation and Grand Total summary
- Save Draft and Generate PDF action buttons

### ℹ️ About
- Large app icon and ProGlass Automation branding
- App name, tagline, and version badge
- About section describing the product
- Developer information card
- Key Features overview
- Copyright footer

---

## 🏗️ Project Structure

```
ProGlassAutomation/
├── App.xaml                              # App entry, default theme loaded
├── App.xaml.cs
├── MainWindow.xaml                       # Shell: floating buttons, drawers, module host
├── MainWindow.xaml.cs
│
├── Commands/
│   └── RelayCommand.cs                   # ICommand implementation
│
├── Services/
│   └── ThemeManager.cs                   # Global light/dark theme switcher
│
├── Themes/
│   ├── LightTheme.xaml                   # Light palette ResourceDictionary
│   └── DarkTheme.xaml                    # Dark palette ResourceDictionary
│
├── ViewModels/
│   ├── ViewModelBase.cs                  # INotifyPropertyChanged base
│   ├── MainViewModel.cs                  # Shell VM: navigation, drawers, theme
│   ├── Dashboard/
│   │   └── DashboardViewModel.cs
│   ├── ProformaInvoice/
│   │   └── ProformaInvoiceViewModel.cs
│   └── About/
│       └── AboutViewModel.cs
│
└── Views/
    ├── Dashboard/
    │   ├── DashboardView.xaml
    │   └── DashboardView.xaml.cs
    ├── ProformaInvoice/
    │   ├── ProformaInvoiceView.xaml
    │   └── ProformaInvoiceView.xaml.cs
    └── About/
        ├── AboutView.xaml
        └── AboutView.xaml.cs
```

---

## 🎨 Theme System

The app uses a `ThemeManager` static service to swap
`ResourceDictionary` files at runtime.

### Available Themes

| Key | Light Value | Dark Value |
|---|---|---|
| `WindowBackgroundBrush` | `#F4F6FA` | `#0F172A` |
| `ModuleBackgroundBrush` | `#FFFFFF` | `#1E293B` |
| `SideMenuBackgroundBrush` | `#FFFFFF` | `#1E293B` |
| `PrimaryForegroundBrush` | `#1F2937` | `#F1F5F9` |
| `SecondaryForegroundBrush` | `#6B7280` | `#94A3B8` |
| `AccentBrush` | `#1E88E5` | `#3B82F6` |
| `MenuHoverBrush` | `#EEF2F7` | `#334155` |
| `OverlayButtonBackgroundBrush` | `#1E88E5` | `#3B82F6` |

### How to Switch Theme

```csharp
// In code
ThemeManager.ApplyTheme(AppTheme.Dark);
ThemeManager.ApplyTheme(AppTheme.Light);
ThemeManager.ToggleTheme();

// Via ViewModel binding (Settings drawer toggle)
IsDarkTheme = true; // triggers ThemeManager.ApplyTheme(AppTheme.Dark)
```

---

## 🧩 MVVM Navigation

Navigation is handled via **ViewModel-first** pattern using
`DataTemplate` entries in `MainWindow.xaml`.

### How It Works

```
User clicks menu item
        ↓
NavigateCommand fires with string parameter
        ↓
MainViewModel.Navigate() sets CurrentViewModel
        ↓
ContentControl renders matching DataTemplate
        ↓
Module View appears full-screen
```

### Adding a New Module (3 steps)

**Step 1** — Create ViewModel:
```csharp
// ViewModels/Reports/ReportsViewModel.cs
namespace ProGlassAutomation.ViewModels.Reports
{
    public class ReportsViewModel : ViewModelBase
    {
        public string Title { get; } = "Reports";
    }
}
```

**Step 2** — Create View:
```xml
<!-- Views/Reports/ReportsView.xaml -->
<UserControl x:Class="ProGlassAutomation.Views.Reports.ReportsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Background="{DynamicResource WindowBackgroundBrush}">
        <!-- module content here -->
    </Grid>
</UserControl>
```

**Step 3** — Register in `MainWindow.xaml` + `MainViewModel.cs`:
```xml
<!-- MainWindow.xaml -->
<DataTemplate DataType="{x:Type vmReports:ReportsViewModel}">
    <viewsReports:ReportsView/>
</DataTemplate>
```
```csharp
// MainViewModel.cs — Navigate()
case "Reports":
    CurrentViewModel = ReportsVM;
    break;
```

---

## 🚀 Getting Started

### Prerequisites

- Windows 10 / 11
- Visual Studio 2022 (with .NET Desktop Development workload)
- .NET 8 SDK

### Run the App

```bash
# Clone the repository
git clone https://github.com/<your-username>/ProGlassAutomation.git

# Open in Visual Studio
start ProGlassAutomation.sln

# Press F5 to build and run
```

---

## 👨‍💻 Developer

| Field | Details |
|---|---|
| **Name** | Juber Saleem |
| **Email** | [Jubersaleem@gmail.com](mailto:Jubersaleem@gmail.com) |
| **Country** | India 🇮🇳 |
| **Project** | ProGlass Automation |
| **Purpose** | Software for Glass Production Workflow |

---

## 📋 Changelog

### v1.0.0 — Initial Release

```
feat: initial ProGlass Automation WPF app with MVVM, theming, and About module

- WPF MVVM project structure with Commands, Services, Themes, ViewModels, Views
- MainWindow shell: full-screen module host, floating hamburger, logo, info buttons
- Floating buttons overlay with drop shadow, rounded style, always on top
- Side menu drawer with card shape, rounded corners, icon + label items
- Settings drawer with dark mode toggle
- Global Light/Dark theme via ThemeManager and dynamic ResourceDictionary
- Dashboard module: stat cards, activity feed
- Proforma Invoice module: form, items table, GST total, action buttons
- About module: app icon, developer info (Juber Saleem), features, copyright
- Per-module folder structure: ViewModels/X and Views/X per module
- ViewModel-first navigation via DataTemplates
```

---

## 📄 License

```
MIT License

Copyright (c) 2025 Juber Saleem

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

<div align="center">
  <strong>ProGlass Automation</strong> — Built with ❤️ in India by Juber Saleem
</div>