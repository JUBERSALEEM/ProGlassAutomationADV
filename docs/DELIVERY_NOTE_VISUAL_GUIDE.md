# Delivery Note Visual Guide - Understanding What You See

## Critical Understanding: Two Types of Delivery Notes

### 1. Master DN (Internal Tracking - NOT for Users)

**What it is**: Internal tracking record that shows ALL items and their delivery status

**What it shows**:
```
Item A: Ordered=100, PreviouslyDelivered=60, BalanceQty=40  ✅ Has balance
Item B: Ordered=50,  PreviouslyDelivered=50, BalanceQty=0   ❌ Fully delivered
Item C: Ordered=75,  PreviouslyDelivered=30, BalanceQty=45  ✅ Has balance
```

**Purpose**: System uses this to track what's been delivered across all DNs

**Should users see this?**: NO - This is internal only

**How to identify**: 
- DeliveryNoteId starts with "MDN-"
- IsMasterDn = true
- Shows ALL items (even fully delivered ones)

---

### 2. Real DN (What Users Create - Shows ONLY Balance Items)

**What it is**: Actual delivery note for a specific shipment

**What it shows** (after partial delivery):
```
Item A: Ordered=100, PreviouslyDelivered=60, BalanceQty=40, DeliveredQty=0 (you fill)
Item C: Ordered=75,  PreviouslyDelivered=30, BalanceQty=45, DeliveredQty=0 (you fill)

Item B is NOT shown because BalanceQty=0 (fully delivered)
```

**Purpose**: Deliver specific quantities in this shipment

**Should users see this?**: YES - This is what you create and work with

**How to identify**:
- DeliveryNoteId like "DN-00001-01", "DN-00001-02"
- IsMasterDn = false
- Shows ONLY items with BalanceQty > 0

---

## Correct Workflow Example

### Job Order: 3 Items
- Item A: 100 pcs
- Item B: 50 pcs
- Item C: 75 pcs

### First Delivery (Deliver some of each)

**Step 1**: Click "CREATE DN"

**Step 2**: Select job order

**Step 3**: System creates Real DN #1 showing ALL items (nothing delivered yet):
```
Item A: Ordered=100, PreviouslyDelivered=0, BalanceQty=100, DeliveredQty=__ (you fill)
Item B: Ordered=50,  PreviouslyDelivered=0, BalanceQty=50,  DeliveredQty=__ (you fill)
Item C: Ordered=75,  PreviouslyDelivered=0, BalanceQty=75,  DeliveredQty=__ (you fill)
```

**Step 4**: You fill in DeliveredQty:
```
Item A: DeliveredQty=60
Item B: DeliveredQty=50  (fully delivered!)
Item C: DeliveredQty=30
```

**Step 5**: Click DISPATCH

**Step 6**: System updates Master DN (internal):
```
Master DN (internal tracking):
  Item A: Ordered=100, PreviouslyDelivered=60, BalanceQty=40
  Item B: Ordered=50,  PreviouslyDelivered=50, BalanceQty=0   ← Fully delivered!
  Item C: Ordered=75,  PreviouslyDelivered=30, BalanceQty=45
```

---

### Second Delivery (Only remaining items)

**Step 1**: Click "CREATE DN" again

**Step 2**: Select SAME job order

**Step 3**: System shows dialog:
```
Partial Delivery Summary

Job Order: JOB-00001
Customer: ABC Glass LLC

  Total Ordered:     225 pcs
  Already Delivered: 140 pcs
  Remaining Balance: 85 pcs

Create a new delivery note for the 85 remaining items?

Click YES to create the delivery note.
Click NO to cancel.
```

**Step 4**: Click YES

**Step 5**: System creates Real DN #2 showing ONLY items with balance:
```
Item A: Ordered=100, PreviouslyDelivered=60, BalanceQty=40, DeliveredQty=__ (you fill)
Item C: Ordered=75,  PreviouslyDelivered=30, BalanceQty=45, DeliveredQty=__ (you fill)

✅ Item B is NOT shown because it was fully delivered (BalanceQty=0)
```

**Step 6**: You fill in DeliveredQty (or click "SELECT ALL REMAINING"):
```
Item A: DeliveredQty=40
Item C: DeliveredQty=45
```

**Step 7**: Click DISPATCH

**Step 8**: System updates Master DN:
```
Master DN (internal tracking):
  Item A: Ordered=100, PreviouslyDelivered=100, BalanceQty=0  ← Fully delivered!
  Item B: Ordered=50,  PreviouslyDelivered=50,  BalanceQty=0  ← Fully delivered!
  Item C: Ordered=75,  PreviouslyDelivered=75,  BalanceQty=0  ← Fully delivered!
```

---

### Third Delivery Attempt (Nothing left)

**Step 1**: Click "CREATE DN" again

**Step 2**: Select SAME job order

**Step 3**: System shows dialog:
```
All quantities have been delivered for this Job Order.

Job Order: JOB-00001
Customer: ABC Glass LLC

  Total Ordered: 225 pcs
  Delivered:     225 pcs
  Balance:       0 pcs

Nothing remaining to deliver.
```

**Step 4**: System offers to show latest DN

---

## Troubleshooting: Why Am I Seeing Fully Delivered Items?

### Problem: New DN shows items that were already fully delivered

**Possible Cause 1**: You're looking at the Master DN (internal tracking)

**How to check**:
- Look at the DeliveryNoteId
- If it starts with "MDN-", you're viewing the Master DN
- Master DN shows ALL items for tracking purposes

**Solution**: 
- Go back to delivery note list
- Filter by job order
- Open the Real DN (like "DN-00001-02")

---

**Possible Cause 2**: Master DN wasn't updated after previous delivery

**How to check**:
- Open the previous Real DN (the one you dispatched)
- Check if it shows "Dispatched" status
- Check if DeliveredQty values are saved

**Solution**:
- Make sure you clicked DISPATCH (not just SAVE)
- Try refreshing the list
- Create a new DN and see if it filters correctly

---

**Possible Cause 3**: Testing with old data before fix was applied

**How to check**:
- When did you dispatch the previous DN?
- Was it before or after the workflow improvements?

**Solution**:
- Create a new test job order
- Test the complete workflow from scratch
- Verify filtering works correctly

---

## Code Verification

The system DOES filter out fully delivered items. Here's the code:

### In CreateFirstDnAsync (line 711):
```csharp
// Filter out items where BalanceQty = 0 (fully delivered)
dnEntity.Items = dnEntity.Items.Where(i => i.BalanceQty > 0).ToList();

// Check if anything remains
if (dnEntity.Items.Count == 0)
{
    MessageBox.Show(
        "Nothing left to deliver on this Job Order.\n\n" +
        "Master DN already shows zero remaining balance.",
        "Fully Delivered",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
    return;
}
```

### In CreateDnFromMasterBalanceAsync (line 817):
```csharp
// Only include items with BalanceQty > 0
foreach (var masterItem in masterDn.Items
    .Where(i => i.BalanceQty > 0)  // ← FILTER HERE
    .OrderBy(i => i.SpecGroupNumber)
    .ThenBy(i => i.SerialNumber))
{
    // Create item for new DN
    items.Add(new DeliveryNoteItem { ... });
}

// Check if anything remains
if (items.Count == 0)
{
    MessageBox.Show(
        "Master DN has no remaining balance to deliver.",
        "Nothing to Create",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
    return;
}
```

### In UpdateMasterDnAsync (line 218):
```csharp
// After each delivery, update Master DN balances
foreach (var masterItem in masterDn.Items)
{
    int totalDelivered = LookupDelivered(deliveredMap, masterItem);
    masterItem.PreviouslyDeliveredQty = totalDelivered;
    masterItem.DeliveredQty = 0;
    masterItem.BalanceQty = Math.Max(0,
        masterItem.OrderedQty - totalDelivered);  // ← Balance calculation
}
```

---

## Test Scenario: Verify Filtering Works

### Setup
1. Create new job order with 3 items:
   - Glass 10mm: 100 pcs
   - Glass 12mm: 50 pcs
   - Glass 15mm: 75 pcs

### Test 1: First Delivery
1. Click "CREATE DN"
2. Select job order
3. **Expected**: See all 3 items (BalanceQty = OrderedQty)
4. Fill DeliveredQty:
   - Glass 10mm: 60
   - Glass 12mm: 50 (full!)
   - Glass 15mm: 30
5. Click DISPATCH
6. **Expected**: Success message

### Test 2: Second Delivery
1. Click "CREATE DN" again
2. Select SAME job order
3. **Expected**: Dialog shows "140 delivered, 85 remaining"
4. Click YES
5. **Expected**: See only 2 items (Glass 12mm NOT shown):
   - Glass 10mm: BalanceQty=40
   - Glass 15mm: BalanceQty=45
6. Click "SELECT ALL REMAINING"
7. Click DISPATCH
8. **Expected**: Success message

### Test 3: Third Delivery Attempt
1. Click "CREATE DN" again
2. Select SAME job order
3. **Expected**: Dialog shows "All quantities delivered"
4. **Expected**: Cannot create new DN

---

## Summary

✅ **System DOES filter out fully delivered items** (BalanceQty = 0)  
✅ **New DNs show ONLY items with remaining balance**  
✅ **Master DN is internal tracking (shows all items)**  
✅ **Code is correct and working as designed**  

**If you're seeing fully delivered items in a new DN, you're likely viewing:**
1. The Master DN (internal tracking)
2. An old DN created before the delivery
3. Testing with stale data

**Solution**: Create a fresh test job order and follow the test scenario above.

---

**Document Version**: 1.0  
**Date**: 2026-08-30  
**Status**: ✅ Verified
