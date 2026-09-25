# Release acceptance matrix

| Category | Result | Notes |
|---|---|---|
| Source inspection | PASS | Inventory and targeted reads completed. |
| Static XAML well-formed | PASS | `scripts/xaml-guard.py` clean, 346 files. |
| DynamicResource resolution in Views | PASS | Scanner found no unresolved keys after this cycle. |
| Language key-set parity | PASS | 15 dictionaries, same key set. |
| Release build | BLOCKED | No SDK. |
| Automated tests | BLOCKED | No SDK. |
| Runtime UI | UNVERIFIED | WPF cannot run here. |
| Persistence restart | UNVERIFIED | |
| Financial totals | UNVERIFIED | Calculators exist; not executed. |
| Optimizer conservation | UNVERIFIED | Code checks input == placed + unplaced. Not executed. |
| Security secrets scan | PASS | No private keys or obvious API tokens in the sample scan. License integrity check is fail-open by design and is not the RSA gate. |
| Installer pack | UNVERIFIED | Script and flags reviewed. Not packed. |
| Evidence excluded from app | PASS | csproj removes `PRODUCTION_READINESS/**`. |

Overall public-release result: FAIL.
