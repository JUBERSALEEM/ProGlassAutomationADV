# Complete Spec-to-Charge Examples (4 Build Types)

**Purpose:** Verify the automation logic is correct. All examples use **HD Grey** (colored glass = wastage required).

---

## **Example 1: SGU (Single Glazing Unit)**

### **Spec Name:**
```
6mm HD Grey FT Glass With All Side Polish As Per DWG
```

### **Step 5 Sizes:**
- W1: 1000mm, H1: 2000mm, Qty: 10
- **SQM1:** 20.00 sqm (10 pieces × 2.0 sqm each)
- **LM1:** 60.00 lm (perimeter = 2×(1+2) × 10 pieces)
- **QTY:** 10 pieces

### **Step 6 Auto-Charges:**

| Charge | Unit | Qty | Rate | Total | Reason |
|---|---|---|---|---|---|
| **Polish (lm)** | LM | 60.00 | 6.00 | 360.00 | Spec: "All Side Polish As Per DWG" |
| **Wastage: HD Grey — outer** | SQM | 20.00 | 0.00 | 0.00 | Colored glass (user sets rate) |

**Notes:**
- Only 1 wastage row (SGU = single pane)
- No overlap/silicon bed (SGU doesn't have these)
- Polish sub-type: "AllSidePolishAsPerDwg"

---

## **Example 2: DGU (Double Glazing Unit) with Overlap**

### **Spec Name:**
```
6mm HD Grey FT Glass + 12mm ASP With U-Insert + 6mm HD Grey FT Glass With Overlap As Per DWG
```

### **Step 5 Sizes:**
- W1: 1000mm, H1: 2000mm, Qty: 10
- W2: (empty), H2: (empty) → uses W1/H1
- **SQM1:** 20.00 sqm (outer pane)
- **SQM2:** 20.00 sqm (inner pane, same as outer when W2/H2 empty)
- **LM1:** 60.00 lm (outer perimeter)
- **LM_COMBINED:** 120.00 lm (both panes: 60 + 60)
- **DGU_CAVITY:** 20.00 sqm (cavity area)
- **QTY:** 10 pieces

### **Step 6 Auto-Charges:**

| Charge | Unit | Qty | Rate | Total | Reason |
|---|---|---|---|---|---|
| **U-Insert (Sqm)** | SQM | 20.00 | 10.00 | 200.00 | Spec: "With U-Insert" |
| **Overlap (lm)** | LM | 60.00 | 6.00 | 360.00 | Spec: "With Overlap As Per DWG" |
| **Silicon bed (lm)** | LM | 60.00 | 6.00 | 360.00 | DGU overlap → always separate charge |
| **Polish (lm)** | LM | 120.00 | 6.00 | 720.00 | (if polish was in spec) |
| **Wastage: HD Grey — outer** | SQM | 20.00 | 0.00 | 0.00 | Colored glass (user sets rate) |
| **Wastage: HD Grey — inner** | SQM | 20.00 | 0.00 | 0.00 | Colored glass (user sets rate) |

**Notes:**
- **2 wastage rows** (DGU = two panes, both HD Grey = both get wastage)
- **Overlap + Silicon Bed are SEPARATE charges** (both always required for DGU overlap)
- Inner pane auto-ticked for wastage
- W2/H2 empty → inner uses same SQM as outer

---

## **Example 3: LAM (Laminated Glass) with Polish**

### **Spec Name:**
```
6mm HD Grey FT Glass + 1.52mm Clear PVB + 6mm HD Grey FT Glass With Both Height Side Polish
```

### **Step 5 Sizes:**
- W1: 1000mm, H1: 2000mm, Qty: 10
- W2: (empty), H2: (empty) → uses W1/H1
- **SQM1:** 20.00 sqm (outer pane)
- **SQM2:** 20.00 sqm (inner pane)
- **LM1:** 60.00 lm (outer perimeter)
- **LM_COMBINED:** 120.00 lm (both panes)
- **QTY:** 10 pieces

### **Step 6 Auto-Charges:**

| Charge | Unit | Qty | Rate | Total | Reason |
|---|---|---|---|---|---|
| **Polish (lm)** | LM | 120.00 | 6.00 | 720.00 | Spec: "Both Height Side Polish" |
| **Wastage: HD Grey — outer** | SQM | 20.00 | 0.00 | 0.00 | Colored glass (user sets rate) |
| **Wastage: HD Grey — inner** | SQM | 20.00 | 0.00 | 0.00 | Colored glass (user sets rate) |

**Notes:**
- **2 wastage rows** (LAM = two glass panes, both HD Grey)
- Polish uses LM_COMBINED (both panes)
- Polish sub-type: "BothHeightSide"
- No overlap/silicon bed (LAM doesn't have these unless spec says so)

---

## **Example 4: DGU+LAM (Double Glazing + Laminated) with All Charges**

### **Spec Name:**
```
6mm HD Grey FT Glass + 12mm ASP With U-Insert + 6mm HD Grey FT Glass + 1.52mm Clear PVB + 6mm HD Grey FT Glass With Overlap As Per DWG With All Side Polish
```

### **Step 5 Sizes:**
- W1: 1000mm, H1: 2000mm, Qty: 10
- W2: 800mm, H2: 1800mm, Qty: 10
- **SQM1:** 20.00 sqm (outer pane: W1×H1)
- **SQM2:** 14.40 sqm (inner/lam panes: W2×H2)
- **LM1:** 60.00 lm (outer perimeter)
- **LM2:** 52.00 lm (inner perimeter)
- **LM_COMBINED:** 112.00 lm (both perimeters)
- **DGU_CAVITY:** 20.00 sqm (cavity area)
- **QTY:** 10 pieces

### **Step 6 Auto-Charges:**

| Charge | Unit | Qty | Rate | Total | Reason |
|---|---|---|---|---|---|
| **U-Insert (Sqm)** | SQM | 20.00 | 10.00 | 200.00 | Spec: "With U-Insert" |
| **Overlap (lm)** | LM | 60.00 | 6.00 | 360.00 | Spec: "With Overlap As Per DWG" |
| **Silicon bed (lm)** | LM | 60.00 | 6.00 | 360.00 | DGU+LAM overlap → always separate |
| **Polish (lm)** | LM | 112.00 | 6.00 | 672.00 | Spec: "All Side Polish" |
| **Wastage: HD Grey — outer** | SQM | 20.00 | 0.00 | 0.00 | Outer pane (W1/H1) |
| **Wastage: HD Grey — lam outer** | SQM | 14.40 | 0.00 | 0.00 | Lam outer pane (W2/H2) |
| **Wastage: HD Grey — lam inner** | SQM | 14.40 | 0.00 | 0.00 | Lam inner pane (W2/H2) |

**Notes:**
- **3 wastage rows** (DGU+LAM = three glass panes: outer + lam outer + lam inner)
- Outer uses SQM1 (W1/H1), lam panes use SQM2 (W2/H2)
- **Overlap + Silicon Bed are SEPARATE** (both always required for DGU+LAM overlap)
- All 3 panes are HD Grey → all 3 get wastage charges

---

## **Summary: Auto-Charge Detection Rules**

| Spec Token | Charge Template | When Applied |
|---|---|---|
| "U-Insert" | U_INSERT | Always |
| "Overlap" | OVERLAP / OVERLAP_SQM | Always |
| "Overlap" + DGU/DGU+LAM | SILICON_BED / SILICON_BED_SQM | **Always separate for DGU** |
| "Argon Gas" | ARGON_GAS | Always |
| "Shutter Bonding" | SHUTTER_BONDING | Always |
| "Fanhole" | FAN_HOLES | Always |
| "Cutout" | CUTOUT | Always |
| "Holes" | HOLES | Always |
| "Cutout And Holes" | CUTOUT + HOLES | Both charges |
| "Mitring" / "Beveled" | MITRING | Always |
| "Sandblasted" / "Frosted" | SANDBLASTING | Always |
| Polish variants | POLISH | Always (17 types detected) |

---

## **Wastage Logic:**

| Build Type | Wastage Rows | Notes |
|---|---|---|
| **SGU** | 1 row (outer) | Single pane |
| **DGU** | 2 rows (outer + inner) | Inner auto-ticked |
| **LAM** | 2 rows (outer + inner) | Both glass panes |
| **DGU+LAM** | 3 rows (outer + lam outer + lam inner) | All three panes |

**Color Logic:**
- **Clear glass:** Marked as "optional (offcuts often reusable)" - user can uncheck
- **Colored glass (HD Grey, Bronze, etc.):** Marked as "offcuts not reusable" - should charge wastage

---

## **Key Points:**

1. **Overlap + Silicon Bed are ALWAYS separate charges for DGU/DGU+LAM**
   - Spec just says "With Overlap" but silicon bed is always required
   - Two distinct physical processes, always billed separately

2. **Inner pane wastage is auto-ticked for DGU/LAM/DGU+LAM**
   - User can uncheck if not needed
   - Prevents missing inner glass wastage

3. **W2/H2 empty → inner uses same SQM as outer**
   - Common case: same size panes
   - Step 9 costs both panes correctly

4. **Clear glass wastage is optional**
   - Marked as "optional (offcuts often reusable)"
   - User decides based on their situation

---

**Commit:** `01f9b39`  
**Files:** OtherChargeAutomationRules.cs, DguParametersViewModel.cs, DguLamParametersViewModel.cs
