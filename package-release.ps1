param(
    [string]$GameDirectory = "",
    [string]$Version = "0.9.0",
    [switch]$WithoutMaps
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$distDirectory = Join-Path $projectDirectory 'dist'

if ([string]::IsNullOrWhiteSpace($GameDirectory)) {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\TheLongDark'),
        (Join-Path $env:ProgramFiles 'Steam\steamapps\common\TheLongDark')
    )
    foreach ($drive in @('D', 'E', 'F', 'G')) {
        $candidates += "${drive}:\Steam\steamapps\common\TheLongDark"
        $candidates += "${drive}:\SteamLibrary\steamapps\common\TheLongDark"
        $candidates += "${drive}:\Program Files (x86)\Steam\steamapps\common\TheLongDark"
    }
    $GameDirectory = $candidates |
        Where-Object { Test-Path -LiteralPath (Join-Path $_ 'tld.exe') } |
        Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($GameDirectory) -or
    -not (Test-Path -LiteralPath (Join-Path $GameDirectory 'tld.exe'))) {
    throw 'The Long Dark was not found. Pass -GameDirectory <path> explicitly.'
}

& (Join-Path $projectDirectory 'build.ps1') -GameDirectory $GameDirectory -SkipInstall
if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE." }

$assembly = Join-Path $projectDirectory 'bin\Release\net6.0\CommunityMinimap.dll'
$calibrations = Join-Path $projectDirectory 'calibrations.json'
$bindings = Join-Path $projectDirectory 'probe-maps.json'
foreach ($required in @($assembly, $calibrations, $bindings)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required release file missing: $required" }
}
Get-Content -LiteralPath $calibrations -Raw -Encoding UTF8 | ConvertFrom-Json | Out-Null
Get-Content -LiteralPath $bindings -Raw -Encoding UTF8 | ConvertFrom-Json | Out-Null

New-Item -ItemType Directory -Path $distDirectory -Force | Out-Null
$stageRoot = Join-Path $distDirectory ('.stage-' + [Guid]::NewGuid().ToString('N'))
$packageName = "CommunityMinimap-$Version"
$packageRoot = Join-Path $stageRoot $packageName
$modsRoot = Join-Path $packageRoot 'Mods'
$dataRoot = Join-Path $modsRoot 'CommunityMinimap'
$mapsRoot = Join-Path $dataRoot 'maps'
New-Item -ItemType Directory -Path $mapsRoot -Force | Out-Null

Copy-Item -LiteralPath $assembly -Destination (Join-Path $modsRoot 'CommunityMinimap.dll')
Copy-Item -LiteralPath $calibrations -Destination $dataRoot
Copy-Item -LiteralPath $bindings -Destination $dataRoot
foreach ($doc in @('README.md', 'README.en.md', 'CHANGELOG.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $projectDirectory $doc) -Destination $packageRoot
}

if (-not $WithoutMaps) {
    $installedMaps = Join-Path $GameDirectory 'Mods\CommunityMinimap\maps'
    $sourceManifest = Get-Content -LiteralPath (Join-Path $projectDirectory 'tools\map-sources.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $fileNames = @($sourceManifest.maps.output | Sort-Object -Unique)
    foreach ($fileName in $fileNames) {
        if ([System.IO.Path]::GetFileName([string]$fileName) -ne [string]$fileName) {
            throw "Unsafe map filename in manifest: $fileName"
        }
        $source = Join-Path $installedMaps $fileName
        if (-not (Test-Path -LiteralPath $source)) { throw "Required map image missing: $source" }
        Copy-Item -LiteralPath $source -Destination $mapsRoot
    }
}

$manifest = [ordered]@{
    mod = 'Community HUD Map'
    version = $Version
    target = 'The Long Dark 2.55 / Unity 6'
    packagedUtc = [DateTime]::UtcNow.ToString('o')
    includesThirdPartyMaps = (-not $WithoutMaps)
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot 'release-manifest.json') -Encoding UTF8

$hashLines = Get-ChildItem -LiteralPath $packageRoot -File -Recurse |
    Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($packageRoot.Length + 1).Replace('\', '/')
        "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
    }
$hashLines | Set-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.txt') -Encoding ASCII

$archivePath = Join-Path $distDirectory "$packageName.zip"
$resolvedDist = [System.IO.Path]::GetFullPath($distDirectory).TrimEnd('\') + '\'
$resolvedArchive = [System.IO.Path]::GetFullPath($archivePath)
if (-not $resolvedArchive.StartsWith($resolvedDist, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to replace archive outside dist: $resolvedArchive"
}
if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
Compress-Archive -LiteralPath $packageRoot -DestinationPath $archivePath -CompressionLevel Optimal

$resolvedStage = [System.IO.Path]::GetFullPath($stageRoot)
if (-not $resolvedStage.StartsWith($resolvedDist, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean staging directory outside dist: $resolvedStage"
}
Remove-Item -LiteralPath $stageRoot -Recurse -Force

Write-Host "Release package: $archivePath"
Write-Host ("sha256 " + (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant())
