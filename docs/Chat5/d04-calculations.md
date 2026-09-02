# ProGlass Automation — Calculations Reference

**Purpose:** All business formulas in one place with worked examples,
edge cases, and actual code snippets. Prevents calculation bugs
across modules (Proforma Invoice, Job Order, Cutting, Reports).

**Currency:** AED (UAE Dirham)
**Units:** mm for dimensions, sqm for area, lm for linear meters
**Rounding:** Display 2 decimals for money, 3 for SQM, 2 for LM
**Rule:** No formula may exist outside this file.

---

## 1. Constants

    MIN_SQM = 0.5m
        Minimum area per glass piece.
        Applied to both Sqm1 and Sqm2 independently.
        Only when W > 0 AND H > 0.

    DIVISION_BASE = 1,000,000
        W and H are in mm.
        W * H / 1,000,000 = sqm

    LM_DIVISION_BASE = 1,000
        W and H in mm.
        (W + H) / 1000 = meters

    RATE_THRESHOLD_SQM = 4.0m
        SQM at/above this uses EffectiveRate.
        SQM below this uses BaseRate.

    DEFAULT_VAT_PERCENT = 5.0m
        UAE standard VAT rate. Editable per invoice.
        Toggleable on/off for zero-rated exports.

---

## 2. Perimeter Factor Table

| GlassType | Factor | Reason                              |
|-----------|--------|-------------------------------------|
| SGU       | 2      | Two sides (W+H) × 2 approximation   |
| DGU       | 2      | Two sides                           |
| LAM       | 4      | All four sides polished             |
| DGU+LAM   | 2      | Treated as DGU for perimeter        |

Rule:

    SelectedGlassType == "LAM" -> factor = 4
    All others                 -> factor = 2

---

## 3. Area Calculations

### 3.1 Formula 01 — Sqm1

**Purpose:** Area of primary glass size (W1 × H1)

**Formula:**

    rawSqm1 = (decimal)W1 * H1 / 1_000_000m
    Sqm1 = MAX(ROUND(rawSqm1, 3), MIN_SQM)   when W1>0 AND H1>0
    Sqm1 = 0                                  when W1=0 OR H1=0

**Examples:**

    W1=1000 H1=500   -> raw=0.5   -> Sqm1=0.5   (exactly min)
    W1=1000 H1=1000  -> raw=1.0   -> Sqm1=1.0
    W1=2000 H1=2000  -> raw=4.0   -> Sqm1=4.0
    W1=300  H1=400   -> raw=0.12  -> Sqm1=0.5   (min applied)
    W1=0    H1=1000  -> raw=0     -> Sqm1=0     (zero, no min)
    W1=1000 H1=0     -> raw=0     -> Sqm1=0     (zero, no min)

**Edge Cases:**
- `W1=0` or `H1=0` → return 0, do NOT apply MIN_SQM
- MIN_SQM only applies to real glass pieces with both dimensions

**Code:**

    decimal rawSqm1 = (decimal)_w1 * _h1 / 1_000_000m;
    _sqm1 = _w1 > 0 && _h1 > 0
        ? Math.Max(Math.Round(rawSqm1, 3), MIN_SQM)
        : 0m;

### 3.2 Formula 02 — TotalSqm (Sqm1 × Qty)

**Formula:**

    TotalSqm = ROUND(Sqm1 * Qty, 3)

**Examples:**

    Sqm1=1.0   Qty=5  -> TotalSqm=5.0
    Sqm1=0.5   Qty=3  -> TotalSqm=1.5
    Sqm1=1.234 Qty=2  -> TotalSqm=2.468
    Sqm1=0     Qty=5  -> TotalSqm=0.0

**Code:**

    _totalSqm = Math.Round(_sqm1 * _qty, 3);

### 3.3 Formula 03 — Sqm2

**Purpose:** Area of secondary glass size (W2 × H2). Used for DGU
inner pane or second spec.

**Formula:**

    rawSqm2 = (decimal)W2 * H2 / 1_000_000m
    Sqm2 = MAX(ROUND(rawSqm2, 3), MIN_SQM)   when W2>0 AND H2>0
    Sqm2 = 0                                  when W2=0 OR H2=0

**Examples:**

    W2=1200 H2=800  -> raw=0.96  -> Sqm2=0.96
    W2=200  H2=300  -> raw=0.06  -> Sqm2=0.5   (min applied)
    W2=0    H2=800  -> Sqm2=0
    W2=0    H2=0    -> Sqm2=0

**Code:**

    decimal rawSqm2 = (decimal)_w2 * _h2 / 1_000_000m;
    _sqm2 = _w2 > 0 && _h2 > 0
        ? Math.Max(Math.Round(rawSqm2, 3), MIN_SQM)
        : 0m;

### 3.4 Formula 04 — TotalSqm2

**Formula:**

    TotalSqm2 = ROUND(Sqm2 * Qty, 3)

**Examples:**

    Sqm2=0.96 Qty=4 -> TotalSqm2=3.84
    Sqm2=0    Qty=5 -> TotalSqm2=0.0

---

## 4. Linear Meter (LM) Calculations

Used for edge treatments: mitring, polish, silicon sealing, argon
gas fill.

### 4.1 Formula 05 — Lm1

**Purpose:** Linear meters of perimeter for W1×H1 glass.
Used for edge polishing, sealing, frame.

**Formula:**

    Lm1 = ROUND((W1+H1) / 1000 * PerimeterFactor * Qty, 3)
    Lm1 = 0  when W1=0 OR H1=0

**Examples (PerimeterFactor=2 for SGU/DGU):**

    W1=1000 H1=500 Qty=1 Factor=2
        -> (1000+500) / 1000 * 2 * 1
        -> 1.5 * 2 = 3.0
        -> Lm1=3.0

    W1=2000 H1=1000 Qty=3 Factor=2
        -> (2000+1000) / 1000 * 2 * 3
        -> 3.0 * 2 * 3 = 18.0
        -> Lm1=18.0

**Examples (PerimeterFactor=4 for LAM):**

    W1=1000 H1=500 Qty=1 Factor=4
        -> (1000+500) / 1000 * 4 * 1
        -> 1.5 * 4 = 6.0
        -> Lm1=6.0

**Edge Cases:**
- `W1=0` or `H1=0` → `Lm1=0` (no glass, no perimeter)

**Code:**

    _lm1 = _w1 > 0 && _h1 > 0
        ? Math.Round(((decimal)_w1 + _h1) / 1000m * _perimeterFactor * _qty, 3)
        : 0m;

### 4.2 Formula 06 — Lm2

**Formula:**

    Lm2 = ROUND((W2+H2) / 1000 * PerimeterFactor * Qty, 3)
    Lm2 = 0  when W2=0 OR H2=0

**Examples (PerimeterFactor=2):**

    W2=1200 H2=800 Qty=2 Factor=2
        -> (1200+800) / 1000 * 2 * 2
        -> 2.0 * 2 * 2 = 8.0
        -> Lm2=8.0

---

## 5. Pricing Calculations

### 5.1 Formula 07 — Value (invoice row value)

**Purpose:** Invoice value for this row.

**Formula:**

    Value = ROUND(Sqm1 * Qty * Rate, 2)

**Examples:**

    Sqm1=1.0  Qty=5  Rate=150  -> Value=750.00
    Sqm1=4.0  Qty=2  Rate=180  -> Value=1440.00
    Sqm1=0.5  Qty=10 Rate=150  -> Value=750.00
    Sqm1=0    Qty=5  Rate=150  -> Value=0.00

**Notes:**
- Value uses `Sqm1` only (NOT `Sqm2`)
- Value uses `Rate` (which may be BaseRate or EffectiveRate)
- Rate is auto-assigned based on SQM size (see 5.3)

**Code:**

    _value = Math.Round(_sqm1 * _qty * _rate, 2);

### 5.2 Formula 08 — EffectiveRate

**Purpose:** Rate with surcharge applied.

**Formula:**

    EffectiveRate = ROUND(BaseRate * (1 + SurchargePercent/100), 2)

**Examples:**

    BaseRate=150  Surcharge=0    -> EffectiveRate=150.00
    BaseRate=150  Surcharge=20   -> EffectiveRate=180.00
    BaseRate=200  Surcharge=10   -> EffectiveRate=220.00
    BaseRate=100  Surcharge=5    -> EffectiveRate=105.00

**Code:**

    public decimal EffectiveRate =>
        Math.Round(_baseRate * (1m + (_surchargePercent / 100m)), 2);

### 5.3 Formula 09 — Rate Auto-Assign

**Purpose:** Large glass gets surcharge, small glass does not.

**Rule:**

    Sqm1 >= 4.0  -> Rate = EffectiveRate
    Sqm1 <  4.0  -> Rate = BaseRate
    Sqm1 = 0     -> Rate = BaseRate (no glass)

**Threshold:** 4.0 sqm

**Examples:**

    Sqm1=3.999  -> BaseRate       (just under threshold)
    Sqm1=4.000  -> EffectiveRate  (at threshold)
    Sqm1=4.001  -> EffectiveRate  (over threshold)
    Sqm1=0.5    -> BaseRate       (small piece)
    Sqm1=10.0   -> EffectiveRate  (large panel)

**Trigger:**
- Fires in `SpecGroup.OnRowChanged` when PropertyName == `Sqm1`
- Suppressed when `row.SuppressRateAutoAdjust == true`

**Code:**

    var targetRate = row.Sqm1 >= 4m ? EffectiveRate : BaseRate;
    if (row.Rate != targetRate)
        row.Rate = targetRate;

### 5.4 Surcharge on rate

**Formula:**

    FinalRate = BaseRate × (1 + SurchargePercent / 100)

**Example:**

    BaseRate = 150, Surcharge = 10%
    FinalRate = 150 × 1.10 = AED 165 per sqm

---

## 6. Polish Sub-Type Formulas

Polish is a special charge in Step 6. LM calculation varies by
sub-type.

### 6.1 All Side Polish

    LM = (W + H) / 1000 × 2 × Qty

Same as SGU/DGU perimeter formula.

### 6.2 Both Height Side

    LM = H / 1000 × 2 × Qty

Only vertical edges polished (2 × height).

### 6.3 Both Width Side

    LM = W / 1000 × 2 × Qty

Only horizontal edges polished (2 × width).

### 6.4 All Side Polish As Per DWG

    LM = (W + H) / 1000 × 2 × Qty
    (same formula as All Side, but with "As Per DWG" note)

### 6.5 Custom

User enters manual quantity + description. No formula applied.

### 6.6 DGU/LAM Polish (double-glass special case)

For double glass (DGU/LAM), polish applies to BOTH glasses:

    Total LM = (CalcPolish(W1, H1) + CalcPolish(W2, H2)) × Qty

---

## 7. Other Charges Grid Sources (Step 6)

Each charge has a **Grid Source** dropdown that determines its
MetricQty. 9 sources available:

| Source          | Definition                                    | Unit |
|-----------------|-----------------------------------------------|------|
| LM1             | Σ Lm1 across all spec groups                  | lm   |
| LM2             | Σ Lm2 across all spec groups                  | lm   |
| LM_COMBINED     | LM1 + LM2                                     | lm   |
| SQM1            | Σ TotalSqm across all spec groups             | sqm  |
| SQM2            | Σ TotalSqm2 across all spec groups            | sqm  |
| SQM_COMBINED    | SQM1 + SQM2                                   | sqm  |
| DGU_CAVITY      | SQM1 for DGU groups only (inner cavity area)  | sqm  |
| TOTAL_PIECES    | Σ Qty across all groups                       | pcs  |
| DOUBLE_GLASS    | Qty × 2 for DGU/LAM, × 1 for SGU              | pcs  |

**Charge amount:**

    Amount = MetricQty × Rate

### 7.1 Predefined charges (8 total)

| Charge        | Typical Source     | Unit | Notes                          |
|---------------|--------------------|------|--------------------------------|
| Argon Gas     | DGU_CAVITY         | sqm  | Inner cavity area only         |
| Silicon Seal  | LM_COMBINED        | lm   | All edges sealed               |
| Overlap       | LM_COMBINED        | lm   | Overlap seam length            |
| Shutter       | TOTAL_PIECES       | pcs  | Per-piece charge               |
| Mitring       | LM_COMBINED        | lm   | Mitred edge length             |
| Polish        | LM_COMBINED / custom | lm/manual | See sub-type formulas   |
| Cutout        | TOTAL_PIECES       | pcs  | Per hole/cutout                |
| Holes         | TOTAL_PIECES       | pcs  | Per hole                       |

---

## 8. Wastage Calculation (Step 6)

**Purpose:** Waste glass accounting per color, aggregated across
matching spec groups.

**Flow:**
1. User selects glass color
2. App checks which spec group sides (outer/inner) match this color
3. Aggregates matching SQM
4. Amount calculated

**Formula:**

    TotalSqm = Σ (checked outer/inner SQM from matching spec groups)
    Amount = TotalSqm × Rate

Typical wastage percentage: 5-10% of original SQM.

---

## 9. Sheet Store Calculations

Used for raw glass sheet inventory (Sheet Store module).

### 9.1 SQM per sheet

    SqmPerSheet = (WidthMm × HeightMm) / 1,000,000

### 9.2 Balance (available inventory)

    Balance = Stock - Used

### 9.3 Total SQM values

    TotalSqmInStock  = SqmPerSheet × Stock
    TotalSqmUsed     = SqmPerSheet × Used
    TotalSqmBalance  = SqmPerSheet × Balance

### 9.4 Inventory monetary values

    TotalPurchaseValue = SqmPerSheet × Stock   × PurchasePricePerSqm
    TotalSellValue     = SqmPerSheet × Stock   × SellPricePerSqm
    BalanceValue       = SqmPerSheet × Balance × SellPricePerSqm

Used in Dashboard "Inventory Value" KPI.

### 9.5 Profit margins

    ProfitPerSqm  = SellPricePerSqm − PurchasePricePerSqm
    ProfitValue   = TotalSellValue − TotalPurchaseValue

### 9.6 Low stock threshold

    IsLowStock = Balance <= 5

Currently hardcoded in Dashboard VMs. Future: user-configurable.

---

## 10. Footer Calculations (per SpecGroup)

Displayed below each SpecGroup's row list.

    FooterTotalQty   = SUM(row.Qty)             for all rows
    FooterTotalSqm   = ROUND(SUM(row.TotalSqm),  3)
    FooterTotalSqm2  = ROUND(SUM(row.TotalSqm2), 3)
    FooterTotalLm1   = ROUND(SUM(row.Lm1),       3)
    FooterTotalLm2   = ROUND(SUM(row.Lm2),       3)
    FooterTotalValue = ROUND(SUM(row.Value),     2)

**Special case (empty collection):**
All footer values = 0

**Performance rule:**
Never call `RecalculateFooter()` inside a loop.
Call once after bulk operations complete.

---

## 11. Financial Summary Waterfall (Step 7)

Displayed in Print Preview + Invoice Main Summary section.

    Glass Materials Total    = Σ (all row Values across all spec groups)
    Other Charges Total      = Σ (active predefined + wastages + custom)
    ─────────────────────────────────────────────
    Net Subtotal             = Glass Materials + Other Charges

    Discount (Percentage):    Net Subtotal × (DiscountValue / 100)
    Discount (Fixed Amount):  DiscountValue
    Discount capped at Net Subtotal (never goes negative)
    ─────────────────────────────────────────────
    Subtotal Before VAT      = Net Subtotal - Discount

    VAT Amount               = Subtotal Before VAT × (VatPercent / 100)
    (default 5%, editable, toggleable on/off)
    ═════════════════════════════════════════════
    GRAND TOTAL (VAT INC)    = Subtotal Before VAT + VAT Amount

### 11.1 Full waterfall example

    Glass Materials:      29,053.23 AED
    Other Charges:         6,114.96 AED
    Net Subtotal:         35,168.19 AED
    Discount (0%):             0.00 AED
    Subtotal Before VAT:  35,168.19 AED
    UAE VAT (5%):          1,758.41 AED
    ═══════════════════════════════════
    GRAND TOTAL:          36,926.60 AED

### 11.2 VAT-exclusive display (zero-rated exports)

When invoice is VAT-exempt:

    IsVatApplicable = false
    VatAmount       = 0
    GrandTotal      = Subtotal Before VAT

### 11.3 Discount capping rule

    if (DiscountAmount > NetSubtotal)
        DiscountAmount = NetSubtotal

Result is always ≥ 0. Grand Total never negative.

---

## 12. VAT Reverse Calculation (from saved entity)

Used in `BuildExportContextFromSnapshot` when reconstructing invoice
from saved DB entity (VatPercent not stored, must be back-calculated).

**Formula:**

    VatPercent = VatAmount /
                 (GlassMaterialsSubtotal + OtherChargesSubtotal
                  - DiscountAmount) × 100

**Example:**

    GlassMaterials = 29053.23
    OtherCharges   =  6114.96
    Discount       =     0.00
    VatAmount      =  1758.41

    NetSubtotal = 29053.23 + 6114.96 - 0.00 = 35168.19
    VatPercent  = 1758.41 / 35168.19 × 100 = 5.0%

---

## 13. Paste Number Cleaning

Before parsing pasted numbers, apply `CleanNum()`.

**Rules:**
- Remove: commas, spaces, en-dashes, hyphens
- Return "0" for null/empty

**Examples:**

    "1,200"  -> "1200"
    "1 200"  -> "1200"
    "1-200"  -> "1200"  (dash treated as artifact)
    "1200"   -> "1200"
    ""       -> "0"
    null     -> "0"
    "N/A"    -> stays as "N/A" (then int.TryParse fails -> 0)

**Code:**

    private static string CleanNum(string s) =>
        s?.Trim()
          .Replace(",", "")
          .Replace(" ", "")
          .Replace("–", "")
          .Replace("-", "") ?? "0";

---

## 14. RecalculateAll — Full Sequence

Called when any input dimension or qty changes on a `GlassItemRow`.
Must recalculate in this exact order:

    1.  rawSqm1    = (decimal)W1 * H1 / 1_000_000m
    2.  Sqm1       = W1>0 && H1>0 ? MAX(ROUND(rawSqm1,3), 0.5) : 0
    3.  TotalSqm   = ROUND(Sqm1 * Qty, 3)
    4.  rawSqm2    = (decimal)W2 * H2 / 1_000_000m
    5.  Sqm2       = W2>0 && H2>0 ? MAX(ROUND(rawSqm2,3), 0.5) : 0
    6.  TotalSqm2  = ROUND(Sqm2 * Qty, 3)
    7.  Lm1        = W1>0 && H1>0 ? ROUND((W1+H1)/1000*Factor*Qty, 3) : 0
    8.  Lm2        = W2>0 && H2>0 ? ROUND((W2+H2)/1000*Factor*Qty, 3) : 0
    9.  Value      = ROUND(Sqm1 * Qty * Rate, 2)
    10. Fire all PropertyChanged (unless _isBulkAdding)

**Notes:**
- Rate is NOT recalculated here
- Rate auto-adjust fires separately via `OnRowChanged` in `SpecGroup`
- This prevents circular dependency

---

## 15. Full Worked Example — Complete Invoice

**Scenario:** 3-row invoice with mixed sizes.

    Row 1: 6mm Clear SGU,  1500×1000 mm, qty 3, rate 150 AED/sqm
    Row 2: 6mm Clear SGU,  400×300   mm, qty 2, rate 150 AED/sqm
    Row 3: 6+6 LAM,        2000×1200 mm, qty 4, rate 300 AED/sqm

### 15.1 Row calculations

    Row 1 (SGU):
        Area        = (1500 × 1000) / 1M = 1.500 sqm  (>= 0.5, use actual)
        BillableTotal = 1.500 × 3 = 4.500 sqm
        Rate check:  Sqm1 (1.500) < 4.0 → BaseRate (150)
        Value       = 4.500 × 150 = 675.00 AED
        Lm1         = (1500+1000)/1000 × 2 × 3 = 15.000 lm

    Row 2 (SGU):
        Area        = (400 × 300) / 1M = 0.120 sqm    (< 0.5, use MIN)
        BillableTotal = 0.500 × 2 = 1.000 sqm
        Rate check:  Sqm1 (0.500) < 4.0 → BaseRate (150)
        Value       = 1.000 × 150 = 150.00 AED
        Lm1         = (400+300)/1000 × 2 × 2 = 2.800 lm

    Row 3 (LAM):
        Area        = (2000 × 1200) / 1M = 2.400 sqm  (>= 0.5, use actual)
        BillableTotal = 2.400 × 4 = 9.600 sqm
        Rate check:  Sqm1 (2.400) < 4.0 → BaseRate (300)
        Value       = 9.600 × 300 = 2,880.00 AED
        Lm1         = (2000+1200)/1000 × 4 × 4 = 51.200 lm  (LAM=4)

### 15.2 Glass Materials Subtotal

    Row 1:   675.00
    Row 2:   150.00
    Row 3: 2,880.00
    ═══════════════
    Total: 3,705.00 AED

### 15.3 Other Charges (example)

    Silicon Seal (LM_COMBINED source):
        CombinedLM = 15.000 + 2.800 + 51.200 = 69.000 lm
        Rate       = 5 AED/lm
        Value      = 69.000 × 5 = 345.00 AED

    Delivery (Custom):
        Qty   = 1 lot
        Rate  = 200 AED
        Value = 200.00 AED

    Other Charges Total = 345.00 + 200.00 = 545.00 AED

### 15.4 Financial waterfall

    Glass Materials Total       :  3,705.00 AED
    Other Charges Total         :    545.00 AED
    ══════════════════════════════════════════
    Net Subtotal                :  4,250.00 AED
    Discount (0%)               :      0.00 AED
    ──────────────────────────────────────────
    Subtotal Before VAT         :  4,250.00 AED
    Standard UAE VAT (5%)       :    212.50 AED
    ══════════════════════════════════════════
    TOTAL EST VALUE (VAT INC)   :  4,462.50 AED

---

## 16. Rounding Rules Summary

| Data type   | Storage precision  | Display format | Example        |
|-------------|--------------------|----------------|----------------|
| Money (AED) | `decimal(18, 2)`   | `"N2"`         | `1,234.56`     |
| SQM         | `decimal(18, 3)`   | `"0.000"`      | `1.234`        |
| LM          | `decimal(18, 3)`   | `"0.###"`      | `12.5` or `12` |
| Percent     | `decimal(5, 2)`    | `"0.##"`       | `5` or `12.5`  |
| Qty         | `int`              | integer        | `3`            |
| W / H (mm)  | `int`              | integer        | `1500`         |

**Rules:**
- Intermediate calculations: use `decimal` (never `double` or `float`)
- Never round intermediate results — only round on display OR at
  final storage step
- Cast to `decimal` before division:
  `(decimal)W1 * H1 / 1_000_000m`

---

## 17. Precision + Type Rules

### 17.1 Never use double/float

For money, SQM, LM, rates: always `decimal`.

    ✅ decimal sqm = (decimal)w * h / 1_000_000m;
    ❌ double sqm = w * h / 1_000_000.0;

### 17.2 Cast before divide

    ✅ (decimal)W1 * H1 / 1_000_000m
    ❌ W1 * H1 / 1_000_000        (integer division loses precision)

### 17.3 Suffix decimal literals

    ✅ 1_000_000m
    ✅ 0.5m
    ❌ 1_000_000    (int, causes integer division)
    ❌ 0.5           (double)

---

## 18. Safe Division Rule

If denominator == 0:
- Throw ValidationError OR
- Return 0m with clear reasoning in comment

**Never** auto-replace denominator with 1.
**Never** return 0 silently without comment.

**Example (SQM):**

    _sqm1 = _w1 > 0 && _h1 > 0
        ? Math.Max(Math.Round(rawSqm1, 3), MIN_SQM)
        : 0m;  // zero when either dimension missing (no glass exists)

---

## 19. Where These Calculations Live in Code

| Calculation                | File                                                                 |
|----------------------------|----------------------------------------------------------------------|
| Row area (Sqm1, TotalSqm)  | `ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.cs`     |
|                            | (`GlassItemRow.RecalculateAll`)                                      |
| Row LM (Lm1, Lm2)          | `GlassItemRow.RecalculateAll`                                        |
| Row Value                  | `GlassItemRow.RecalculateAll`                                        |
| EffectiveRate              | `SpecGroup.EffectiveRate` (computed property)                        |
| Rate auto-assign           | `SpecGroup.OnRowChanged`                                             |
| Footer per group           | `SpecGroup.RecalculateFooter`                                        |
| Grand Total Summary        | `ViewModels/.../Step5DataGrid/GrandTotalSummary/`                    |
|                            | `GrandTotalSummaryViewModel.cs`                                      |
| Metric Hub aggregates      | `ViewModels/.../Step6OtherCharges/MetricDiscoveryHubViewModel.cs`   |
| Other Charges              | `ViewModels/.../Step6OtherCharges/OtherChargesViewModel.cs`         |
| Wastage                    | `ViewModels/.../Step6OtherCharges/Wastages/`                         |
|                            | `WastagesSectionViewModel.cs`                                        |
| Custom charges             | `ViewModels/.../Step6OtherCharges/CustomCharges/`                    |
|                            | `CustomChargesSectionViewModel.cs`                                   |
| VAT + Grand Total          | `ViewModels/.../Step7PrintPreview/InvoiceMainSummary/`               |
|                            | `InvoiceMainSummaryViewModel.cs`                                     |
| VAT reverse calc           | `Services/ProformaInvoice/PdfExport/PdfExportContextBuilder.cs`     |
|                            | (`BuildExportContextFromSnapshot`)                                   |
| Sheet inventory computed   | `Models/SheetStore/GlassSheet.cs` (computed properties)              |
| Dashboard aggregates       | `ViewModels/Dashboard/Overview/OverviewDashboardViewModel.cs`       |
|                            | (`LoadCoreAsync`)                                                    |

---

## 20. When Updating Any Formula

Follow this protocol:

1. **Update this document FIRST** — add new formula OR modify
   existing with worked example
2. **Update the code** — modify the implementation
3. **Add a worked example** here if new formula
4. **Test all dependent modules:**
    - Step 5 (row calculations)
    - Step 6 (Metric Hub, Other Charges)
    - Step 7 (Invoice Summary, Print Preview)
    - Dashboard KPIs
    - PDF/Excel/CSV exports
5. **Update commit** with `data/d04-calculations.md` in Files modified