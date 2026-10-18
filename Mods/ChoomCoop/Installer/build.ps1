# Builds ChoomCoopSetup.exe from the current xmake install output.
#
#   xmake -y -j 4
#   xmake install -y -o distrib
#   .\Mods\ChoomCoop\Installer\build.ps1
#
# Output: Mods\ChoomCoop\Installer\dist\ChoomCoopSetup.exe
$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$repo = (Resolve-Path (Join-Path $here '..\..\..')).Path
$distrib = Join-Path $repo 'distrib'
$mod = Join-Path $distrib 'launcher\mod'
$scripts = Join-Path $distrib 'choomcoop\r6\scripts\ChoomCoop'
$server = Join-Path $distrib 'launcher\server'

& (Join-Path $here '..\tools\CheckScripts.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Redscript check failed - not packaging a build that would disable every script.' }

foreach ($p in @($mod, $scripts, $server)) {
    if (-not (Test-Path $p)) { throw "Missing $p - run 'xmake install -y -o distrib' first." }
}
if (-not (Test-Path (Join-Path $server 'plugins\ChoomCoopSystem\ChoomCoopSystem.dll'))) {
    throw 'ChoomCoopSystem plugin missing from distrib - rebuild with xmake.'
}

# Stage the payload: client\ is laid out relative to the game folder, server\ is the server folder.
$stage = Join-Path $here 'payload\stage'
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force (Join-Path $stage 'client\red4ext\plugins\zzzCyberpunkMP') | Out-Null
New-Item -ItemType Directory -Force (Join-Path $stage 'client\r6\scripts') | Out-Null
New-Item -ItemType Directory -Force (Join-Path $stage 'server') | Out-Null

Copy-Item -Recurse (Join-Path $mod '*') (Join-Path $stage 'client\red4ext\plugins\zzzCyberpunkMP')
Copy-Item -Recurse $scripts (Join-Path $stage 'client\r6\scripts\ChoomCoop')
Get-ChildItem $server | Where-Object { $_.Name -notin @('config', 'logs') } |
    ForEach-Object { Copy-Item -Recurse $_.FullName (Join-Path $stage 'server') }
Get-ChildItem -Recurse (Join-Path $stage 'server') -Filter *.pdb | Remove-Item

$commit = (git -C $repo rev-parse --short HEAD).Trim()
$dirty = if ((git -C $repo status --porcelain --untracked-files=no)) { '-dirty' } else { '' }
Set-Content (Join-Path $stage 'build.txt') "commit $commit$dirty built $(Get-Date -Format s)"

$zip = Join-Path $here 'payload\payload.zip'
if (Test-Path $zip) { Remove-Item -Force $zip }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item -Recurse -Force $stage
Write-Host "Payload: $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB, commit $commit$dirty)"

$out = Join-Path $here 'dist'
dotnet publish (Join-Path $here 'ChoomCoopSetup.csproj') -c Release -o $out --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
Get-ChildItem $out -Exclude 'ChoomCoopSetup.exe' | Remove-Item -Recurse -Force
$exe = Get-Item (Join-Path $out 'ChoomCoopSetup.exe')
Write-Host "Built $($exe.FullName) ($([math]::Round($exe.Length / 1MB, 1)) MB)"
