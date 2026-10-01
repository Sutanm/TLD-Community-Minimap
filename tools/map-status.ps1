<#
.SYNOPSIS
只读检查22个地图定义的原版捕获、社区图片、校准和部署同步状态。

.EXAMPLE
.\tools\map-status.ps1

.EXAMPLE
.\tools\map-status.ps1 -VerifyHashes

.EXAMPLE
.\tools\map-status.ps1 -PassThru | Where-Object 校准 -eq '未校准'
#>
[CmdletBinding()]
param(
    [string]$Workspace = (Split-Path -Parent $PSScriptRoot),
    [string]$GameDirectory = "",
    [switch]$VerifyHashes,
    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
    if ([string]::IsNullOrWhiteSpace($GameDirectory)) {
        throw "未找到 The Long Dark；请传入 -GameDirectory <路径>。"
    }
}

function Get-FileFingerprint {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    $item = Get-Item -LiteralPath $Path
    [pscustomobject]@{
        Length = $item.Length
        Hash = if ($VerifyHashes) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash } else { $null }
    }
}

function Test-FilesMatch {
    param([string]$Left, [string]$Right)

    $a = Get-FileFingerprint $Left
    $b = Get-FileFingerprint $Right
    if ($null -eq $a -or $null -eq $b) {
        return $false
    }
    if ($a.Length -ne $b.Length) {
        return $false
    }
    return -not $VerifyHashes -or $a.Hash -eq $b.Hash
}

function Get-ImageSize {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    try {
        Add-Type -AssemblyName System.Drawing.Common -ErrorAction SilentlyContinue
        $image = [System.Drawing.Image]::FromFile($Path)
        try {
            return [pscustomobject]@{ Width = $image.Width; Height = $image.Height }
        }
        finally {
            $image.Dispose()
        }
    }
    catch {
        Write-Warning "无法读取图片尺寸：$Path ($($_.Exception.Message))"
        return $null
    }
}

function Read-MapCatalog {
    param([string]$Path)

    $source = Get-Content -LiteralPath $Path -Raw
    $pattern = 'Add\s*\(\s*new\s+MapDefinition\s*\(\s*"(?<id>[^"]+)"\s*,\s*"(?<name>[^"]+)"\s*,\s*"(?<file>[^"]+)"\s*,\s*CalibrationProfile\.[A-Za-z0-9_]+\s*,(?<scenes>.*?)\)\s*\)\s*;'
    $matches = [regex]::Matches($source, $pattern, [Text.RegularExpressions.RegexOptions]::Singleline)
    if ($matches.Count -eq 0) {
        throw "未能从 $Path 解析任何 MapDefinition。"
    }

    foreach ($match in $matches) {
        $scenes = [regex]::Matches($match.Groups['scenes'].Value, '"([^"]+)"') |
            ForEach-Object { $_.Groups[1].Value }
        [pscustomobject]@{
            MapId = $match.Groups['id'].Value
            DisplayName = $match.Groups['name'].Value
            FileName = $match.Groups['file'].Value
            Scenes = @($scenes)
        }
    }
}

function Read-Calibrations {
    param([string]$Path)

    $result = @{}
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $result
    }

    $json = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    foreach ($map in @($json.maps)) {
        $result[$map.mapId] = $map
    }
    return $result
}

$catalogPath = Join-Path $Workspace "MapDefinition.cs"
$sourceManifestPath = Join-Path $Workspace "tools\map-sources.json"
$repoCalibrationPath = Join-Path $Workspace "calibrations.json"
$preparedDirectory = Join-Path $Workspace "prepared-maps"
$modDataDirectory = Join-Path $GameDirectory "Mods\CommunityMinimap"
$gameMapDirectory = Join-Path $modDataDirectory "maps"
$capturedDirectory = Join-Path $modDataDirectory "captured"
$gameCalibrationPath = Join-Path $modDataDirectory "calibrations.json"

if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
    throw "找不到地图目录文件：$catalogPath"
}
if (-not (Test-Path -LiteralPath $sourceManifestPath -PathType Leaf)) {
    throw "找不到社区地图来源清单：$sourceManifestPath"
}

$catalog = @(Read-MapCatalog $catalogPath)
$sourceManifest = Get-Content -LiteralPath $sourceManifestPath -Raw | ConvertFrom-Json
$sourceMaps = @($sourceManifest.maps)
$sourceByOutput = @{}
foreach ($sourceMap in $sourceMaps) {
    $sourceByOutput[$sourceMap.output] = $sourceMap
}
$calibrations = Read-Calibrations $repoCalibrationPath
$rows = foreach ($map in $catalog) {
    $repoMapPath = Join-Path $preparedDirectory $map.FileName
    $gameMapPath = Join-Path $gameMapDirectory $map.FileName
    $calibration = if ($calibrations.ContainsKey($map.MapId)) { $calibrations[$map.MapId] } else { $null }
    $sourceMap = if ($sourceByOutput.ContainsKey($map.FileName)) { $sourceByOutput[$map.FileName] } else { $null }
    $mapKind = if ($null -ne $sourceMap) { [string]$sourceMap.kind } else { "未知" }
    $communitySize = Get-ImageSize $repoMapPath

    $capture = $null
    if ($mapKind -eq "region") {
        foreach ($scene in $map.Scenes) {
            $candidate = Join-Path $capturedDirectory "$scene.png"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $capture = [pscustomobject]@{
                    Scene = $scene
                    Path = $candidate
                    FramingPath = "$candidate.framing"
                    Size = Get-ImageSize $candidate
                }
                break
            }
        }
    }

    $captureState = if ($mapKind -eq "region") { "缺少" } else { "不适用" }
    if ($mapKind -eq "region" -and $null -ne $capture) {
        $is2048 = $null -ne $capture.Size -and $capture.Size.Width -eq 2048 -and $capture.Size.Height -eq 2048
        $hasFraming = Test-Path -LiteralPath $capture.FramingPath -PathType Leaf
        if ($is2048 -and $hasFraming) { $captureState = "完成" }
        elseif (-not $is2048 -and -not $hasFraming) { $captureState = "尺寸/构图异常" }
        elseif (-not $is2048) { $captureState = "尺寸异常" }
        else { $captureState = "缺.framing" }
    }

    $communityState = if (-not (Test-Path -LiteralPath $repoMapPath -PathType Leaf)) {
        "仓库缺少"
    }
    elseif (-not (Test-Path -LiteralPath $gameMapPath -PathType Leaf)) {
        "游戏缺少"
    }
    elseif (Test-FilesMatch $repoMapPath $gameMapPath) {
        "已同步"
    }
    else {
        if ($VerifyHashes) { "内容不同" } else { "大小不同" }
    }

    $calibrationState = if ($null -eq $calibration) {
        "未校准"
    }
    else {
        $pointCount = @($calibration.points).Count
        $dimensionMatches = $null -ne $communitySize -and
            $communitySize.Width -eq [int]$calibration.imageWidth -and
            $communitySize.Height -eq [int]$calibration.imageHeight
        if ($dimensionMatches) { "$pointCount 点" } else { "$pointCount 点/尺寸不符" }
    }

    [pscustomobject]@{
        MapId = $map.MapId
        地图 = $map.DisplayName
        类型 = $mapKind
        主场景 = $map.Scenes[0]
        原版2048 = $captureState
        社区图片 = $communityState
        校准 = $calibrationState
    }
}

$assetRows = foreach ($sourceMap in $sourceMaps) {
    $repoMapPath = Join-Path $preparedDirectory $sourceMap.output
    $gameMapPath = Join-Path $gameMapDirectory $sourceMap.output
    $deployState = if (-not (Test-Path -LiteralPath $repoMapPath -PathType Leaf)) {
        "仓库缺少"
    }
    elseif (-not (Test-Path -LiteralPath $gameMapPath -PathType Leaf)) {
        "游戏缺少"
    }
    elseif (Test-FilesMatch $repoMapPath $gameMapPath) {
        "已同步"
    }
    else {
        if ($VerifyHashes) { "内容不同" } else { "大小不同" }
    }
    $bindings = @($catalog | Where-Object FileName -eq $sourceMap.output)
    [pscustomobject]@{
        类型 = [string]$sourceMap.kind
        来源图片 = [string]$sourceMap.source
        输出文件 = [string]$sourceMap.output
        部署 = $deployState
        运行时定义 = if ($bindings.Count) { ($bindings.MapId -join ", ") } else { "未接入" }
    }
}

$originalRegionCount = @($rows | Where-Object 类型 -eq "region").Count
$captureComplete = @($rows | Where-Object { $_.类型 -eq "region" -and $_.原版2048 -eq "完成" }).Count
$calibrated = @($rows | Where-Object { $_.校准 -match '^\d+ 点$' }).Count
$communityReady = @($assetRows | Where-Object 部署 -eq "已同步").Count
$runtimeBoundAssets = @($assetRows | Where-Object 运行时定义 -ne "未接入").Count
$unboundAssets = @($assetRows | Where-Object 运行时定义 -eq "未接入")
$calibrationSync = if (-not (Test-Path -LiteralPath $repoCalibrationPath -PathType Leaf)) {
    "仓库缺少"
}
elseif (-not (Test-Path -LiteralPath $gameCalibrationPath -PathType Leaf)) {
    "游戏缺少"
}
elseif (Test-FilesMatch $repoCalibrationPath $gameCalibrationPath) {
    "已同步"
}
else {
    if ($VerifyHashes) { "内容不同" } else { "大小不同" }
}

Write-Host "社区HUD地图 · 内容状态" -ForegroundColor Cyan
Write-Host "工作区：$Workspace"
Write-Host "游戏目录：$GameDirectory"
Write-Host "HUD场景定义：$($catalog.Count)（绑定 $runtimeBoundAssets / $($sourceMaps.Count) 张社区图片）"
Write-Host "原版区域2048：$captureComplete / $originalRegionCount"
Write-Host "社区校准：$calibrated / $($catalog.Count)"
Write-Host "社区图片同步：$communityReady / $($sourceMaps.Count)"
Write-Host "未接入社区图片：$($unboundAssets.Count)"
Write-Host "calibrations.json：$calibrationSync"
if (-not $VerifyHashes) {
    Write-Host "同步判定使用文件大小；加 -VerifyHashes 可进行 SHA256 严格校验。" -ForegroundColor DarkGray
}
if ($PassThru) {
    $rows
}
else {
    Write-Host ""
    $rows | Format-Table -AutoSize
    if ($unboundAssets.Count) {
        Write-Host "未接入运行时的社区图片：" -ForegroundColor Yellow
        $unboundAssets | Format-Table -AutoSize
    }
}
