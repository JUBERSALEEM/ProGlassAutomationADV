# ═══════════════════════════════════════════════════════════════════════
# FILE: tools/build-installer.ps1
# PROJECT: ProGlass Automation
# MODULE: Installer pipeline — local build (Windows developer box)
#
# What this script does (mirrors the .github/workflows/release.yml
# job, minus the GitHub Release publish step):
#
#   1.  Verify the toolchain: .NET 8 SDK on PATH.
#   2.  dotnet restore + build + test the app and the test project
#       (the test gate is non-negotiable; a red test run aborts the
#       installer build).
#   3.  dotnet publish using the Release.pubxml profile, producing
#       a self-contained win-x64 tree at .\publish\.
#   4.  Velopack pack the publish\ tree into a release artefact set:
#         • Setup.exe                        (full installer)
#         • ProGlassAutomation-2.0.0-win-x64.nupkg   (delta source)
#         • ProGlassAutomation-2.0.0-full.nupkg     (full payload)
#         • ProGlassAutomation-2.0.0-delta.nupkg    (delta against the
#                                                     previous release, if any)
#         • ProGlassAutomation-Portable-2.0.0.zip    (no-installer zip)
#       into .\releases\.
#   5.  Print a summary of what was produced and the SHA-256 of each
#       artefact, so the operator can sanity-check before attaching
#       to a GitHub Release.
#
# Usage (from the repo root, Windows PowerShell 5.1 OR PowerShell 7+):
#
#     powershell -ExecutionPolicy Bypass -File .\tools\build-installer.ps1
#     powershell -ExecutionPolicy Bypass -File .\tools\build-installer.ps1 -Version 2.0.1
#     powershell -ExecutionPolicy Bypass -File .\tools\build-installer.ps1 -SkipTests
#     powershell -ExecutionPolicy Bypass -File .\tools\build-installer.ps1 -PackId ProGlassAutomation
#
# (Use `pwsh` instead of `powershell` if PowerShell 7+ is installed; the
#  script is compatible with both — the only difference is the
#  ExecutionPolicy flag, which is a Windows-PowerShell-5.1-only quirk.)
#
# Exit codes:
#   0  success
#   1  dotnet / vpk missing
#   2  build or test gate failed
#   3  publish step failed
#   4  vpk pack step failed
# ═══════════════════════════════════════════════════════════════════════

[CmdletBinding()]
param(
    [string]$Version = "2.0.0",
    [string]$PackId = "ProGlassAutomation",
    [string]$PackTitle = "ProGlass Automation",
    [string]$PackAuthors = "Juber Saleem",
    [switch]$SkipTests,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

# ── Paths (resolved relative to the repo root) ─────────────────────
$RepoRoot     = Resolve-Path (Join-Path $PSScriptRoot "..")
$PublishDir   = Join-Path $RepoRoot "publish"
$ReleasesDir  = Join-Path $RepoRoot "releases"
$VpkManifest  = Join-Path $RepoRoot "installer/velopack.json"
$PortableZip  = "ProGlassAutomation-Portable-$Version.zip"

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host " ProGlass Automation — installer build" -ForegroundColor Cyan
Write-Host "   Version   : $Version" -ForegroundColor Cyan
Write-Host "   PackId    : $PackId" -ForegroundColor Cyan
Write-Host "   Repo root : $RepoRoot" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ── 0. Pre-flight: toolchain present? ──────────────────────────────
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".NET CLI not on PATH. Install .NET 8 SDK and retry."
    exit 1
}

$dotnetInfo = & dotnet --info 2>&1 | Select-String -Pattern "Version:" |
              Select-Object -First 1
if ($dotnetInfo -notmatch "8\.\d") {
    Write-Warning "dotnet reports a non-8.x SDK as the active one. The app targets net8.0-windows; pick an 8.x SDK with 'dotnet --list-sdks' and a global.json at the repo root, then re-run."
}

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Error "vpk (Velopack CLI) not on PATH. Install with:  dotnet tool install -g vpk"
    exit 1
}

# ── 1. Clean ───────────────────────────────────────────────────────
if ($Clean) {
    if (Test-Path $PublishDir)  { Remove-Item -Recurse -Force $PublishDir }
    if (Test-Path $ReleasesDir) { Remove-Item -Recurse -Force $ReleasesDir }
}

# ── 2. Build (Release) + restore ──────────────────────────────────
Write-Host "▶ Restoring + building Release configuration..." -ForegroundColor Yellow
& dotnet restore "$RepoRoot/ProGlassAutomation.csproj" --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit 2 }

& dotnet build "$RepoRoot/ProGlassAutomation.csproj" -c Release --no-restore --verbosity minimal -p:Version=$Version
if ($LASTEXITCODE -ne 0) { exit 2 }

# ── 3. Test gate ──────────────────────────────────────────────────
if (-not $SkipTests) {
    Write-Host "▶ Running xUnit test gate..." -ForegroundColor Yellow
    & dotnet test "$RepoRoot/Tests/ProGlassAutomation.Tests.csproj" -c Release --no-build --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Test gate failed. Refusing to package an installer that ships a red test suite."
        exit 2
    }
} else {
    Write-Warning "Skipping the test gate (-SkipTests). Do NOT publish this build to customers."
}

# ── 4. Publish (self-contained win-x64) ───────────────────────────
# Uses the Release.pubxml profile so the settings (SelfContained,
# win-x64, ReadyToRun, deterministic, no PDBs) live in one place
# and are shared with .github/workflows/release.yml.
Write-Host "▶ Publishing self-contained win-x64 (Release.pubxml)..." -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
& dotnet publish "$RepoRoot/ProGlassAutomation.csproj" -c Release -r win-x64 --self-contained true -p:PublishProfile=Release -o $PublishDir -p:Version=$Version
if ($LASTEXITCODE -ne 0) { exit 3 }

# ── 5. Velopack pack ──────────────────────────────────────────────
Write-Host "▶ Running vpk pack..." -ForegroundColor Yellow
if (Test-Path $ReleasesDir) { Remove-Item -Recurse -Force $ReleasesDir }

# The installer/velopack.json manifest is the durable source of
# packId / title / authors; version is overridden on the command
# line so the same manifest can pack any release.
& vpk pack --config $VpkManifest --packVersion $Version --packDir $PublishDir --outputDir $ReleasesDir
if ($LASTEXITCODE -ne 0) { exit 4 }

# ── 6. Portable zip (no installer; for sysadmins who block Setup.exe)
Write-Host "▶ Building portable zip..." -ForegroundColor Yellow
$zipPath = Join-Path $ReleasesDir $PortableZip
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

# ── 7. Summary ────────────────────────────────────────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host (" Build complete. Artefacts in {0}:" -f $ReleasesDir) -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
Get-ChildItem $ReleasesDir | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    "{0,-60} {1,12:N0} bytes   sha256={2}" -f $_.Name, $_.Length, $hash
} | Write-Host

Write-Host ""
Write-Host "Next step: drag the files into a GitHub Release (draft a release first," -ForegroundColor Cyan
Write-Host "then 'Attach binaries'). The .github/workflows/release.yml workflow" -ForegroundColor Cyan
Write-Host "does the same steps automatically when you push a 'v*' tag." -ForegroundColor Cyan
