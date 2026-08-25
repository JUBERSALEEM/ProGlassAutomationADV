# ═══════════════════════════════════════════════════════════════════
# recover-local-checkout.ps1 — ProGlassAutomationADV
#
# USE WHEN: Visual Studio shows clusters of CS0103 errors like
#   "InitializeComponent does not exist", "ChatPanel does not exist",
#   "MessagesScroll does not exist" in Views/AI/AiChatView.xaml.cs
#
# CAUSE: those symbols are generated from AiChatView.xaml at build
# time. They disappear when your local checkout is conflicted or
# stale (e.g. a pull that hit the branch-pointer resets), so the XAML
# compiler never generates the partial class.
#
# WHAT THIS DOES (safe, no data outside the repo is touched):
#   1. fetches the arena branch
#   2. hard-resets tracked files to it (clears conflict markers and
#      stale versions — local edits to tracked files are discarded)
#   3. deletes bin/ and obj/ so stale generated code cannot survive
#
# After it runs: reopen the solution in Visual Studio and REBUILD.
# ═══════════════════════════════════════════════════════════════════

$ErrorActionPreference = "Stop"
Set-Location -Path (Split-Path -Parent $MyInvocation.MyCommand.Path)

$branch = "arena/01a038f2-proglassautomationadv"

Write-Host "Fetching origin/$branch ..."
git fetch origin $branch

Write-Host "Resetting tracked files to origin/$branch (discards conflicts and stale versions)..."
git reset --hard "origin/$branch"

Write-Host "Clearing build caches (bin/, obj/)..."
Get-ChildItem -Directory -Recurse -Include bin, obj |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Done. Reopen the solution in Visual Studio and REBUILD (not Build)." -ForegroundColor Green
