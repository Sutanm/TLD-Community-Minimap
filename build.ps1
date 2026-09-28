param(
    [string]$GameDirectory = "",
    [switch]$SkipInstall
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
if (-not (Test-Path -LiteralPath $gameExecutable)) {
    throw "The Long Dark was not found at: $GameDirectory"
}
if (-not (Test-Path -LiteralPath $modSettings)) {
    throw "ModSettings.dll was not found at: $modSettings"
}

# Overwriting the DLL of a running game does nothing useful and leaves the process with a
# half-loaded mod, so refuse rather than copy into a live install. The name is matched exactly:
# a wildcard like 'tld*' also matches TLDConsole.
$running = @(Get-Process -Name 'TheLongDark' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    throw "The Long Dark is running (PID $($running[0].Id)). Close it before building and installing."
}

$outputAssembly = Join-Path $projectDirectory 'bin\Release\net6.0\CommunityMinimap.dll'
$modsDirectory = Join-Path $GameDirectory 'Mods'
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

# Verify what landed on disk is what was built; a locked or partially written file would
# otherwise only show up as a mysterious runtime failure.
$sourceHash = (Get-FileHash -LiteralPath $outputAssembly -Algorithm SHA256).Hash
$installedHash = (Get-FileHash -LiteralPath $installedAssembly -Algorithm SHA256).Hash
if ($sourceHash -ne $installedHash) {
    throw "Installed DLL does not match the build output ($installedHash vs $sourceHash)."
}

Write-Host "Built and installed: $installedAssembly"
Write-Host "  sha256 $installedHash"
