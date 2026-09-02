# Delivery Note Workflow - User Guide

## Quick Start

### Creating Your First Delivery Note

1. **Click "CREATE DN"** button in the delivery note list
2. **Select Job Order** from the picker dialog
3. **System creates delivery note** with all ordered items
4. **Fill DELIVERED QTY** for each item being delivered
5. **Click DISPATCH** to complete the delivery

### Creating a Partial Delivery

**Scenario**: Job order has 100 pcs, you want to deliver 60 pcs now, 40 pcs later

**First Delivery (60 pcs):**
1. Click "CREATE DN"
2. Select job order
3. System shows all 100 pcs (BalanceQty = 100)
4. Change DeliveredQty to **60** for each item
5. Click DISPATCH
6. System updates: 60 delivered, 40 remaining

**Second Delivery (40 pcs):**
1. Click "CREATE DN" again
2. Select same job order
3. System shows dialog: "60 delivered, 40 remaining. Create DN for remaining?"
4. Click YES
5. System creates DN with only the 40 remaining pcs
6. **Click "SELECT ALL REMAINING"** button to auto-fill DeliveredQty = 40
7. Click DISPATCH
8. Delivery complete!

---

## Key Features

### ✅ SELECT ALL REMAINING Button

**Location**: Delivery note detail view, above the items grid

**What it does**: Automatically fills DeliveredQty = BalanceQty for all items

**When to use**: When delivering ALL remaining items (full delivery)

**Example**:
- BalanceQty = 40 pcs
- Click "SELECT ALL REMAINING"
- DeliveredQty automatically set to 40
- Click DISPATCH

---

### ✅ RESET QTY Button

**Location**: Delivery note detail view, next to "SELECT ALL REMAINING"

**What it does**: Sets DeliveredQty = 0 for all items

**When to use**: When you want to start over or enter quantities manually

**Example**:
- You accidentally clicked "SELECT ALL REMAINING"
- Click "RESET QTY"
- All DeliveredQty values reset to 0
- Enter quantities manually

---

### ✅ Balance Tracking

**How it works**:
- System automatically tracks what's been delivered
- Shows "Previously Delivered" quantity for each item
- Calculates remaining balance automatically

**Example**:
```
Item: Glass Panel 10mm
  Ordered:              100 pcs
  Previously Delivered:  60 pcs  (from previous DNs)
  This Delivery:         40 pcs  (you enter this)
  Remaining After:        0 pcs  (calculated)
```

---

## Common Workflows

### Workflow 1: Full Delivery (All at Once)

**Scenario**: Job order for 100 pcs, delivering all 100 pcs

1. Click "CREATE DN"
2. Select job order
3. System creates DN with 100 pcs
4. **Click "SELECT ALL REMAINING"** (auto-fills DeliveredQty = 100)
5. Click DISPATCH
6. ✅ Complete!

---

### Workflow 2: Partial Delivery (Multiple Shipments)

**Scenario**: Job order for 100 pcs, delivering 60 now, 40 later

**First Delivery:**
1. Click "CREATE DN"
2. Select job order
3. System creates DN with 100 pcs (BalanceQty = 100)
4. Enter DeliveredQty = **60** (manually)
5. Click DISPATCH
6. System shows: "60 delivered, 40 remaining"

**Second Delivery:**
1. Click "CREATE DN" again
2. Select same job order
3. System shows: "60 delivered, 40 remaining. Create DN for remaining?"
4. Click YES
5. System creates DN with 40 pcs
6. **Click "SELECT ALL REMAINING"** (auto-fills DeliveredQty = 40)
7. Click DISPATCH
8. ✅ Complete!

---

### Workflow 3: Multiple Partial Deliveries

**Scenario**: Job order for 100 pcs, delivering 30, then 30, then 40

**First Delivery (30 pcs):**
1. Create DN → Enter DeliveredQty = 30 → Dispatch
2. System: "30 delivered, 70 remaining"

**Second Delivery (30 pcs):**
1. Create DN → Select job order
2. System: "30 delivered, 70 remaining. Create DN for remaining?"
3. Click YES → DN shows 70 pcs (PreviouslyDelivered = 30)
4. Enter DeliveredQty = 30 → Dispatch
5. System: "60 delivered, 40 remaining"

**Third Delivery (40 pcs):**
1. Create DN → Select job order
2. System: "60 delivered, 40 remaining. Create DN for remaining?"
3. Click YES → DN shows 40 pcs (PreviouslyDelivered = 60)
4. **Click "SELECT ALL REMAINING"** → Dispatch
5. ✅ Complete!

---

## Understanding the UI

### Delivery Note List View

**Columns**:
- **DN #**: Delivery note number (e.g., DN-00001-01)
- **Job Order**: Associated job order number
- **Customer**: Customer name
- **Status**: Draft, Dispatched, Approved, etc.
- **Type**: Original, Rework, Return
- **Total Qty**: Total ordered quantity
- **Delivered Qty**: Total delivered so far
- **Balance Qty**: Remaining to deliver

**Filters**:
- Status: All, Draft, Dispatched, Approved, etc.
- Type: All, Original, Rework, Return
- Customer: Filter by customer
- Date Range: Filter by creation date

---

### Delivery Note Detail View

**Header Section**:
- DN Number, Job Order, Customer
- Status, Delivery Date
- Created By, Created Date

**Items Grid**:
- **Item**: Product name/code
- **Description**: Product description
- **Ordered Qty**: Total ordered quantity
- **Previously Delivered**: Already delivered (from previous DNs)
- **Delivered Qty**: **YOU ENTER THIS** (this delivery)
- **Balance Qty**: Remaining after this delivery
- **Unit**: Unit of measure
- **Rate**: Price per unit
- **Amount**: Total amount (DeliveredQty × Rate)

**Action Buttons**:
- **SELECT ALL REMAINING**: Auto-fill DeliveredQty = BalanceQty
- **RESET QTY**: Reset all DeliveredQty to 0
- **SAVE**: Save changes (draft)
- **DISPATCH**: Finalize and dispatch delivery note

---

## Tips & Best Practices

### ✅ DO:
- **Use "SELECT ALL REMAINING"** when delivering all remaining items
- **Check "Previously Delivered"** to see what's already been shipped
- **Verify Balance Qty** before dispatching
- **Create separate DNs** for each shipment/delivery
- **Use partial delivery** when shipping in multiple batches

### ❌ DON'T:
- **Don't manually calculate** remaining quantities (system does it)
- **Don't create duplicate DNs** for the same delivery
- **Don't ignore the balance** (check what's remaining)
- **Don't dispatch without filling DeliveredQty** (will be 0)

---

## Troubleshooting

### Problem: "All quantities have been delivered"

**Cause**: Job order is fully delivered, no balance remaining

**Solution**: 
- View the latest delivery note (click YES on dialog)
- Or select a different job order

---

### Problem: Delivery note shows 0 items

**Cause**: All items have BalanceQty = 0 (fully delivered)

**Solution**:
- This shouldn't happen (system prevents creating DN with 0 items)
- Check job order status
- Contact support if issue persists

---

### Problem: "SELECT ALL REMAINING" button is disabled

**Cause**: 
- Delivery note is already dispatched (read-only)
- Or all items have BalanceQty = 0

**Solution**:
- Button only works on draft delivery notes
- Check if DN is dispatched (can't modify after dispatch)

---

### Problem: Balance doesn't match what I expect

**Cause**: Previous delivery notes exist that you're not aware of

**Solution**:
- Check delivery note list for this job order
- Filter by job order number
- Review all DNs to see total delivered

---

## FAQ

**Q: What's the difference between "Ordered Qty" and "Balance Qty"?**  
A: Ordered Qty = total ordered in job order. Balance Qty = remaining to deliver (Ordered - Previously Delivered).

**Q: Can I deliver more than the Balance Qty?**  
A: No, system prevents over-delivery. DeliveredQty must be ≤ BalanceQty.

**Q: What if I make a mistake in DeliveredQty?**  
A: Click "RESET QTY" to start over, or just change the value before dispatching.

**Q: Can I edit a dispatched delivery note?**  
A: No, dispatched DNs are locked. Create a return/rework DN if needed.

**Q: Why do I see "Previously Delivered" on some DNs?**  
A: This shows what was already delivered in previous DNs for the same job order.

**Q: What happens if I dispatch with DeliveredQty = 0?**  
A: System will warn you. Dispatching with 0 quantities is unusual.

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+N` | Create new delivery note |
| `Ctrl+S` | Save (draft) |
| `Ctrl+D` | Dispatch |
| `Ctrl+R` | Reset quantities |
| `F5` | Refresh list |
| `Esc` | Cancel/Go back |

---

## Support

For issues or questions:
1. Check this user guide first
2. Review the delivery note list for the job order
3. Contact your system administrator
4. Check system logs for errors

---

**Document Version**: 1.0  
**Last Updated**: 2026-08-30  
**Applies to**: ProGlass Automation Advanced v1.0
