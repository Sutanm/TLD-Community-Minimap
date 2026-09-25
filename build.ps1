param(
    [string]$GameDirectory = ""
)

$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($GameDirectory)) {
    $GameDirectory = Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\TheLongDark'
}

$gameExecutable = Join-Path $GameDirectory 'tld.exe'
$modSettings = Join-Path $GameDirectory 'Mods\ModSettings.dll'
if (-not (Test-Path -LiteralPath $gameExecutable)) {
    throw "The Long Dark was not found at: $GameDirectory"
}
if (-not (Test-Path -LiteralPath $modSettings)) {
    throw "ModSettings.dll was not found at: $modSettings"
}

dotnet build (Join-Path $projectDirectory 'CommunityMinimap.csproj') `
    --configuration Release `
    -p:GameDirectory="$GameDirectory"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

$outputAssembly = Join-Path $projectDirectory 'bin\Release\net6.0\CommunityMinimap.dll'
$modsDirectory = Join-Path $GameDirectory 'Mods'
Copy-Item -LiteralPath $outputAssembly -Destination (Join-Path $modsDirectory 'CommunityMinimap.dll') -Force
Write-Host "Built and installed: $outputAssembly"

