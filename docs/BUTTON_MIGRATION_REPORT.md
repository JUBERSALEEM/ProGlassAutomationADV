# Button Standardization Migration Report (master prompt v1.0)

## Summary
- Files scanned: 96 XAML under `Views/` (+ Controls/)
- Files modified: 34
- Buttons migrated: ~180
- Emojis replaced: ~120 (icon slots, Tags, Contents, badges)
- Legacy styles removed: 17 (ScPrimaryButton, ActionButtonStyle, ExportButtonStyle,
  HeaderActionButtonStyle, HeaderUploadButtonStyle, CreateDnButtonStyle,
  CreateFromMasterButtonStyle, FillRemainingButtonStyle, ResetQtyButtonStyle,
  OpenButtonStyle, HistoryButtonStyle, CloseButtonStyle, SecondaryButtonStyle,
  PrimaryButtonStyle, QtyButtonStyle, CancelButtonStyle, ConfirmButtonStyle,
  FilterActionButtonStyle, CancelImportBtn — where usage reached zero)
- Tooltips added: ~60

## Centralized system (`Styles/ButtonStyles.xaml`)
| Variant | Shape | Role mapping used |
|---|---|---|
| PrimaryPillButton | pill H40 | Save/Create/Refresh/Back-primary/Download |
| SuccessPillButton | pill H40 | Export/Confirm/Mark Paid/Restore/Install |
| OutlinedPillButton | pill H40 | Secondary/Cancel/Back/Clear |
| DangerButton | rect R8 H32 | Delete/Deactivate/Cancel-import |
| TabPillButton | alias | — |
| RowPrimary/RowSuccess/RowOutlined/RowDanger | rect R8 H26 | in-row compact actions (one shape family) |

Templates bind Height/Padding via TemplateBinding so compact row usage
stays inline per-usage (rule 7 honored).

## Per-area notes
- **Shells (DN/JO/TI):** tabs → STEP D two-button visibility pattern
  (WPF-correct; no Style-in-trigger). Grey oval strip removed.
- **DN list:** rebuilt once from baseline after a regex overran
  (regression post-mortem: migration tooling now uses a depth-balanced
  element scanner; loss-audit on bindings + x:Names after every file).
- **God Views / details / dialogs:** toolbar + row actions on the system;
  all Click handlers, x:Names, IsCancel/IsDefault, RelativeSource
  commands preserved.
- **Settings:** visibility Style blocks re-inserted with BasedOn merge
  (no dual-Style).
- **PI wizard:** own pill chrome (Hero/Ghost/Neutral, Nav buttons) kept —
  not on the forbidden list; emojis removed/MDL2'd.
- **Dashboards/AI:** icon slots converted to Segoe MDL2 Assets.

## Unmapped emojis (map extensions used)
🚚→E7F4 (Truck), 🎯→E71D, 💰/💸→E7BF, 📈→E9F9, 📀→dropped, ⏳/🕘→E81C,
↩️→E7A7, ⚡→E7E8, 📎→E723, →E728, →E718, →E774, ←/Back→E72B,
🏭→E822, →E8A5, /→E713, →E767.

## Buttons requiring manual review
None functionally; visual QA of pill density on 4K/large fonts recommended.

## Suggested next enhancements
- IconOnlyButton + GhostButton variants for toolbars
- Focus-visible ring audit (WCAG) on row variants
- Dashboard hero cards → theme-token gradients (deferred visual pass)
