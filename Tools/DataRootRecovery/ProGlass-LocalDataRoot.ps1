#requires -Version 5.1
<#
.SYNOPSIS
    Copies or restores the local ProGlass data root with SHA-256 verification.
.DESCRIPTION
    Windows-only, local-profile helper for a quiescent %APPDATA%\ProGlassAutomation
    root. It refuses a shared-root configuration so a startup fallback cannot
    silently make the operator copy the wrong dataset. Restore stages and verifies
    the snapshot before renaming the current root aside and installing the staged
    copy. It never deletes the pre-restore root.

    This script is not a substitute for the app-level Windows restore test. Run it
    only after confirming Settings -> Data shows local mode and all app instances
    are closed. See docs/BACKUP.md for the complete procedure and limitations.

.EXAMPLE
    .\Tools\DataRootRecovery\ProGlass-LocalDataRoot.ps1 -Action Backup `
        -RootPath "$env:APPDATA\ProGlassAutomation" `
        -SnapshotPath "$env:USERPROFILE\Desktop\PG_Backup_Phase0_202610"

.EXAMPLE
    .\Tools\DataRootRecovery\ProGlass-LocalDataRoot.ps1 -Action Restore `
        -RootPath "$env:APPDATA\ProGlassAutomation" `
        -SnapshotPath "$env:USERPROFILE\Desktop\PG_Backup_Phase0_202610"
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Backup', 'Restore')]
    [string] $Action,

    [Parameter(Mandatory = $true)]
    [string] $RootPath,

    [Parameter(Mandatory = $true)]
    [string] $SnapshotPath
)

$ErrorActionPreference = 'Stop'

function Get-NormalizedPath {
    param([Parameter(Mandatory = $true)][string] $Path)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $pathRoot = [System.IO.Path]::GetPathRoot($fullPath)
    if ($fullPath.Equals($pathRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath
    }
    return $fullPath.TrimEnd([char[]]@(92, 47))
}

function Test-PathWithin {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Parent
    )
    $normalizedPath = Get-NormalizedPath $Path
    $normalizedParent = Get-NormalizedPath $Parent
    if ($normalizedPath.Equals($normalizedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }
    $separator = [System.IO.Path]::DirectorySeparatorChar
    $prefix = if ($normalizedParent.EndsWith([string]$separator)) {
        $normalizedParent
    }
    else {
        $normalizedParent + $separator
    }
    return $normalizedPath.StartsWith(
        $prefix,
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-NonOverlappingPaths {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $Other
    )
    if ((Test-PathWithin -Path $Other -Parent $Root) -or
        (Test-PathWithin -Path $Root -Parent $Other)) {
        throw "Snapshot path and live data root must be separate, non-nested folders. Root='$Root'; snapshot='$Other'."
    }
}

function Test-ConfiguredSharedRoot {
    param([Parameter(Mandatory = $true)][string] $Folder)
    $configPath = Join-Path $Folder 'config.json'
    if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
        return $false
    }

    try {
        $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Cannot safely read $configPath to verify local/shared mode: $($_.Exception.Message)"
    }

    if ($null -eq $config -or $null -eq $config.DataConnectivity) {
        return $false
    }
    return [bool]$config.DataConnectivity.UseSharedDataRoot
}

function Assert-LocalProfileRoot {
    param([Parameter(Mandatory = $true)][string] $Folder)

    if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
        throw 'This helper is Windows-only.'
    }
    if ([string]::IsNullOrWhiteSpace($env:APPDATA)) {
        throw 'APPDATA is not set for this Windows profile.'
    }

    $expected = Get-NormalizedPath (Join-Path $env:APPDATA 'ProGlassAutomation')
    $actual = Get-NormalizedPath $Folder
    if (-not $actual.Equals($expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "This helper only operates on the local profile root '$expected'. The supplied root was '$actual'. Confirm the active root in Settings -> Data; shared/custom roots are intentionally unsupported by this helper."
    }

    if (Test-ConfiguredSharedRoot -Folder $actual) {
        throw "Shared data is enabled in '$actual\config.json'. This helper refuses to guess whether ProGlass used the share or fell back to local data. Configure an isolated local-only Windows profile and confirm Settings -> Data before retrying."
    }

    $driveRoot = [System.IO.Path]::GetPathRoot($actual)
    $drive = [System.IO.DriveInfo]::new($driveRoot)
    if ($drive.DriveType -eq [System.IO.DriveType]::Network) {
        throw "The data root is on a network drive ('$driveRoot'); this helper is local-only."
    }
}

function Assert-ProGlassClosed {
    $running = @(Get-Process -Name 'ProGlassAutomation' -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) {
        throw 'ProGlassAutomation is still running in this Windows session. Exit the app and retry.'
    }
}

function Assert-DatabaseRoot {
    param([Parameter(Mandatory = $true)][string] $Folder)
    if (-not (Test-Path -LiteralPath $Folder -PathType Container)) {
        throw "Data-root folder does not exist: $Folder"
    }
    $databases = @(Get-ChildItem -LiteralPath $Folder -Filter '*.db' -File -Force -ErrorAction Stop)
    if ($databases.Count -eq 0) {
        throw "No top-level SQLite .db files found in '$Folder'; refusing to treat it as a ProGlass data snapshot."
    }
}

function Get-FileInventory {
    param([Parameter(Mandatory = $true)][string] $Folder)

    $base = (Get-NormalizedPath $Folder) + [System.IO.Path]::DirectorySeparatorChar
    $inventory = @{}
    $files = @(Get-ChildItem -LiteralPath $Folder -File -Force -Recurse -ErrorAction Stop)
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($base.Length)
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256 -ErrorAction Stop).Hash
        $inventory[$relative] = [string]$file.Length + ':' + $hash
    }
    Write-Output -NoEnumerate $inventory
}

function Get-DirectoryInventory {
    param([Parameter(Mandatory = $true)][string] $Folder)

    $base = (Get-NormalizedPath $Folder) + [System.IO.Path]::DirectorySeparatorChar
    $inventory = @{}
    $directories = @(Get-ChildItem -LiteralPath $Folder -Directory -Force -Recurse -ErrorAction Stop)
    foreach ($directory in $directories) {
        $relative = $directory.FullName.Substring($base.Length)
        $inventory[$relative] = $true
    }
    Write-Output -NoEnumerate $inventory
}

function Assert-TreesIdentical {
    param(
        [Parameter(Mandatory = $true)][string] $Expected,
        [Parameter(Mandatory = $true)][string] $Actual
    )

    $expectedFiles = Get-FileInventory -Folder $Expected
    $actualFiles = Get-FileInventory -Folder $Actual
    $expectedDirectories = Get-DirectoryInventory -Folder $Expected
    $actualDirectories = Get-DirectoryInventory -Folder $Actual
    $differences = [System.Collections.Generic.List[string]]::new()

    foreach ($relative in $expectedFiles.Keys) {
        if (-not $actualFiles.ContainsKey($relative)) {
            $differences.Add("missing file: $relative")
        }
        elseif ($expectedFiles[$relative] -cne $actualFiles[$relative]) {
            $differences.Add("hash/size mismatch: $relative")
        }
    }
    foreach ($relative in $actualFiles.Keys) {
        if (-not $expectedFiles.ContainsKey($relative)) {
            $differences.Add("unexpected file: $relative")
        }
    }
    foreach ($relative in $expectedDirectories.Keys) {
        if (-not $actualDirectories.ContainsKey($relative)) {
            $differences.Add("missing directory: $relative")
        }
    }
    foreach ($relative in $actualDirectories.Keys) {
        if (-not $expectedDirectories.ContainsKey($relative)) {
            $differences.Add("unexpected directory: $relative")
        }
    }

    if ($differences.Count -gt 0) {
        $sample = ($differences | Select-Object -First 10) -join '; '
        throw "File/folder inventory mismatch between '$Expected' and '$Actual': $sample"
    }
    return $expectedFiles.Count
}

function Copy-TreeWithRobocopy {
    param(
        [Parameter(Mandatory = $true)][string] $Source,
        [Parameter(Mandatory = $true)][string] $Destination
    )

    $robocopy = Join-Path $env:SystemRoot 'System32\robocopy.exe'
    if (-not (Test-Path -LiteralPath $robocopy -PathType Leaf)) {
        throw "Windows robocopy.exe was not found at '$robocopy'."
    }

    & $robocopy $Source $Destination /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /XJ /NP
    $exitCode = $LASTEXITCODE
    if ($exitCode -ge 8) {
        throw "Robocopy failed with exit code $exitCode while copying '$Source' to '$Destination'."
    }
}

$root = Get-NormalizedPath $RootPath
$snapshot = Get-NormalizedPath $SnapshotPath
Assert-LocalProfileRoot -Folder $root
Assert-ProGlassClosed
Assert-NonOverlappingPaths -Root $root -Other $snapshot

if ($Action -eq 'Backup') {
    Assert-DatabaseRoot -Folder $root
    if (Test-Path -LiteralPath $snapshot) {
        throw "Backup destination already exists; choose a new dated folder: $snapshot"
    }
    $snapshotParent = Split-Path -Parent $snapshot
    if (-not (Test-Path -LiteralPath $snapshotParent -PathType Container)) {
        throw "Backup destination parent does not exist: $snapshotParent"
    }

    if (-not $PSCmdlet.ShouldProcess($snapshot, "Copy and SHA-256 verify the complete local ProGlass root '$root'")) {
        return
    }

    try {
        Copy-TreeWithRobocopy -Source $root -Destination $snapshot
        $count = Assert-TreesIdentical -Expected $root -Actual $snapshot
        Write-Host "Backup verified: $count file(s)." -ForegroundColor Green
        Write-Host "Data root : $root"
        Write-Host "Snapshot : $snapshot"
    }
    catch {
        if (Test-Path -LiteralPath $snapshot -PathType Container) {
            Remove-Item -LiteralPath $snapshot -Recurse -Force -ErrorAction SilentlyContinue
        }
        throw
    }
    return
}

# Restore path: validate the snapshot before creating a staging directory.
Assert-DatabaseRoot -Folder $snapshot
if (Test-ConfiguredSharedRoot -Folder $snapshot) {
    throw "The snapshot's config.json enables shared-root mode. Restoring it into a local profile could redirect ProGlass away from this restored root; use a local-only snapshot/profile."
}

$parent = Split-Path -Parent $root
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    throw "The local profile parent folder does not exist: $parent"
}

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$stage = Join-Path $parent ('.ProGlassAutomation_restore_' + $suffix)
$preRestore = Join-Path $parent ('ProGlassAutomation_pre_restore_' + $stamp + '_' + $suffix)
$failedRestore = Join-Path $parent ('ProGlassAutomation_failed_restore_' + $stamp + '_' + $suffix)

if (-not $PSCmdlet.ShouldProcess($root, "Stage and verify '$snapshot', then replace the local root; preserve the old root at '$preRestore'")) {
    return
}

$oldRootMoved = $false
$newRootInstalled = $false
try {
    Copy-TreeWithRobocopy -Source $snapshot -Destination $stage
    $count = Assert-TreesIdentical -Expected $snapshot -Actual $stage

    if (Test-Path -LiteralPath $root -PathType Container) {
        Move-Item -LiteralPath $root -Destination $preRestore -ErrorAction Stop
        $oldRootMoved = $true
    }

    Move-Item -LiteralPath $stage -Destination $root -ErrorAction Stop
    $newRootInstalled = $true

    # Verify the installed copy before reporting success. If this fails, the
    # catch path moves the failed tree aside and restores the previous root.
    $installedCount = Assert-TreesIdentical -Expected $snapshot -Actual $root
    if ($installedCount -ne $count) {
        throw "Installed file count changed from $count to $installedCount."
    }

    Write-Host "Restore verified: $installedCount file(s)." -ForegroundColor Green
    Write-Host "Data root       : $root"
    if ($oldRootMoved) {
        Write-Host "Pre-restore copy: $preRestore"
    }
    Write-Host 'Start ProGlass and verify the intended records before using the data.' -ForegroundColor Yellow
}
catch {
    if ($newRootInstalled -and (Test-Path -LiteralPath $root -PathType Container)) {
        try { Move-Item -LiteralPath $root -Destination $failedRestore -ErrorAction Stop }
        catch { Write-Warning "Could not move the failed restored root aside: $($_.Exception.Message)" }
    }
    if ($oldRootMoved -and (Test-Path -LiteralPath $preRestore -PathType Container) -and
        -not (Test-Path -LiteralPath $root)) {
        try { Move-Item -LiteralPath $preRestore -Destination $root -ErrorAction Stop }
        catch { Write-Warning "Could not roll the pre-restore root back into place: $($_.Exception.Message)" }
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $stage -PathType Container) {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}
