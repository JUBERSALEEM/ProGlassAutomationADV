# =======================================================================
# FILE: tools/build-installer.ps1
# PROJECT: ProGlass Automation
# MODULE: Installer pipeline - local build (Windows developer box)
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
#   4.  Paint the Prism splash and MSI bitmaps, then Velopack-pack
#       the publish\ tree into a release artefact set:
#         * Setup.exe                        (full installer)
#         * ProGlassAutomation-2.0.0-win-x64.nupkg   (delta source)
#         * ProGlassAutomation-2.0.0-full.nupkg     (full payload)
#         * ProGlassAutomation-2.0.0-delta.nupkg    (delta against the
#                                                     previous release, if any)
#         * ProGlassAutomation-Portable-2.0.0.zip    (no-installer zip)
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
#  script is compatible with both - the only difference is the
#  ExecutionPolicy flag, which is a Windows-PowerShell-5.1-only quirk.)
#
# Exit codes:
#   0  success
#   1  dotnet / vpk missing
#   2  build or test gate failed
#   3  publish step failed
#   4  vpk pack step failed
# =======================================================================

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

# -- Paths (resolved relative to the repo root) ---------------------
$RepoRoot     = Resolve-Path (Join-Path $PSScriptRoot "..")
$PublishDir   = Join-Path $RepoRoot "publish"
$ReleasesDir  = Join-Path $RepoRoot "releases"
# The Velopack manifest. Kept on disk for documentation; the
# build script does not pass --config to vpk because the
# installed vpk version (1.x via `dotnet tool install -g vpk`)
# does not accept that flag. See the vpk pack invocation below
# for the equivalent explicit-flag invocation.
$VpkManifest  = Join-Path $RepoRoot "installer/velopack.json"
$PortableZip  = "ProGlassAutomation-Portable-$Version.zip"

Write-Host ""
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host " ProGlass Automation - installer build" -ForegroundColor Cyan
Write-Host "   Version   : $Version" -ForegroundColor Cyan
Write-Host "   PackId    : $PackId" -ForegroundColor Cyan
Write-Host "   Repo root : $RepoRoot" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""

# -- 0. Pre-flight: toolchain present? ------------------------------
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

# -- 1. Clean -------------------------------------------------------
if ($Clean) {
    if (Test-Path $PublishDir)  { Remove-Item -Recurse -Force $PublishDir }
    if (Test-Path $ReleasesDir) { Remove-Item -Recurse -Force $ReleasesDir }
}

# -- 2. Build (Release) + restore ----------------------------------
# We build the test project (not the app project directly) so the
# test DLL is produced as part of the same step. The test project
# has a ProjectReference on the app project, so MSBuild compiles
# the app first, then the test project. This avoids the bug where
# the previous 'dotnet build app.csproj' skipped the test project
# (because the app's csproj has <Compile Remove="Tests/**" />) and
# 'dotnet test --no-build' then failed with "test source file not
# found".
Write-Host "> Restoring + building Release configuration..." -ForegroundColor Yellow
& dotnet restore "$RepoRoot/Tests/ProGlassAutomation.Tests.csproj" --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit 2 }

& dotnet build "$RepoRoot/Tests/ProGlassAutomation.Tests.csproj" -c Release --no-restore --verbosity minimal -p:Version=$Version
if ($LASTEXITCODE -ne 0) { exit 2 }

# -- 3. Test gate --------------------------------------------------
if (-not $SkipTests) {
    Write-Host "> Running xUnit test gate..." -ForegroundColor Yellow
    # --no-build is correct here: the test DLL was just produced
    # by the previous step. The previous version of this script
    # called `dotnet test` after `dotnet build app.csproj`, which
    # skipped the test project entirely and produced "test source
    # file not found".
    & dotnet test "$RepoRoot/Tests/ProGlassAutomation.Tests.csproj" -c Release --no-build --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Test gate failed. Refusing to package an installer that ships a red test suite."
        exit 2
    }
} else {
    Write-Warning "Skipping the test gate (-SkipTests). Do NOT publish this build to customers."
}

# -- 4. Publish (self-contained win-x64) ---------------------------
# Uses the Release.pubxml profile so the settings (SelfContained,
# win-x64, ReadyToRun, deterministic, no PDBs) live in one place
# and are shared with .github/workflows/release.yml.
Write-Host "> Publishing self-contained win-x64 (Release.pubxml)..." -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
& dotnet publish "$RepoRoot/ProGlassAutomation.csproj" -c Release -r win-x64 --self-contained true -p:PublishProfile=Release -o $PublishDir -p:Version=$Version
if ($LASTEXITCODE -ne 0) { exit 3 }

# -- 5. Velopack pack ----------------------------------------------
Write-Host "> Running vpk pack..." -ForegroundColor Yellow
if (Test-Path $ReleasesDir) { Remove-Item -Recurse -Force $ReleasesDir }

# Prism splash + MSI bitmaps. Generated, not committed.
$artScript = Join-Path $PSScriptRoot "generate-installer-art.ps1"
& $artScript -RepoRoot $RepoRoot
if (-not $?) { exit 4 }
$artDir = Join-Path $RepoRoot "installer\generated"
$contentDir = Join-Path $RepoRoot "installer\content"

# The installer/velopack.json manifest is the durable source
# of pack metadata (id, title, authors, icon). The
# Velopack CLI 1.x we install on the build machine does not
# accept --config, so we pass the manifest's values as
# explicit command-line flags. Version is overridden on the
# command line so the same manifest can pack any release.
#
# Newer vpk builds also accept splash and MSI page flags.
# Probe --help so an older CLI still packs Setup.exe.
$vpkHelp = & vpk pack --help 2>&1 | Out-String
$vpkArgs = @(
    "pack",
    "--packId", $PackId,
    "--packVersion", $Version,
    "--packDir", $PublishDir,
    "--outputDir", $ReleasesDir,
    "--packTitle", $PackTitle,
    "--packAuthors", $PackAuthors,
    "--icon", (Join-Path $RepoRoot "favicon.ico"),
    "--channel", "stable",
    "--mainExe", "ProGlassAutomation.exe"
)

function Add-VpkFlag([string]$name, [string[]]$values) {
    # Whole flag only. "--msi" must not match "--msiBanner".
    if ($vpkHelp -match ("(?i)--" + [regex]::Escape($name) + "(?![\w])")) {
        $script:vpkArgs += "--$name"
        if ($values -and $values.Count -gt 0) { $script:vpkArgs += $values }
        Write-Host "  vpk --$name" -ForegroundColor DarkCyan
    } else {
        Write-Warning "This vpk does not support --$name. Update vpk to ship that installer page."
    }
}

Add-VpkFlag "splashImage" @((Join-Path $artDir "splash.png"))
# None keeps the rounded splash corners. A live bar is a square
# strip painted over the bottom 12px, which would cut the card.
Add-VpkFlag "splashProgressColor" @("None")
Add-VpkFlag "msi" @()
Add-VpkFlag "instLocation" @("Either")
Add-VpkFlag "instWelcome" @((Join-Path $contentDir "welcome.txt"))
Add-VpkFlag "instReadme" @((Join-Path $contentDir "readme.md"))
Add-VpkFlag "instLicense" @((Join-Path $contentDir "license.md"))
Add-VpkFlag "instConclusion" @((Join-Path $contentDir "conclusion.txt"))
if ($vpkHelp -match "msiTopBanner") {
    Add-VpkFlag "msiTopBanner" @((Join-Path $artDir "msi-banner.bmp"))
    Add-VpkFlag "msiDialogBackground" @((Join-Path $artDir "msi-dialog.bmp"))
} else {
    Add-VpkFlag "msiBanner" @((Join-Path $artDir "msi-banner.bmp"))
    Add-VpkFlag "msiLogo" @((Join-Path $artDir "msi-dialog.bmp"))
}

& vpk @vpkArgs
if ($LASTEXITCODE -ne 0) { exit 4 }

# -- 6. Portable zip (no installer; for sysadmins who block Setup.exe)
Write-Host "> Building portable zip..." -ForegroundColor Yellow
$zipPath = Join-Path $ReleasesDir $PortableZip
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

# -- 7. Summary ----------------------------------------------------
Write-Host ""
Write-Host "=======================================================" -ForegroundColor Green
Write-Host (" Build complete. Artefacts in {0}:" -f $ReleasesDir) -ForegroundColor Green
Write-Host "=======================================================" -ForegroundColor Green
Get-ChildItem $ReleasesDir | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    "{0,-60} {1,12:N0} bytes   sha256={2}" -f $_.Name, $_.Length, $hash
} | Write-Host

Write-Host ""
Write-Host "Next step: drag the files into a GitHub Release (draft a release first," -ForegroundColor Cyan
Write-Host "then 'Attach binaries'). The .github/workflows/release.yml workflow" -ForegroundColor Cyan
Write-Host "does the same steps automatically when you push a 'v*' tag." -ForegroundColor Cyan
