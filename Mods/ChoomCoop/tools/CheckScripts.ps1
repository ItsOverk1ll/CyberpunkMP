# Compile-checks every redscript file the game will load (ChoomCoop + CyberpunkMP + the
# installed ArchiveXL/Codeware/TweakXL scripts) against the game's own script bundle,
# using the official redscript CLI. No game launch, nothing in the game folder is written.
#
#   .\Mods\ChoomCoop\tools\CheckScripts.ps1 [-Game "G:\SteamLibrary\steamapps\common\Cyberpunk 2077"]
#
# Exit code 0 = compiles. A single bad .reds disables every script in-game, so run this
# before every test build (build.ps1 does).
param([string]$Game)
$ErrorActionPreference = 'Stop'

$cliVersion = '0.5.31'
$cliSha256 = 'cdcbed2e0c943322bbcbbac4a9c62ef29adc5620e4b0934f0d2a31a8282b5b62'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path

if (-not $Game) {
    $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if ($steam) {
        $libs = @($steam) + ((Select-String -Path "$steam\steamapps\libraryfolders.vdf" -Pattern '"path"\s+"(.+?)"').Matches |
            ForEach-Object { $_.Groups[1].Value -replace '\\\\', '\' })
        $Game = $libs | ForEach-Object { Join-Path $_ 'steamapps\common\Cyberpunk 2077' } |
            Where-Object { Test-Path (Join-Path $_ 'bin\x64\Cyberpunk2077.exe') } | Select-Object -First 1
    }
    if (-not $Game) { throw 'Game folder not found; pass -Game <folder>.' }
}

# The vanilla bundle: redscript keeps the original as .bk once it has compiled mods in.
$bundle = Join-Path $Game 'r6\cache\final.redscripts'
if (Test-Path "$bundle.bk") { $bundle = "$bundle.bk" }
if (-not (Test-Path $bundle)) { throw "No script bundle at $bundle" }

$tools = Join-Path $env:LOCALAPPDATA 'cpmp-buildtools'
$cli = Join-Path $tools "redscript-cli-$cliVersion.exe"
if (-not (Test-Path $cli) -or (Get-FileHash $cli -Algorithm SHA256).Hash.ToLower() -ne $cliSha256) {
    New-Item -ItemType Directory -Force $tools | Out-Null
    Invoke-WebRequest "https://github.com/jac3km4/redscript/releases/download/v$cliVersion/redscript-cli.exe" -OutFile $cli -UseBasicParsing
    if ((Get-FileHash $cli -Algorithm SHA256).Hash.ToLower() -ne $cliSha256) { Remove-Item $cli; throw 'redscript CLI checksum mismatch' }
}

# Stage the same file set the game compiles.
$work = Join-Path ([IO.Path]::GetTempPath()) "choomcoop-scriptcheck"
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
$src = Join-Path $work 'src'
New-Item -ItemType Directory -Force "$src\ChoomCoop\Protocol", "$src\CyberpunkMP" | Out-Null
Copy-Item -Recurse "$repo\Mods\ChoomCoop\Scripts\*" "$src\ChoomCoop"
Copy-Item "$repo\Mods\ChoomCoop\Shared\Protocol\*.reds" "$src\ChoomCoop\Protocol"
Copy-Item -Recurse "$repo\code\assets\redscript\*" "$src\CyberpunkMP"
foreach ($p in 'ArchiveXL', 'Codeware', 'TweakXL') {
    $s = Join-Path $Game "red4ext\plugins\$p\Scripts"
    if (Test-Path $s) { New-Item -ItemType Directory -Force "$src\$p" | Out-Null; Copy-Item -Recurse "$s\*" "$src\$p" }
    else { Write-Warning "$p scripts not installed in the game; dependent code may fail to resolve." }
}
Get-ChildItem -Recurse $src -File | Where-Object Extension -ne '.reds' | Remove-Item

$count = (Get-ChildItem -Recurse $src -Filter *.reds).Count
Write-Host "Compiling $count scripts against $bundle"
$out = & $cli compile -s $src -b $bundle -o (Join-Path $work 'final.redscripts') 2>&1 | Out-String
Remove-Item -Recurse -Force $work
if ($out -match 'ERROR') {
    Write-Host $out
    Write-Host 'Script check FAILED' -ForegroundColor Red
    exit 1
}
Write-Host 'Script check passed' -ForegroundColor Green
exit 0
