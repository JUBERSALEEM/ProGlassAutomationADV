# ProGlass Automation — Calculations Reference

**Version:** 3.0 (Chat 10 update — through Phase 10.9a)

**Purpose:** All business formulas in one place with worked examples,
edge cases, and actual code snippets. Prevents calculation bugs
across modules (Proforma Invoice, Job Order, Delivery Note, Tax
Invoice, Reports).

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

    MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024   (10 MB)
        Delivery Note attachment size limit.

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

**Formula:** Same as Sqm1 but with W2, H2.

    rawSqm2 = (decimal)W2 * H2 / 1_000_000m
    Sqm2 = MAX(ROUND(rawSqm2, 3), MIN_SQM)   when W2>0 AND H2>0
    Sqm2 = 0                                  when W2=0 OR H2=0

### 3.4 Formula 04 — TotalSqm2

    TotalSqm2 = ROUND(Sqm2 * Qty, 3)

---

## 4. Linear Meter (LM) Calculations

Used for edge treatments: mitring, polish, silicon sealing, argon
gas fill.

### 4.1 Formula 05 — Lm1

**Formula:**

    Lm1 = ROUND((W1+H1) / 1000 * PerimeterFactor * Qty, 3)
    Lm1 = 0  when W1=0 OR H1=0

**Examples (Factor=2 for SGU/DGU):**

    W1=1000 H1=500 Qty=1  -> (1500/1000)*2*1 = 3.0
    W1=2000 H1=1000 Qty=3 -> (3000/1000)*2*3 = 18.0

**Examples (Factor=4 for LAM):**

    W1=1000 H1=500 Qty=1  -> (1500/1000)*4*1 = 6.0

**Code:**

    _lm1 = _w1 > 0 && _h1 > 0
        ? Math.Round(((decimal)_w1 + _h1) / 1000m * _perimeterFactor * _qty, 3)
        : 0m;

### 4.2 Formula 06 — Lm2

Same as Lm1 but with W2, H2.

    Lm2 = ROUND((W2+H2) / 1000 * PerimeterFactor * Qty, 3)
    Lm2 = 0  when W2=0 OR H2=0

---

## 5. Pricing Calculations

### 5.1 Formula 07 — Value (invoice row value)

**Formula:**

    Value = ROUND(Sqm1 * Qty * Rate, 2)

**Notes:**
- Value uses `Sqm1` only (NOT `Sqm2`)
- Rate is auto-assigned based on SQM size (see 5.3)

**Code:**

    _value = Math.Round(_sqm1 * _qty * _rate, 2);

### 5.2 Formula 08 — EffectiveRate

**Formula:**

    EffectiveRate = ROUND(BaseRate * (1 + SurchargePercent/100), 2)

**Examples:**

    BaseRate=150 Surcharge=0   -> 150.00
    BaseRate=150 Surcharge=20  -> 180.00
    BaseRate=200 Surcharge=10  -> 220.00

### 5.3 Formula 09 — Rate Auto-Assign

**Rule:**

    Sqm1 >= 4.0  -> Rate = EffectiveRate
    Sqm1 <  4.0  -> Rate = BaseRate
    Sqm1 = 0     -> Rate = BaseRate

**Trigger:**
- Fires in `SpecGroup.OnRowChanged` when PropertyName == `Sqm1`
- Suppressed when `row.SuppressRateAutoAdjust == true`

---

## 6. Polish Sub-Type Formulas

Polish is a special charge in Step 6. LM calculation varies by
sub-type.

### 6.1 All Side Polish

    LM = (W + H) / 1000 × 2 × Qty

### 6.2 Both Height Side

    LM = H / 1000 × 2 × Qty

### 6.3 Both Width Side

    LM = W / 1000 × 2 × Qty

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

Each charge has a **Grid Source** dropdown. 9 sources available:

| Source          | Definition                                    | Unit |
|-----------------|-----------------------------------------------|------|
| LM1             | Σ Lm1 across all spec groups                  | lm   |
| LM2             | Σ Lm2 across all spec groups                  | lm   |
| LM_COMBINED     | LM1 + LM2                                     | lm   |
| SQM1            | Σ TotalSqm across all spec groups             | sqm  |
| SQM2            | Σ TotalSqm2 across all spec groups            | sqm  |
| SQM_COMBINED    | SQM1 + SQM2                                   | sqm  |
| DGU_CAVITY      | SQM1 for DGU groups only                      | sqm  |
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

**Flow:**
1. User selects glass color
2. App checks which spec group sides match this color
3. Aggregates matching SQM
4. Amount calculated

**Formula:**

    TotalSqm = Σ (checked outer/inner SQM from matching spec groups)
    Amount = TotalSqm × Rate

Typical wastage percentage: 5-10% of original SQM.

---

## 9. Sheet Store Calculations

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

### 9.5 Profit margins

    ProfitPerSqm  = SellPricePerSqm − PurchasePricePerSqm
    ProfitValue   = TotalSellValue − TotalPurchaseValue

### 9.6 Low stock threshold

    IsLowStock = Balance <= 5

Currently hardcoded. Future: user-configurable.

---

## 10. Footer Calculations (per SpecGroup)

    FooterTotalQty   = SUM(row.Qty)
    FooterTotalSqm   = ROUND(SUM(row.TotalSqm),  3)
    FooterTotalSqm2  = ROUND(SUM(row.TotalSqm2), 3)
    FooterTotalLm1   = ROUND(SUM(row.Lm1),       3)
    FooterTotalLm2   = ROUND(SUM(row.Lm2),       3)
    FooterTotalValue = ROUND(SUM(row.Value),     2)

**Special case (empty collection):** all footer values = 0

**Performance rule:** Never call `RecalculateFooter()` inside a loop.

---

## 11. Financial Summary Waterfall (Step 7)

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

    IsVatApplicable = false
    VatAmount       = 0
    GrandTotal      = Subtotal Before VAT

### 11.3 Discount capping rule

    if (DiscountAmount > NetSubtotal)
        DiscountAmount = NetSubtotal

Result is always ≥ 0. Grand Total never negative.

---

## 12. VAT Reverse Calculation (from saved entity)

Used when reconstructing invoice from saved DB entity (VatPercent
not stored, must be back-calculated).

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

## 13. Delivery Note Calculations (Phase 10.1 — CRITICAL)

### 13.1 Formula 10 — Balance per item (Gotcha #40)

**Rule:** Balance is a POINT-IN-TIME invariant. Never sum stored
snapshots across siblings — that double-counts undelivered units.

**Formula:**

    Balance = MAX(0, Ordered - PreviouslyDelivered - Delivered)

Applied per DN item on save.

**Examples:**

    Ordered=100  Prev=0   Delivered=40  -> Balance=60
    Ordered=100  Prev=40  Delivered=30  -> Balance=30
    Ordered=100  Prev=70  Delivered=30  -> Balance=0
    Ordered=100  Prev=100 Delivered=0   -> Balance=0
    Ordered=100  Prev=200 Delivered=0   -> Balance=0 (MAX protects)

**Code (DnItemRowViewModel):**

    public int BalanceQty =>
        Math.Max(0, OrderedQty - PreviouslyDeliveredQty - DeliveredQty);

### 13.2 Formula 11 — Total Balance per JO (Gotcha #40)

**Rule:** For JO-level balance, always compute at query time from
raw totals — NEVER sum sibling DN BalanceQty values.

**Formula:**

    TotalBalance = MAX(0, TotalOrdered - TotalDelivered)

    Where:
    TotalOrdered   = Sum of OrderedQty across all items on all
                     Original + Dummy DNs for this JO (first DN's
                     OrderedQty is the authoritative source)
    TotalDelivered = Sum of DeliveredQty across all NON-Draft
                     sibling DNs for this JO

**Repository implementation:**

    // GetDeliveryHistoryAsync (fixed in Phase 10.7)
    TotalBalance = Math.Max(0, totalOrdered - totalDelivered);

**Why not sum BalanceQty:**

    Ordered=100 for JO
    DN-01: Delivered=40, Balance snapshot = 60
    DN-02: Delivered=30, Balance snapshot = 30
    DN-03: Delivered=30, Balance snapshot = 0

    WRONG:  Sum snapshots = 60 + 30 + 0 = 90  (nonsense)
    RIGHT:  100 - (40+30+30)          = 0    (correct)

### 13.3 Formula 12 — Previously Delivered Map (Phase 10.7)

**Purpose:** Self-heal the `PreviouslyDeliveredQty` field on load
so stale DB snapshots don't cause wrong PREV/Balance values.

**Repository method:**

    Task<Dictionary<string, int>> GetPreviouslyDeliveredMapAsync(
        string jobOrderId,
        string excludeDnId)

**Key format:** `$"{specGroupNumber}|{serialNumber}"`

**Logic:**
1. Fetch all NON-Draft DNs for this JO except `excludeDnId`
2. For each item on those DNs, accumulate DeliveredQty by key
3. Return the dictionary

**Consumer (on DN load):**

    var prevMap = await App.DeliveryNotes.GetPreviouslyDeliveredMapAsync(
                            entity.JobOrderId,
                            entity.DeliveryNoteId);
    foreach (var item in entity.Items)
    {
        var key = $"{item.SpecGroupNumber}|{item.SerialNumber}";
        item.PreviouslyDeliveredQty =
            prevMap.TryGetValue(key, out var prev) ? prev : 0;
    }

### 13.4 Formula 13 — DN-level totals (Phase 10.8a)

Displayed on DN detail view + PDF totals strip.

    TotalOrderedQty              = Σ item.OrderedQty
    TotalPreviouslyDeliveredQty  = Σ item.PreviouslyDeliveredQty
    TotalDeliveredQty            = Σ item.DeliveredQty
    TotalBalanceQty              = Σ item.BalanceQty

**Code (DeliveryNoteDetailViewModel.RecalculateTotals):**

    private void RecalculateTotals()
    {
        TotalOrderedQty              = ItemRows.Sum(r => r.OrderedQty);
        TotalPreviouslyDeliveredQty  = ItemRows.Sum(r => r.PreviouslyDeliveredQty);
        TotalDeliveredQty            = ItemRows.Sum(r => r.DeliveredQty);
        TotalBalanceQty              = ItemRows.Sum(r => r.BalanceQty);
    }

### 13.5 Formula 14 — DeliveredQty clamping (Phase 10.7)

User cannot deliver more than what's available:

    max        = MAX(0, Ordered - PreviouslyDelivered)
    Delivered  = CLAMP(userInput, 0, max)

**Code (DnItemRowViewModel.DeliveredQty setter):**

    set
    {
        if (_deliveredQty == value) return;
        int max = Math.Max(0, OrderedQty - PreviouslyDeliveredQty);
        _deliveredQty = Math.Max(0, Math.Min(value, max));
        OnPropertyChanged();
        OnPropertyChanged(nameof(BalanceQty));
        DeliveredQtyChanged?.Invoke();
    }

### 13.6 DN Sequence Numbering

**JO ID → DN ID pattern:**

    JOB-2026-001  → extract "2026-001" → DN-2026-001-{seq:D2}

    Sequence starts at 1, increments per DN for that JO.
    Format: 2 digits, zero-padded.

    First delivery:   DN-2026-001-01
    Second delivery:  DN-2026-001-02
    Third delivery:   DN-2026-001-03

---

## 14. Paste Number Cleaning

Before parsing pasted numbers, apply `CleanNum()`.

**Rules:**
- Remove: commas, spaces, en-dashes, hyphens
- Return "0" for null/empty

**Examples:**

    "1,200"  -> "1200"
    "1 200"  -> "1200"
    "1-200"  -> "1200"
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

## 15. RecalculateAll — Full Sequence

Called when any input dimension or qty changes on a `GlassItemRow`.

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

---

## 16. Full Worked Example — Complete Invoice + Delivery Chain

**Scenario:** 3-row invoice → JO → partial delivery chain

    Row 1: 6mm Clear SGU,  1500×1000 mm, qty 3, rate 150 AED/sqm
    Row 2: 6mm Clear SGU,  400×300   mm, qty 2, rate 150 AED/sqm
    Row 3: 6+6 LAM,        2000×1200 mm, qty 4, rate 300 AED/sqm

### 16.1 Row calculations

    Row 1 (SGU):
        Area = 1.500 sqm  → Total = 4.500 sqm
        Rate = 150 (< 4 sqm → BaseRate)
        Value = 4.500 × 150 = 675.00 AED

    Row 2 (SGU):
        Area = 0.500 sqm (MIN applied)  → Total = 1.000 sqm
        Rate = 150
        Value = 1.000 × 150 = 150.00 AED

    Row 3 (LAM):
        Area = 2.400 sqm  → Total = 9.600 sqm
        Rate = 300
        Value = 9.600 × 300 = 2,880.00 AED

### 16.2 PI Financial waterfall

    Glass Materials:            3,705.00 AED
    Other Charges (Silicon+Del):  545.00 AED
    Net Subtotal:               4,250.00 AED
    Discount (0%):                  0.00 AED
    Subtotal Before VAT:        4,250.00 AED
    UAE VAT (5%):                 212.50 AED
    ══════════════════════════════════════
    GRAND TOTAL:                4,462.50 AED

### 16.3 Convert to JO (Phase 9)

    JOB-2026-001 created with embedded PI snapshot (immutable)
    ProductionDetails initialized: Status=Draft, Priority=Medium

    Total ordered qty = Row1(3) + Row2(2) + Row3(4) = 9 pieces

### 16.4 First Delivery — DN-2026-001-01

    User creates DN from JO detail view
    All 9 items appear with OrderedQty populated
    User enters DeliveredQty:
        Row 1: 3 pcs delivered
        Row 2: 0 pcs
        Row 3: 2 pcs

    Save → Balance calculated per item:
        Row 1: 3 - 0 - 3 = 0
        Row 2: 2 - 0 - 0 = 2
        Row 3: 4 - 0 - 2 = 2

    DN Totals:
        TotalOrdered   = 9
        TotalPrev      = 0
        TotalDelivered = 5
        TotalBalance   = 4

    User clicks "Make DN" → Status = Dispatched
    Balance > 0 → Auto-create Dummy DN-2026-001-02
        DN-02 items ordered qty = balance from DN-01 items
        Row 2: 2 pcs to deliver
        Row 3: 2 pcs to deliver
        (Row 1 excluded — zero balance)

### 16.5 Second Delivery — DN-2026-001-02

    User opens DN-02 (Draft)
    On load: HealItemPrevQtyAsync fetches confirmed sibling delivered:
        DN-01 (Dispatched, non-Draft) has:
            Row 2 SN 1: 0
            Row 3 SN 1: 2

    So DN-02 items get:
        Row 2 SN 1: OrderedQty=2, PrevQty=0
        Row 3 SN 1: OrderedQty=2, PrevQty=2

    User delivers Row 2: 2 pcs, Row 3: 1 pc
    Save → per-item balance:
        Row 2: 2 - 0 - 2 = 0
        Row 3: 2 - 2 - 1 = -1 → clamped to 0 (max protection)
        Wait — DN-02 OrderedQty for Row 3 was 2 (from balance)
        But real formula: 4 (JO ordered) - 2 (DN-01) - 1 (DN-02) = 1
        So Row 3 balance on JO level: 1 pc still remaining

    "Make DN" on DN-02 → auto-create DN-03 for balance 1 pc

### 16.6 JO-Level History Query (Phase 10.7 fix)

    GetDeliveryHistoryAsync("JOB-2026-001") should return:

    TotalOrdered   = 9   (from JO snapshot / first DN)
    TotalDelivered = 3 + 3 + 1 = 7   (DN-01 + DN-02 non-Draft sums)
    TotalBalance   = MAX(0, 9 - 7) = 2   (correct — 2 pcs remain)

    WRONG (summing snapshots):
    DN-01 balance = 4, DN-02 balance = 1
    Sum = 5 ← wrong (double-counts overlapping balance)

    RIGHT (formula):
    Balance = 9 - 7 = 2 ← correct

---

## 17. Rounding Rules Summary

| Data type   | Storage precision  | Display format | Example        |
|-------------|--------------------|----------------|----------------|
| Money (AED) | `decimal(18, 2)`   | `"N2"`         | `1,234.56`     |
| SQM         | `decimal(18, 3)`   | `"0.000"`      | `1.234`        |
| LM          | `decimal(18, 3)`   | `"0.###"`      | `12.5` or `12` |
| Percent     | `decimal(5, 2)`    | `"0.##"`       | `5` or `12.5`  |
| Qty         | `int`              | integer        | `3`            |
| W / H (mm)  | `int`              | integer        | `1500`         |
| Balance qty | `int`              | integer        | `0`            |

**Rules:**
- Intermediate calculations: use `decimal`
- Never round intermediate results
- Only round on display OR at final storage step
- Cast to `decimal` before division

---

## 18. Precision + Type Rules

### 18.1 Never use double/float

For money, SQM, LM, rates: always `decimal`.

### 18.2 Cast before divide

    ✅ (decimal)W1 * H1 / 1_000_000m
    ❌ W1 * H1 / 1_000_000        (integer division loses precision)

### 18.3 Suffix decimal literals

    ✅ 1_000_000m
    ✅ 0.5m
    ❌ 1_000_000    (int, causes integer division)
    ❌ 0.5           (double)

---

## 19. Safe Division Rule

If denominator == 0:
- Throw ValidationError OR
- Return 0m with clear reasoning in comment

**Never** auto-replace denominator with 1.
**Never** return 0 silently without comment.

---

## 20. Where These Calculations Live in Code

| Calculation                | File                                                                 |
|----------------------------|----------------------------------------------------------------------|
| Row area / LM / Value      | `ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.cs`     |
|                            | (`GlassItemRow.RecalculateAll`)                                      |
| EffectiveRate + auto-assign| `SpecGroup.EffectiveRate` + `SpecGroup.OnRowChanged`                 |
| Footer per group           | `SpecGroup.RecalculateFooter`                                        |
| Grand Total Summary        | `ViewModels/.../Step5DataGrid/GrandTotalSummary/`                    |
|                            | `GrandTotalSummaryViewModel.cs`                                      |
| Metric Hub aggregates      | `ViewModels/.../Step6OtherCharges/MetricDiscoveryHubViewModel.cs`   |
| Other Charges              | `ViewModels/.../Step6OtherCharges/OtherChargesViewModel.cs`         |
| Wastage                    | `ViewModels/.../Step6OtherCharges/Wastages/`                         |
| Custom charges             | `ViewModels/.../Step6OtherCharges/CustomCharges/`                    |
| VAT + Grand Total          | `ViewModels/.../Step7PrintPreview/InvoiceMainSummary/`               |
| VAT reverse calc           | `Services/ProformaInvoice/PdfExport/PdfExportContextBuilder.cs`     |
| Sheet inventory computed   | `Models/SheetStore/GlassSheet.cs` (computed properties)              |
| Dashboard aggregates       | `ViewModels/Dashboard/Overview/OverviewDashboardViewModel.cs`       |
| **DN item Balance**        | `ViewModels/DeliveryNote/DeliveryNoteDetail/`                        |
|                            | `DeliveryNoteDetailViewModel.cs` (`DnItemRowViewModel.BalanceQty`)  |
| **DN Save entity balance** | `DeliveryNoteDetailViewModel.SaveAsync`                              |
| **DN totals**              | `DeliveryNoteDetailViewModel.RecalculateTotals`                      |
| **JO-level balance**       | `Services/DeliveryNote/Repository/DeliveryNoteRepository.cs`         |
|                            | (`GetDeliveryHistoryAsync`)                                          |
| **Prev delivered map**     | `DeliveryNoteRepository.GetPreviouslyDeliveredMapAsync`             |

---

## 21. When Updating Any Formula

Follow this protocol:

1. **Update this document FIRST** — add new formula OR modify
   existing with worked example
2. **Update the code** — modify the implementation
3. **Add a worked example** here if new formula
4. **Test all dependent modules:**
    - Step 5 (row calculations)
    - Step 6 (Metric Hub, Other Charges)
    - Step 7 (Invoice Summary, Print Preview)
    - Job Order (embedded PI snapshot integrity)
    - Delivery Note (item balance, JO-level totals)
    - Dashboard KPIs
    - PDF/Excel/CSV exports
5. **Update commit** with `data/d04-calculations.md` in Files modified