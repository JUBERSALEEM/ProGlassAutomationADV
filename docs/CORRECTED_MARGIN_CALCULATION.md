# ✅ Corrected Margin Calculation - Example

## Your Example: 6mm HD Grey FT + 16mm ASP + 6mm Clear FT

### **Inputs:**
```
6mm HD Grey FT Glass: AED 47 per SQM
6mm Clear FT Glass: AED 47 per SQM
16mm ASP: AED 50 per SQM
Yield Factor: 0.85 (85%)
```

### **Step 4: Base Cost Calculation**
```
Base Cost = (47 + 47) / 0.85 + 50
          = 94 / 0.85 + 50
          = 110.59 + 50
          = 160.59 per SQM
```

---

## **Scenario: Selling at AED 160 per SQM (20 SQM order)**

### **Revenue:**
```
Selling Price: 160 × 20 = AED 3,200 (including VAT)
```

### **Total Base Cost:**
```
Base Cost: 160.59 × 20 = AED 3,211.80
```

---

## **P&L Shows TWO KPI Cards:**

### **KPI Card 1: COST-BASED MARGIN (Main KPI)**
```
Formula: (Gross Revenue - Base Cost) / Base Cost × 100

Calculation:
  = (3,200 - 3,211.80) / 3,211.80 × 100
  = -11.80 / 3,211.80 × 100
  = -0.37%
  ≈ -0.4%

Display: -0.4%
```

### **KPI Card 2: PRICE-BASED MARGIN (Secondary KPI)**
```
Formula: (Gross Revenue - Base Cost) / Gross Revenue × 100

Calculation:
  = (3,200 - 3,211.80) / 3,200 × 100
  = -11.80 / 3,200 × 100
  = -0.37%
  ≈ -0.4%

Display: -0.4%
```

**Both show -0.4% (LOSS) because you're selling below cost!**

---

## **Other KPI Cards (Unchanged):**

```
┌─────────────────────────────────────────┐
│ Gross Revenue (incl. VAT): AED 3,200    │
│ Net Revenue (ex-VAT): AED 3,048         │
│ Total Cost: AED 3,212                   │
│ Profit/Loss: AED -12                    │
│                                         │
│ COST-BASED MARGIN: -0.4%                │
│ PRICE-BASED MARGIN: -0.4%               │
└─────────────────────────────────────────┘
```

---

## **Scenario: Selling at AED 180 per SQM (20 SQM order)**

### **Revenue:**
```
Selling Price: 180 × 20 = AED 3,600 (including VAT)
```

### **Total Base Cost:**
```
Base Cost: 160.59 × 20 = AED 3,211.80
```

### **KPI Card 1: COST-BASED MARGIN**
```
= (3,600 - 3,211.80) / 3,211.80 × 100
= 388.20 / 3,211.80 × 100
= 12.1%

Display: 12.1%
```

### **KPI Card 2: PRICE-BASED MARGIN**
```
= (3,600 - 3,211.80) / 3,600 × 100
= 388.20 / 3,600 × 100
= 10.8%

Display: 10.8%
```

**Notice: Cost-based (12.1%) is higher than price-based (10.8%) for profitable sales!**

---

## **Why Two Different Percentages?**

### **Cost-Based Margin (12.1%):**
- Measures profit as % of **what you spent**
- "For every AED 100 I spent, I made AED 12.10 profit"
- **This is the MAIN KPI** - matches Step 4's margin pattern

### **Price-Based Margin (10.8%):**
- Measures profit as % of **what you earned**
- "For every AED 100 I earned, AED 10.80 is profit"
- This is the SECONDARY KPI - accounting perspective

---

## **Key Changes Made:**

### **Before (WRONG):**
```
Used Net Revenue (ex-VAT): 3,048
Margin = (3,048 - 3,212) / 3,048 × 100 = -5.4%

❌ Didn't match user's 15% margin pattern
❌ Confusing because VAT is separate
```

### **After (CORRECT):**
```
Uses Gross Revenue (incl. VAT): 3,200
Cost-based = (3,200 - 3,212) / 3,212 × 100 = -0.4%
Price-based = (3,200 - 3,212) / 3,200 × 100 = -0.4%

✅ Matches Step 4's margin pattern
✅ VAT is shown separately in other KPIs
✅ Both cost-based and price-based shown
```

---

## **Job Profitability Module:**

The same formulas are now applied to the portfolio totals:

```
Total Gross Revenue: Sum of all job revenues (incl. VAT)
Total Cost: Sum of all job costs
Total Profit: Total Gross - Total Cost

Overall Cost-Based Margin = (Total Gross - Total Cost) / Total Cost × 100
Overall Price-Based Margin = (Total Gross - Total Cost) / Total Gross × 100
```

---

## **Commit Information:**

**Commit:** `e840813`  
**Files Modified:**
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Calculation.cs`
- `ViewModels/JobProfitability/JobProfitabilityViewModel.cs`

**Changes:**
1. Both modules now use **Gross Revenue (including VAT)** instead of Net Revenue
2. Both show **TWO margin KPIs**: cost-based (main) and price-based (secondary)
3. Formulas match Step 4's margin pattern exactly

---

## **Summary:**

✅ **P&L now shows correct margins using your pattern**  
✅ **Both cost-based and price-based margins displayed**  
✅ **Uses gross revenue (VAT separate in other KPIs)**  
✅ **Job Profitability uses same formulas**  
✅ **Matches Step 4's margin calculation pattern**  

The system now correctly shows -0.4% for your example (selling at 160 when cost is 160.59), and will show positive margins when selling above cost!
