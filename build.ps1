param(
    [string]$GameDirectory = "",
    [switch]$SkipInstall,
    [switch]$CalibrationsOnly,
    [switch]$ProbeMapsOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($GameDirectory)) {
    # The library is not always on C:, so probe the usual Steam roots instead of assuming the
    # one this project happened to be set up on.
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
    if ([string]::IsNullOrWhiteSpace($GameDirectory)) {
        throw "The Long Dark was not found. Pass -GameDirectory <path> explicitly."
    }
    Write-Host "Using game directory: $GameDirectory"
}

$gameExecutable = Join-Path $GameDirectory 'tld.exe'
$modSettings = Join-Path $GameDirectory 'Mods\ModSettings.dll'
$modsDirectory = Join-Path $GameDirectory 'Mods'
$sourceCalibrations = Join-Path $projectDirectory 'calibrations.json'
$sourceProbeMaps = Join-Path $projectDirectory 'probe-maps.json'
$preparedMapsDirectory = Join-Path $projectDirectory 'prepared-maps'
$modDataDirectory = Join-Path $modsDirectory 'CommunityMinimap'
$installedCalibrations = Join-Path $modDataDirectory 'calibrations.json'
$installedProbeMaps = Join-Path $modDataDirectory 'probe-maps.json'
$installedMapsDirectory = Join-Path $modDataDirectory 'maps'
if (-not (Test-Path -LiteralPath $gameExecutable)) {
    throw "The Long Dark was not found at: $GameDirectory"
}

# calibrations.json is runtime data rather than an embedded resource. Validate it before either
# installation path so a partial editor save can never replace the last working copy.
try {
    # Windows PowerShell 5 defaults Get-Content to the active ANSI code page for UTF-8 files
    # without a BOM. Chinese labels can then consume an adjacent ASCII quote as part of a DBCS
    # sequence, making otherwise valid JSON appear truncated. The repository stores this file as
    # UTF-8, so make the decoding explicit.
    Get-Content -LiteralPath $sourceCalibrations -Raw -Encoding UTF8 | ConvertFrom-Json | Out-Null
}
catch {
    throw "Invalid calibrations.json; nothing was installed. $($_.Exception.Message)"
}

try {
    $probeMapData = Get-Content -LiteralPath $sourceProbeMaps -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($probeMapData.formatVersion -ne 1) {
        throw "Unsupported formatVersion '$($probeMapData.formatVersion)'."
    }
}
catch {
    throw "Invalid probe-maps.json; nothing was installed. $($_.Exception.Message)"
}

if ($CalibrationsOnly -and $ProbeMapsOnly) {
    throw 'Choose either -CalibrationsOnly or -ProbeMapsOnly, not both.'
}

# Calibration data is deliberately hot-reloadable. This path is safe while the game is running:
# it never touches the loaded assembly and the mod notices the JSON timestamp on its next update.
if ($CalibrationsOnly) {
    New-Item -ItemType Directory -Path $modDataDirectory -Force | Out-Null
    Copy-Item -LiteralPath $sourceCalibrations -Destination $installedCalibrations -Force
    # Copy-Item preserves the source timestamp. Re-syncing unchanged JSON during a test session
    # must still wake the runtime watcher, so stamp the installed copy after it is complete.
    [System.IO.File]::SetLastWriteTimeUtc($installedCalibrations, [DateTime]::UtcNow)
    $sourceCalibrationHash = (Get-FileHash -LiteralPath $sourceCalibrations -Algorithm SHA256).Hash
    $installedCalibrationHash = (Get-FileHash -LiteralPath $installedCalibrations -Algorithm SHA256).Hash
    if ($sourceCalibrationHash -ne $installedCalibrationHash) {
        throw "Installed calibrations.json does not match the source file."
    }
    Write-Host "Hot-synced calibrations: $installedCalibrations"
    Write-Host "  sha256 $installedCalibrationHash"
    return
}

# Probe bindings and their images are runtime data. The assembly watcher reloads both after the
# JSON timestamp changes, so crop/binding iterations are safe while the game is running.
if ($ProbeMapsOnly) {
    New-Item -ItemType Directory -Path $installedMapsDirectory -Force | Out-Null
    foreach ($probeMap in @($probeMapData.maps)) {
        $fileName = [string]$probeMap.fileName
        if ([string]::IsNullOrWhiteSpace($fileName) -or
            [System.IO.Path]::GetFileName($fileName) -ne $fileName) {
            throw "Invalid probe image name: '$fileName'."
        }
        $sourceImage = Join-Path $preparedMapsDirectory $fileName
        if (-not (Test-Path -LiteralPath $sourceImage)) {
            throw "Prepared probe image was not found: $sourceImage"
        }
        $installedImage = Join-Path $installedMapsDirectory $fileName
        $temporaryImage = "$installedImage.$PID.tmp"
        Copy-Item -LiteralPath $sourceImage -Destination $temporaryImage -Force
        Move-Item -LiteralPath $temporaryImage -Destination $installedImage -Force
    }

    New-Item -ItemType Directory -Path $modDataDirectory -Force | Out-Null
    $temporaryProbeMaps = "$installedProbeMaps.$PID.tmp"
    Copy-Item -LiteralPath $sourceProbeMaps -Destination $temporaryProbeMaps -Force
    Move-Item -LiteralPath $temporaryProbeMaps -Destination $installedProbeMaps -Force
    [System.IO.File]::SetLastWriteTimeUtc($installedProbeMaps, [DateTime]::UtcNow)
    Write-Host "Hot-synced $(@($probeMapData.maps).Count) probe bindings and images."
    Write-Host "  $installedProbeMaps"
    return
}

if (-not (Test-Path -LiteralPath $modSettings)) {
    throw "ModSettings.dll was not found at: $modSettings"
}

# Overwriting the DLL of a running game does nothing useful and leaves the process with a
# half-loaded mod, so refuse rather than copy into a live install.
#
# The executable is tld.exe, so the process is named 'tld' - NOT 'TheLongDark'. Checking the
# latter silently never matched and let an install run into the locked file, which surfaced only
# as an IOException from Copy-Item. The names are matched without wildcards on purpose: 'tld*'
# would also catch TLDConsole, which is a separate tool that does not lock the mod.
$running = @(Get-Process -Name @('tld', 'TheLongDark') -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    $names = ($running | ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }) -join ', '
    throw "The Long Dark is running: $names. Close it before building and installing."
}

$outputAssembly = Join-Path $projectDirectory 'bin\Release\net6.0\CommunityMinimap.dll'
$installedAssembly = Join-Path $modsDirectory 'CommunityMinimap.dll'

# Delete the previous output first. A failed build leaves the last good DLL in place, and
# checking only the exit code is not enough: a stale file plus a copy step is how a broken
# build silently ships. With the file gone, "the copy succeeded" proves the build produced it.
if (Test-Path -LiteralPath $outputAssembly) {
    Remove-Item -LiteralPath $outputAssembly -Force
}

$buildStartedUtc = [DateTime]::UtcNow
dotnet build (Join-Path $projectDirectory 'CommunityMinimap.csproj') `
    --configuration Release `
    -p:GameDirectory="$GameDirectory"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE; nothing was installed."
}
if (-not (Test-Path -LiteralPath $outputAssembly)) {
    throw "dotnet build reported success but produced no assembly at $outputAssembly; nothing was installed."
}

# The assembly must be at least as new as every input. Comparing against the wall clock instead
# would be wrong: an up-to-date incremental build copies the previous assembly into place and
# MSBuild preserves its timestamp, so a perfectly good build can look "old".
$newestSource = @(Get-ChildItem -LiteralPath $projectDirectory -Filter '*.cs' -File) +
    @(Get-Item -LiteralPath (Join-Path $projectDirectory 'CommunityMinimap.csproj')) |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if ($null -ne $newestSource -and
    (Get-Item -LiteralPath $outputAssembly).LastWriteTimeUtc -lt $newestSource.LastWriteTimeUtc) {
    throw ("The assembly at $outputAssembly is older than $($newestSource.Name); " +
           "the build did not pick up the latest source. Nothing was installed.")
}

if ($SkipInstall) {
    Write-Host "Built (not installed): $outputAssembly"
    return
}

# Keep the DLL that is about to be replaced, so a bad build can be rolled back without a
# rebuild. The mod directory is used rather than the Mods root so the backups travel with the
# mod's own folder and are never mistaken for a loadable mod.
if (Test-Path -LiteralPath $installedAssembly) {
    $backupDirectory = Join-Path $GameDirectory 'Mods\CommunityMinimap\backups'
    New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
    $stamp = (Get-Item -LiteralPath $installedAssembly).LastWriteTime.ToString('yyyyMMdd_HHmmss')
    $backupPath = Join-Path $backupDirectory "CommunityMinimap_$stamp.dll"
    if (-not (Test-Path -LiteralPath $backupPath)) {
        Copy-Item -LiteralPath $installedAssembly -Destination $backupPath -Force
        Write-Host "Backed up previous build: $backupPath"
    }
}

Copy-Item -LiteralPath $outputAssembly -Destination $installedAssembly -Force
New-Item -ItemType Directory -Path $modDataDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceCalibrations -Destination $installedCalibrations -Force
Copy-Item -LiteralPath $sourceProbeMaps -Destination $installedProbeMaps -Force

# Verify what landed on disk is what was built; a locked or partially written file would
# otherwise only show up as a mysterious runtime failure.
$sourceHash = (Get-FileHash -LiteralPath $outputAssembly -Algorithm SHA256).Hash
$installedHash = (Get-FileHash -LiteralPath $installedAssembly -Algorithm SHA256).Hash
if ($sourceHash -ne $installedHash) {
    throw "Installed DLL does not match the build output ($installedHash vs $sourceHash)."
}
$sourceCalibrationHash = (Get-FileHash -LiteralPath $sourceCalibrations -Algorithm SHA256).Hash
$installedCalibrationHash = (Get-FileHash -LiteralPath $installedCalibrations -Algorithm SHA256).Hash
if ($sourceCalibrationHash -ne $installedCalibrationHash) {
    throw "Installed calibrations.json does not match the source file."
}
$sourceProbeHash = (Get-FileHash -LiteralPath $sourceProbeMaps -Algorithm SHA256).Hash
$installedProbeHash = (Get-FileHash -LiteralPath $installedProbeMaps -Algorithm SHA256).Hash
if ($sourceProbeHash -ne $installedProbeHash) {
    throw "Installed probe-maps.json does not match the source file."
}

Write-Host "Built and installed: $installedAssembly"
Write-Host "  sha256 $installedHash"
Write-Host "Installed calibrations: $installedCalibrations"
Write-Host "  sha256 $installedCalibrationHash"
Write-Host "Installed probe bindings: $installedProbeMaps"
Write-Host "  sha256 $installedProbeHash"
