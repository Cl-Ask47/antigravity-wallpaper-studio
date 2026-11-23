param(
  [switch]$PurgeData
)

$ErrorActionPreference = "Stop"
$TargetDir = Join-Path $env:USERPROFILE ".gemini\config\plugins\wallpaper-studio"
$DataDir = Join-Path $env:USERPROFILE ".gemini\antigravity\plugin_data\wallpaper-studio"
$RuntimeDir = Join-Path $env:TEMP "agy_wallpaper_studio"

Write-Host "=== Wallpaper Studio Uninstaller ===" -ForegroundColor Yellow

# Stop companion and force-restore 100% Antigravity window opacity
Get-Process | Where-Object { $_.ProcessName -like "studio_companion*" } | Stop-Process -Force -ErrorAction SilentlyContinue
$CompExe = Get-ChildItem $RuntimeDir -Filter "studio_companion_*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($CompExe) {
  try { & $CompExe.FullName --restore } catch {}
}

if (Test-Path $TargetDir) {
  Remove-Item -Recurse -Force $TargetDir
  Write-Host "Removed plugin directory: $TargetDir" -ForegroundColor Green
} else {
  Write-Host "Plugin directory not found (already removed)."
}

if ($PurgeData -and (Test-Path $DataDir)) {
  Remove-Item -Recurse -Force $DataDir
  Write-Host "Purged saved user config: $DataDir" -ForegroundColor Yellow
} else {
  Write-Host "User config preserved at: $DataDir (use -PurgeData to delete)."
}

Write-Host "Done! Antigravity opacity has been restored." -ForegroundColor Green
