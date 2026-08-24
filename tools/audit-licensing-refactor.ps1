# =======================================================================
# FILE: tools/audit-licensing-refactor.ps1
# PROJECT: ProGlass Automation
# MODULE: Structural audit of the licensing refactor (Commits 1-6)
#
# PURPOSE
# ───────
# Run this script AFTER a successful Release build to confirm the
# licensing refactor (Commits 73b7d02 ... 1ef9bc9) is internally
# consistent. It does NOT run the .NET test suite (use the build
# script for that) and does NOT package the installer. It walks the
# source tree and checks:
#
#   1. Every modified .cs file has balanced braces / parens.
#   2. Every modified .xaml is well-formed XML.
#   3. Every new public surface (TrialSource, TrialExpiresAt,
#      TrialExpired, LicenseStatus.TrialExpired, TrialPublicKeyXml,
#      EnforceTrialExpiry, IssueAutoTrialIfEligible, etc.) is
#      referenced at the expected call site.
#   4. The [CollectionDefinition("LicensePublicKey", ...)] in
#      Tests/Licensing/LicensePublicKeyCollection.cs is paired
#      with [Collection("LicensePublicKey")] on all 5 test
#      classes.
#   5. The pre-login WelcomeView is gone (only documentation
#      comments may mention it).
#   6. The audit/admin seed in AuditService.InitializeAsync is
#      removed.
#   7. The xUnit test count (active + skipped) matches the expected
#      end-of-refactor baseline (383 active + 166 skipped).
#
# Exit code: 0 if every check passes, 1 otherwise.
#
# USAGE
# ─────
#   pwsh -File tools\audit-licensing-refactor.ps1
#   pwsh -File tools\audit-licensing-refactor.ps1 -RepoRoot C:\path
#
# Runs on PowerShell 5.1 (default on Windows) AND PowerShell 7+.
# Pure file-system / regex work — no dotnet, vpk, or other
# toolchain dependencies.
# =======================================================================

[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$failed = $false

function Check-True {
    param([string]$Label, [bool]$Condition)
    $script:failed = $script:failed -or (-not $Condition)
    $status = if ($Condition) { "[OK]  " } else { "[FAIL]" }
    Write-Host "$status $Label"
}

function Check-Balanced {
    param([string]$Path)
    $text = Get-Content -Raw -Path $Path

    # Strip block comments, line comments, and string literals
    # (naive but adequate for our well-disciplined codebase).
    $noBlockComments = [regex]::Replace($text, '/\*.*?\*/', '',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $lines = $noBlockComments -split "`n"
    $noLineComments = ($lines | ForEach-Object {
        $idx = $_.IndexOf('//')
        if ($idx -ge 0) { $_.Substring(0, $idx) } else { $_ }
    }) -join "`n"
    $noStrings = [regex]::Replace(
        $noLineComments,
        '@\$?"(?:[^"\\]|\\.)*"',
        '""')
    $noStrings = [regex]::Replace(
        $noStrings,
        '\$?"(?:[^"\\]|\\.)*"',
        '""')

    $openBraces  = ([regex]::Matches($noStrings, '{')).Count
    $closeBraces = ([regex]::Matches($noStrings, '}')).Count
    $openParens  = ([regex]::Matches($noStrings, '\(')).Count
    $closeParens = ([regex]::Matches($noStrings, '\)')).Count

    $ok = ($openBraces -eq $closeBraces) -and ($openParens -eq $closeParens)
    Check-True "$Path braces/parens" $ok
    if (-not $ok) {
        Write-Host "       braces $openBraces/$closeBraces, parens $openParens/$closeParens"
    }
}

function Check-XmlWellFormed {
    param([string]$Path)
    try {
        [xml]$doc = Get-Content -Raw -Path $Path
        Check-True "$Path XML well-formed" $true
    } catch {
        Check-True "$Path XML well-formed" $false
        Write-Host "       $($_.Exception.Message)"
    }
}

function Check-Contains {
    param([string]$Path, [string]$Token, [int]$MinCount = 1)
    $text = Get-Content -Raw -Path $Path
    $n = ([regex]::Matches($text, [regex]::Escape($Token))).Count
    $ok = $n -ge $MinCount
    Check-True "$Path contains '$Token' (>= $MinCount, got $n)" $ok
}

Set-Location $RepoRoot

Write-Host "============================================================"
Write-Host " Licensing Refactor Audit (Commits 73b7d02 ... 1ef9bc9)"
Write-Host "============================================================"
Write-Host ""

# ── 1. C# balance for every modified .cs file ────────────────
Write-Host "--- 1. C# brace / paren balance ---"
$csFiles = @(
    "Services/Licensing/LicensePayload.cs",
    "Services/Licensing/LicenseVerifier.cs",
    "Views/Licensing/LicenseUiText.cs",
    "Services/Licensing/LicensePublicKey.cs",
    "Services/Licensing/LicenseService.cs",
    "Services/AI/AIConfig/AppConfig.cs",
    "Views/Licensing/LicenseActivationView.xaml.cs",
    "App.xaml.cs",
    "ViewModels/Licensing/LicenseStatusViewModel.cs",
    "Services/Audit/AuditService.cs",
    "Views/Login/FirstRunSetupPanel.xaml.cs",
    "Views/Splash/SplashView.xaml.cs",
    "Tests/Licensing/LicensePublicKeyCollection.cs",
    "Tests/Licensing/LicenseSignerTests.cs",
    "Tests/Licensing/LicenseGateTests.cs",
    "Tests/Licensing/LicenseServiceTests.cs",
    "Tests/Licensing/LicenseStatusViewModelTests.cs",
    "Tests/Logging/CrashReportServiceTests.cs"
)
foreach ($f in $csFiles) {
    if (Test-Path $f) { Check-Balanced $f }
    else { Check-True "$f exists" $false }
}
Write-Host ""

# ── 2. XAML well-formedness ──────────────────────────────────
Write-Host "--- 2. XAML well-formedness ---"
$xamlFiles = @(
    "Views/Licensing/LicenseActivationView.xaml",
    "MainWindow.xaml",
    "Views/Login/FirstRunSetupPanel.xaml",
    "Views/Splash/SplashView.xaml"
)
foreach ($f in $xamlFiles) {
    if (Test-Path $f) { Check-XmlWellFormed $f }
}
Write-Host ""

# ── 3. New public surfaces ──────────────────────────────────
Write-Host "--- 3. New public surfaces (Commit 1: payload) ---"
Check-Contains "Services/Licensing/LicensePayload.cs" "public string TrialSource"
Check-Contains "Services/Licensing/LicensePayload.cs" "public DateTime TrialExpiresAt"
Check-Contains "Services/Licensing/LicensePayload.cs" "TrialExpired = 9"

Write-Host ""
Write-Host "--- 3. New public surfaces (Commit 2: service) ---"
Check-Contains "Services/Licensing/LicensePublicKey.cs" "TrialPublicKeyXml"
Check-Contains "Services/Licensing/LicensePublicKey.cs" "TrialPrivateKeyXml"
Check-Contains "Services/Licensing/LicensePublicKey.cs" "IsTrialKeyPlaceholder"
Check-Contains "Services/Licensing/LicenseService.cs" "EnforceTrialExpiry"
Check-Contains "Services/Licensing/LicenseService.cs" "IssueAutoTrialIfEligible"
Check-Contains "Services/Licensing/LicenseService.cs" "AutoTrialDays = 7"
Check-Contains "Services/Licensing/LicenseService.cs" 'AutoTrialSourceMarker = "auto-7d"'
Check-Contains "Services/AI/AIConfig/AppConfig.cs" "AutoTrialIssuedAtUtc"
Check-Contains "Services/AI/AIConfig/AppConfig.cs" "AutoTrialWasIssued"

Write-Host ""
Write-Host "--- 3. New public surfaces (Commit 3: activation view) ---"
Check-Contains "Views/Licensing/LicenseActivationView.xaml.cs" "public event EventHandler? Activated"
Check-Contains "Views/Licensing/LicenseActivationView.xaml.cs" "public event EventHandler? Closed"

Write-Host ""
Write-Host "--- 3. New public surfaces (Commit 4: overlay) ---"
Check-Contains "MainWindow.xaml" 'x:Name="LicenseOverlay"'
Check-Contains "MainWindow.xaml" "x:Static vmLicensing:LicenseStatusViewModel.Instance"
Check-Contains "MainWindow.xaml" 'IsClosable="False"'
Check-Contains "ViewModels/Licensing/LicenseStatusViewModel.cs" "IsLicenseRequired"

Write-Host ""
Write-Host "--- 3. New public surfaces (Commit 5: first-run wizard) ---"
Check-Contains "Services/Audit/AuditService.cs" "HasAnyUsersAsync"
Check-Contains "Views/Login/FirstRunSetupPanel.xaml.cs" "public event Action<string>? Created"
Check-Contains "Views/Splash/SplashView.xaml.cs" "RunFirstRunSetupAsync"
Check-Contains "App.xaml.cs" "HasAnyUsersAsync"
Check-Contains "App.xaml.cs" "RunFirstRunSetupAsync"

Write-Host ""
Write-Host "--- 3. New public surfaces (Commit 6: xUnit collection) ---"
Check-Contains "Tests/Licensing/LicensePublicKeyCollection.cs" '[CollectionDefinition("LicensePublicKey"'
Check-Contains "Tests/Licensing/LicensePublicKeyCollection.cs" "DisableParallelization = true"

# ── 4. Collection pairing ────────────────────────────────────
Write-Host ""
Write-Host "--- 4. Test collection pairing (Commit 6) ---"
$pairedFiles = @(
    "Tests/Licensing/LicenseSignerTests.cs",
    "Tests/Licensing/LicenseGateTests.cs",
    "Tests/Licensing/LicenseServiceTests.cs",
    "Tests/Licensing/LicenseStatusViewModelTests.cs",
    "Tests/Logging/CrashReportServiceTests.cs"
)
$pairedCount = 0
foreach ($f in $pairedFiles) {
    if (Test-Path $f) {
        $text = Get-Content -Raw -Path $f
        if ($text -match '\[Collection\("LicensePublicKey"\)\]') {
            $pairedCount++
        }
    }
}
Check-True "5 test classes have [Collection(\"LicensePublicKey\")] (got $pairedCount)" ($pairedCount -eq 5)
Write-Host ""

# ── 5. WelcomeView removal ──────────────────────────────────
Write-Host "--- 5. WelcomeView fully removed (Commit 3) ---"
Check-True "Views/Licensing/WelcomeView.xaml deleted" (-not (Test-Path "Views/Licensing/WelcomeView.xaml"))
Check-True "Views/Licensing/WelcomeView.xaml.cs deleted" (-not (Test-Path "Views/Licensing/WelcomeView.xaml.cs"))
# No CODE references in any .cs / .xaml (comments are allowed)
$codeRefs = 0
Get-ChildItem -Recurse -Include "*.cs", "*.xaml" |
    Where-Object { -not $_.FullName.Contains("\bin\") -and -not $_.FullName.Contains("\obj\") } |
    ForEach-Object {
        $raw = Get-Content -Raw -Path $_.FullName
        # Strip XML comments from XAML
        $stripped = if ($_.Name.EndsWith(".xaml")) {
            [regex]::Replace($raw, '<!--.*?-->', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
        } else { $raw }
        # Strip /* */ block comments
        $stripped = [regex]::Replace($stripped, '/\*.*?\*/', '',
            [System.Text.RegularExpressions.RegexOptions]::Singleline)
        # Strip // line comments per-line
        $stripped = ($stripped -split "`n" | ForEach-Object {
            $idx = $_.IndexOf('//')
            if ($idx -ge 0) { $_.Substring(0, $idx) } else { $_ }
        }) -join "`n"
        if ($stripped -match 'WelcomeView' -or $stripped -match 'WelcomeResult') {
            $codeRefs++
        }
    }
Check-True "No CODE references to WelcomeView / WelcomeResult (comments allowed)" ($codeRefs -eq 0)
Write-Host ""

# ── 6. Admin seed removed ───────────────────────────────────
Write-Host "--- 6. admin/admin seed removed from AuditService (Commit 5) ---"
$audit = Get-Content -Raw -Path "Services/Audit/AuditService.cs"
Check-True "no 'BuildUser(username: DefaultAdminUsername' seed in InitializeAsync" (
    $audit -notmatch 'BuildUser\(username: DefaultAdminUsername')
Write-Host ""

# ── 7. Test count ───────────────────────────────────────────
Write-Host "--- 7. xUnit test count ---"
$totalActive = 0
$totalSkipped = 0
Get-ChildItem -Recurse -Path "Tests" -Include "*Tests.cs" |
    Where-Object { -not $_.FullName.Contains("\bin\") -and -not $_.FullName.Contains("\obj\") } |
    ForEach-Object {
        $text = Get-Content -Raw -Path $_.FullName
        $active = ([regex]::Matches($text, '\[(Fact|Theory)\]\s*$',
            [System.Text.RegularExpressions.RegexOptions]::Multiline)).Count
        $skipped = ([regex]::Matches($text, '\[(Fact|Theory)\(Skip = ')).Count
        $script:totalActive += $active
        $script:totalSkipped += $skipped
    }
Write-Host "  active:  $totalActive"
Write-Host "  skipped: $totalSkipped"
Write-Host "  expected after refactor: ~383 active + 166 skipped"
Check-True "test count roughly matches expected (active >= 380, skipped >= 160)" (
    ($totalActive -ge 380) -and ($totalSkipped -ge 160))
Write-Host ""

# ── Summary ────────────────────────────────────────────────
Write-Host "============================================================"
if ($failed) {
    Write-Host " AUDIT: FAIL"
    Write-Host " One or more checks did not pass. Re-read the output above."
    exit 1
} else {
    Write-Host " AUDIT: PASS"
    Write-Host " The licensing refactor is internally consistent."
    Write-Host " Run 'dotnet test' to confirm the actual test gate is green."
    exit 0
}
