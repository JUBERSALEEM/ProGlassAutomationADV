# Architecture baseline

WPF shell (`MainWindow`) hosts module views and view-models. Persistence is several SQLite files created with `EnsureCreated`, then brought forward by `SchemaMigrationRunner.Upgrade`.

`Upgrade` returns false when the pre-migration backup cannot be made or a migration throws. Most repository `Initialize` methods ignore that return value and continue. A failed migration leaves `user_version` unchanged, so the next open retries. EF writes against a model newer than the file can still throw. That path is logged, not shown as a single startup banner for every database. Status: UNVERIFIED at runtime. Not changed in this cycle because a hard lockout without a tested recovery UI could block a customer who can still read existing data.

Localization keeps `en-US` merged as fallback (`LocalizationService`). A missing translation does not blank a label if the English key exists.

Print preview consumes `Print.*` dynamic resources. Those keys were referenced and undefined. This cycle defined them in `Themes/Styles/PrintTokens.xaml`.
