[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install the .NET 10 SDK x64 from https://dotnet.microsoft.com/download/dotnet/10.0 and run this script again.'
}

function Invoke-Dotnet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE. Arguments: $($Arguments -join ' ')"
    }
}

Invoke-Dotnet -Arguments @('--info')
Invoke-Dotnet -Arguments @('restore', 'src/QuadDesk/QuadDesk.csproj', '-r', 'win-x64', '--configfile', 'NuGet.Config')
Invoke-Dotnet -Arguments @('restore', 'tests/QuadDesk.Tests/QuadDesk.Tests.csproj', '--configfile', 'NuGet.Config')
Invoke-Dotnet -Arguments @('build', 'src/QuadDesk/QuadDesk.csproj', '-c', $Configuration, '-r', 'win-x64', '--no-restore')
Invoke-Dotnet -Arguments @('test', 'tests/QuadDesk.Tests/QuadDesk.Tests.csproj', '-c', $Configuration, '--no-restore', '--logger', 'trx;LogFileName=QuadDesk.Tests.trx', '--results-directory', 'artifacts/test-results')

Write-Host 'Build and unit tests completed successfully.'
Write-Host 'Run the Windows 11 UI smoke test separately.'
