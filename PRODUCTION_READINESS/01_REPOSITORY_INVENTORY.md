# Repository inventory

- App: `ProGlassAutomation.csproj`, `OutputType=WinExe`, `TargetFramework=net8.0-windows`, `UseWPF=true`.
- Solution marker: `ProGlassAutomation.slnx`.
- Tests: `Tests/ProGlassAutomation.Tests.csproj` (`net8.0-windows`), `Licensing/ProGlass.Licensing.Tests/ProGlass.Licensing.Tests.csproj` (`net8.0`).
- Installer: `scripts/build-installer.ps1`, `installer/velopack.json`, `installer/content/`, `scripts/generate-installer-art.ps1`.
- Languages: `Themes/Languages/*.xaml` (15).
- Databases: multiple SQLite contexts under `Services/**/Database` and repositories. Schema upgrades go through `SchemaMigrationRunner` (backup, one transaction, fail closed on that file).
- SDK: not installed here. Microsoft download endpoints unreachable.

Counts recorded at baseline: 1653 `.cs`, 346 `.xaml`, 275 test source files.
