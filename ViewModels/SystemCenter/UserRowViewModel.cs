// ═══════════════════════════════════════════════════════════════════════
// FILE: UserRowViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: SystemCenterViewModel.cs
//
// UserRowViewModel split out of SystemCenterViewModel.cs (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Audit;
using ProGlassAutomation.Services.Audit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SystemCenter
{
    public class UserRowViewModel : ViewModelBase
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "";
        public string CreatedDisplay { get; set; } = "";

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private bool _isCurrent;
        /// <summary>True when this user is the active session user.</summary>
        public bool IsCurrent
        {
            get => _isCurrent;
            set { _isCurrent = value; OnPropertyChanged(); }
        }

        public string RoleBadge => Role switch
        {
            "Administrator" or "Admin" => "🛡 Administrator",
            "Manager" => "⭐ Manager",
            "Estimator" => "📐 Estimator",
            "Sales" => "💼 Sales",
            "Production" => "🏭 Production",
            "Store" or "Operator" => "📦 Store",
            "Accountant" => "🧮 Accountant",
            _ => "👁 Viewer"
        };

        /// <summary>How many permissions this role carries.</summary>
        public string PermissionCount =>
            ProGlassAutomation.Services.Audit.RolePermissions
                .For(Role).Count + " permissions";
    }
}
