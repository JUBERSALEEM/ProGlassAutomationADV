# ═══════════════════════════════════════════════════════════════════════
# ProGlass screen recorder — RECORDING METRICS REPORT (validation phase 4)
#
# WHAT THIS IS
#   Turns the recorder's OWN log output into the metrics table the validation
#   phase asks for. It invents nothing: every number comes from a log line the
#   application already wrote while a real recording ran on a real machine.
#   Where a value is absent, the cell reads "not logged" — it is never guessed
#   and never estimated.
#
# WHY IT EXISTS
#   The recorder emits its evidence across several tagged lines per session
#   ([RecResolution], [RecStageTimings], [RecFPS], [RecPerformance],
#   [RecCompleted], [RecDegraded] ...). Reading them by eye across seven
#   recordings is how numbers get transcribed wrongly. This parses them.
#
# USAGE
#   # newest log in the default folder
#   powershell -ExecutionPolicy Bypass -File RecordingMetricsReport.ps1
#
#   # a specific log, or every log in a folder
#   powershell -ExecutionPolicy Bypass -File RecordingMetricsReport.ps1 "C:\path\app-20260916.log"
#   powershell -ExecutionPolicy Bypass -File RecordingMetricsReport.ps1 -AllLogs
#
#   # write CSV for the report
#   powershell -ExecutionPolicy Bypass -File RecordingMetricsReport.ps1 -CsvOut metrics.csv
#
# OUTPUT
#   One row per completed recording, with capture vs output resolution kept
#   DISTINCT, the stage-attribution breakdown, and a derived answer to each
#   decision-gate question that the log can actually support. Questions that
#   need eyes on the video are printed as REQUIRES VISUAL INSPECTION rather
#   than answered from a number.
# ═══════════════════════════════════════════════════════════════════════
param(
    [Parameter(Position = 0)]
    [string]$LogPath,

    [switch]$AllLogs,

    [string]$CsvOut
)

$ErrorActionPreference = "Stop"

$LogFolder = Join-Path $env:APPDATA "ProGlassAutomation\logs"

# ── resolve which logs to read ──────────────────────────────────────────
$logs = @()
if ($LogPath -and (Test-Path $LogPath -PathType Leaf)) {
    $logs = @(Get-Item $LogPath)
} elseif ($LogPath -and (Test-Path $LogPath -PathType Container)) {
    $logs = @(Get-ChildItem -Path $LogPath -Filter "app-*.log" | Sort-Object Name)
} elseif ($AllLogs) {
    if (-not (Test-Path $LogFolder)) {
        Write-Error "Log folder not found: $LogFolder. Has the app run on this machine?"
        exit 1
    }
    $logs = @(Get-ChildItem -Path $LogFolder -Filter "app-*.log" | Sort-Object Name)
} else {
    if (-not (Test-Path $LogFolder)) {
        Write-Error "Log folder not found: $LogFolder. Has the app run on this machine?"
        exit 1
    }
    $logs = @(Get-ChildItem -Path $LogFolder -Filter "app-*.log" |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1)
}

if ($logs.Count -eq 0) {
    Write-Error "No app-*.log files found. Run a recording first, then re-run this report."
    exit 1
}

Write-Host ""
Write-Host ("Reading {0} log file(s):" -f $logs.Count) -ForegroundColor Cyan
foreach ($l in $logs) { Write-Host ("  {0}  ({1} KB)" -f $l.FullName, [math]::Round($l.Length / 1KB)) }
Write-Host ""

# ── parsing helpers ─────────────────────────────────────────────────────
function Get-Number($v) {
    # Pull the leading number out of tokens like "0.769Mbps", "360.02s",
    # "1234567B". Returns $null when there is no number at all, so a missing
    # measurement stays visibly missing instead of becoming 0.
    if ($null -eq $v) { return $null }
    $m = [regex]::Match([string]$v, '^-?\d+(?:\.\d+)?')
    if (-not $m.Success) { return $null }
    return [double]$m.Value
}

function Get-KvPairs($text) {
    # Quoted values first (provenance="...", cfgFallback="..."), then bare
    # tokens. A bare-token match can never start with a quote, so the two
    # passes cannot disagree about the same key.
    $h = @{}
    foreach ($m in [regex]::Matches($text, '([A-Za-z_][A-Za-z0-9_]*)="([^"]*)"')) {
        $h[$m.Groups[1].Value] = $m.Groups[2].Value
    }
    foreach ($m in [regex]::Matches($text, '([A-Za-z_][A-Za-z0-9_]*)=([^\s"]+)')) {
        $k = $m.Groups[1].Value
        if (-not $h.ContainsKey($k)) { $h[$k] = $m.Groups[2].Value }
    }
    return $h
}

# ── walk the logs, grouping lines into recording sessions ───────────────
# A session accumulates every [Rec*] field it sees and is FINALISED by
# [RecCompleted], which the service writes once per finished recording.
$sessions = New-Object System.Collections.Generic.List[object]
$cur = $null
$tagRe = [regex]'\[Rec([A-Za-z]+)\]'

foreach ($log in $logs) {
    foreach ($line in [System.IO.File]::ReadLines($log.FullName)) {
        $tm = $tagRe.Match($line)
        if (-not $tm.Success) { continue }
        $tag = "Rec" + $tm.Groups[1].Value

        # Timestamp is the first field of every line the service writes.
        $tsm = [regex]::Match($line, '^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})')
        $ts = if ($tsm.Success) { $tsm.Groups[1].Value } else { "" }

        $body = $line.Substring($tm.Index)
        $kv = Get-KvPairs $body

        if ($tag -eq "RecCompleted") {
            if ($null -eq $cur) { $cur = @{} }
            foreach ($k in $kv.Keys) { $cur[$k] = $kv[$k] }
            $cur["__tag_RecCompleted"] = $true
            $cur["__completedAt"] = $ts
            $cur["__log"] = $log.Name
            $sessions.Add([pscustomobject]$cur)
            $cur = $null
            continue
        }

        if ($null -eq $cur) { $cur = @{} }
        foreach ($k in $kv.Keys) {
            # Later lines win within a session, but the FIRST provenance /
            # stage line is the one describing this recording, so keep it.
            if (-not $cur.ContainsKey($k)) { $cur[$k] = $kv[$k] }
        }
        $cur["__tag_$tag"] = $true
        if ($tag -eq "RecDegraded") { $cur["__degraded"] = $true }
    }
}

# Anything never finalised by [RecCompleted] is an ABANDONED or FAILED
# recording — report it rather than dropping it silently.
if ($null -ne $cur) {
    $cur["__tag_RecCompleted"] = $false
    $cur["__log"] = if ($logs.Count -gt 0) { $logs[-1].Name } else { "" }
    $sessions.Add([pscustomobject]$cur)
}

if ($sessions.Count -eq 0) {
    Write-Host "No [RecCompleted] lines found in these logs." -ForegroundColor Yellow
    Write-Host "Either no recording has finished, or these logs predate the"
    Write-Host "telemetry. Check for [RecSaved] / [RecSummary] lines instead."
    exit 0
}

# ── build the report rows ───────────────────────────────────────────────
$rows = New-Object System.Collections.Generic.List[object]
foreach ($s in $sessions) {
    function V($key) { if ($s.PSObject.Properties[$key]) { return $s.$key } return "not logged" }

    $reqRes  = V "requested"          # e.g. 3840x2160@30fps
    $deliv   = V "delivered"          # ffprobe-reported
    $prov    = V "provenance"
    $reqFps  = Get-Number ([regex]::Match([string]$reqRes, '@(\d+(?:\.\d+)?)fps').Groups[1].Value)
    $reqWH   = [regex]::Match([string]$reqRes, '(\d+)x(\d+)')

    # ffprobe dimensions come from delivered=, e.g. 3840x2160@59.94fps
    $delWH   = [regex]::Match([string]$deliv, '(\d+)x(\d+)')
    $delFps  = Get-Number ([regex]::Match([string]$deliv, '@(\d+(?:\.\d+)?)fps').Groups[1].Value)

    $capFps  = Get-Number (V "captureFps")
    $encFps  = Get-Number (V "encodedFps")
    $captured= Get-Number (V "captured")
    $encoded = Get-Number (V "encoded")
    $dropped = Get-Number (V "dropped")
    $dup     = Get-Number (V "duplicated")
    $sizeB   = Get-Number (V "size")
    $mbps    = Get-Number (V "avgBitrate")
    $dur     = Get-Number (V "duration")

    # Is the encoded output physically bigger than what was captured? The log
    # already says so in words; recompute it from the numbers as a cross-check.
    $upscaled = $null
    if ($prov -ne "not logged") { $upscaled = ([string]$prov -match "UPSCALED") }

    # Capture fps vs requested, using the recorder's own 95% pass band.
    $fpsVerdict = "unknown"
    if ($null -ne $capFps -and $null -ne $reqFps -and $reqFps -gt 0) {
        $fpsVerdict = if ($capFps -ge ($reqFps * 0.95)) { "PASS" }
                      elseif ($capFps -ge ($reqFps * 0.5)) { "DEGRADED" }
                      else { "STARVED" }
    }

    # CPU / memory are logged by [RecPerformance] under whatever field names
    # that line uses, so discover them instead of hardcoding.
    $cpuKeys = @($s.PSObject.Properties | Where-Object { $_.Name -match '(?i)cpu' } |
                 Select-Object -First 3)
    $memKeys = @($s.PSObject.Properties | Where-Object { $_.Name -match '(?i)(mem|working|private)' } |
                 Select-Object -First 3)
    $cpuTxt = if ($cpuKeys.Count -gt 0) { ($cpuKeys | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join " " } else { "not logged" }
    $memTxt = if ($memKeys.Count -gt 0) { ($memKeys | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join " " } else { "not logged" }

    $sizeMB = if ($null -ne $sizeB) { [math]::Round($sizeB / 1MB, 1) } else { $null }

    $rows.Add([pscustomobject]@{
        When             = V "__completedAt"
        Completed        = V "__tag_RecCompleted"
        Tier             = V "tier"
        Codec            = V "codec"
        Encoder          = V "encoder"
        Preset           = V "preset"
        QualityValue     = V "quality"
        RateControl      = V "rateControl"
        PixFmt           = V "pixfmt"
        Gop              = V "gop"
        Provenance       = $prov
        Upscaled         = $upscaled
        RequestedRes     = $(if ($reqWH.Success) { "$($reqWH.Groups[1].Value)x$($reqWH.Groups[2].Value)" } else { "not logged" })
        RequestedFps     = $reqFps
        CaptureRes       = $(if ($prov -match 'from (\d+)x(\d+) capture') { $Matches[1] + "x" + $Matches[2] } else { "not logged" })
        ffprobeRes       = $(if ($delWH.Success) { "$($delWH.Groups[1].Value)x$($delWH.Groups[2].Value)" } else { "not logged" })
        ffprobeFps       = $delFps
        CaptureFps       = $capFps
        EncodedFps       = $encFps
        FpsVerdict       = $fpsVerdict
        CapturedFrames   = $captured
        EncodedFrames    = $encoded
        DroppedFrames    = $dropped
        DuplicatedFrames = $dup
        ThrottleDowns    = V "throttleDowns"
        ThrottleUps      = V "throttleUps"
        RetimeScale      = V "retime"
        DurationSec      = $dur
        SizeMB           = $sizeMB
        AvgBitrateMbps   = $mbps
        PeakBitrate      = $( $pk = $s.PSObject.Properties | Where-Object { $_.Name -match '(?i)peak' } | Select-Object -First 1
                             if ($pk) { $pk.Value } else { "not logged" } )
        Cpu              = $cpuTxt
        Memory           = $memTxt
        FfmpegExitCode   = $( $ec = $s.PSObject.Properties | Where-Object { $_.Name -match '(?i)exit' } | Select-Object -First 1
                              if ($ec) { $ec.Value } else { "not logged" } )
        StageDominant    = V "dominant"
        BlitAvgMs        = Get-Number (V "blitAvg")
        CopyAvgMs        = Get-Number (V "copyAvg")
        WriteAvgMs       = Get-Number (V "writeAvg")
        OtherAvgMs       = Get-Number (V "otherAvg")
        WriteMaxMs       = Get-Number (V "writeMax")
        WritesBlocked    = V "writesBlocked"
        Validation       = V "validation"
        ConfigFallback   = V "cfgFallback"
        Degraded         = $(if ($s.PSObject.Properties["__degraded"]) { $true } else { $false })
    })
}

# ── the Phase-4 table ───────────────────────────────────────────────────
Write-Host "══ PHASE 4 — RECORDED METRICS (parsed from the app's own log) ══" -ForegroundColor Cyan
Write-Host ""
$i = 0
foreach ($r in $rows) {
    $i++
    $hdr = "Recording #{0}  {1}  {2} {3} @ {4} fps  [{5}]" -f $i, $r.When, $r.RequestedRes, `
           $r.Tier, $r.RequestedFps, $r.Codec
    $colour = if (-not $r.Completed) { "Red" } elseif ($r.Degraded -or $r.FpsVerdict -eq "STARVED") { "Yellow" } else { "Green" }
    Write-Host $hdr -ForegroundColor $colour
    if (-not $r.Completed) {
        Write-Host "   ⚠ NO [RecCompleted] line — this recording did not finish cleanly." -ForegroundColor Red
    }
    Write-Host ("   CAPTURE  resolution : {0}" -f $r.CaptureRes)
    Write-Host ("   OUTPUT   resolution : {0}   (requested {1})" -f $r.ffprobeRes, $r.RequestedRes)
    Write-Host ("   PROVENANCE          : {0}" -f $r.Provenance)
    Write-Host ("   NATIVE 4K           : {0}" -f $(
        if ($r.Upscaled -eq $true) { "NO — 4K UPSCALED" }
        elseif ($r.Upscaled -eq $false) { "YES" } else { "unknown" }))
    Write-Host ("   FPS  requested/capture/encoded/ffprobe : {0} / {1} / {2} / {3}   [{4}]" -f `
        $r.RequestedFps, $r.CaptureFps, $r.EncodedFps, $r.ffprobeFps, $r.FpsVerdict)
    Write-Host ("   FRAMES captured/encoded/dropped/duplicated : {0} / {1} / {2} / {3}" -f `
        $r.CapturedFrames, $r.EncodedFrames, $r.DroppedFrames, $r.DuplicatedFrames)
    Write-Host ("   THROTTLE downs/ups   : {0} / {1}   retime={2}" -f `
        $r.ThrottleDowns, $r.ThrottleUps, $r.RetimeScale)
    Write-Host ("   ENCODER              : {0}  codec={1}  preset={2}  {3}={4}  pixfmt={5}  gop={6}" -f `
        $r.Encoder, $r.Codec, $r.Preset, $r.RateControl, $r.QualityValue, $r.PixFmt, $r.Gop)
    Write-Host ("   SIZE / BITRATE       : {0} MB   avg {1} Mbps   peak {2}" -f `
        $r.SizeMB, $r.AvgBitrateMbps, $r.PeakBitrate)
    Write-Host ("   DURATION             : {0} s" -f $r.DurationSec)
    Write-Host ("   CPU                  : {0}" -f $r.Cpu)
    Write-Host ("   MEMORY               : {0}" -f $r.Memory)
    Write-Host ("   FFMPEG EXIT          : {0}" -f $r.FfmpegExitCode)
    Write-Host ("   STAGE (ms avg)       : blit={0} copy={1} write={2} other={3}  maxWrite={4} blocked={5}" -f `
        $r.BlitAvgMs, $r.CopyAvgMs, $r.WriteAvgMs, $r.OtherAvgMs, $r.WriteMaxMs, $r.WritesBlocked)
    Write-Host ("   DOMINANT STAGE       : {0}" -f $r.StageDominant)
    Write-Host ("   VALIDATION           : {0}   degraded={1}   cfgFallback={2}" -f `
        $r.Validation, $r.Degraded, $r.ConfigFallback)
    Write-Host ""
}

# ── PHASE 5 / gate Q8: is the synchronous write the bottleneck? ─────────
Write-Host "══ PHASE 5 — BOTTLENECK ATTRIBUTION (measured, not assumed) ══" -ForegroundColor Cyan
$timed = @($rows | Where-Object { $null -ne $_.WriteAvgMs -and $null -ne $_.BlitAvgMs })
if ($timed.Count -eq 0) {
    Write-Host "  No [RecStageTimings] data. These logs predate the stage instrumentation," -ForegroundColor Yellow
    Write-Host "  or no recording completed. Re-record with this build, then re-run."
    Write-Host "  Until then the bottleneck is UNKNOWN — do not guess it."
} else {
    foreach ($r in $timed) {
        $tot = $r.BlitAvgMs + $r.CopyAvgMs + $r.WriteAvgMs + $r.OtherAvgMs
        if ($tot -le 0) { continue }
        Write-Host ("  {0} {1}@{2}fps  total {3:N2} ms/frame" -f `
            $r.When, $r.RequestedRes, $r.RequestedFps, $tot)
        Write-Host ("      gdi-blit      {0,7:N2} ms  {1,5:P1}" -f $r.BlitAvgMs, ($r.BlitAvgMs / $tot))
        Write-Host ("      marshal-copy  {0,7:N2} ms  {1,5:P1}" -f $r.CopyAvgMs, ($r.CopyAvgMs / $tot))
        Write-Host ("      encoder-write {0,7:N2} ms  {1,5:P1}   <- synchronous pipe backpressure" -f `
            $r.WriteAvgMs, ($r.WriteAvgMs / $tot))
        Write-Host ("      loop-overhead {0,7:N2} ms  {1,5:P1}" -f $r.OtherAvgMs, ($r.OtherAvgMs / $tot))
        Write-Host ("      => dominant: {0}   (writes >= 25ms: {1})" -f $r.StageDominant, $r.WritesBlocked) -ForegroundColor White
        Write-Host ""
    }
}

# ── PHASE 10: decision gate, answered ONLY where the data supports it ───
Write-Host "══ PHASE 10 — DECISION GATE ══" -ForegroundColor Cyan
function Answer-Res($res, $fps) {
    $m = @($rows | Where-Object {
        $_.RequestedRes -eq $res -and $_.RequestedFps -eq $fps -and $_.Completed })
    if ($m.Count -eq 0) { return "NO DATA — recording ${res}@${fps}fps not present in these logs" }
    $best = $m | Sort-Object CaptureFps -Descending | Select-Object -First 1
    return ("{0} — capture {1} fps of {2} requested [{3}], dropped {4}, {5} MB, {6} Mbps" -f `
        $best.FpsVerdict, $best.CaptureFps, $fps, $best.RequestedFps, $best.DroppedFrames,
        $best.SizeMB, $best.AvgBitrateMbps)
}
Write-Host ("  1. Sustain 4K/60?      {0}" -f (Answer-Res "3840x2160" 60))
Write-Host ("  2. Sustain 4K/30?      {0}" -f (Answer-Res "3840x2160" 30))

$r30 = @($rows | Where-Object { $_.RequestedRes -eq "3840x2160" -and $_.RequestedFps -eq 30 -and $_.Completed })
$r60 = @($rows | Where-Object { $_.RequestedRes -eq "3840x2160" -and $_.RequestedFps -eq 60 -and $_.Completed })
if ($r30.Count -gt 0 -and $r60.Count -gt 0) {
    $a = $r30[0]; $b = $r60[0]
    Write-Host ("  3. 30 vs 60 fps        size {0} vs {1} MB; bitrate {2} vs {3} Mbps; captured {4} vs {5} fps" -f `
        $a.SizeMB, $b.SizeMB, $a.AvgBitrateMbps, $b.AvgBitrateMbps, $a.CaptureFps, $b.CaptureFps)
    Write-Host "                         QUALITY difference between them: REQUIRES VISUAL INSPECTION"
    Write-Host "                         (compare frame crops at 200%/400% — size and bitrate do not"
    Write-Host "                          measure perceived sharpness)"
} else {
    Write-Host "  3. 30 vs 60 fps        NO DATA — need both 4K/30 and 4K/60 recordings"
}

$codecs = @($rows | Select-Object -ExpandProperty Codec -Unique)
if ($codecs.Count -gt 1) {
    Write-Host ("  4. Best codec balance  codecs present: {0}" -f ($codecs -join ", "))
    foreach ($c in $codecs) {
        $m = @($rows | Where-Object { $_.Codec -eq $c -and $_.Completed })
        $avg = ($m | Measure-Object AvgBitrateMbps -Average).Average
        $sz = ($m | Measure-Object SizeMB -Average).Average
        Write-Host ("       {0,-6} n={1}  avg {2:N2} Mbps  avg {3:N1} MB" -f $c, $m.Count, $avg, $sz)
    }
    Write-Host "       equal-quality judgement still REQUIRES VISUAL INSPECTION at matched CRF"
} else {
    Write-Host "  4. Best codec balance  NO DATA — only one codec recorded. Set Codec=HEVC in"
    Write-Host "                         recording settings and repeat the same scene."
}

if ($timed.Count -gt 0) {
    $w = ($timed | Measure-Object WriteAvgMs -Average).Average
    # Measure-Object -Property takes a property NAME, not a script block, so
    # the per-frame totals are projected first and then averaged.
    $totals = @($timed | ForEach-Object { $_.BlitAvgMs + $_.CopyAvgMs + $_.WriteAvgMs + $_.OtherAvgMs })
    $t = ($totals | Measure-Object -Average).Average
    if ($t -gt 0) {
        Write-Host ("  8. Is the synchronous write the bottleneck?  {0:P1} of the frame budget on average" -f ($w / $t))
        Write-Host ("       dominant stage across runs: {0}" -f (($timed | Group-Object StageDominant |
            Sort-Object Count -Descending | ForEach-Object { "$($_.Name) (x$($_.Count))" }) -join ", "))
    }
} else {
    Write-Host "  8. Synchronous write bottleneck?  NO DATA — see Phase 5 section above"
}

$up = @($rows | Where-Object { $_.Upscaled -eq $true })
$nat = @($rows | Where-Object { $_.Upscaled -eq $false })
Write-Host ("  9. Native 4K?          {0} upscaled / {1} native of {2} recordings" -f $up.Count, $nat.Count, $rows.Count)
if ($up.Count -gt 0 -and $nat.Count -eq 0) {
    Write-Host "       EVERY 4K recording in these logs is UPSCALED from a smaller capture."
    Write-Host "       Whether native 4K rendering is NEEDED depends on whether the upscaled"
    Write-Host "       crops are acceptable at 200%/400% — a visual call, not a numeric one."
}

Write-Host "  5/6/7/10. Preset headroom, best CRF for UI clarity, 6-minute size, and the"
Write-Host "            default profile: derive these from the rows above plus the visual"
Write-Host "            comparison. Do NOT pick them from file size."
Write-Host ""

# ── optional CSV ────────────────────────────────────────────────────────
if ($CsvOut) {
    $rows | Export-Csv -Path $CsvOut -NoTypeInformation -Encoding UTF8
    Write-Host ("Wrote {0} ({1} recordings)" -f $CsvOut, $rows.Count) -ForegroundColor Cyan
}
