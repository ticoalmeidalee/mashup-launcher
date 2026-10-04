# Packs the launcher for players: dist\MashupLauncher-<version>.zip with the exe, an empty library (mashups\README.md)
# and the docs. Unzip anywhere and run MashupLauncher.exe; mashups come from Browse, Add .zip or Create.
param([string]$Version = 'dev')
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$exe = Join-Path $root 'MashupLauncher.exe'
if (-not (Test-Path $exe)) { throw 'Build the launcher first (launcher\build.bat)' }
$dist = New-Item -ItemType Directory -Force (Join-Path $root 'dist')
$stage = Join-Path $dist "MashupLauncher-$Version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'mashups') | Out-Null
Copy-Item $exe $stage
foreach ($f in 'README.md', 'LICENSE', 'NOTICE.md', 'SECURITY.md') { if (Test-Path (Join-Path $root $f)) { Copy-Item (Join-Path $root $f) $stage } }
Copy-Item (Join-Path $root 'docs\mashup-format.md') (Join-Path $stage 'mashups\README.md')
$zip = Join-Path $dist "MashupLauncher-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip)
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
Set-Content (Join-Path $dist "MashupLauncher-$Version.sha256") $sha
Write-Host "launcher: $zip"
Write-Host "sha256:   $sha"
