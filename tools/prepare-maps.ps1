[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,

    [string]$DestinationDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) "prepared-maps"),

    [ValidateRange(70, 100)]
    [int]$JpegQuality = 95,

    [switch]$Force,

    [switch]$SkipHashValidation
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.Drawing

function Get-JpegEncoder {
    foreach ($encoder in [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders()) {
        if ($encoder.MimeType -eq "image/jpeg") {
            return $encoder
        }
    }

    throw "The System.Drawing JPEG encoder is unavailable."
}

function Get-ImageSize {
    param([Parameter(Mandatory = $true)][string]$Path)

    $image = [System.Drawing.Image]::FromFile($Path)
    try {
        return [PSCustomObject]@{
            Width = $image.Width
            Height = $image.Height
        }
    }
    finally {
        $image.Dispose()
    }
}

$manifestPath = Join-Path $PSScriptRoot "map-sources.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$sourceRoot = (Resolve-Path -LiteralPath $SourceDirectory).Path

if (-not (Test-Path -LiteralPath $DestinationDirectory)) {
    New-Item -ItemType Directory -Path $DestinationDirectory | Out-Null
}
$destinationRoot = (Resolve-Path -LiteralPath $DestinationDirectory).Path

if ($sourceRoot.TrimEnd('\') -eq $destinationRoot.TrimEnd('\')) {
    throw "SourceDirectory and DestinationDirectory must be different."
}

$validatedMaps = [System.Collections.Generic.List[object]]::new()
$outputNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($map in $manifest.maps) {
    if (-not $outputNames.Add([string]$map.output)) {
        throw "Duplicate output name in the manifest: $($map.output)"
    }

    $sourcePath = Join-Path $sourceRoot $map.source
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Missing source image: $sourcePath"
    }

    $sourceSize = Get-ImageSize -Path $sourcePath
    if ($sourceSize.Width -ne [int]$map.width -or $sourceSize.Height -ne [int]$map.height) {
        throw "Unexpected dimensions for '$($map.source)': $($sourceSize.Width)x$($sourceSize.Height), expected $($map.width)x$($map.height)."
    }

    $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if (-not $SkipHashValidation -and $sourceHash -ne $map.sha256) {
        throw "SHA256 mismatch for '$($map.source)'. The file is not the expected source revision."
    }

    $crop = if ($null -ne $map.PSObject.Properties["crop"]) { $map.crop } else { $null }
    if ($null -ne $crop) {
        if ($crop.Count -ne 4) {
            throw "Crop rectangle for '$($map.source)' must contain four values."
        }

        $left = [int]$crop[0]
        $top = [int]$crop[1]
        $right = [int]$crop[2]
        $bottom = [int]$crop[3]
        if ($left -lt 0 -or $top -lt 0 -or $right -le $left -or $bottom -le $top -or
            $right -gt $sourceSize.Width -or $bottom -gt $sourceSize.Height) {
            throw "Invalid crop rectangle for '$($map.source)'."
        }
    }

    $outputPath = Join-Path $destinationRoot $map.output
    if ((Test-Path -LiteralPath $outputPath) -and -not $Force) {
        throw "Output already exists: $outputPath. Use -Force to replace prepared files."
    }

    $validatedMaps.Add([PSCustomObject]@{
        Definition = $map
        SourcePath = $sourcePath
        SourceSize = $sourceSize
        SourceHash = $sourceHash
        Crop = $crop
        OutputPath = $outputPath
    })
}

$jpegEncoder = Get-JpegEncoder
$encoderParameters = [System.Drawing.Imaging.EncoderParameters]::new(1)
$qualityParameter = [System.Drawing.Imaging.EncoderParameter]::new(
    [System.Drawing.Imaging.Encoder]::Quality,
    [long]$JpegQuality
)
$encoderParameters.Param[0] = $qualityParameter
$results = [System.Collections.Generic.List[object]]::new()

try {
    $index = 0
    foreach ($validatedMap in $validatedMaps) {
        $index++
        $map = $validatedMap.Definition
        Write-Progress -Activity "Preparing community maps" -Status $map.output -PercentComplete (($index / $manifest.maps.Count) * 100)

        $sourcePath = $validatedMap.SourcePath
        $sourceSize = $validatedMap.SourceSize
        $sourceHash = $validatedMap.SourceHash
        $crop = $validatedMap.Crop
        $outputPath = $validatedMap.OutputPath
        $temporaryPath = "$($validatedMap.OutputPath).$PID.tmp"
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue

        try {
            if ($null -ne $crop -and $crop.Count -eq 4) {
                $left = [int]$crop[0]
                $top = [int]$crop[1]
                $right = [int]$crop[2]
                $bottom = [int]$crop[3]

                $sourceBitmap = [System.Drawing.Bitmap]::FromFile($sourcePath)
                try {
                    $rectangle = [System.Drawing.Rectangle]::FromLTRB($left, $top, $right, $bottom)
                    $croppedBitmap = $sourceBitmap.Clone($rectangle, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
                    try {
                        $croppedBitmap.Save($temporaryPath, $jpegEncoder, $encoderParameters)
                    }
                    finally {
                        $croppedBitmap.Dispose()
                    }
                }
                finally {
                    $sourceBitmap.Dispose()
                }
            }
            else {
                Copy-Item -LiteralPath $sourcePath -Destination $temporaryPath
            }

            Move-Item -LiteralPath $temporaryPath -Destination $outputPath -Force
        }
        finally {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        }

        $outputSize = Get-ImageSize -Path $outputPath
        $results.Add([PSCustomObject]@{
            kind = $map.kind
            source = $map.source
            output = $map.output
            sourceSha256 = $sourceHash
            outputSha256 = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
            width = $outputSize.Width
            height = $outputSize.Height
            crop = $crop
        })
    }
}
finally {
    Write-Progress -Activity "Preparing community maps" -Completed
    $qualityParameter.Dispose()
    $encoderParameters.Dispose()
}

$receipt = [PSCustomObject]@{
    formatVersion = 1
    sourcePack = $manifest.sourcePack
    sourceDirectory = $sourceRoot
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    jpegQuality = $JpegQuality
    maps = $results
}

$receiptPath = Join-Path $destinationRoot "prepared-maps.json"
$receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $receiptPath -Encoding UTF8

Write-Host "Prepared $($results.Count) maps in: $destinationRoot"
Write-Host "Receipt: $receiptPath"
