# Phase 3 test-skip triage

## Scope and result

A repository-wide scan of C# files in the three test projects found **88 skipped xUnit test declarations** (`[Fact(Skip = ...)]` / `[Theory(Skip = ...)]`) before triage. This count is declarations/methods, not expanded `[InlineData]` cases. Source review found several skip notes that no longer described the current production types or behavior. **67 declarations have been re-enabled** after verifying their current APIs and test inputs; they were not executed in this environment. **21 declarations remain skipped** and are listed below.

Test projects scanned:

- `Tests/ProGlassAutomation.Tests.csproj`
- `Licensing/ProGlass.Licensing.Tests/ProGlass.Licensing.Tests.csproj`
- `Tools/EngineRegression/EngineRegression.csproj`

The re-enabled groups are `CorrelationContextTests` (5), `DeliveryNoteValidationServiceTests` (10), `PermissionGateTests` (12), `ProfitLossIntegrityTests` (17), `StatementSnapshotProjectorTests` (3), 11 deterministic `ProcessStartGuardTests`, and 9 `ScreenRecordSettingsTests` whose stale expected values were ported to the current settings contract. The test that can launch a system `ffmpeg` executable remains skipped.

## Remaining skipped tests

### Process start guard — 1 test

Shared skip reason: the test may launch a real external `ffmpeg` process when one is present on `PATH`; split allowlist validation from process execution before enabling it in routine CI.

| Priority | Test name | File path | Description |
|---|---|---|---|
| Normal | `TryStartTool_allows_ffmpeg_system_name` | `Tests/Agent/ProcessStartGuardTests.cs` | Checks recognition of `ffmpeg` as an allowed system tool; currently invokes it if installed. |

### Profit and loss glass-layer view-model tests — 4 tests

Shared skip reason: `RecalculateAsync` queries `SheetStorePricingService`, which can read the live Sheet Store database. Add a deterministic fake/isolated pricing seam and verify the WPF view-model expectations before enabling these tests.

| Priority | Test name | File path | Description |
|---|---|---|---|
| Normal | `Clear_clear_dgu_lists_outer_then_spacer_then_inner` | `Tests/Agent/ProfitLossLayerTests.cs` | Verifies Step 9 shows the outer pane, spacer, and inner pane in order. |
| Normal | `Identical_colours_still_yield_two_glass_rows` | `Tests/Agent/ProfitLossLayerTests.cs` | Verifies same-colour DGU still produces two glass layers. |
| Normal | `Glass_rows_are_visible_even_when_step4_rates_are_zero` | `Tests/Agent/ProfitLossLayerTests.cs` | Verifies unpriced glass remains visible and does not acquire a fabricated cost. |
| Normal | `Missing_spec_name_still_synthesises_dgu_layers_from_step4` | `Tests/Agent/ProfitLossLayerTests.cs` | Verifies layer descriptions can be derived when the saved spec name is blank. |

### Agent provider fallback tests — 9 tests

Shared skip reason: the fixture persists `AgentConfigurationService` changes to the real `%AppData%` settings file and can consult the real credential vault. Add isolated settings/key-store seams before enabling these tests.

| Priority | Test name | File path | Description |
|---|---|---|---|
| Normal | `LocalOnly_selects_the_local_provider` | `Tests/Agent/ProviderFallbackTests.cs` | Confirms explicit local-only mode chooses the local provider. |
| Normal | `No_consent_selects_the_local_provider` | `Tests/Agent/ProviderFallbackTests.cs` | Confirms missing cloud consent keeps provider selection local. |
| Normal | `Consent_without_a_stored_key_still_selects_local` | `Tests/Agent/ProviderFallbackTests.cs` | Confirms consent without a provider key does not select cloud. |
| Normal | `Selection_never_returns_null` | `Tests/Agent/ProviderFallbackTests.cs` | Confirms invalid settings still yield a provider. |
| Normal | `Local_provider_answers_without_any_model_file` | `Tests/Agent/ProviderFallbackTests.cs` | Confirms the knowledge-base local provider can answer without a model download. |
| Normal | `Advice_is_a_recommendation_and_never_a_download` | `Tests/Agent/ProviderFallbackTests.cs` | Confirms model advice is explicitly recommendation-only. |
| Normal | `Low_memory_machines_are_told_no_model_rather_than_a_smaller_lie` | `Tests/Agent/ProviderFallbackTests.cs` | Confirms low-memory recommendations do not claim an unsupported model is usable. |
| Normal | `Advice_scales_with_available_memory` | `Tests/Agent/ProviderFallbackTests.cs` | Checks hardware-based model tier recommendations. |
| Normal | `Recommended_models_are_permissively_licensed` | `Tests/Agent/ProviderFallbackTests.cs` | Checks candidate model license wording/metadata. |

### Screen-record capture setting — 1 test

Shared skip reason: `Supersample` is a legacy persisted flag and is not a reliable proxy for the current physical capture pipeline. Replace the assertion with one against the current capture contract before enabling it.

| Priority | Test name | File path | Description |
|---|---|---|---|
| Normal | `Capture_is_native_output_size_by_default` | `Tests/Agent/ScreenRecordSettingsTests.cs` | Checks default physical capture dimensions versus configured output dimensions. |

### Inquiry attachment manifest — 6 tests

Shared skip reason: the fixture uses `AppDataRoot.CurrentFolder` and creates application-data files; Add/Delete also start fire-and-forget audit writes. Isolate the root and audit completion before enabling. The prior skip note about missing `InquiryAttachment` members was obsolete: those members are present on the current model.

| Priority | Test name | File path | Description |
|---|---|---|---|
| **HIGH PRIORITY — data preservation** | `Add_persists_to_manifest_and_disk_without_any_entity_save` | `Tests/Inquiry/InquiryAttachmentManifestTests.cs` | Verifies a copied attachment and manifest entry survive without requiring an EF entity write. |
| **HIGH PRIORITY — data preservation** | `Delete_removes_manifest_entry_and_file` | `Tests/Inquiry/InquiryAttachmentManifestTests.cs` | Verifies an explicit attachment deletion removes its manifest entry and file. |
| **HIGH PRIORITY — data integrity** | `Corrupt_manifest_reads_as_empty_never_throws` | `Tests/Inquiry/InquiryAttachmentManifestTests.cs` | Covers corrupt attachment-manifest handling; review that data is not silently lost when this is re-enabled. |
| **HIGH PRIORITY — legacy migration/data preservation** | `Legacy_ef_rows_migrate_into_manifest_with_resolved_paths` | `Tests/Inquiry/InquiryAttachmentManifestTests.cs` | Verifies legacy EF attachment rows and relocated files are migrated/resolved into the manifest. |
| **HIGH PRIORITY — legacy migration/data preservation** | `Broken_legacy_table_never_blocks_the_module` | `Tests/Inquiry/InquiryAttachmentManifestTests.cs` | Verifies an unreadable legacy attachment table does not prevent manifest-backed access/addition. |
| Normal | `Unsupported_extension_or_missing_file_is_rejected_upfront` | `Tests/Inquiry/InquiryAttachmentManifestTests.cs` | Checks rejection of unsupported extensions and missing source files. |

## High-priority un-skip queue

The remaining **HIGH PRIORITY** skipped declarations are the five attachment-manifest tests above, especially the EF-row-to-manifest legacy migration and unreadable-legacy-table cases. Their root/audit fixture must be made hermetic before they are run in shared Windows CI.

These critical tests are no longer skipped after source review; they are **HIGH PRIORITY — now enabled, not yet executed**:

| Priority | Test(s) | File path | Coverage |
|---|---|---|---|
| **HIGH PRIORITY — permissions/confirmation** | 12 tests in `PermissionGateTests` | `Tests/Agent/PermissionGateTests.cs` | Role permission denials, identity matching, missing permission, confirmation fail-closed behavior, explicit approval, and read confirmation policy. |
| **HIGH PRIORITY — TI/DN lifecycle guard** | `ValidateTaxInvoiceConversion_fails_when_already_invoiced`; `ValidateTaxInvoiceConversion_fails_on_invalid_status`; `ValidateTaxInvoiceConversion_succeeds_on_valid` | `Tests/Agent/DeliveryNoteValidationServiceTests.cs` | Delivery Note-to-Tax Invoice conversion preconditions. These are adjacent lifecycle checks, not full reconciliation-worker tests. |

The following requested critical areas have **no remaining skipped declarations found**:

- **Auth/bootstrap/last-admin:** no matching skipped test was found. This is not evidence of coverage; add/verify dedicated tests before release.
- **Permissions/read-only:** the 12 `PermissionGateTests` were re-enabled because the stale note claimed `ToolDefinition.Name` / `RequiredPermission` were absent when both exist. These tests were source-reviewed but not executed; verify their results in Windows CI.
- **Backup/restore and restore-drill:** no matching tests are currently marked skipped. Keep existing backup tests enabled and complete the Windows restore-drill/overlay acceptance checks.
- **TI/DN reconciliation:** no dedicated reconciliation test was found among skipped declarations. The three Delivery Note-to-Tax Invoice conversion guard tests were re-enabled; they are adjacent lifecycle tests, not a substitute for a reconciliation test.
- **Schema migration/data preservation:** no generic schema-migration test was found skipped. The legacy Inquiry attachment migration tests above remain high priority.

## Skip-reason updates and verification

Obsolete reasons were replaced with current blockers for the remaining tests: live AppData/credential-vault mutation, live Sheet Store lookup, legacy capture semantics, and launching `ffmpeg`. Several stale reasons were removed by re-enabling source-ready tests after checking current APIs. `ScreenRecordSettingsTests` expectations were ported for current defaults/quality tiers on tests that were re-enabled.

No build or tests were run here. The .NET SDK is unavailable, and this preparation task does not run CI. Execute the re-enabled tests on the Windows workflow before treating them as passing; investigate any failures rather than re-adding broad skip attributes.
