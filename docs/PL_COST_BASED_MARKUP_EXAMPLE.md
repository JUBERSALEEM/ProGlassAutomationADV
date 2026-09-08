# P&L Cost-Based Markup Implementation - Example

## 📊 Example: 6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass

### **Inputs (from Step 4)**
```
Outer Glass (6mm HD Grey FT): AED 48 per SQM
Inner Glass (6mm Clear FT): AED 25 per SQM
Spacer (12mm ASP): AED 45 per SQM
Yield Factor: 0.85 (85% yield = 15% wastage)
Area: 20 SQM (10 pieces × 1000mm × 2000mm)
```

---

## **Step 4: Base Cost Calculation**

```
Formula:
  Base Cost = (Outer + Inner) / Yield + Spacer
            = (48 + 25) / 0.85 + 45
            = 73 / 0.85 + 45
            = 85.88 + 45
            = 130.88 per SQM

With 15% markup:
  Selling Price = Base Cost × (1 + Markup%)
                = 130.88 × 1.15
                = 150.51 per SQM

Customer Quote: AED 150.51 per SQM
Total Quote: 20 SQM × 150.51 = AED 3,010.20
```

---

## **Step 9: P&L Calculation (NEW - Cost-Based Markup)**

### **Revenue**
```
Customer pays: AED 3,010.20 (incl. VAT)
Net Revenue (ex-VAT): 3,010.20 / 1.05 = AED 2,866.86
```

### **Base Cost Calculation (Using Yield Factor)**

```
Base Cost per SQM:
  = (48 + 25) / 0.85 + 45
  = 130.88 per SQM

Total Base Cost:
  = 130.88 × 20 SQM
  = AED 2,617.60
```

### **Profit Calculation**
```
Profit = Net Revenue - Total Base Cost
       = 2,866.86 - 2,617.60
       = AED 249.26

Markup on Cost = Profit / Total Base Cost × 100
               = 249.26 / 2,617.60 × 100
               = 9.5%
```

**Wait! Why 9.5% instead of 15%?**

Because VAT reduces the net revenue:
- Customer pays: 150.51 (incl. VAT)
- Net revenue: 150.51 / 1.05 = 143.34 (ex-VAT)
- Base cost: 130.88
- Markup on net revenue: (143.34 - 130.88) / 130.88 = 9.5%

**To get exactly 15% markup on cost (after VAT):**
```
Required Net Revenue = Base Cost × 1.15
                     = 130.88 × 1.15
                     = 150.51 per SQM (ex-VAT)

Required Selling Price = 150.51 × 1.05
                       = 158.04 per SQM (incl. VAT)

Total Quote = 158.04 × 20 = AED 3,160.80
```

---

## **Comparison: OLD vs NEW P&L**

### **OLD P&L (Price-Based Margin)**
```
Margin = Profit / Net Revenue × 100
       = 249.26 / 2,866.86 × 100
       = 8.7%

❌ PROBLEM: Step 4 said 15% markup, P&L showed 8.7% margin
   (Different calculation methods)
```

### **NEW P&L (Cost-Based Markup)**
```
Markup = Profit / Total Base Cost × 100
       = 249.26 / 2,617.60 × 100
       = 9.5%

✅ CORRECT: Both use cost-based calculation
   (Difference is due to VAT impact on net revenue)
```

---

## **What If User Enters Different Selling Price?**

### **Scenario 1: Selling at AED 120 per SQM (below cost)**
```
Customer pays: 120 × 20 = AED 2,400 (incl. VAT)
Net Revenue: 2,400 / 1.05 = AED 2,285.71

Base Cost: 2,617.60
Profit: 2,285.71 - 2,617.60 = -331.89 (LOSS)

Markup = -331.89 / 2,617.60 × 100 = -12.7%

P&L shows: -12.7% (RED - indicates loss)
```

### **Scenario 2: Selling at AED 150.51 per SQM (as quoted)**
```
Customer pays: 150.51 × 20 = AED 3,010.20 (incl. VAT)
Net Revenue: 3,010.20 / 1.05 = AED 2,866.86

Base Cost: 2,617.60
Profit: 2,866.86 - 2,617.60 = 249.26

Markup = 249.26 / 2,617.60 × 100 = 9.5%

P&L shows: 9.5% markup on cost
```

### **Scenario 3: Selling at AED 160 per SQM (higher price)**
```
Customer pays: 160 × 20 = AED 3,200 (incl. VAT)
Net Revenue: 3,200 / 1.05 = AED 3,047.62

Base Cost: 2,617.60
Profit: 3,047.62 - 2,617.60 = 430.02

Markup = 430.02 / 2,617.60 × 100 = 16.4%

P&L shows: 16.4% markup on cost (GREEN - good profit)
```

---

## **What You'll See in the P&L Screen**

```
┌─────────────────────────────────────────┐
│ KPIs                                    │
├─────────────────────────────────────────┤
│ Gross Revenue: AED 3,010.20             │
│ Net Revenue (ex-VAT): AED 2,866.86      │
│                                         │
│ Total Base Cost: AED 2,617.60           │
│ Profit: AED 249.26                      │
│                                         │
│ MARKUP ON COST: 9.5%                    │
└─────────────────────────────────────────┘
```

---

## **Key Formula**

### **Base Cost Calculation (Step 4)**
```
Base Cost per SQM = (Glass Prices / Yield) + Production
                  = (48 + 25) / 0.85 + 45
                  = 130.88 per SQM
```

### **Markup Calculation (Step 9)**
```
Markup % = (Net Revenue - Total Base Cost) / Total Base Cost × 100
         = (2,866.86 - 2,617.60) / 2,617.60 × 100
         = 9.5%
```

---

## **Why This is Correct**

**Step 4 calculates:**
```
Base Cost = 130.88 per SQM
With 15% markup: 130.88 × 1.15 = 150.51 per SQM
```

**P&L calculates the SAME way:**
```
Base Cost = (48 + 25) / 0.85 + 45 = 130.88 per SQM
Total Base Cost = 130.88 × 20 = 2,617.60
Markup = (Net Revenue - Base Cost) / Base Cost × 100
```

**Result:** P&L markup now matches Step 4's markup pattern!

---

## **Understanding the VAT Impact**

When Step 4 says "15% markup", it means:
```
Selling Price = Base Cost × 1.15
              = 130.88 × 1.15 = 150.51
```

But P&L works with **net revenue (ex-VAT)**:
```
If selling at 150.51 (incl. VAT):
  Net revenue = 150.51 / 1.05 = 143.34 (ex-VAT)
  Markup on net = (143.34 - 130.88) / 130.88 = 9.5%
```

**To achieve exactly 15% markup after VAT:**
```
Required net revenue = 130.88 × 1.15 = 150.51
Required selling price = 150.51 × 1.05 = 158.04 (incl. VAT)
```

---

## **Verification**

**To verify this is working correctly:**

1. Create a quote in Step 4 with:
   - 6mm HD Grey FT Glass (outer): AED 48/SQM
   - 12mm ASP: AED 45/SQM
   - 6mm Clear FT Glass (inner): AED 25/SQM
   - Yield: 0.85

2. Check Step 4 shows:
   - Base Cost: 130.88 per SQM

3. Add sizes in Step 5:
   - 10 pieces × 1000mm × 2000mm = 20 SQM

4. Enter selling price:
   - AED 158.04 per SQM (to get exactly 15% markup after VAT)

5. Check Step 9 P&L:
   - Total Base Cost: AED 2,617.60
   - Net Revenue: AED 3,010.29 (158.04 × 20 / 1.05)
   - Profit: AED 392.69
   - **Markup on Cost: 15%** ✅

---

## **Commit Information**

**Commit:** `7d521bd`  
**Files Modified:** 
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Calculation.cs`
- `Themes/Languages/en-US.xaml`
- `Themes/Languages/ar-AE.xaml`

**Changes:**
1. Changed `PriceBasedMarginPercent` calculation from `Profit / Revenue` to `Profit / Cost`
2. Updated UI label from "PRICE-BASED PROFIT MARGIN" to "MARKUP ON COST"
3. Updated Arabic translation to "الربح على التكلفة" (Profit on Cost)

---

## **Summary**

✅ **P&L now calculates markup on cost**  
✅ **Formula: (Net Revenue - Base Cost) / Base Cost × 100**  
✅ **Matches Step 4's markup pattern**  
✅ **Shows negative markup (loss) when selling below cost**  
✅ **Shows positive markup when selling above cost**  

The P&L now correctly shows the markup percentage based on base cost, matching the same pattern used in Step 4 pricing calculations.
