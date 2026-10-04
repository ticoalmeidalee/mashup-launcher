# Fetches what building the GTA plugin needs into third_party\ (none of it may be redistributed, so it isn't in the repo):
#   shv\       ScriptHookV SDK: main.h, nativeCaller.h, types.h, ScriptHookV.lib (dev-c.com; needs browser headers)
#   reshade\   ReShade's add-on API headers (crosire/reshade at v6.8.0)
# Players never need this: Mashup Launcher downloads ScriptHookV, its loader and ReShade from their sites at install time.
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$reshade = '6.8.0'
$ua = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36'
$headers = @{ 'User-Agent' = $ua; 'Accept' = 'text/html,application/octet-stream,*/*'; 'Accept-Language' = 'en-US' }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# dev-c.com sometimes doesn't answer (seen from CI machines): retry with backoff before giving up
function Get-WithRetry([string]$Uri, [hashtable]$Headers, [string]$OutFile) {
    for ($attempt = 1; ; $attempt++) {
        try {
            if ($OutFile) { Invoke-WebRequest -UseBasicParsing -Headers $Headers $Uri -OutFile $OutFile -TimeoutSec 60; return }
            return (Invoke-WebRequest -UseBasicParsing -Headers $Headers $Uri -TimeoutSec 60).Content
        }
        catch {
            if ($attempt -ge 5) { throw }
            Write-Host "attempt $attempt for $Uri failed ($($_.Exception.Message)); retrying in $(15 * $attempt) s"
            Start-Sleep -Seconds (15 * $attempt)
        }
    }
}

if ((Test-Path (Join-Path $here 'third_party\shv\ScriptHookV.lib')) -and (Test-Path (Join-Path $here 'third_party\reshade\reshade.hpp'))) {
    Write-Host 'third_party already present (cached)'; return
}

$shv = New-Item -ItemType Directory -Force (Join-Path $here 'third_party\shv')
$rs = New-Item -ItemType Directory -Force (Join-Path $here 'third_party\reshade')
$tmp = New-Item -ItemType Directory -Force (Join-Path ([IO.Path]::GetTempPath()) ("shv-" + [Guid]::NewGuid().ToString('N').Substring(0, 8)))
try {
    $page = Get-WithRetry 'https://www.dev-c.com/gtav/scripthookv/' $headers
    $sdk = [regex]::Match($page, '/files/ScriptHookV_SDK_[^"]*\.zip').Value
    if (-not $sdk) { throw 'No ScriptHookV SDK link on https://www.dev-c.com/gtav/scripthookv/' }
    $zip = Join-Path $tmp 'sdk.zip'
    Get-WithRetry "https://www.dev-c.com$sdk" ($headers + @{ 'Referer' = 'https://www.dev-c.com/gtav/scripthookv/' }) $zip
    Expand-Archive $zip (Join-Path $tmp 'sdk') -Force
    foreach ($f in 'main.h', 'nativeCaller.h', 'types.h') { Copy-Item (Join-Path $tmp "sdk\inc\$f") $shv -Force }
    Copy-Item (Join-Path $tmp 'sdk\lib\ScriptHookV.lib') $shv -Force

    foreach ($f in 'reshade.hpp', 'reshade_api.hpp', 'reshade_api_device.hpp', 'reshade_api_pipeline.hpp', 'reshade_api_resource.hpp',
                   'reshade_api_format.hpp', 'reshade_events.hpp', 'reshade_overlay.hpp') {
        Get-WithRetry "https://raw.githubusercontent.com/crosire/reshade/v$reshade/include/$f" @{} (Join-Path $rs $f)
    }
    Write-Host "third_party ready: $(Get-ChildItem $shv, $rs -Name)"
}
finally { Remove-Item $tmp -Recurse -Force }
