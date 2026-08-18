# Line-item DataGrid — analysis + proposed shared chrome

Approve before any Step 5 / JO / DN / TI detail rewrite.

## 1. Proforma Workspace / God View / Activity

**Not removed.** Proforma never had those tabs.

| Module | Shell | Why |
|---|---|---|
| Job Order / Delivery Note / Tax Invoice | Workspace · God View · Activity | List ↔ detail + all-records + audit |
| **Proforma Invoice** | List ↔ 9-step **wizard** | Quote builder, not a document workspace |

`git log -S GodView -- Views/ProformaInvoice` is empty. The list redesign only touched `SavedInvoicesListView` chrome + the list header button. Wizard steps are intact.

Adding PI God View / Activity would be a **new feature** (new VM + view + shell tabs). Say if you want that after the grid pass.

## 2. Step 5 DataGrid — verdict

**Functionally good. Chrome is local and theme-leaky. Do not rewrite the Excel engine.**

Keep (this is the factory grid):

| Col | Binding | Role |
|---|---|---|
| # | `SerialNumber` | read-only |
| GLASS REF | `GlassRef` | edit |
| W1 H1 W2 H2 | `W1` `H1` `W2` `H2` | integer edit |
| QTY | `Qty` | integer edit |
| SQM | `Sqm1` | computed |
| T.SQM | `TotalSqm` | computed (SQM × Qty) |
| RATE | `Rate` | decimal edit |
| VALUE | `Value` | computed AED |
| (del) | `RemoveRowCommand` | row action |

Also keep: spec groups, glass type + spec autocomplete, imported price / surcharge badges, Excel nav (`DataGridNavigationBehavior`), Ctrl+V paste, find/replace, multi-select, fill-down, footer subtotals, generate-N-rows.

Not a WPF `<DataGrid>`. It is a **ListView + aligned Grid** so cells stay editable. Shared styles must target that pattern. JO / DN / TI overview sections copy the same 12-column recipe by hand.

Gaps vs the industrial list language:

- ~15 local styles live only in Step 5 (`CompactIntInputStyle`, row container, combo, generate-rows).
- Selection / find flash hardcode `#DBEAFE` `#3B82F6` `#FEF08A` `#EAB308` — stay light on Dark theme.
- Generate-rows uses `CornerRadius="6,0,0,6"` and `#C7D2FE`.
- Header is a fully-rounded pill sitting above rows (should be one-sided `12,12,0,0` on the table card).
- Column widths are repeated 3× (header / row / footer) per file.

## 3. Proposed shared primitives (`Themes/Styles/DataGridStyles.xaml`)

One column recipe + cell/row/header/footer styles. Views keep their bindings.

| Style | Use |
|---|---|
| `LineItemsTableStyle` | outer table card |
| `LineItemsHeaderStyle` | pale header, one-sided 12 px |
| `LineItemsHeaderTextStyle` | 10 px bold muted, uppercase |
| `LineItemsRowStyle` | 40–44 px row, hover `MenuHoverBrush`, selected `TintBlueBrush` + accent edge |
| `LineItemsFindHighlightBrush` | theme token, not `#FEF08A` |
| `LineItemsFooterStyle` | subtotal bar |
| `LineItemsSerialStyle` | # |
| `LineItemsTextCellStyle` | Glass Ref |
| `LineItemsIntCellStyle` | W1 H1 W2 H2 Qty |
| `LineItemsDecimalCellStyle` | Rate |
| `LineItemsReadonlyStyle` | SQM / T.SQM |
| `LineItemsMoneyStyle` | Value (AED, success) |

Canonical column set (same `Grid.ColumnDefinitions` in header, row, footer):

```
26 | 1.8* | 0.75* | 0.75* | 0.75* | 0.75* | 0.7* | 0.9* | 0.9* | 0.85* | 1.2* | 28
#    REF    W1      H1      W2      H2      QTY    SQM    T.SQM   RATE    VALUE   del
```

Read-only detail (JO/DN/TI overview) uses the same columns; cells are `TextBlock`s with `LineItemsReadonlyStyle` / `LineItemsMoneyStyle` instead of TextBoxes.

## 4. Apply order (after you approve)

1. Register `DataGridStyles.xaml` (file is drafted, not merged yet).
2. Pilot **Step 5 only** — swap local compact/header/row styles to the shared keys. No binding / Excel-nav changes.
3. JO overview → DN overview → TI overview (same column recipe).
4. Do **not** touch printable invoice.

Mockups: `Docs/datagrid-target-step5.png`, `Docs/datagrid-target-detail.png`.
