// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatusCheckItem.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement — status filter chip
//
// StatusCheckItem extracted from the large StatementGeneratorViewModel file
// (Phase 11/12 God-VM split) — behaviour unchanged, relocated for
// organisation only.
// ═══════════════════════════════════════════════════════════════════════

using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Pdf;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.Statement.Workflow;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Views.Reports.Workflow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public class StatusCheckItem : ViewModelBase
    {
        public string Status { get; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set { if (_isChecked == value) return; _isChecked = value; OnPropertyChanged(); }
        }

        private int _count;
        public int Count
        {
            get => _count;
            set { if (_count == value) return; _count = value; OnPropertyChanged(); OnPropertyChanged(nameof(CountDisplay)); OnPropertyChanged(nameof(HasCount)); }
        }

        public string CountDisplay => _count.ToString("N0");
        public bool HasCount => _count > 0;

        public StatusCheckItem(string status, bool isChecked = false)
        {
            Status = status;
            _isChecked = isChecked;
        }

        public string StatusLabel => Status switch
        {
            "PartiallyPaid" => "Partially Paid",
            _ => Status
        };

        public string StatusColor => Status switch
        {
            "Paid" => "#047857",
            "PartiallyPaid" => "#047857",
            "Issued" => "#B45309",
            "Overdue" => "#B91C1C",
            "Cancelled" => "#B91C1C",
            "Void" => "#B91C1C",
            _ => "#475569"
        };
    }
}
