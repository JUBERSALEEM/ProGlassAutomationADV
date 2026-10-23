# Phase 3 — Windows dependency inventory and SQLite SBOM check

This is a preparation guide only. It does not change package versions or the `net8.0-windows` target, and no packages were installed or restored for this note.

## 1. Produce the resolved package list on Windows

Use a Windows development prompt (PowerShell) at the repository root. Restore first so the assets graph exists:

```powershell
dotnet restore ProGlassAutomation.csproj
dotnet restore Tests/ProGlassAutomation.Tests.csproj
dotnet restore Licensing/ProGlass.Licensing.Tests/ProGlass.Licensing.Tests.csproj
dotnet restore Tools/EngineRegression/EngineRegression.csproj
```

Then list direct **and transitive** packages for each project and retain the output with the release evidence:

```powershell
New-Item -ItemType Directory -Force .\artifacts\dependencies | Out-Null

$projects = @(
  "ProGlassAutomation.csproj",
  "Tests/ProGlassAutomation.Tests.csproj",
  "Licensing/ProGlass.Licensing.Tests/ProGlass.Licensing.Tests.csproj",
  "Tools/EngineRegression/EngineRegression.csproj"
)

foreach ($project in $projects) {
  Write-Host "=== $project ==="
  dotnet list $project package --include-transitive
  if ($LASTEXITCODE -ne 0) { throw "Package listing failed for $project" }
}
```

**There is currently no `.sln` file in this checkout**, so run the command for each project as above. If a solution is added later, the equivalent aggregate command is:

```powershell
dotnet list ProGlassAutomation.sln package --include-transitive
```

`dotnet list package` is a resolved NuGet package inventory, not by itself a signed release SBOM. The `obj/project.assets.json` files are useful corroborating evidence for exact package versions selected by restore; do not treat an un-restored `.csproj` reference alone as the complete dependency graph.

## 2. Generate a CycloneDX SBOM (optional release evidence)

On the Windows development machine, install/update the official .NET CycloneDX tool if it is not already available:

```powershell
dotnet tool install --global CycloneDX
# If already installed:
# dotnet tool update --global CycloneDX
```

The application project and its project references can be scanned recursively:

```powershell
dotnet-CycloneDX ProGlassAutomation.csproj --recursive --output .\artifacts\sbom\application
```

Since no solution exists, create separate output directories if SBOMs are also needed for the test projects:

```powershell
dotnet-CycloneDX Tests/ProGlassAutomation.Tests.csproj --recursive --output .\artifacts\sbom\application-tests
dotnet-CycloneDX Licensing/ProGlass.Licensing.Tests/ProGlass.Licensing.Tests.csproj --recursive --output .\artifacts\sbom\licensing-tests
dotnet-CycloneDX Tools/EngineRegression/EngineRegression.csproj --recursive --output .\artifacts\sbom\engine-regression-tests
```

CycloneDX for .NET usage/options: [CycloneDX/cyclonedx-dotnet](https://github.com/CycloneDX/cyclonedx-dotnet). Keep the generated BOMs and command output with the release evidence; do not commit generated files unless the release process later requires it.

## 3. SQLite package and native engine checks

The main app directly references the EF Core SQLite provider package **`Microsoft.EntityFrameworkCore.Sqlite`** (alongside EF Core Design/Tools). Its resolved graph commonly includes **`Microsoft.Data.Sqlite.Core`** and the **SQLitePCLRaw** packages, including:

- `SQLitePCLRaw.bundle_e_sqlite3`
- `SQLitePCLRaw.core`
- `SQLitePCLRaw.provider.e_sqlite3`
- `SQLitePCLRaw.lib.e_sqlite3`

Use the actual `dotnet list ... package --include-transitive` result and `obj/project.assets.json` to confirm which packages and versions the Windows build resolves. In particular, inspect the `SQLitePCLRaw.lib.e_sqlite3` entry and the Windows native asset (`e_sqlite3.dll`) included in the application output. Package names/versions alone are not proof of the SQLite engine version bundled inside a native asset.

For an authoritative check of the native SQLite library actually loaded by the same `Microsoft.Data.Sqlite` dependency graph, run a diagnostic from a small Windows test/console project that references the app's dependency graph:

```csharp
using Microsoft.Data.Sqlite;

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();
using var command = connection.CreateCommand();
command.CommandText = "SELECT sqlite_version();";
Console.WriteLine(command.ExecuteScalar());
```

Record that result for the release evidence. Do not use a separately installed `sqlite3.exe --version` as proof of the application bundle; it may report a different SQLite library from the one the app loads.

### CVE-2025-6965 reference

- GitHub advisory: [GHSA-2m69-gcr7-jv3q — CVE-2025-6965](https://github.com/advisories/GHSA-2m69-gcr7-jv3q).
- The advisory describes SQLite versions before **3.50.2** as affected and lists `SQLitePCLRaw.lib.e_sqlite3` versions through **2.1.11** in its affected range. At the time this preparation note was written, the GitHub advisory did **not** provide a `first_patched_version` for that NuGet package. Re-check the live advisory and package metadata at release time; do not guess a fixed NuGet package version from SQLite's engine version number.
- For release acceptance, verify that the SQLite engine actually loaded by the app reports **3.50.2 or later**, and resolve any affected `SQLitePCLRaw` package shown by the current advisory before shipping. If package metadata and the runtime engine check disagree, stop and resolve the discrepancy.

## 4. Windows pre-release checklist

- [ ] Restore all four projects and capture `dotnet list <project> package --include-transitive` output.
- [ ] Review the resolved EF Core SQLite / Microsoft.Data.Sqlite / SQLitePCLRaw dependency chain, including `SQLitePCLRaw.lib.e_sqlite3`.
- [ ] Inspect the Windows native `e_sqlite3.dll` asset and run the `SELECT sqlite_version()` diagnostic through the application's provider.
- [ ] Confirm the native SQLite engine is **at least 3.50.2** for CVE-2025-6965, and re-check the [GitHub advisory](https://github.com/advisories/GHSA-2m69-gcr7-jv3q) for updated package ranges/fixed-version data.
- [ ] If anything is affected or below the required native version, update the compatible package set, rebuild, rerun every test project on Windows, and regenerate the SBOM.
- [ ] Archive the package list, native-version result, SBOM, build/test logs, and any approved exceptions with the release evidence.
