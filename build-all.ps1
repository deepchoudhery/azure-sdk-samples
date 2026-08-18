#Requires -Version 5.1
<#
.SYNOPSIS
    Builds all three legacy Azure SDK samples and reports pass/fail per sample.

.DESCRIPTION
    Use this to confirm the pre-migration baseline is green, and again after running a
    migration skill to confirm the post-migration state still builds.
#>
[CmdletBinding()]
param(
    [switch]$Restore
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$samples = @(
    @{ Name = 'keyvault-sample';   Project = 'Contoso.Secrets.csproj' }
    @{ Name = 'servicebus-sample'; Project = 'Contoso.Ordering.csproj' }
    @{ Name = 'storage-sample';    Project = 'Contoso.Documents.csproj' }
)

$results = @()

foreach ($sample in $samples) {
    $projectPath = Join-Path $root (Join-Path $sample.Name $sample.Project)

    Write-Host ""
    Write-Host "=== $($sample.Name) ===" -ForegroundColor Cyan

    if ($Restore) {
        & dotnet restore $projectPath | Out-Null
    }

    $output = & dotnet build $projectPath --nologo -v q 2>&1
    $succeeded = $LASTEXITCODE -eq 0

    if ($succeeded) {
        Write-Host "build succeeded" -ForegroundColor Green
    }
    else {
        Write-Host "build FAILED" -ForegroundColor Red
        $output | Where-Object { $_ -match 'error|warning' } | ForEach-Object { Write-Host "  $_" }
    }

    $results += [pscustomobject]@{
        Sample  = $sample.Name
        Result  = if ($succeeded) { 'pass' } else { 'fail' }
    }
}

Write-Host ""
$results | Format-Table -AutoSize

if ($results.Result -contains 'fail') {
    exit 1
}

exit 0
