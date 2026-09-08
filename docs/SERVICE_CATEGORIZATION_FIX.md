# Service Profitability - Business Logic Clarification

## ✅ Fixed: Service Categorization Based on Glass Type

**Commit:** `edcad03` (pushed)

---

## Business Rules Clarified

### **DGU (Double Glazed Units)**
```
Spacer/ASP Charge: AED 45 per SQM
  ↓
INCLUDES (bundled):
  ✓ Cutting
  ✓ Tempering
  ✓ Production
  ✓ Assembly
```

**Key Point:** For DGU, the spacer charge is a **bundled service**. We don't charge cutting and tempering separately - they're included in the spacer price.

**In Step 6:** Only one line item for spacer  
**In P&L:** Only one "Spacer" row  
**In Service Profitability:** Show as "Spacer/ASP Service (incl. cutting, tempering, production)"

---

### **SGU (Single Glazed Units)**
```
Glass Charge: AED X per SQM
  ↓
SEPARATE charges:
  ✓ Cutting (AED Y per SQM)
  ✓ Tempering (AED Z per SQM)
  ✓ Holes (AED A per hole)
  ✓ Cutouts (AED B per cutout)
  ✓ Polishing (AED C per LM)
```

**Key Point:** For SGU, each service is charged **separately** as individual line items.

**In Step 6:** Multiple line items (cutting, tempering, holes, etc.)  
**In P&L:** Multiple "Production" rows  
**In Service Profitability:** Show each service separately

---

### **LAM (Laminated)**
```
Glass Charge: AED X per SQM
  ↓
SEPARATE charges (outsourced):
  ✓ Cutting (AED Y per SQM)
  ✓ Tempering (AED Z per SQM)
  ✓ PVB Interlayer (AED W per SQM) - outsourced
  ✓ Holes (AED A per hole)
  ✓ Cutouts (AED B per cutout)
```

**Key Point:** For LAM, cutting and tempering are **outsourced** and charged separately, along with PVB interlayer.

**In Step 6:** Multiple line items  
**In P&L:** Multiple "Production" rows  
**In Service Profitability:** Show each service separately, mark PVB as "outsourced"

---

### **DGU+LAM (Hybrid)**
```
Spacer/ASP Charge: AED 45 per SQM (includes DGU cutting/tempering)
  ↓
PLUS separate charges:
  ✓ PVB Interlayer (AED W per SQM) - outsourced
  ✓ LAM Cutting (AED Y per SQM)
  ✓ LAM Tempering (AED Z per SQM)
  ✓ Holes (AED A per hole)
  ✓ Cutouts (AED B per cutout)
```

**Key Point:** Hybrid gets bundled DGU services PLUS separate LAM services.

---

## What Was Fixed

### **BEFORE (Incorrect)**
```csharp
private string CategorizeServiceType(string kind, string description)
{
    if (kind == "Glass") return "Glass Margin";
    if (kind == "Spacer") return "Spacer/ASP Service";
    if (kind == "Production")
    {
        if (desc.Contains("Cutting")) return "Cutting Service";
        if (desc.Contains("Tempering")) return "Tempering Service";
        // ❌ WRONG: This would categorize cutting/tempering for DGU too!
    }
}
```

**Problem:** This would show "Cutting Service" and "Tempering Service" for DGU jobs, even though those are already included in the spacer charge. This would **double-count** services and give misleading profitability data.

---

### **AFTER (Correct)**
```csharp
private string CategorizeServiceType(string kind, string description, int specNumber, List<SpecGroupSnapshot> specGroups)
{
    // Get glass type from spec group
    var specGroup = specGroups?.FirstOrDefault(s => s.GroupNumber == specNumber);
    var glassType = specGroup?.SelectedGlassType?.ToUpperInvariant() ?? "";

    if (kind == "Glass") return "Glass Margin";

    if (kind == "Spacer")
    {
        // ✅ DGU: Spacer INCLUDES cutting + tempering + production
        return "Spacer/ASP Service (incl. cutting, tempering, production)";
    }

    if (kind == "Production")
    {
        var desc = description ?? "";

        // ✅ Check if this is DGU (spacer already includes these)
        if (glassType.Contains("DGU") && !glassType.Contains("LAM"))
        {
            // DGU: Only show extras (U-Insert, Holes/Cutouts)
            if (desc.Contains("U-Insert")) return "U-Insert Service";
            if (desc.Contains("Hole")) return "Holes/Cutouts Service";
            
            // Skip cutting/tempering for DGU (already in spacer)
            return null; // ✅ Don't categorize
        }

        // ✅ SGU or LAM: Cutting and tempering are SEPARATE charges
        if (desc.Contains("Cutting")) return "Cutting Service";
        if (desc.Contains("Tempering")) return "Tempering Service";
        if (desc.Contains("PVB")) return "PVB Interlayer Service (outsourced)";
        // ... etc
    }
}
```

**Solution:** Now the categorization checks the glass type and only categorizes services that are actually charged separately.

---

## Example: How It Works Now

### **Scenario 1: DGU Job**
```
Spec: 6mm Clear FT + 12mm ASP + 6mm Clear FT
Glass Type: DGU

Step 6 Charges:
  - Glass: AED 1,000
  - Spacer (12mm ASP): AED 450 ← INCLUDES cutting + tempering

P&L Rows:
  - Glass: AED 1,000
  - Spacer: AED 450

Service Profitability Shows:
  ✓ Glass Margin: AED 1,000
  ✓ Spacer/ASP Service (incl. cutting, tempering, production): AED 450
  
  ❌ Does NOT show:
    - Cutting Service (already in spacer)
    - Tempering Service (already in spacer)
```

---

### **Scenario 2: SGU Job**
```
Spec: 6mm Clear FT
Glass Type: SGU

Step 6 Charges:
  - Glass: AED 500
  - Cutting: AED 40 ← SEPARATE charge
  - Tempering: AED 60 ← SEPARATE charge
  - Holes (2): AED 20 ← SEPARATE charge

P&L Rows:
  - Glass: AED 500
  - Production (Cutting): AED 40
  - Production (Tempering): AED 60
  - Production (Holes): AED 20

Service Profitability Shows:
  ✓ Glass Margin: AED 500
  ✓ Cutting Service: AED 40
  ✓ Tempering Service: AED 60
  ✓ Holes/Cutouts Service: AED 20
```

---

### **Scenario 3: LAM Job**
```
Spec: 6mm Clear FT + 1.52mm PVB + 6mm Clear FT
Glass Type: LAM

Step 6 Charges:
  - Glass: AED 1,000
  - Cutting: AED 80 ← SEPARATE (outsourced)
  - Tempering: AED 120 ← SEPARATE (outsourced)
  - PVB Interlayer: AED 150 ← SEPARATE (outsourced)

P&L Rows:
  - Glass: AED 1,000
  - Production (Cutting): AED 80
  - Production (Tempering): AED 120
  - Production (PVB interlayer): AED 150

Service Profitability Shows:
  ✓ Glass Margin: AED 1,000
  ✓ Cutting Service: AED 80
  ✓ Tempering Service: AED 120
  ✓ PVB Interlayer Service (outsourced): AED 150
```

---

## Service Categories Now Shown

### **Always Shown (All Glass Types)**
- **Glass Margin** - Profit from buying vs selling glass

### **DGU Only**
- **Spacer/ASP Service (incl. cutting, tempering, production)** - Bundled service
- **U-Insert Service** - If U-insert is added
- **Holes/Cutouts Service** - If holes/cutouts are added

### **SGU Only**
- **Cutting Service** - Separate charge
- **Tempering Service** - Separate charge
- **Holes/Cutouts Service** - Separate charge
- **Polishing Service** - Separate charge
- **Other Production Service** - Any other production charges

### **LAM Only**
- **Cutting Service** - Separate charge (outsourced)
- **Tempering Service** - Separate charge (outsourced)
- **PVB Interlayer Service (outsourced)** - Separate charge
- **Holes/Cutouts Service** - Separate charge
- **Polishing Service** - Separate charge

### **DGU+LAM (Hybrid)**
- **Spacer/ASP Service (incl. cutting, tempering, production)** - DGU bundled service
- **PVB Interlayer Service (outsourced)** - LAM separate charge
- **Cutting Service** - LAM cutting (separate)
- **Tempering Service** - LAM tempering (separate)
- **Holes/Cutouts Service** - Separate charge
- **U-Insert Service** - If added

---

## Why This Matters

### **Before Fix (Incorrect)**
```
DGU Job Analysis:
  Glass Margin: AED 100 profit
  Spacer Service: AED 200 profit
  Cutting Service: AED 50 profit  ❌ WRONG (already in spacer)
  Tempering Service: AED 75 profit ❌ WRONG (already in spacer)
  
  Total Profit: AED 425 ❌ OVERSTATED
```

### **After Fix (Correct)**
```
DGU Job Analysis:
  Glass Margin: AED 100 profit
  Spacer/ASP Service (incl. cutting, tempering, production): AED 275 profit ✅
  
  Total Profit: AED 375 ✅ ACCURATE
```

**Impact:** Prevents double-counting and gives accurate service profitability data.

---

## Implementation Details

### **Method Signature Changed**
```csharp
// OLD
private string CategorizeServiceType(string kind, string description)

// NEW
private string CategorizeServiceType(
    string kind, 
    string description, 
    int specNumber, 
    List<SpecGroupSnapshot> specGroups)
```

### **Glass Type Detection**
```csharp
var specGroup = specGroups?.FirstOrDefault(s => s.GroupNumber == specNumber);
var glassType = specGroup?.SelectedGlassType?.ToUpperInvariant() ?? "";

// Check for DGU (but not DGU+LAM)
if (glassType.Contains("DGU") && !glassType.Contains("LAM"))
{
    // DGU logic
}
```

### **Service Label Updated**
```csharp
// OLD: "Spacer/ASP Service"
// NEW: "Spacer/ASP Service (incl. cutting, tempering, production)"
```

This makes it clear to users that the spacer charge is a bundled service.

---

## Testing Recommendations

### **Test Case 1: Pure DGU Job**
1. Create a PI with DGU spec (6mm + 12mm ASP + 6mm)
2. Add spacer charge in Step 6
3. Confirm and save
4. Check Service Profitability tab
5. **Expected:** Only shows "Spacer/ASP Service (incl. cutting, tempering, production)"
6. **Should NOT show:** Cutting Service, Tempering Service

### **Test Case 2: Pure SGU Job**
1. Create a PI with SGU spec (6mm Clear FT)
2. Add cutting, tempering, holes in Step 6
3. Confirm and save
4. Check Service Profitability tab
5. **Expected:** Shows Glass Margin, Cutting Service, Tempering Service, Holes/Cutouts Service separately

### **Test Case 3: LAM Job**
1. Create a PI with LAM spec (6mm + PVB + 6mm)
2. Add cutting, tempering, PVB in Step 6
3. Confirm and save
4. Check Service Profitability tab
5. **Expected:** Shows each service separately, PVB marked as "outsourced"

### **Test Case 4: DGU+LAM Hybrid**
1. Create a PI with DGU+LAM spec
2. Add spacer, PVB, LAM cutting/tempering in Step 6
3. Confirm and save
4. Check Service Profitability tab
5. **Expected:** Shows bundled DGU services + separate LAM services

---

## Summary

✅ **Fixed service categorization** to respect business rules  
✅ **Prevents double-counting** of bundled services in DGU  
✅ **Accurate profitability analysis** for each glass type  
✅ **Clear labels** showing which services are bundled vs separate  
✅ **Proper handling** of SGU, LAM, DGU, and DGU+LAM  

The service profitability analysis now correctly reflects your business model and provides accurate insights for strategic decision-making.
