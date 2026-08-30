# Database Migration Fix: WeightPerPiece Column

## Issue
**Error**: `SQLite error 1: no such column: d.WeightPerPiece`

**Root Cause**: The `WeightPerPiece` property was added to the `DeliveryNoteItem` model, but no database migration was created to add the corresponding column to existing SQLite databases.

## Solution

### 1. Added Schema Migration
**File**: `Services/Database/SchemaMigrations/AppSchemaMigrations.cs`

```csharp
// deliverynotes.db
public static IReadOnlyList<SchemaMigration> DeliveryNotes { get; } = new List<SchemaMigration>
{
    new(1, "Add WeightPerPiece column for glass weight tracking", connection =>
    {
        SchemaMigrationSql.AddColumnIfMissing(connection, "DeliveryNoteItems", "WeightPerPiece", "TEXT NOT NULL DEFAULT '0'");
    })
};
```

### 2. Migration Details
- **Version**: 1
- **Table**: `DeliveryNoteItems`
- **Column**: `WeightPerPiece`
- **Type**: `TEXT NOT NULL DEFAULT '0'` (SQLite stores decimals as TEXT)
- **Idempotent**: Uses `AddColumnIfMissing` to safely skip if column already exists
- **Default Value**: `'0'` for existing rows

### 3. How It Works

The migration system follows this flow:

1. **App Startup** → `DeliveryNoteRepository.InitializeAsync()` is called
2. **EnsureCreated** → Creates database and tables if they don't exist
3. **SchemaMigrationRunner.Upgrade()** → Checks current schema version via `PRAGMA user_version`
4. **Apply Pending Migrations** → Runs any migrations with version > current version
5. **Update Version** → Sets `PRAGMA user_version` to the latest applied migration
6. **Transaction Safety** → All migrations run in a single transaction with automatic rollback on failure
7. **Backup** → Creates a backup before migration in `MigrationBackups` folder

### 4. Migration Execution

When the app starts on an existing database:

```
[INFO] Schema upgraded deliverynotes.db → v1 (1 migration(s) applied).
```

The migration:
- Adds the `WeightPerPiece` column to the `DeliveryNoteItems` table
- Sets the value to `'0'` for all existing rows
- Future inserts will calculate the actual weight

### 5. Files Modified

| File | Change |
|------|--------|
| `Services/Database/SchemaMigrations/AppSchemaMigrations.cs` | Added migration v1 to DeliveryNotes registry |

### 6. Testing

**Before Migration**:
```
SQLite error 1: no such column: d.WeightPerPiece
```

**After Migration**:
- ✅ Delivery notes load successfully
- ✅ WeightPerPiece column exists in database
- ✅ Existing rows have WeightPerPiece = '0'
- ✅ New delivery notes calculate weight correctly

### 7. Related Fixes

This migration was part of a larger feature implementation:

1. **GlassWeightCalculator** - New utility for calculating glass weight
2. **DeliveryNoteItem.WeightPerPiece** - New property to store weight per piece
3. **DeliveryNoteItem.TotalWeight** - Computed property (WeightPerPiece × DeliveredQty)
4. **DeliveryNoteMapper** - Calculates weight when creating delivery notes from job orders
5. **Export Support** - Weight included in CSV/Excel/PDF exports

### 8. Database Schema

**Before Migration**:
```sql
CREATE TABLE DeliveryNoteItems (
    Id INTEGER PRIMARY KEY,
    DeliveryNoteId TEXT NOT NULL,
    JobOrderId TEXT NOT NULL,
    SpecGroupNumber INTEGER NOT NULL,
    SerialNumber INTEGER NOT NULL,
    GlassRef TEXT,
    W1 INTEGER NOT NULL,
    H1 INTEGER NOT NULL,
    W2 INTEGER NOT NULL,
    H2 INTEGER NOT NULL,
    OrderedQty INTEGER NOT NULL,
    PreviouslyDeliveredQty INTEGER NOT NULL,
    DeliveredQty INTEGER NOT NULL,
    BalanceQty INTEGER NOT NULL,
    ReworkQty INTEGER NOT NULL,
    ReturnedQty INTEGER NOT NULL,
    Notes TEXT
);
```

**After Migration**:
```sql
CREATE TABLE DeliveryNoteItems (
    Id INTEGER PRIMARY KEY,
    DeliveryNoteId TEXT NOT NULL,
    JobOrderId TEXT NOT NULL,
    SpecGroupNumber INTEGER NOT NULL,
    SerialNumber INTEGER NOT NULL,
    GlassRef TEXT,
    W1 INTEGER NOT NULL,
    H1 INTEGER NOT NULL,
    W2 INTEGER NOT NULL,
    H2 INTEGER NOT NULL,
    OrderedQty INTEGER NOT NULL,
    PreviouslyDeliveredQty INTEGER NOT NULL,
    DeliveredQty INTEGER NOT NULL,
    BalanceQty INTEGER NOT NULL,
    ReworkQty INTEGER NOT NULL,
    ReturnedQty INTEGER NOT NULL,
    WeightPerPiece TEXT NOT NULL DEFAULT '0',  -- NEW COLUMN
    Notes TEXT
);
```

### 9. Migration Safety Features

✅ **Idempotent**: Can be run multiple times safely  
✅ **Transactional**: Rolls back on failure  
✅ **Backup**: Creates pre-migration backup  
✅ **Version Tracking**: Uses `PRAGMA user_version`  
✅ **Non-Destructive**: Never drops tables or columns  
✅ **Additive Only**: Only adds new schema elements  

### 10. Rollback Procedure

If migration fails:
1. Check logs for error details
2. Backup created at: `%AppData%\ProGlassAutomation\MigrationBackups\deliverynotes_pre_v1_YYYYMMDD_HHMMSS.db`
3. Restore backup if needed:
   ```bash
   copy "MigrationBackups\deliverynotes_pre_v1_*.db" "deliverynotes.db"
   ```
4. Fix the issue and restart app to retry migration

### 11. Verification

To verify the migration was applied:

```sql
-- Check schema version
PRAGMA user_version;  -- Should return 1

-- Check column exists
PRAGMA table_info(DeliveryNoteItems);
-- Should include: WeightPerPiece TEXT NOT NULL DEFAULT '0'

-- Check existing data
SELECT Id, WeightPerPiece FROM DeliveryNoteItems LIMIT 5;
-- Existing rows should have WeightPerPiece = '0'
```

### 12. Future Migrations

To add more migrations to `deliverynotes.db`:

```csharp
public static IReadOnlyList<SchemaMigration> DeliveryNotes { get; } = new List<SchemaMigration>
{
    new(1, "Add WeightPerPiece column", connection => { ... }),
    new(2, "Add NextFeature column", connection => { ... }),  // Future
    new(3, "Add AnotherColumn", connection => { ... })        // Future
};
```

**Rules**:
- Always increment version number
- Never modify existing migrations (they're already applied)
- Use idempotent operations (AddColumnIfMissing, CreateTableIfNotExists)
- Test on a copy of production database first

## Summary

✅ Migration v1 added to `deliverynotes.db`  
✅ `WeightPerPiece` column added to `DeliveryNoteItems` table  
✅ Existing rows initialized with `'0'`  
✅ Migration runs automatically on app startup  
✅ Safe, idempotent, and reversible  

**Commit**: `4e91ab8`  
**Status**: ✅ Complete and tested
