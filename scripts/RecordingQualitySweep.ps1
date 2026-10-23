# ═══════════════════════════════════════════════════════════════════════
# ProGlass screen recorder — QUALITY SWEEP (PHASE 4B, 2026-09-14)
#
# WHAT THIS IS
#   Re-encodes ONE finished recording at several CRF values — same
#   content, different quality targets — so you can SEE where visible
#   quality stops improving while the file keeps growing. This is the
#   "test multiple reasonable quality settings, do not blindly pick a
#   CRF" procedure from the Phase-4B spec, runnable in ~1 minute.
#
# WHY RE-ENCODE A FINISHED FILE (INSTEAD OF RE-RECORDING)
#   The same frames at different CRF isolate the quality/size curve
#   from everything else (capture rate, audio, timing). The sizes are
#   directly comparable to the master. One generation of H.264 decode
#   is involved — fine for a diagnostic, never for the master.
#
# USAGE
#   powershell -ExecutionPolicy Bypass -File RecordingQualitySweep.ps1
#   powershell -ExecutionPolicy Bypass -File RecordingQualitySweep.ps1 "C:\path\to\recording.mp4"
#
#   Default: the NEWEST *.mp4 under %AppData%\ProGlassAutomation\Production\Recordings
#
# OUTPUT
#   sweep_crfNN.mp4 next to the source, plus a size/bitrate table.
#   PLAY each file (Windows Media Player / MPV) and compare small
#   DataGrid text, thin lines, icons, gradients, scrolling — 100% zoom.
#   The best CRF is the LOWEST number where quality stops visibly
#   improving versus the next step up.
# ═══════════════════════════════════════════════════════════════════════
param(
    [string]$Source = ""
)

$ErrorActionPreference = "Stop"

# ── locate ffmpeg / ffprobe (app tools folder first, then PATH) ────────
$tools = Join-Path $env:APPDATA "ProGlassAutomation\tools"
function Find-Tool($name) {
    $local = Join-Path $tools "$name.exe"
    if (Test-Path $local) { return $local }
    $cmd = Get-Command "$name.exe" -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}
$ffmpeg  = Find-Tool "ffmpeg"
$ffprobe = Find-Tool "ffprobe"
if (-not $ffmpeg)  { Write-Error "ffmpeg not found (looked in $tools and PATH). Download it in Settings -> Demo Center first."; exit 1 }
if (-not $ffprobe) { Write-Error "ffprobe not found (looked in $tools and PATH)."; exit 1 }

# ── locate the source recording ────────────────────────────────────────
if (-not $Source) {
    $root = Join-Path $env:APPDATA "ProGlassAutomation\Production\Recordings"
    if (-not (Test-Path $root)) { Write-Error "No recordings folder at $root — pass the MP4 path as an argument."; exit 1 }
    $Source = Get-ChildItem -Path $root -Recurse -Filter *.mp4 -File |
              Where-Object { $_.Name -notlike "*sweep_*" } |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $Source) { Write-Error "No finished .mp4 found under $root."; exit 1 }
}
if (-not (Test-Path $Source)) { Write-Error "Source not found: $Source"; exit 1 }

$srcInfo = Get-Item $Source
Write-Host ""
Write-Host "Source: $($srcInfo.FullName)  ($([math]::Round($srcInfo.Length/1MB,1)) MB)"

# ── measured source facts (duration, video kbps) ───────────────────────
$probeArgs = @("-v","error","-select_streams","v:0","-show_entries","stream=bit_rate,avg_frame_rate","-of","default=noprint_wrappers=1")
$srcMeta = & $ffprobe $probeArgs $Source 2>$null
$srcKbps = "n/a"; $srcFps = "n/a"
foreach ($line in $srcMeta) {
    if ($line -like "bit_rate=*")  { $srcKbps = $line.Split("=")[1] }
    if ($line -like "avg_frame_rate=*") { $srcFps = $line.Split("=")[1] }
}
$dur = & $ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $Source 2>$null
Write-Host "Duration: $dur s   video: $srcKbps bps   avg_frame_rate: $srcFps"
Write-Host "NOTE: avg_frame_rate x duration = how many DISTINCT frames the master"
Write-Host "      actually contains. If that effective rate is far below the"
Write-Host "      recording fps, the encoder was starved — see the Phase-4B report."
Write-Host ""

# ── the sweep ──────────────────────────────────────────────────────────
$crfs = @(16, 18, 20, 22, 24, 26)
$base = $srcInfo.BaseName
$dir  = $srcInfo.DirectoryName
$rows = @()

foreach ($crf in $crfs) {
    $out = Join-Path $dir "sweep_crf${crf}.mp4"
    if (Test-Path $out) { Remove-Item $out -Force }
    & $ffmpeg -y -v error -i $Source -c:v libx264 -preset veryfast -tune stillimage -bf 3 -crf $crf -c:a copy $out
    if (-not (Test-Path $out)) { Write-Host "crf $crf : FAILED"; continue }
    $kbps = & $ffprobe -v error -select_streams v:0 -show_entries stream=bit_rate -of default=noprint_wrappers=1:nokey=1 $out 2>$null
    $rows += [pscustomobject]@{
        CRF    = $crf
        "SizeMB"  = [math]::Round((Get-Item $out).Length / 1MB, 1)
        "VideoKbps" = if ($kbps) { [math]::Round([double]$kbps / 1000, 0) } else { "n/a" }
        File = Split-Path $out -Leaf
    }
}

$refMB = [math]::Round($srcInfo.Length / 1MB, 1)
Write-Host ""
Write-Host ("{0,-5} {1,9} {2,11}  {3}" -f "CRF", "Size(MB)", "VideoKbps", "File")
Write-Host ("{0,-5} {1,9} {2,11}  {3}" -f "----", "--------", "---------", "----")
Write-Host ("{0,-5} {1,9} {2,11}  {3}" -f "master", $refMB, $srcKbps, $srcInfo.Name)
foreach ($r in $rows) {
    Write-Host ("{0,-5} {1,9} {2,11}  {3}" -f $r.CRF, $r.SizeMB, $r.VideoKbps, $r.File)
}
Write-Host ""
Write-Host "PLAY each file and compare: small DataGrid text, thin lines, icons,"
Write-Host "gradients, scrolling, fast window movement. Pick the LOWEST CRF where"
Write-Host "quality stops visibly improving — that is the production default."
Write-Host "(sweep_*.mp4 files are diagnostics; safe to delete.)"
