# Known limitations

- This environment cannot build or launch the WPF app. Every runtime claim is UNVERIFIED.
- `AssemblyIntegrity` returns true on check errors. The comment says it is a speed bump, not the license signature gate. Not changed.
- `SchemaMigrationRunner.Upgrade` failure is logged. Callers generally do not stop the user with a repair action. Not changed, to avoid an untested lockout.
- MSI cannot be a custom rounded wizard. Windows draws that chrome. Already the shipped design.
- Print token values are paper-safe defaults, not a pixel match of a previously approved PDF. Windows preview still needs a human look.
