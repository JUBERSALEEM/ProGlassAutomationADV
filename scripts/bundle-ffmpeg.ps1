# Places the official Gyan FFmpeg essentials build into publish/tools
# so Setup.exe installs ffmpeg.exe and ffprobe.exe with the app.
# GitHub's CDN is tried first. www.gyan.dev is the same build, slower.
# Settings can still download later if this step is skipped.

[CmdletBinding()]
param(
    [string]$PublishDir,
    [switch]$AllowMissing
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")) "publish"
}

$urls = @(
    "https://github.com/GyanD/codexffmpeg/releases/download/9.0.2/ffmpeg-9.0.2-essentials_build.zip",
    "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
)

$tools = Join-Path $PublishDir "tools"
New-Item -ItemType Directory -Force -Path $tools | Out-Null

$zip = Join-Path ([IO.Path]::GetTempPath()) ("proglass-ffmpeg-" + [guid]::NewGuid().ToString("N") + ".zip")
$extract = Join-Path ([IO.Path]::GetTempPath()) ("proglass-ffmpeg-" + [guid]::NewGuid().ToString("N"))

function Save-Zip([string]$url) {
    Write-Host "> FFmpeg mirror: $url"
    $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
    if ($curl) {
        & curl.exe -L --fail --retry 2 --retry-delay 2 -o $zip $url
        if ($LASTEXITCODE -ne 0) { throw "curl exited $LASTEXITCODE" }
        return
    }
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
}

$saved = $false
$last = ""
foreach ($url in $urls) {
    try {
        if (Test-Path $zip) { Remove-Item -Force $zip }
        Save-Zip $url
        if ((Get-Item $zip).Length -lt 1MB) { throw "Download was too small to be the essentials zip." }
        $saved = $true
        break
    } catch {
        $last = $_.Exception.Message
        Write-Warning $last
    }
}

if (-not $saved) {
    if ($AllowMissing) {
        Write-Warning "FFmpeg was not bundled. Setup.exe will not contain it. $last"
        exit 0
    }
    Write-Error "FFmpeg essentials zip could not be downloaded. $last"
    exit 1
}

try {
    Expand-Archive -Path $zip -DestinationPath $extract -Force
    $ffmpeg = Get-ChildItem -Path $extract -Recurse -Filter ffmpeg.exe | Select-Object -First 1
    $ffprobe = Get-ChildItem -Path $extract -Recurse -Filter ffprobe.exe | Select-Object -First 1
    if (-not $ffmpeg -or -not $ffprobe) {
        throw "The zip did not contain ffmpeg.exe and ffprobe.exe."
    }
    Copy-Item $ffmpeg.FullName (Join-Path $tools "ffmpeg.exe") -Force
    Copy-Item $ffprobe.FullName (Join-Path $tools "ffprobe.exe") -Force
    Write-Host "> Bundled ffmpeg into $tools"
} finally {
    if (Test-Path $zip) { Remove-Item -Force $zip }
    if (Test-Path $extract) { Remove-Item -Recurse -Force $extract }
}
