# ═══════════════════════════════════════════════════════════════════════
# ProGlass screen recorder — PRESET / CODEC / GOP BENCHMARK (crisp-demo pass)
#
# WHY THIS EXISTS
#   The crisp-demo work deliberately did NOT hard-code a preset, a CRF-to-
#   Mbps claim, or an HEVC quality offset, because none of those can be
#   established without measurement — and the development sandbox has no
#   ffmpeg, no ffprobe and no Windows display. This script is where those
#   numbers actually come from. Run it ON THE TARGET MACHINE.
#
#   It answers, with measurements:
#     • Phase 5  — which x264 preset can SUSTAIN the requested frame rate
#                  at each resolution (so "slow" is never chosen blindly)
#     • Phase 6  — what each CRF actually costs in Mbps and MB, per content
#     • Phase 4  — H.264 vs HEVC at the same quality number, and the real
#                  offset to put in VideoEncoder.HevcQualityOffset
#     • Phase 7  — what 4:4:4 costs over 4:2:0 on this encoder
#     • Phase 8  — what a 2-second GOP saves over a 1-second GOP
#     • Phase 17 — feasibility of the hardware matrix BEFORE you record it
#     • Phase 19 — whether 6 minutes at 4K lands near 400-600 MB
#
# WHAT IT MEASURES
#   sustained fps = (frames in the output) / (wall-clock seconds to encode).
#   That includes the decode of the source, so it is a LOWER BOUND on pure
#   encode throughput — the same conservative convention EncoderProbe uses.
#   If a preset clears the requested fps here, it will clear it live.
#
# USAGE
#   # fastest useful run — the item-17 feasibility matrix on your real content
#   powershell -ExecutionPolicy Bypass -File RecordingPresetBenchmark.ps1 -Matrix
#
#   # full sweep (presets x CRFs), ~several minutes
#   powershell -ExecutionPolicy Bypass -File RecordingPresetBenchmark.ps1
#
#   # specific source / codec / chroma comparison
#   powershell -ExecutionPolicy Bypass -File RecordingPresetBenchmark.ps1 `
#       "C:\path\to\recording.mp4" -Codecs h264,hevc -Chroma yuv420p,yuv444p
#
#   Default source: the NEWEST *.mp4 under
#   %AppData%\ProGlassAutomation\Production\Recordings
#   Use -Synthetic if you have no recording yet (generates UI-like test
#   content with lavfi; less representative of real text, but always works).
#
# OUTPUT
#   benchmark_*.mp4 next to the source, benchmark_results.csv, and a console
#   table. Nothing here is uploaded or deleted.
#
# HOW TO READ THE RESULT
#   A row is USABLE only if SustainedFps >= RequestedFps. Among usable rows
#   pick the SLOWEST preset (best compression per unit of quality) — that is
#   exactly what VideoEncoder.AdjustPreset does at runtime, and this script is
#   how you confirm the runtime measurement is telling the truth.
#   Then: the best CRF is the LOWEST number where quality stops visibly
#   improving versus the next step up. Judge that by LOOKING at 200%/400%
#   crops of small DataGrid text, never by file size.
# ═══════════════════════════════════════════════════════════════════════
param(
    [Parameter(Position = 0)]
    [string]$Source,

    # x264 rungs to test, cheapest first. Matches VideoEncoder.X264PresetLadder.
    [string[]]$Presets = @("ultrafast", "superfast", "veryfast", "faster", "fast", "medium"),

    # CRF values to test. Matches the tier baselines (22 / 18 / 13) plus the
    # crisp-demo neighbourhood.
    [int[]]$Crfs = @(13, 18, 20, 22),

    [string[]]$Codecs = @("h264"),          # add "hevc" to compare
    [string[]]$Chroma = @("yuv420p"),        # add "yuv444p" to price it

    # Seconds of source to encode per run. 20s is enough to be stable and
    # fast; raise it for a final decision run.
    [int]$Seconds = 20,

    # Phase 17 feasibility matrix: resolution x fps, each measured at every
    # preset. Overrides -Presets/-Crfs with a single representative CRF.
    [switch]$Matrix,

    [int]$MatrixCrf = 18,

    # Generate synthetic content instead of using a recording.
    [switch]$Synthetic,

    # Keep the encoded files for visual comparison (Phase 18).
    [switch]$KeepFiles
)

$ErrorActionPreference = "Stop"

# ── locate ffmpeg / ffprobe (app tools folder first, then PATH) ──────────
function Find-Tool($name) {
    $tools = Join-Path $env:APPDATA "ProGlassAutomation\tools"
    foreach ($cand in @((Join-Path $tools "$name.exe"), $name)) {
        $cmd = Get-Command $cand -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }
    $direct = Join-Path $tools "$name.exe"
    if (Test-Path $direct) { return $direct }
    return $null
}

$ffmpeg = Find-Tool "ffmpeg"
$ffprobe = Find-Tool "ffprobe"
if (-not $ffmpeg) {
    Write-Error "ffmpeg not found (looked in $env:APPDATA\ProGlassAutomation\tools and PATH). Download it in Settings -> Demo Center first."
    exit 1
}
if (-not $ffprobe) {
    Write-Error "ffprobe not found next to ffmpeg. The benchmark needs it to count frames and read bitrates."
    exit 1
}

# ── the item-17 matrix ──────────────────────────────────────────────────
$MatrixCells = @(
    @{ W = 1920; H = 1080; Fps = 30; Seconds = 60; Label = "1080p30" },
    @{ W = 1920; H = 1080; Fps = 60; Seconds = 60; Label = "1080p60" },
    @{ W = 2560; H = 1440; Fps = 60; Seconds = 60; Label = "1440p60" },
    @{ W = 3840; H = 2160; Fps = 30; Seconds = 60; Label = "4K30" },
    @{ W = 3840; H = 2160; Fps = 60; Seconds = 60; Label = "4K60 (optional)" },
    @{ W = 3840; H = 2160; Fps = 30; Seconds = 360; Label = "4K30 x 6min (critical)" }
)

# ── resolve the source ──────────────────────────────────────────────────
$srcFile = $null
if (-not $Synthetic) {
    if ($Source -and (Test-Path $Source)) {
        $srcFile = Get-Item $Source
    } else {
        $recDir = Join-Path $env:APPDATA "ProGlassAutomation\Production\Recordings"
        if (Test-Path $recDir) {
            $srcFile = Get-ChildItem -Path $recDir -Filter *.mp4 -Recurse -ErrorAction SilentlyContinue |
                       Sort-Object LastWriteTime -Descending | Select-Object -First 1
        }
    }
    if (-not $srcFile) {
        Write-Host "No recording found; falling back to -Synthetic content." -ForegroundColor Yellow
        $Synthetic = $true
    }
}

$outDir = if ($srcFile) { $srcFile.DirectoryName } else { (Get-Location).Path }
$stem   = if ($srcFile) { [IO.Path]::GetFileNameWithoutExtension($srcFile.Name) } else { "synthetic" }

Write-Host ""
if ($srcFile) {
    Write-Host ("Source : {0}  ({1} MB)" -f $srcFile.FullName, [math]::Round($srcFile.Length / 1MB, 1))
} else {
    Write-Host "Source : synthetic lavfi testsrc2 (not representative of real UI text)"
}
Write-Host ("ffmpeg : {0}" -f $ffmpeg)
Write-Host ""

# ── helpers ─────────────────────────────────────────────────────────────
function Get-VideoFacts($path) {
    # avg_frame_rate x duration = how many DISTINCT frames the file holds.
    $json = & $ffprobe -v error -select_streams v:0 -print_format json `
            -show_streams -show_format $path 2>$null | Out-String | ConvertFrom-Json
    $st = $json.streams[0]
    $frames = 0
    if ($st.nb_frames -and $st.nb_frames -ne "N/A") { $frames = [int]$st.nb_frames }
    $dur = 0.0
    if ($json.format.duration) { $dur = [double]$json.format.duration }
    $bits = 0
    if ($json.format.bit_rate) { $bits = [long]$json.format.bit_rate }
    return @{
        Width      = [int]$st.width
        Height     = [int]$st.height
        Frames     = $frames
        Duration   = $dur
        BitRate    = $bits
        CodecName  = $st.codec_name
        PixFmt     = $st.pix_fmt
        AvgFps     = $st.avg_frame_rate
    }
}

function Get-EncoderName($codec, $hw) {
    if ($codec -eq "hevc") { return $(if ($hw) { "hevc_nvenc" } else { "libx265" }) }
    return $(if ($hw) { "h264_nvenc" } else { "libx264" })
}

# Mirrors VideoEncoder.BFramesArg: x264 ultrafast sets bframes=0 internally,
# so emitting -bf there is argument text the encode never applies.
function Get-BFramesArg($preset, $codec) {
    if ($codec -ne "h264") { return @() }
    if ($preset -eq "ultrafast") { return @() }
    return @("-bf", "3")
}

# Mirrors VideoEncoder.GopFor: a TWO-second keyframe interval.
function Get-Gop($fps) {
    $g = [int][math]::Round($fps * 2.0)
    if ($g -lt 2) { $g = 2 }
    if ($g -gt 600) { $g = 600 }
    return $g
}

$results = New-Object System.Collections.Generic.List[object]
$runIndex = 0

function Invoke-BenchRun {
    param($Codec, $ChromaFmt, $Preset, $Crf, $W, $H, $Fps, $Secs, $Label, $GopSeconds)

    $script:runIndex++
    $tag = "benchmark_{0:d3}_{1}_{2}_{3}_crf{4}_{5}x{6}" -f $script:runIndex, $Codec, $ChromaFmt, $Preset, $Crf, $W, $H
    $out = Join-Path $outDir "$tag.mp4"

    $gop = if ($GopSeconds -gt 0) { [int][math]::Round($Fps * $GopSeconds) } else { Get-Gop $Fps }

    # Input: real recording (representative UI content) or synthetic.
    if ($srcFile) {
        $inArgs = @("-y", "-v", "error", "-t", "$Secs", "-i", $srcFile.FullName)
    } else {
        $inArgs = @("-y", "-v", "error",
                    "-f", "lavfi",
                    "-i", "testsrc2=duration=${Secs}:size=${W}x${H}:rate=${Fps}")
    }

    $encName = Get-EncoderName $Codec $false
    $vf = "scale=${W}:${H}:flags=lanczos:force_original_aspect_ratio=decrease," +
          "pad=${W}:${H}:(ow-iw)/2:(oh-ih)/2:color=black,format=${ChromaFmt}"

    $encArgs = @("-c:v", $encName)
    if ($Codec -eq "h264") {
        # Exactly the argument shape FfmpegRawPipe emits for the software path.
        $encArgs += @("-preset", $Preset, "-tune", "stillimage", "-threads", "0", "-crf", "$Crf")
    } else {
        # libx265 has no "stillimage" tune; preset names are shared with x264.
        $encArgs += @("-preset", $Preset, "-threads", "0", "-crf", "$Crf")
    }
    $encArgs += @("-g", "$gop")
    $encArgs += Get-BFramesArg $Preset $Codec
    # -r forces CONSTANT output at the target rate, so the encoded frame count
    # is Secs x Fps regardless of what the source happened to be. Without it,
    # SustainedFps would be computed from the source's own frame count and the
    # "HOLDS/STARVES" verdict would compare unrelated numbers.
    $encArgs += @("-r", "$Fps", "-pix_fmt", $ChromaFmt,
                  "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709",
                  "-movflags", "+faststart", "-an")

    $allArgs = $inArgs + @("-vf", $vf) + $encArgs + @($out)

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & $ffmpeg @allArgs 2>$null
    $exit = $LASTEXITCODE
    $sw.Stop()

    if ($exit -ne 0 -or -not (Test-Path $out)) {
        Write-Host ("  FAILED  {0,-28} {1} {2} crf{3} {4}x{5}  (ffmpeg exit {6})" -f `
            $Label, $Codec, $Preset, $Crf, $W, $H, $exit) -ForegroundColor Red
        if (-not $KeepFiles -and (Test-Path $out)) { Remove-Item $out -Force -ErrorAction SilentlyContinue }
        return
    }

    $f = Get-VideoFacts $out
    $elapsed = $sw.Elapsed.TotalSeconds
    if ($elapsed -le 0) { $elapsed = 0.001 }

    $sustained = if ($f.Frames -gt 0) { $f.Frames / $elapsed } else { 0 }
    $sizeMB = (Get-Item $out).Length / 1MB
    $mbps = if ($f.Duration -gt 0) { ($sizeMB * 8) / $f.Duration } else { 0 }
    # What 6 minutes at this rate would cost — the Phase-19 question.
    $projected6minMB = $mbps * 360 / 8

    $usable = $sustained -ge $Fps

    $results.Add([pscustomobject]@{
        Label            = $Label
        Codec            = $Codec
        Encoder          = $f.CodecName
        Preset           = $Preset
        Crf              = $Crf
        Chroma           = $ChromaFmt
        EncodedPixFmt    = $f.PixFmt
        RequestedFps     = $Fps
        SustainedFps     = [math]::Round($sustained, 2)
        UsableAtFps      = $usable
        EncodeSeconds    = [math]::Round($elapsed, 2)
        SizeMB           = [math]::Round($sizeMB, 2)
        AvgBitrateMbps   = [math]::Round($mbps, 3)
        Projected6min4K_MB = [math]::Round($projected6minMB, 1)
        Duration         = [math]::Round($f.Duration, 2)
        Frames           = $f.Frames
        Resolution       = "$($f.Width)x$($f.Height)"
        Gop              = $gop
        File             = (Split-Path $out -Leaf)
    })

    $colour = if ($usable) { "Green" } else { "Yellow" }
    Write-Host ("  {0,-26} {1,-5} {2,-9} crf{3,-3} {4,-9} {5,7:N2} fps {6,8:N1} MB {7,8:N2} Mbps  6min~{8,7:N0} MB  {9}" -f `
        $Label, $Codec, $Preset, $Crf, $ChromaFmt, $sustained, $sizeMB, $mbps, $projected6minMB, `
        $(if ($usable) { "HOLDS" } else { "STARVES" })) -ForegroundColor $colour

    if (-not $KeepFiles) { Remove-Item $out -Force -ErrorAction SilentlyContinue }
}

# ══════════════════════════════════════════════════════════════════════
Write-Host "── sustained-encode benchmark ───────────────────────────────────" -ForegroundColor Cyan
Write-Host "   a row is usable only when SustainedFps >= RequestedFps"
Write-Host ""

if ($Matrix) {
    # Phase 17 feasibility: can this machine even hold each cell, per preset?
    foreach ($cell in $MatrixCells) {
        Write-Host ("── {0}  ({1}x{2} @ {3} fps, {4}s) ──" -f `
            $cell.Label, $cell.W, $cell.H, $cell.Fps, $cell.Seconds) -ForegroundColor White
        foreach ($p in $Presets) {
            Invoke-BenchRun -Codec "h264" -ChromaFmt "yuv420p" -Preset $p -Crf $MatrixCrf `
                -W $cell.W -H $cell.H -Fps $cell.Fps -Secs $cell.Seconds `
                -Label $cell.Label -GopSeconds 2
        }
        Write-Host ""
    }
} else {
    foreach ($codec in $Codecs) {
        foreach ($chromaFmt in $Chroma) {
            if ($chromaFmt -eq "yuv444p" -and $codec -ne "h264") {
                Write-Host "  skipping ${codec}+yuv444p (4:4:4 is a software-encoder path)" -ForegroundColor DarkGray
                continue
            }
            Write-Host ("── codec {0}, chroma {1} ──" -f $codec, $chromaFmt) -ForegroundColor White
            foreach ($p in $Presets) {
                foreach ($crf in $Crfs) {
                    Invoke-BenchRun -Codec $codec -ChromaFmt $chromaFmt -Preset $p -Crf $crf `
                        -W 1920 -H 1080 -Fps 60 -Secs $Seconds `
                        -Label "1080p60" -GopSeconds 2
                }
            }
            Write-Host ""
        }
    }

    # Phase 8: what does the 2-second GOP actually buy versus the old 1-second one?
    Write-Host "── GOP comparison (Phase 8) ──" -ForegroundColor White
    foreach ($gopSec in @(1, 2)) {
        Invoke-BenchRun -Codec "h264" -ChromaFmt "yuv420p" -Preset "veryfast" -Crf 18 `
            -W 1920 -H 1080 -Fps 60 -Secs $Seconds -Label "GOP ${gopSec}s" -GopSeconds $gopSec
    }
    Write-Host ""
}

# ── write the CSV ───────────────────────────────────────────────────────
if ($results.Count -eq 0) {
    Write-Host "No successful runs — nothing to report." -ForegroundColor Red
    exit 1
}

$csv = Join-Path $outDir "benchmark_results.csv"
$results | Export-Csv -Path $csv -NoTypeInformation -Encoding UTF8
Write-Host ("Wrote {0} ({1} runs)" -f $csv, $results.Count) -ForegroundColor Cyan

# ── the decisions this data exists to make ──────────────────────────────
Write-Host ""
Write-Host "── what to do with these numbers ────────────────────────────────" -ForegroundColor Cyan
foreach ($label in ($results | Select-Object -ExpandProperty Label -Unique)) {
    $rows = $results | Where-Object { $_.Label -eq $label }
    $fps = ($rows | Select-Object -First 1).RequestedFps
    $usable = $rows | Where-Object { $_.UsableAtFps }
    if ($usable) {
        # Slowest usable preset = best compression that still holds the rate.
        $best = $usable | Sort-Object SustainedFps | Select-Object -First 1
        Write-Host ("  {0,-24} slowest preset that HOLDS {1} fps : {2} ({3} fps, {4} Mbps)" -f `
            $label, $fps, $best.Preset, $best.SustainedFps, $best.AvgBitrateMbps)
    } else {
        Write-Host ("  {0,-24} NO preset held {1} fps — record at a lower fps or resolution," -f `
            $label, $fps) -ForegroundColor Red
        Write-Host "                          or use a hardware encoder. Do NOT raise the bitrate to compensate."
    }
}

$hevc = $results | Where-Object { $_.Codec -eq "hevc" }
$h264 = $results | Where-Object { $_.Codec -eq "h264" }
if ($hevc -and $h264) {
    $hAvg = ($h264 | Measure-Object AvgBitrateMbps -Average).Average
    $eAvg = ($hevc  | Measure-Object AvgBitrateMbps -Average).Average
    if ($hAvg -gt 0) {
        Write-Host ""
        Write-Host ("  HEVC vs H.264 at the same CRF numbers: {0:N2} vs {1:N2} Mbps ({2:P0} of H.264)" -f `
            $eAvg, $hAvg, ($eAvg / $hAvg))
        Write-Host "  Set VideoEncoder.HevcQualityOffset from this, and only enable HEVC by"
        Write-Host "  default after confirming the files play in your target players."
    }
}

$c444 = $results | Where-Object { $_.Chroma -eq "yuv444p" }
$c420 = $results | Where-Object { $_.Chroma -eq "yuv420p" }
if ($c444 -and $c420) {
    $a420 = ($c420 | Measure-Object AvgBitrateMbps -Average).Average
    $a444 = ($c444 | Measure-Object AvgBitrateMbps -Average).Average
    if ($a420 -gt 0) {
        Write-Host ("  4:4:4 costs {0:P0} more bitrate than 4:2:0 here ({1:N2} vs {2:N2} Mbps)" -f `
            (($a444 / $a420) - 1), $a444, $a420)
        Write-Host "  Only worth it if the 200%/400% crops of coloured text and thin coloured"
        Write-Host "  borders are visibly better — check that by eye, not by size."
    }
}

Write-Host ""
Write-Host "── Phase 19: the 6-minute 4K size target ────────────────────────" -ForegroundColor Cyan
$fourK = $results | Where-Object { $_.Resolution -eq "3840x2160" -and $_.UsableAtFps }
if ($fourK) {
    foreach ($r in ($fourK | Sort-Object Projected6min4K_MB)) {
        $verdict = if ($r.Projected6min4K_MB -ge 300 -and $r.Projected6min4K_MB -le 700) { "IN TARGET BAND" }
                   elseif ($r.Projected6min4K_MB -lt 300) { "below band (still fine if quality holds)" }
                   else { "above band" }
        Write-Host ("  crf{0,-3} {1,-9} -> ~{2,7:N0} MB per 6 min   {3}" -f `
            $r.Crf, $r.Preset, $r.Projected6min4K_MB, $verdict)
    }
    Write-Host "  Target band is 400-600 MB; 300 MB at excellent quality is acceptable and"
    Write-Host "  700 MB is acceptable if visibly better. Size is a TARGET, never a cap —"
    Write-Host "  do not add -b:v/-maxrate/-bufsize to force it."
} else {
    Write-Host "  No usable 4K row in this run. Re-run with -Matrix to find what this"
    Write-Host "  machine can actually hold at 4K before choosing a size target."
}

Write-Host ""
if (-not $KeepFiles) {
    Write-Host "Encoded files were deleted. Re-run with -KeepFiles for the Phase-18 visual"
    Write-Host "comparison (200% and 400% crops of small text, thin borders, icons)."
} else {
    Write-Host "Encoded files were KEPT for the Phase-18 visual comparison. Compare at"
    Write-Host "200% and 400% zoom, focusing on the smallest text in the scene."
}
Write-Host ""
