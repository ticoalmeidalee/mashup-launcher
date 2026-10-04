# Assembles the GTA V x Minecraft package for Mashup Launcher from the builds:
#   gta\build\MCPassthrough.asi   (gta\fetch_deps.ps1, then gta\build.bat)
#   mc\build\libs\passthrough-*.jar   (mc: gradlew build, JDK 25)
# Output: <repo>\dist\gta-minecraft-<version>.zip and its SHA-256 (what library\index.json pins).
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$manifest = Get-Content (Join-Path $here 'mashup.json') -Raw | ConvertFrom-Json
$version = $manifest.version
$dist = New-Item -ItemType Directory -Force (Join-Path $here '..\..\dist')
$stage = Join-Path $dist "gta-minecraft-$version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$game = New-Item -ItemType Directory -Force (Join-Path $stage 'payload\game\reshade-shaders\Shaders')
$mc = New-Item -ItemType Directory -Force (Join-Path $stage 'payload\minecraft')

$asi = Join-Path $here 'gta\build\MCPassthrough.asi'
$jar = Get-ChildItem (Join-Path $here 'mc\build\libs') -Filter 'passthrough-*.jar' | Where-Object { $_.Name -notmatch 'sources' } | Select-Object -First 1
if (-not (Test-Path $asi)) { throw "Build the GTA plugin first: $asi is missing" }
if (-not $jar) { throw 'Build the Minecraft mod first: mc\build\libs\passthrough-*.jar is missing' }

Copy-Item (Join-Path $here 'mashup.json'), (Join-Path $here 'cover.png'), (Join-Path $here 'README.md') $stage
Copy-Item (Join-Path $here 'package\game\*') (Join-Path $stage 'payload\game')
Copy-Item $asi (Join-Path $stage 'payload\game')
Copy-Item (Join-Path $here 'gta\shaders\MCPassthrough.fx') $game
Copy-Item $jar.FullName (Join-Path $mc 'gta-minecraft.jar')

$zip = Join-Path $dist "gta-minecraft-$version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip)
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
Write-Host "package: $zip"
Write-Host "sha256:  $sha"
Set-Content (Join-Path $dist "gta-minecraft-$version.sha256") $sha
