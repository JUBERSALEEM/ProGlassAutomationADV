# Delivery Note Module - Complete Rewrite Plan

## Current Problems (User Feedback)
1. ❌ Too many popups and confirmations
2. ❌ "Save" button is confusing (should auto-save)
3. ❌ Job picker shows ALL job orders (should show only those needing DNs)
4. ❌ "Previously Delivered" column is confusing
5. ❌ "Open" button is redundant (row click should open)
6. ❌ Shows fully delivered items (should filter out)
7. ❌ Missing glass weight and total weight
8. ❌ "Dummy DN" concept is confusing (should be invisible)

## New Simple Workflow

### User Journey:
```
1. User clicks "CREATE DN" button
   ↓
2. Job picker shows ONLY job orders with:
   - No delivery notes yet, OR
   - Balance remaining (not fully delivered)
   ↓
3. User selects job order
   ↓
4. Detail view opens DIRECTLY (no popup)
   Shows ONLY items with BalanceQty > 0
   Columns: Item | Description | Ordered | Balance | Delivered | Weight
   ↓
5. User fills DeliveredQty (or clicks "Fill All Balance")
   ↓
6. User clicks "DISPATCH"
   ↓
7. System saves and returns to list
   ↓
8. Done! ✅
```

## Implementation Changes

### 1. Job Picker Filter (Show Only Job Orders Needing DNs)

**File**: `ViewModels/DeliveryNote/DeliveryNoteList/DeliveryNoteListViewModel.cs`

**Change**: Filter job orders to show only those with balance remaining

```csharp
private async Task OpenJobOrderPickerAsync()
{
    // Get all job orders
    var allJobOrders = await App.Jobs.GetAllAsync();
    
    // Get all delivery notes
    var allDns = await App.DeliveryNotes.GetAllAsync();
    
    // Filter: Only job orders with balance remaining
    var jobOrdersWithBalance = new List<SavedJobOrder>();
    
    foreach (var jo in allJobOrders)
    {
        var masterDn = await App.DeliveryNotes.GetMasterDnForJobOrderAsync(jo.JobOrderId);
        
        if (masterDn == null)
        {
            // No DN created yet - show this job order
            jobOrdersWithBalance.Add(jo);
        }
        else if (masterDn.TotalBalanceQty > 0)
        {
            // Has balance remaining - show this job order
            jobOrdersWithBalance.Add(jo);
        }
        // else: Fully delivered - don't show
    }
    
    // Show picker with filtered list
    var dialog = new JobOrderPickerDialog(jobOrdersWithBalance, allDns);
    if (dialog.ShowDialog() == true)
    {
        OnCreateDnFromJobOrder?.Invoke(dialog.SelectedJobOrder);
    }
}
```

---

### 2. Remove All Confirmation Popups

**File**: `ViewModels/DeliveryNote/DeliveryNoteViewModel.cs`

**Remove**:
- "Remaining Balance Found" popup (line 548-560)
- "All quantities delivered" popup (line 505-520)
- "Delivery Note Created" confirmation (line 685-700)

**Replace with**: Direct action, no questions

---

### 3. Remove "Save" Button (Auto-Save on Dispatch)

**File**: `Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml`

**Remove**: Save button

**Keep**: Only Dispatch button

**File**: `ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.cs`

**Change**: Auto-save before dispatch

```csharp
private async Task ConfirmDispatchAsync()
{
    // Auto-save first
    await SaveInternalAsync();
    
    // Then dispatch
    // ... existing dispatch logic
}
```

---

### 4. Remove "Previously Delivered" Column

**File**: `Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml`

**Remove**: PreviouslyDeliveredQty column from items grid

**Keep**: OrderedQty, BalanceQty, DeliveredQty

**File**: `ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailItemRow.cs`

**Remove**: PreviouslyDeliveredQty property (or hide it)

---

### 5. Add Glass Weight Columns

**File**: `Models/DeliveryNote/SavedDeliveryNote/DeliveryNoteItem.cs`

**Add**:
```csharp
public decimal WeightPerPiece { get; set; }
public decimal TotalWeight => DeliveredQty * WeightPerPiece;
```

**File**: `Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml`

**Add columns**:
- Weight/Piece (kg)
- Total Weight (kg)

**Add footer**: Total Weight for all items

---

### 6. Row Click Opens DN (Remove "Open" Button)

**File**: `Views/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml`

**Change**: Add MouseDoubleClick or PreviewMouseLeftButtonDown to row

```xml
<DataGrid.RowStyle>
    <Style TargetType="DataGridRow">
        <EventSetter Event="MouseDoubleClick" 
                     Handler="DeliveryNoteRow_MouseDoubleClick"/>
    </Style>
</DataGrid.RowStyle>
```

**File**: `Views/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml.cs`

**Add**:
```csharp
private void DeliveryNoteRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
{
    if (sender is DataGridRow row && row.Item is DeliveryNoteRowViewModel dn)
    {
        ViewModel.OpenCommand.Execute(dn);
    }
}
```

---

### 7. Replace "Open" with "Create DN" for Job Orders

**File**: `Views/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml`

**Change**: In job order group row, show "Create DN" button instead of "Open"

```xml
<Button Content="Create DN"
        Command="{Binding DataContext.CreateDnForJobOrderCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
        CommandParameter="{Binding JobOrderId}"
        Visibility="{Binding HasBalance, Converter={StaticResource BoolToVisConverter}}"/>
```

---

### 8. Simplify Detail View (Remove Confusing Columns)

**File**: `Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml`

**Remove**:
- "Previously Delivered" column
- "This DN Total" summary card
- Complex balance calculations display

**Keep**:
- Item, Description, Ordered, Balance, Delivered
- Weight/Piece, Total Weight
- Simple total at bottom

---

## Database Changes

### Add Weight Columns to DeliveryNoteItems

**Migration**:
```sql
ALTER TABLE DeliveryNoteItems ADD COLUMN WeightPerPiece REAL DEFAULT 0;
```

**File**: `Services/DeliveryNote/Repository/DeliveryNoteRepository.cs`

**Update**: Insert/Update methods to include WeightPerPiece

---

## UI Mockup

### List View
```
┌─────────────────────────────────────────────────────────────┐
│  CREATE DN    │  Filter: [All ▼]  Search: [________]        │
├─────────────────────────────────────────────────────────────┤
│  DN-00001-01  │  JOB-00001  │  ABC Glass  │  Dispatched     │
│  DN-00001-02  │  JOB-00001  │  ABC Glass  │  Draft          │
│  DN-00002-01  │  JOB-00002  │  XYZ LLC    │  Dispatched     │
└─────────────────────────────────────────────────────────────┘

Click row → Opens detail view
```

### Detail View
```
┌─────────────────────────────────────────────────────────────┐
│  Delivery Note: DN-00001-01                                 │
│  Job Order: JOB-00001  │  Customer: ABC Glass LLC          │
├─────────────────────────────────────────────────────────────┤
│  Item        │ Ordered │ Balance │ Delivered │ Weight │ Total│
│              │   (pcs) │  (pcs)  │   (pcs)   │(kg/pc) │(kg)  │
├──────────────┼─────────┼─────────┼───────────┼────────┼──────┤
│  Glass 10mm  │   100   │    40   │    [40]   │  25.5  │ 1020 │
│  Glass 15mm  │    75   │    45   │    [45]   │  38.2  │ 1719 │
├──────────────┴─────────┴─────────┴───────────┴────────┼──────┤
│  Total: 85 pcs                                        │ 2739 │
└───────────────────────────────────────────────────────┴──────┘

  [FILL ALL BALANCE]                    [DISPATCH]
```

**No "Save" button** - Auto-saves on dispatch  
**No "Previously Delivered"** - Only shows balance  
**Weight columns** - Important for delivery logistics

---

## Testing Checklist

- [ ] Job picker shows only job orders with balance
- [ ] No popups when creating DN
- [ ] Detail view shows only balance items
- [ ] "Previously Delivered" column removed
- [ ] Weight columns added and calculated
- [ ] Row click opens DN
- [ ] No "Save" button, only "Dispatch"
- [ ] Auto-save works before dispatch
- [ ] Fully delivered items filtered out
- [ ] Total weight calculated correctly

---

## Benefits

✅ **Simple** - No confusing popups or buttons  
✅ **Fast** - Direct action, no confirmations  
✅ **Clear** - Only shows what needs to be delivered  
✅ **Complete** - Includes weight for logistics  
✅ **Professional** - Industrial-grade workflow  

---

**Status**: Ready for Implementation  
**Estimated Time**: 4-6 hours  
**Priority**: High
