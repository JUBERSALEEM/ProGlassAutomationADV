🪟 ProGlass Automation

«Professional ERP-grade desktop software for glass manufacturing and production management.
Built with WPF (.NET 8) using MVVM architecture, modular design, SQLite databases, reporting, AI integration, and production workflow automation.»

---

🚀 Project Status

"Platform" (https://img.shields.io/badge/Platform-Windows%2010%2F11-blue)
"Framework" (https://img.shields.io/badge/.NET-8-purple)
"Language" (https://img.shields.io/badge/C%23-Latest-green)
"UI" (https://img.shields.io/badge/UI-WPF-orange)
"Architecture" (https://img.shields.io/badge/Pattern-MVVM-red)
"Database" (https://img.shields.io/badge/Database-SQLite-success)

---

📌 Overview

ProGlass Automation is a modern Windows desktop ERP application built specifically for glass manufacturing companies. It manages the complete production lifecycle from quotation to dispatch while providing reporting, inventory management, optimization, document generation, AI-powered tools, and offline operation.

The software is designed for high performance, scalability, maintainability, and enterprise-level workflow management.

---

✨ Features

📋 Sales

- Proforma Invoice
- Tax Invoice
- Delivery Note
- Customer Statements
- Reports
- PDF Generation
- Excel Export
- CSV Import/Export

🏭 Production

- Job Orders
- Production Tracking
- Glass Optimization
- Sheet Store
- Glass Specifications
- Material Usage
- Production Dashboard

📊 Reporting

- Dashboard
- Sales Reports
- Statement Generator
- Excel Reports
- PDF Reports
- CSV Export

🤖 AI Features

- Local AI Assistant
- Whisper Speech Recognition
- Voice Commands
- Offline Processing
- AI Chat
- Voice Interaction

💾 Data

- SQLite Databases
- EF Core
- Automatic Backups
- Offline First
- Multi Database Architecture

---

🏗 Architecture

- .NET 8
- WPF
- MVVM
- Entity Framework Core
- SQLite
- Repository Pattern
- Service Layer
- Dependency Injection Ready
- Modular Architecture

---

📂 Quick Links

- Project: https://github.com/JUBERSALEEM/ProGlassAutomationADV
- Docs: "docs/" (see "docs/Chat5", "docs/Chat5-9", "docs/Chat6")
- New helper documents:
  - BUG_FIXES.md — high-priority defects to fix
  - PERFORMANCE_OPTIMIZATION.md — performance & smoothness checklist
  - UI_UX_GUIDELINES.md — professional UI/UX style guide
  - MASTER_PROMPTS.md — master prompts for developer chat tasks
  - PROJECT_STRUCTURE.md — canonical project tree (top-level)

---

📚 Documentation

This repository was audited and several helper documents were added to help triage, fix, and optimize the project.

Files added/updated:

- "BUG_FIXES.md" — prioritized bug list by module for step-by-step fixes.
- "PERFORMANCE_OPTIMIZATION.md" — concrete checklist and techniques to reach stable 60/90/120/144 Hz UI.
- "UI_UX_GUIDELINES.md" — professional UI/UX rules, accessibility, and style recommendations.
- "MASTER_PROMPTS.md" — ready-to-use prompts for automated / human-assisted bug fixing and PR drafting.
- "PROJECT_STRUCTURE.md" — up-to-date top-level project structure and key files.
- "docs/Chat6/README.md" — new chat6 folder (placeholder for next-phase docs and transcripts).

---

📖 Short Project Overview

ProGlass Automation is a Windows desktop application for managing glass production workflows:

- Inventory (Sheet Store)
- Proforma Invoices
- Job Orders
- Delivery Notes
- Tax Invoices
- Statements
- Reports
- Dashboard
- PDF Generation
- Excel Export
- CSV Import/Export

The application follows a modular ViewModel-first MVVM architecture where each module is dynamically loaded into the MainWindow through DataTemplate bindings.

---

🛠 Technology Stack

Language

- C#
- .NET 8

UI

- WPF
- XAML
- MVVM

Database

- Entity Framework Core
- SQLite

Libraries

- ClosedXML
- CsvHelper
- QuestPDF
- PdfSharp-WPF
- SkiaSharp
- Whisper.net
- NAudio
- System.Speech

---

📘 How to Use the Helper Files

- Start with "BUG_FIXES.md": address Critical → High issues in order. Each item includes file references.
- Use "PERFORMANCE_OPTIMIZATION.md" when optimizing UI for smoothness and batching work off the UI thread.
- Follow "UI_UX_GUIDELINES.md" to harmonize styles and ensure accessibility.
- Use "MASTER_PROMPTS.md" when collaborating with assistants or bots to prepare patches, PRs, or code reviews.
- The file "PROJECT_STRUCTURE.md" lists top-level folders and important files for onboarding and navigation.

---

💻 Run the Application

Requirements

- Windows 10 / Windows 11
- .NET 8 SDK
- Visual Studio 2022 or newer
- .NET Desktop Development Workload

Clone Repository

git clone https://github.com/JUBERSALEEM/ProGlassAutomationADV.git

cd ProGlassAutomationADV

Open Project

Open

ProGlassAutomation.slnx

or

ProGlassAutomation.csproj

inside Visual Studio.

Press F5 to run.

---

📦 Releases

GitHub Actions automatically builds Release packages and publishes build artifacts.

Release builds are available under the repository Releases page after successful CI builds.

---

💾 Application Data

When running, the application creates local SQLite databases under:

%AppData%/ProGlassAutomation/

Back up these files before making database schema changes.

---

📂 Project Structure

See "PROJECT_STRUCTURE.md" for the canonical folder tree and descriptions of all important modules.

---

🤝 Contributing

1. Read "BUG_FIXES.md".
2. Pick a high-priority issue.
3. Create a branch:

fix/<short-description>

4. Implement the fix.
5. Run the application.
6. Verify exports and reports.
7. Submit a Pull Request with a detailed description.

---

📄 License

This project is currently maintained by the author. Licensing terms may be updated in future releases.

---

<div align="center">🪟 ProGlass Automation

Professional Glass Manufacturing ERP

Built with passion by Juber Saleem

Powered by .NET 8 • WPF • MVVM • SQLite • Entity Framework Core

</div>
