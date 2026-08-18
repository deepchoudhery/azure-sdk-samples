#Requires -Version 5.1
<#
.SYNOPSIS
    Resets every sample repository back to its committed pre-migration baseline.

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

    if (-not (Test-Path (Join-Path $path '.git'))) {
        Write-Warning "$sample is not a git repository; skipping."
        continue
    }

    if ($PSCmdlet.ShouldProcess($sample, 'git reset --hard && git clean -xfd')) {
        & git -C $path reset --hard HEAD | Out-Null
        & git -C $path clean -xfd | Out-Null
        Write-Host "$sample reset to baseline." -ForegroundColor Green
    }
}
