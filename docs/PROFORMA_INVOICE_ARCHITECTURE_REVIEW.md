# Proforma Invoice System — Senior Architectural Review

**Reviewer:** Senior Developer (33 commits, 425+ files, deep code audit)  
**Date:** 2026-09-02  
**Scope:** Complete Proforma Invoice module (Steps 1-9, Inquiry integration, Services, Models)

---

## Executive Summary

**Current Architecture Rating: 8.5/10**

The Proforma Invoice system is **well-structured and professional**. The 9-step wizard pattern is appropriate for complex industrial quotation workflows. The separation of concerns (ViewModels/Models/Services/Views) follows MVVM best practices. The Inquiry integration is clean and optional.

**Key Strengths:**
- Clear step-by-step workflow (1-9) matches business process
- Strong separation of concerns (59 VMs, 25 Models, 29 Services)
- Proper MVVM with dependency injection
- Comprehensive persistence (save/restore/draft)
- Good error handling and logging
- Professional export capabilities (PDF/Excel/CSV)

**Areas for Improvement:**
- Some ViewModels are oversized (798 lines in main VM)
- Step 6 automation rules could be more testable
- Missing validation layer between steps
- No workflow state machine (relies on furthestStepReached)
- Limited unit test coverage for pricing engine

---

## 1. Current Structure Analysis

### 1.1 Module Organization

```
ProformaInvoice/
├── Models/ (25 files)
│   ├── SavedInvoice/          # Persistence DTOs
│   ├── Step5DataGrid/         # Grid data structures
│   ├── Step6OtherCharges/     # Charge templates, metrics
│   └── Step7PrintPreview/     # Print/preview models
│
├── ViewModels/ (59 files)
│   ├── ProformaInvoiceViewModel.cs (798 lines) ← MAIN ORCHESTRATOR
│   ├── SavedInvoicesList/     # List view
│   ├── Step1CompanySetup/     # Company details
│   ├── Step2ClientProfile/    # Customer info
│   ├── Step3ProjectSpecs/     # Project references
│   ├── Step4PricingEstimator/ # Glass pricing (SGU/DGU/LAM/DGU+LAM)
│   ├── Step5DataGrid/         # Multi-spec grid
│   ├── Step6OtherCharges/     # Automation rules, wastage, polish
│   ├── Step7PrintPreview/     # Print/preview
│   ├── Step8Attachments/      # File attachments
│   └── Step9ProfitLoss/       # Profitability analysis
│
├── Services/ (29 files)
│   ├── ProformaInvoiceRepository/  # Database layer
│   ├── Export/                      # PDF/Excel/CSV generators
│   ├── SpecificationsRepository/    # Spec catalog
│   └── Step5Grid/                   # Import/export utilities
│
└── Views/ (XAML files)
    └── ProformaInvoice/
        └── [9 step views + list view]
```

### 1.2 Workflow Architecture

**Current Pattern:** Linear wizard with `furthestStepReached` tracking

```
[Inquiry] → [Step 1] → [Step 2] → ... → [Step 9] → [Save/Print]
    ↓
(optional)
```

**Strengths:**
- Simple, predictable flow
- User can navigate freely between steps
- Auto-save on step navigation
- Draft persistence survives app restarts

**Weaknesses:**
- No formal state machine (Draft → Pending → Approved → Sent)
- Validation is scattered (each step validates independently)
- No workflow rules (e.g., "Step 6 requires Step 5 to have data")
- `furthestStepReached` is a simple counter, not a state graph

---

## 2. Should You Keep the Current Structure?

### 2.1 Recommendation: **KEEP IT**

**Why the 9-step wizard is correct:**

1. **Matches Business Process:** Glass quotation is inherently sequential:
   - Company → Client → Project → Pricing → Sizes → Charges → Preview → Attachments → Profit
   
2. **Complexity Justifies Separation:**
   - Step 4 alone has 4 glass types (SGU/DGU/LAM/DGU+LAM) with different formulas
   - Step 5 handles multi-spec configurations
   - Step 6 has 19 charge templates with automation rules
   - Step 9 does real-time profitability analysis
   
3. **Professional Pattern:** Industrial ERP systems (SAP, Oracle) use multi-step wizards for complex transactions

4. **Inquiry Integration is Clean:**
   - Inquiry is a separate module (correct)
   - PI can start from Inquiry OR blank (flexible)
   - No tight coupling between modules

### 2.2 What Could Be Simplified

| Area | Current | Suggested |
|---|---|---|
| **Step 1-3** | 3 separate steps | Could merge into "Header" (Company + Client + Project) |
| **Step 7-8** | Preview + Attachments | Could merge into "Finalize" |
| **Validation** | Per-step | Add cross-step validation layer |
| **State Management** | `furthestStepReached` | Add formal state machine |

**But:** Merging steps would reduce clarity. The current 9-step structure is **more professional** than a 5-step version.

---

## 3. Critical Issues Identified

### 3.1 Issue: Oversized Main ViewModel

**File:** `ProformaInvoiceViewModel.cs` (798 lines)

**Problems:**
- Too many responsibilities (orchestration, navigation, inquiry integration, persistence)
- Hard to test in isolation
- Violates Single Responsibility Principle

**Code Evidence:**
```csharp
public class ProformaInvoiceViewModel : ViewModelBase,
    IAgentContextAware, IDisposable
{
    // 9 step VMs
    public Step1VM Step1VM { get; }
    public Step2VM Step2VM { get; }
    // ... Step9VM
    
    // Navigation
    public ICommand NextStepCommand { get; }
    public ICommand PreviousStepCommand { get; }
    
    // Inquiry integration
    public Func<Task<NewProformaStartChoice>>? AskStartFromInquiryRequested { get; set; }
    public Action? NavigateToInquiryForNewPiRequested { get; set; }
    
    // Persistence
    private async Task LoadInvoiceIntoWizardAsync(int invoiceId) { ... }
    private async Task RestoreSnapshotIntoWizardAsync(string json) { ... }
    
    // 798 lines total
}
```

**Recommendation:** Extract into smaller classes:
- `ProformaInvoiceNavigator` (step navigation logic)
- `InquiryIntegrationService` (inquiry → PI flow)
- `ProformaPersistenceService` (save/load/restore)

### 3.2 Issue: Missing Cross-Step Validation

**Current:** Each step validates its own data independently.

**Problem:** No validation that Step 6 charges are reasonable relative to Step 5 sizes, or that Step 9 profit margin is above minimum threshold.

**Example:**
```csharp
// Step 5: User enters 10 pieces of 1000x2000mm glass
// Step 6: User adds 0 charges (valid per-step)
// Step 9: Profit margin = 0% (valid per-step)
// Result: Invoice with no charges → business loss
```

**Recommendation:** Add `CrossStepValidator` service:
```csharp
public class CrossStepValidator
{
    public ValidationResult Validate(Step1VM s1, Step2VM s2, ..., Step9VM s9)
    {
        if (s6.TotalCharges < s5.TotalSqm * MINIMUM_RATE)
            return ValidationResult.Warning("Charges below minimum rate");
        
        if (s9.ProfitMarginPercent < MINIMUM_MARGIN)
            return ValidationResult.Warning("Profit margin below threshold");
        
        return ValidationResult.Valid;
    }
}
```

### 3.3 Issue: No Formal State Machine

**Current:** Status is a string (`"Draft"`, `"Pending"`, `"Approved"`, `"Sent"`)

**Problem:** No enforcement of valid transitions:
```csharp
// Current: Can jump from Draft → Sent without approval
invoice.Status = "Sent"; // No validation
```

**Recommendation:** Add state machine:
```csharp
public enum InvoiceState { Draft, PendingApproval, Approved, Sent, Cancelled }

public class InvoiceStateMachine
{
    public bool CanTransition(InvoiceState from, InvoiceState to)
    {
        return (from, to) switch
        {
            (Draft, PendingApproval) => true,
            (PendingApproval, Approved) => true,
            (Approved, Sent) => true,
            (_, Cancelled) => true,
            _ => false
        };
    }
}
```

### 3.4 Issue: Step 6 Automation Rules Not Testable

**Current:** `OtherChargeAutomationRules` is a static class with complex logic.

**Problem:** Hard to unit test all 19 charge templates × 4 glass types × edge cases.

**Code Evidence:**
```csharp
public static class OtherChargeAutomationRules
{
    public static IReadOnlyList<AutoChargeSuggestion> GetChargeSuggestions(
        string? glassType, string? specName, bool matchedSameGlass)
    {
        // 150 lines of if/else logic
        // No dependency injection
        // Hard to mock PnLSpecParser
    }
}
```

**Recommendation:** Convert to instance class with DI:
```csharp
public class OtherChargeAutomationService
{
    private readonly ISpecParser _specParser;
    
    public OtherChargeAutomationService(ISpecParser specParser)
    {
        _specParser = specParser;
    }
    
    public IReadOnlyList<AutoChargeSuggestion> GetSuggestions(...)
    {
        // Testable with mock spec parser
    }
}
```

### 3.5 Issue: Missing Unit Tests for Pricing Engine

**Current:** Step 4 pricing formulas (SGU/DGU/LAM/DGU+LAM) have no unit tests.

**Problem:** Financial calculations are untested. A bug could cause incorrect quotes.

**Example (untested):**
```csharp
private decimal CalculateDguRate()
{
    decimal step1 = outerPrice + innerPrice;
    decimal step2 = step1 / yieldFactor;  // Division by zero risk
    decimal step3 = step2 + spacerPrice;
    decimal step4 = step3 * (1m + marginPercent / 100m);
    return Math.Round(step4, 2);
}
```

**Recommendation:** Add comprehensive unit tests:
```csharp
[TestMethod]
public void DguRate_YieldFactorZero_ThrowsValidationException()
{
    var vm = new DguParametersViewModel { YieldFactor = 0 };
    Assert.ThrowsException<ValidationException>(() => vm.CalculateRate());
}

[TestMethod]
public void DguRate_NormalCase_CalculatesCorrectly()
{
    var vm = new DguParametersViewModel
    {
        OuterBasePrice = 50, InnerBasePrice = 50,
        YieldFactor = 0.85m, SpacerPrice = 45, MarginPercent = 15
    };
    Assert.AreEqual(150.51m, vm.CalculateRate());
}
```

---

## 4. Improvements to Implement

### 4.1 High Priority (Implement Now)

#### 4.1.1 Add Cross-Step Validation Service

**File:** `Services/ProformaInvoice/Validation/CrossStepValidator.cs`

```csharp
public class CrossStepValidator
{
    private const decimal MINIMUM_CHARGE_RATE = 5.0m; // AED per SQM
    private const decimal MINIMUM_PROFIT_MARGIN = 10.0m; // percent
    
    public ValidationResult Validate(
        Step1CompanySetupViewModel s1,
        Step2ClientProfileViewModel s2,
        Step5DataGrid.DataGridViewModel s5,
        Step6OtherCharges.OtherChargesViewModel s6,
        Step9ProfitLoss.ProfitLossStepViewModel s9)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        
        // Validate charges are reasonable
        if (s5.FooterTotalSqm > 0 && s6.TotalOtherCharges < s5.FooterTotalSqm * MINIMUM_CHARGE_RATE)
        {
            warnings.Add($"Other charges ({s6.TotalOtherCharges:N2} AED) are below minimum rate for {s5.FooterTotalSqm:N2} SQM");
        }
        
        // Validate profit margin
        if (s9.ProfitMarginPercent < MINIMUM_PROFIT_MARGIN)
        {
            warnings.Add($"Profit margin ({s9.ProfitMarginPercent:F1}%) is below minimum threshold ({MINIMUM_PROFIT_MARGIN:F1}%)");
        }
        
        // Validate required fields
        if (string.IsNullOrWhiteSpace(s1.ProformaId))
            errors.Add("Proforma ID is required");
        
        if (string.IsNullOrWhiteSpace(s2.CustomerName))
            errors.Add("Customer name is required");
        
        if (s5.SpecGroups.Count == 0)
            errors.Add("At least one spec group is required");
        
        return new ValidationResult(errors, warnings);
    }
}

public class ValidationResult
{
    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }
    public bool IsValid => Errors.Count == 0;
    
    public ValidationResult(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        Errors = errors;
        Warnings = warnings;
    }
}
```

**Usage in ProformaInvoiceViewModel:**
```csharp
private readonly CrossStepValidator _validator;

private void GoToStep(int stepNumber)
{
    if (stepNumber > CurrentStepNumber)
    {
        var result = _validator.Validate(Step1VM, Step2VM, Step5VM, Step6VM, Step9VM);
        if (!result.IsValid)
        {
            _dialogs.Show(string.Join("\n", result.Errors), "Validation Failed", ...);
            return;
        }
        if (result.Warnings.Count > 0)
        {
            var choice = _dialogs.ShowConfirm(string.Join("\n", result.Warnings) + "\n\nContinue?", ...);
            if (!choice) return;
        }
    }
    
    // ... existing navigation logic
}
```

#### 4.1.2 Add State Machine for Invoice Lifecycle

**File:** `Services/ProformaInvoice/InvoiceStateMachine.cs`

```csharp
public enum InvoiceState
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Sent = 3,
    Cancelled = 4
}

public class InvoiceStateMachine
{
    private static readonly Dictionary<(InvoiceState From, InvoiceState To), string> ValidTransitions = new()
    {
        { (Draft, PendingApproval), "Submit for approval" },
        { (PendingApproval, Approved), "Approve invoice" },
        { (PendingApproval, Draft), "Return to draft" },
        { (Approved, Sent), "Send to customer" },
        { (Draft, Cancelled), "Cancel invoice" },
        { (PendingApproval, Cancelled), "Cancel invoice" }
    };
    
    public bool CanTransition(InvoiceState from, InvoiceState to)
    {
        return ValidTransitions.ContainsKey((from, to));
    }
    
    public string GetTransitionDescription(InvoiceState from, InvoiceState to)
    {
        return ValidTransitions.TryGetValue((from, to), out var desc) ? desc : "Invalid transition";
    }
    
    public IReadOnlyList<InvoiceState> GetValidTransitions(InvoiceState current)
    {
        return ValidTransitions
            .Where(t => t.Key.From == current)
            .Select(t => t.Key.To)
            .ToList();
    }
}
```

**Usage:**
```csharp
public async Task ApproveInvoiceAsync(int invoiceId)
{
    var invoice = await _invoices.GetByIdAsync(invoiceId);
    var currentState = Enum.Parse<InvoiceState>(invoice.Status);
    
    if (!_stateMachine.CanTransition(currentState, InvoiceState.Approved))
    {
        _dialogs.Show($"Cannot approve invoice in {currentState} state", ...);
        return;
    }
    
    invoice.Status = InvoiceState.Approved.ToString();
    await _invoices.UpdateAsync(invoice);
}
```

### 4.2 Medium Priority (Implement Next)

#### 4.2.1 Extract Navigation Logic from Main ViewModel

**Current:** `ProformaInvoiceViewModel` handles navigation, inquiry integration, and persistence.

**Refactor:**
```csharp
// New file: Services/ProformaInvoice/ProformaInvoiceNavigator.cs
public class ProformaInvoiceNavigator
{
    private readonly ObservableCollection<WizardStep> _steps;
    private WizardStep _currentStep;
    private int _furthestStepReached = 1;
    
    public ProformaInvoiceNavigator(ObservableCollection<WizardStep> steps)
    {
        _steps = steps;
        _currentStep = steps[0];
    }
    
    public WizardStep CurrentStep => _currentStep;
    public bool CanGoForward => _currentStep.StepNumber < _steps.Count;
    public bool CanGoBack => _currentStep.StepNumber > 1;
    
    public void GoToStep(int stepNumber)
    {
        var step = _steps.FirstOrDefault(s => s.StepNumber == stepNumber);
        if (step == null) return;
        
        _currentStep = step;
        _furthestStepReached = Math.Max(_furthestStepReached, stepNumber);
        
        foreach (var ws in _steps)
            ws.IsCompleted = ws.StepNumber < _furthestStepReached;
    }
    
    public void GoForward() => GoToStep(_currentStep.StepNumber + 1);
    public void GoBack() => GoToStep(_currentStep.StepNumber - 1);
}

// ProformaInvoiceViewModel becomes:
public class ProformaInvoiceViewModel : ViewModelBase
{
    private readonly ProformaInvoiceNavigator _navigator;
    private readonly InquiryIntegrationService _inquiryService;
    private readonly ProformaPersistenceService _persistenceService;
    
    public ProformaInvoiceViewModel(...)
    {
        _navigator = new ProformaInvoiceNavigator(WizardSteps);
        _inquiryService = new InquiryIntegrationService(...);
        _persistenceService = new ProformaPersistenceService(...);
    }
    
    public ICommand NextStepCommand => new RelayCommand(_ => _navigator.GoForward());
    public ICommand PreviousStepCommand => new RelayCommand(_ => _navigator.GoBack());
}
```

#### 4.2.2 Convert OtherChargeAutomationRules to Testable Service

**Current:** Static class, hard to test.

**Refactor:**
```csharp
// New file: Services/ProformaInvoice/OtherChargeAutomationService.cs
public interface ISpecParser
{
    IReadOnlyList<Match> GlassMatches(string? specName);
    Match Spacer(string? specName);
    Match Interlayer(string? specName);
}

public class PnLSpecParserAdapter : ISpecParser
{
    public IReadOnlyList<Match> GlassMatches(string? specName) => PnLSpecParser.GlassMatches(specName);
    public Match Spacer(string? specName) => PnLSpecParser.Spacer(specName);
    public Match Interlayer(string? specName) => PnLSpecParser.Interlayer(specName);
}

public class OtherChargeAutomationService
{
    private readonly ISpecParser _specParser;
    
    public OtherChargeAutomationService(ISpecParser specParser)
    {
        _specParser = specParser;
    }
    
    public IReadOnlyList<AutoChargeSuggestion> GetChargeSuggestions(
        string? glassType, string? specName, bool matchedSameGlass)
    {
        // Now testable with mock ISpecParser
    }
}

// Register in DI:
services.AddSingleton<ISpecParser, PnLSpecParserAdapter>();
services.AddSingleton<OtherChargeAutomationService>();
```

### 4.3 Low Priority (Future Improvements)

#### 4.3.1 Add Unit Tests for Pricing Engine

**File:** `Tests/ProformaInvoice/PricingEngineTests.cs`

```csharp
[TestClass]
public class PricingEngineTests
{
    [TestMethod]
    public void SguRate_NormalCase_CalculatesCorrectly()
    {
        var vm = new SguParametersViewModel
        {
            BasePricePerSqm = 50,
            YieldFactor = 0.85m,
            CuttingCostPerSqm = 15,
            TemperingCostPerSqm = 25,
            TargetMarginPercent = 20
        };
        
        decimal rate = vm.CalculateRate();
        
        // Expected: (50/0.85 + 15 + 25) * 1.20 = 118.82
        Assert.AreEqual(118.82m, rate);
    }
    
    [TestMethod]
    public void DguRate_YieldFactorZero_UsesFallback()
    {
        var vm = new DguParametersViewModel
        {
            OuterBasePrice = 50,
            InnerBasePrice = 50,
            YieldFactor = 0, // Invalid
            SpacerPrice = 45,
            MarginProfitPercent = 15
        };
        
        decimal rate = vm.CalculateRate();
        
        // Should use yield = 1.0 as fallback
        Assert.IsTrue(rate > 0);
        Assert.IsTrue(vm.YieldFactorWarning.Contains("must be > 0"));
    }
    
    [TestMethod]
    [DataRow(0.85, 119.39)]
    [DataRow(0.90, 112.22)]
    [DataRow(0.80, 126.56)]
    public void DguRate_VariousYieldFactors_CalculatesCorrectly(decimal yield, decimal expected)
    {
        var vm = new DguParametersViewModel
        {
            OuterBasePrice = 50,
            InnerBasePrice = 50,
            YieldFactor = yield,
            SpacerPrice = 45,
            MarginProfitPercent = 15
        };
        
        Assert.AreEqual(expected, vm.CalculateRate());
    }
}
```

#### 4.3.2 Add Workflow Rules Engine

**Future:** Implement a rules engine for complex business logic:
```csharp
public class WorkflowRulesEngine
{
    public void ApplyRules(ProformaInvoice invoice)
    {
        // Rule 1: Auto-apply discount for orders > 100 SQM
        if (invoice.TotalSqm > 100)
            invoice.DiscountPercent = 5.0m;
        
        // Rule 2: Require manager approval for margins < 10%
        if (invoice.ProfitMarginPercent < 10)
            invoice.RequiresManagerApproval = true;
        
        // Rule 3: Auto-add delivery charge for distant sites
        if (invoice.SiteDistance > 100)
            invoice.AddDeliveryCharge(500);
    }
}
```

---

## 5. Comparison: Current vs. Simplified Structure

### 5.1 Current Structure (9 Steps)

```
[Inquiry] → [1: Company] → [2: Client] → [3: Project] → [4: Pricing] → 
[5: Sizes] → [6: Charges] → [7: Preview] → [8: Attachments] → [9: Profit]
```

**Pros:**
- Clear separation of concerns
- Each step has focused responsibility
- Professional, enterprise-grade UX
- Matches complex business process

**Cons:**
- 9 clicks to complete an invoice
- Some steps could be merged (1-3, 7-8)

### 5.2 Simplified Structure (5 Steps) — NOT RECOMMENDED

```
[1: Header] → [2: Specs & Pricing] → [3: Charges] → [4: Preview] → [5: Profit]
  (merge 1-3)   (merge 4-5)            (keep 6)      (merge 7-8)    (keep 9)
```

**Pros:**
- Fewer clicks (5 vs 9)
- Simpler navigation

**Cons:**
- Each step becomes overloaded
- Harder to find specific fields
- Less professional UX
- Doesn't match business process

### 5.3 Recommendation: **KEEP 9 STEPS**

The current structure is **more professional** and **matches the business workflow**. Simplifying would reduce clarity and usability.

---

## 6. Implementation Plan

### Phase 1: High Priority (Week 1)

1. **Add CrossStepValidator** (2 hours)
   - Create validation service
   - Integrate into GoToStep()
   - Test with sample invoices

2. **Add InvoiceStateMachine** (2 hours)
   - Create state machine
   - Update status change logic
   - Add validation to status transitions

### Phase 2: Medium Priority (Week 2)

3. **Extract Navigator** (4 hours)
   - Create ProformaInvoiceNavigator
   - Refactor main VM
   - Test navigation flow

4. **Convert AutomationRules to Service** (3 hours)
   - Create interface + adapter
   - Refactor OtherChargeAutomationRules
   - Add unit tests

### Phase 3: Low Priority (Week 3-4)

5. **Add Pricing Engine Tests** (8 hours)
   - Write tests for SGU/DGU/LAM/DGU+LAM
   - Test edge cases (yield=0, negative prices)
   - Achieve 80% coverage

6. **Add Workflow Rules Engine** (6 hours)
   - Design rules DSL
   - Implement common rules
   - Add rule editor UI

---

## 7. Conclusion

**Current System: 8.5/10 — Professional and Well-Structured**

The Proforma Invoice module is **production-ready** and follows best practices. The 9-step wizard is appropriate for the complexity. The Inquiry integration is clean.

**Key Improvements Needed:**
1. ✅ Cross-step validation (prevent underpriced invoices)
2. ✅ State machine (enforce workflow rules)
3. ✅ Unit tests for pricing engine (prevent financial bugs)
4. ✅ Refactor oversized main VM (improve testability)

**Recommendation:** Keep the current structure. Implement the high-priority improvements (validation + state machine) to reach **9.5/10**.

---

**Next Steps:**
1. Review this analysis
2. Approve implementation plan
3. I'll implement Phase 1 (validation + state machine) immediately
4. Then Phase 2 (refactoring)
5. Finally Phase 3 (testing)

**Shall I proceed with implementing the high-priority improvements?**
