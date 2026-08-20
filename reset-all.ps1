#Requires -Version 5.1
<#
.SYNOPSIS
    Resets every sample directory back to its committed pre-migration baseline.

.DESCRIPTION
    Run this between skill runs so each test starts from an identical clean tree.
    Discards all local changes and removes untracked files, including build output.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$samples = @('keyvault-sample', 'servicebus-sample', 'storage-sample')

foreach ($sample in $samples) {
    $path = Join-Path $root $sample

    if (-not (Test-Path $path)) {
        Write-Warning "$sample was not found under $root; skipping."
        continue
    }

    if ($PSCmdlet.ShouldProcess($sample, 'git checkout && git clean -xfd')) {
        & git -C $root checkout -- $sample | Out-Null
        & git -C $root clean -xfd -- $sample | Out-Null
        Write-Host "$sample reset to baseline." -ForegroundColor Green
    }
}
