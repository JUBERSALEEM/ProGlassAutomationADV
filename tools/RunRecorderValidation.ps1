# ═══════════════════════════════════════════════════════════════════════
# ProGlass screen recorder — VALIDATION DRIVER (phases 1 & 2)
#
# WHAT THIS IS
#   Runs, on a real Windows machine, the four things the validation phase
#   demands be kept SEPARATE:
#
#       1. BUILD              — compile the actual Windows project
#       2. UNIT TESTS         — pure logic, no display / no ffmpeg
#       3. INTEGRATION TESTS  — need real ffmpeg on PATH or in the app tools
#       4. REAL RECORDING     — needs a human, a window and a clock
#
#   Each stage prints its own verdict from the REAL exit code and the REAL
#   tool output. A stage that could not run says SKIPPED with the reason. It
#   never prints PASS for something that did not execute, and it never
#   describes an expected value as an observed one.
#
# USAGE
#   powershell -ExecutionPolicy Bypass -File RunRecorderValidation.ps1
#   powershell -ExecutionPolicy Bypass -File RunRecorderValidation.ps1 -Stage build
#   powershell -ExecutionPolicy Bypass -File RunRecorderValidation.ps1 -Stage unit
#   powershell -ExecutionPolicy Bypass -File RunRecorderValidation.ps1 -Stage benchmark
#   powershell -ExecutionPolicy Bypass -File RunRecorderValidation.ps1 -Stage report
#
#   -Stage all (default) runs build, unit, integration, benchmark, report.
#   The REAL RECORDING stage is always printed as a procedure — no script can
#   honestly automate a 6-minute 4K capture of a live WPF window.
#
# AFTERWARDS
#   Paste the full console output into the report. Do not summarise it by hand.
# ═══════════════════════════════════════════════════════════════════════
param(
    [ValidateSet("all", "build", "unit", "integration", "benchmark", "report")]
    [string]$Stage = "all",

    [string]$Configuration = "Release",

    # Pure-logic recorder tests: no display, no ffmpeg, no hardware.
    [string]$UnitFilter =
        "FullyQualifiedName~CrispDemoPolicyTests" +
        "|FullyQualifiedName~VideoEncoderSelectionTests" +
        "|FullyQualifiedName~RecordingThrottleTests" +
        "|FullyQualifiedName~RecordingFidelityTests" +
        "|FullyQualifiedName~RecordingRetimeTests" +
        "|FullyQualifiedName~ScreenRecordQualityTierTests" +
        "|FullyQualifiedName~RecordingTimeTests",

    # Tests that genuinely need a real ffmpeg binary and real files.
    [string]$IntegrationFilter =
        "FullyQualifiedName~Mp4ValidationTests|FullyQualifiedName~LiveEncodeTests",

    [switch]$SkipBenchmark
)

$ErrorActionPreference = "Continue"
$repo = Split-Path -Parent $PSScriptRoot      # tools/.. == repo root
$appProj  = Join-Path $repo "ProGlassAutomation.csproj"
$testProj = Join-Path $repo "Tests\ProGlassAutomation.Tests.csproj"
$toolsDir = $PSScriptRoot

$verdicts = [ordered]@{}

function Write-StageHeader($n, $title) {
    Write-Host ""
    Write-Host ("=" * 74) -ForegroundColor DarkCyan
    Write-Host ("  STAGE {0} — {1}" -f $n, $title) -ForegroundColor Cyan
    Write-Host ("=" * 74) -ForegroundColor DarkCyan
}

function Find-Tool($name) {
    $t = Join-Path $env:APPDATA "ProGlassAutomation\tools"
    $c = Get-Command $name -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $d = Join-Path $t "$name.exe"
    if (Test-Path $d) { return $d }
    return $null
}

function Invoke-Logged($exe, $argList, $what) {
    # Runs a tool, streams its output, and returns the REAL exit code.
    Write-Host ("  $ {0} {1}" -f $exe, ($argList -join " ")) -ForegroundColor DarkGray
    $out = & $exe @argList 2>&1
    $code = $LASTEXITCODE
    $out | ForEach-Object { Write-Host ("    " + $_) }
    Write-Host ("  -> exit code {0}" -f $code) -ForegroundColor $(if ($code -eq 0) { "Green" } else { "Red" })
    return @{ ExitCode = $code; Output = ($out | Out-String) }
}

# ══════════════════════════════════════════════════════════════════════
#  STAGE 1 — BUILD
# ══════════════════════════════════════════════════════════════════════
if ($Stage -eq "all" -or $Stage -eq "build") {
    Write-StageHeader 1 "BUILD (actual Windows project)"

    $dotnet = Find-Tool "dotnet"
    if (-not $dotnet) {
        $verdicts["BUILD"] = "NOT RUN — dotnet SDK not found on this machine"
        Write-Host "  dotnet not found. Install the .NET 8 SDK, then re-run." -ForegroundColor Red
    } elseif (-not (Test-Path $appProj)) {
        $verdicts["BUILD"] = "NOT RUN — $appProj missing"
        Write-Host ("  Project not found: {0}" -f $appProj) -ForegroundColor Red
    } else {
        Write-Host ("  Repo: {0}" -f $repo)
        Write-Host ("  Configuration: {0}" -f $Configuration)

        $r = Invoke-Logged $dotnet @("build", $appProj, "-c", $Configuration, "-v", "minimal") "build app"
        if ($r.ExitCode -ne 0) {
            $verdicts["BUILD"] = "FAILED — exit $($r.ExitCode); errors are printed verbatim above"
            Write-Host ""
            Write-Host "  Build FAILED. Report the exact error list above." -ForegroundColor Red
            Write-Host "  Fix ONLY errors caused by the recorder changes; do not silence them." -ForegroundColor Yellow
        } else {
            # Build the test project too — it is a separate assembly.
            $r2 = Invoke-Logged $dotnet @("build", $testProj, "-c", $Configuration, "-v", "minimal") "build tests"
            if ($r2.ExitCode -eq 0) {
                $verdicts["BUILD"] = "SUCCEEDED — app and test assemblies compiled (exit 0)"
            } else {
                $verdicts["BUILD"] = "PARTIAL — app compiled, TEST project failed (exit $($r2.ExitCode))"
            }
        }
    }
}

# ══════════════════════════════════════════════════════════════════════
#  STAGE 2 — UNIT TESTS (pure logic)
# ══════════════════════════════════════════════════════════════════════
if ($Stage -eq "all" -or $Stage -eq "unit") {
    Write-StageHeader 2 "UNIT TESTS (pure logic — no display, no ffmpeg)"

    $dotnet = Find-Tool "dotnet"
    if (-not $dotnet) {
        $verdicts["UNIT TESTS"] = "NOT RUN — dotnet SDK not found"
    } elseif (-not (Test-Path $testProj)) {
        $verdicts["UNIT TESTS"] = "NOT RUN — test project missing"
    } else {
        Write-Host "  These tests assert on PURE functions: preset mapping, GOP,"
        Write-Host "  B-frame suppression, codec selection, chroma compatibility,"
        Write-Host "  resolution provenance, stage attribution, CRF matrix."
        Write-Host "  They prove the POLICY is self-consistent. They do NOT prove"
        Write-Host "  that any recording looked sharp or held its frame rate."
        Write-Host ""
        $r = Invoke-Logged $dotnet @(
            "test", $testProj, "-c", $Configuration,
            "--filter", $UnitFilter, "-v", "normal") "unit tests"

        $summary = ""
        foreach ($pat in @('Passed!\s*-\s*Failed:\s*\d+,\s*Passed:\s*\d+[^,]*,\s*Skipped:\s*\d+',
                           'Failed:\s*\d+,\s*Passed:\s*\d+',
                           'total:\s*\d+')) {
            $m = [regex]::Match($r.Output, $pat)
            if ($m.Success) { $summary = $m.Value; break }
        }

        if ($r.ExitCode -eq 0) {
            $verdicts["UNIT TESTS"] = "PASSED — exit 0$(if ($summary) { " ($summary)" })"
        } else {
            $verdicts["UNIT TESTS"] = "FAILED — exit $($r.ExitCode)$(if ($summary) { " ($summary)" })"
        }
    }
}

# ══════════════════════════════════════════════════════════════════════
#  STAGE 3 — INTEGRATION TESTS (need real ffmpeg)
# ══════════════════════════════════════════════════════════════════════
if ($Stage -eq "all" -or $Stage -eq "integration") {
    Write-StageHeader 3 "INTEGRATION TESTS (need a real ffmpeg binary)"

    $ff = Find-Tool "ffmpeg"
    $fp = Find-Tool "ffprobe"
    $dotnet = Find-Tool "dotnet"
    Write-Host ("  ffmpeg : {0}" -f $(if ($ff) { $ff } else { "NOT FOUND" }))
    Write-Host ("  ffprobe: {0}" -f $(if ($fp) { $fp } else { "NOT FOUND" }))

    if (-not $ff -or -not $fp) {
        $verdicts["INTEGRATION TESTS"] = "SKIPPED — ffmpeg/ffprobe not present; these tests would fail for environmental reasons, not product reasons"
        Write-Host "  Skipping rather than reporting a false failure. Download ffmpeg via" -ForegroundColor Yellow
        Write-Host "  Settings -> Demo Center, then re-run with -Stage integration."
    } elseif (-not $dotnet) {
        $verdicts["INTEGRATION TESTS"] = "NOT RUN — dotnet SDK not found"
    } else {
        $r = Invoke-Logged $dotnet @(
            "test", $testProj, "-c", $Configuration,
            "--filter", $IntegrationFilter, "-v", "normal") "integration tests"
        $verdicts["INTEGRATION TESTS"] = $(if ($r.ExitCode -eq 0) { "PASSED — exit 0" }
                                            else { "FAILED — exit $($r.ExitCode)" })
    }
}

# ══════════════════════════════════════════════════════════════════════
#  STAGE 4 — REAL RECORDING TESTS (human-in-the-loop, cannot be automated)
# ══════════════════════════════════════════════════════════════════════
Write-StageHeader 4 "REAL RECORDING TESTS (manual — a script cannot do this honestly)"
$verdicts["REAL RECORDING"] = "MANUAL — see the procedure printed below; collect with -Stage report"
Write-Host @"
  Run the app, then record EACH of these. Minimum 60 seconds for A-E.

    A  1920x1080  30 fps  Standard
    B  1920x1080  60 fps  High
    C  2560x1440  60 fps  High
    D  3840x2160  30 fps  Crisp Demo (SoftwareDemo)
    E  3840x2160  60 fps  Crisp Demo (SoftwareDemo)
    F  3840x2160  30 fps  Crisp Demo   ~6 minutes
    G  3840x2160  60 fps  Crisp Demo   ~6 minutes

  For every run, use the SAME controlled ProGlass scene (small text, PI and
  JO numbers, prices and decimals, tables, thin borders, icons, buttons,
  glass cards, gradients, shadows, blur, scrolling, cursor movement, UI
  transitions). Same scene every time, or the comparison means nothing.

  Do not change any setting between runs except the one under test.
  Do not stop a run early because it looks slow — a slow run is a result.

  Then:   powershell -ExecutionPolicy Bypass -File RecordingMetricsReport.ps1 -AllLogs -CsvOut metrics.csv

  That parses the recorder's own [RecCompleted] / [RecResolution] /
  [RecStageTimings] lines into the metrics table. It reports "not logged"
  for anything the run did not produce; it never fills a gap with a guess.
"@

# ══════════════════════════════════════════════════════════════════════
#  STAGE 5 — BENCHMARK (phase 2 of the validation brief)
# ══════════════════════════════════════════════════════════════════════
if (($Stage -eq "all" -or $Stage -eq "benchmark") -and -not $SkipBenchmark) {
    Write-StageHeader 5 "PRESET / CODEC / GOP BENCHMARK"

    $bench = Join-Path $toolsDir "RecordingPresetBenchmark.ps1"
    $ff = Find-Tool "ffmpeg"
    if (-not (Test-Path $bench)) {
        $verdicts["BENCHMARK"] = "NOT RUN — RecordingPresetBenchmark.ps1 missing"
    } elseif (-not $ff) {
        $verdicts["BENCHMARK"] = "SKIPPED — ffmpeg not found"
        Write-Host "  ffmpeg not found; the benchmark cannot run." -ForegroundColor Yellow
    } else {
        Write-Host "  Running the item-17 feasibility matrix. This measures sustained"
        Write-Host "  encode fps per preset per resolution. Do NOT edit the benchmark"
        Write-Host "  to obtain better numbers — if a cell STARVES, that is the result."
        Write-Host ""
        & powershell -ExecutionPolicy Bypass -File $bench -Matrix
        $code = $LASTEXITCODE
        $verdicts["BENCHMARK"] = $(if ($code -eq 0) { "COMPLETED — see benchmark_results.csv" }
                                   else { "FAILED — exit $code" })
    }
}

# ══════════════════════════════════════════════════════════════════════
#  SUMMARY — the only place a verdict is stated
# ══════════════════════════════════════════════════════════════════════
if ($Stage -eq "all" -or $Stage -eq "report") {
    Write-StageHeader 6 "SEPARATED VERDICTS"
    foreach ($k in $verdicts.Keys) {
        $v = $verdicts[$k]
        $c = if ($v -match '^(SUCCEEDED|PASSED|COMPLETED)') { "Green" }
             elseif ($v -match '^(FAILED|PARTIAL)') { "Red" }
             else { "Yellow" }
        Write-Host ("  {0,-18} {1}" -f $k, $v) -ForegroundColor $c
    }
    Write-Host ""
    Write-Host "  Nothing above is a simulation. A stage marked NOT RUN or SKIPPED did"
    Write-Host "  not execute, and its result is unknown — not assumed to be fine."
    Write-Host ""
    Write-Host "  Still outstanding after this script:" -ForegroundColor White
    Write-Host "    • the seven real recordings (stage 4)"
    Write-Host "    • the visual comparison at 100% / 200% / 400% crops"
    Write-Host "    • CPU and memory during a 4K/60 run (Task Manager or the"
    Write-Host "      [RecPerformance] log line, which RecordingMetricsReport parses)"
    Write-Host ""
}
