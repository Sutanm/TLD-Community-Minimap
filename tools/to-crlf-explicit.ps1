# Convert a NAMED list of text files to CRLF.
#
# Deliberately not a general-purpose tool. An earlier script walked the repository and
# decided per path whether to convert, got git's `binary` attribute semantics backwards
# twice, and corrupted eight tracked PNGs on two separate runs (both restored from git).
# The lesson taken from that is not "write the guard more carefully" but "do not batch
# over a mixed tree at all": this one converts only the paths listed below, which are
# text files created during the documentation rewrite, and it re-reads each one to
# confirm it still parses as text before writing.
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location ..

$targets = @(
    'README.md',
    '.gitattributes',
    'docs\INDEX.md',
    'docs\STATUS.md',
    'docs\ARCHITECTURE.md',
    'docs\PITFALLS.md',
    'docs\PERFORMANCE.md',
    'docs\SETTINGS.md',
    'docs\SCENE-TEST-CHECKLIST.md',
    'tools\map-status.ps1',
    'tools\split-modeentry.py',
    'tools\verify-split-equivalence.py',
    'tools\to-crlf-explicit.ps1'
)

foreach ($rel in $targets) {
    if (-not (Test-Path -LiteralPath $rel)) {
        Write-Host "skip (absent): $rel"
        continue
    }

    $bytes = [System.IO.File]::ReadAllBytes($rel)

    # Refuse anything containing a NUL byte: that is a binary file whatever its extension
    # claims, and rewriting its line endings would destroy it.
    if ($bytes -contains 0) {
        Write-Host "REFUSED (binary content): $rel"
        continue
    }

    $bare = 0
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 10 -and ($i -eq 0 -or $bytes[$i - 1] -ne 13)) { $bare++ }
    }
    if ($bare -eq 0) {
        Write-Host "already CRLF: $rel"
        continue
    }

    $text = [System.IO.File]::ReadAllText($rel)
    $text = [regex]::Replace($text, "`r?`n", "`r`n")
    [System.IO.File]::WriteAllText($rel, $text)
    Write-Host "converted: $rel ($bare bare LF)"
}

Write-Host ""
Write-Host "done."
