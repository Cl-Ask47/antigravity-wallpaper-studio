param(
  [string]$Repo = "Cl-Ask47/antigravity-wallpaper-studio",
  [string]$Branch = "main"
)

$ErrorActionPreference = "Stop"
$TargetDir = Join-Path $env:USERPROFILE ".gemini\config\plugins\wallpaper-studio"

Write-Host "=== Wallpaper Studio for Antigravity Installer ===" -ForegroundColor Cyan

# Stop any previously running companion so DLLs are not locked
Get-Process | Where-Object { $_.ProcessName -like "studio_companion*" } | Stop-Process -Force -ErrorAction SilentlyContinue

$LocalRoot = $null
if ($PSScriptRoot) {
  $Candidate = Split-Path -Parent $PSScriptRoot
  if (Test-Path (Join-Path $Candidate "plugin.json")) {
    $LocalRoot = $Candidate
  }
}

$TempExtract = $null
try {
  if ($LocalRoot) {
    Write-Host "[1/2] Installing from local directory: $LocalRoot"
    $SourceRoot = $LocalRoot
  } else {
    Write-Host "[1/2] Downloading latest archive from GitHub ($Repo@$Branch)..."
    $TempZip = Join-Path $env:TEMP ("agy_ws_install_" + [Guid]::NewGuid().ToString("N") + ".zip")
    $TempExtract = Join-Path $env:TEMP ("agy_ws_extract_" + [Guid]::NewGuid().ToString("N"))
    $Url = "https://github.com/$Repo/archive/refs/heads/$Branch.zip"
    Invoke-WebRequest -Uri $Url -OutFile $TempZip -UseBasicParsing
    Expand-Archive -Path $TempZip -DestinationPath $TempExtract -Force
    Remove-Item $TempZip -Force -ErrorAction SilentlyContinue
    $Sub = Get-ChildItem $TempExtract -Directory | Select-Object -First 1
    if (-not $Sub) { throw "Downloaded archive is empty." }
    $SourceRoot = $Sub.FullName
  }

  Write-Host "[2/2] Copying plugin files to: $TargetDir"
  New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

  $CopyItems = @("plugin.json", "README.md", "LICENSE", "THIRD_PARTY_NOTICES.md", "assets", "rules", "skills", "sidecars")
  foreach ($item in $CopyItems) {
    $srcItem = Join-Path $SourceRoot $item
    if (Test-Path $srcItem) {
      Copy-Item -Path $srcItem -Destination $TargetDir -Recurse -Force
    }
  }

  Write-Host ""
  Write-Host "Install complete!" -ForegroundColor Green
  Write-Host "Plugin path: $TargetDir"
  Write-Host "Open Antigravity -> Customizations -> Installed -> Wallpaper Studio (auto-loads without restart)."
} finally {
  if ($TempExtract -and (Test-Path $TempExtract)) {
    Remove-Item -Recurse -Force $TempExtract -ErrorAction SilentlyContinue
  }
}
