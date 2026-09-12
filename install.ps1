$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
& .\build.ps1 @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$modsDir = if ($env:VINTAGE_STORY_DATA) { Join-Path $env:VINTAGE_STORY_DATA "Mods" }
           else { Join-Path $env:APPDATA "VintagestoryData\Mods" }
Remove-Item -Recurse -Force (Join-Path $modsDir "noticeboard") -ErrorAction SilentlyContinue
Copy-Item -Recurse Releases\noticeboard (Join-Path $modsDir "noticeboard")
Write-Host "Installed to $(Join-Path $modsDir 'noticeboard')"
