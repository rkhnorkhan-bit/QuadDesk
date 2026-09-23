[CmdletBinding()]
param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Get-ProjectVersion {
    $propsPath = Join-Path $PSScriptRoot 'Directory.Build.props'
    [xml]$props = Get-Content -LiteralPath $propsPath
    $version = [string]$props.Project.PropertyGroup.Version

    if ([string]::IsNullOrWhiteSpace($version)) {
        throw 'Version was not found in Directory.Build.props.'
    }

    return $version.Trim()
}

function Find-Iscc {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @()

    if (${env:ProgramFiles(x86)}) {
        $candidates += (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
    }
    if ($env:ProgramFiles) {
        $candidates += (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    }
    if ($env:LOCALAPPDATA) {
        $candidates += (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    }

    return ($candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1)
}

function Invoke-DotnetPublish {
    param(
        [Parameter(Mandatory = $true)][string]$OutputDirectory
    )

    & dotnet publish 'src/QuadDesk/QuadDesk.csproj' `
        -c Release `
        -r win-x64 `
        --self-contained true `
        --no-restore `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $OutputDirectory

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }
}

$version = Get-ProjectVersion
Write-Host "=== QuadDesk v$version release build ==="

& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release

$releaseRoot = Join-Path $PSScriptRoot 'artifacts\release'
$publishRoot = Join-Path $PSScriptRoot 'artifacts\publish\QuadDesk'

Remove-Item -LiteralPath $publishRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $releaseRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

Invoke-DotnetPublish -OutputDirectory $publishRoot

$exe = Join-Path $publishRoot 'QuadDesk.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw 'QuadDesk.exe was not created.'
}

Copy-Item -LiteralPath 'config.default.json' -Destination $publishRoot -Force
Copy-Item -LiteralPath 'README.md' -Destination $publishRoot -Force
Copy-Item -LiteralPath 'LICENSE' -Destination $publishRoot -Force
Copy-Item -LiteralPath 'layouts' -Destination $publishRoot -Recurse -Force

$portableStage = Join-Path $releaseRoot "QuadDesk-v$version-win-x64-portable"
Copy-Item -LiteralPath $publishRoot -Destination $portableStage -Recurse

$portableZip = Join-Path $releaseRoot "QuadDesk-v$version-win-x64-portable.zip"
Compress-Archive -Path "$portableStage\*" -DestinationPath $portableZip -CompressionLevel Optimal -Force
Remove-Item -LiteralPath $portableStage -Recurse -Force

$installerPath = $null
if (-not $SkipInstaller) {
    $iscc = Find-Iscc

    if (-not $iscc) {
        throw @'
Inno Setup 6 was not found.
Install it once with:
  winget install --id JRSoftware.InnoSetup -e
Then run .\publish.ps1 again.
Use .\publish.ps1 -SkipInstaller only when you need the portable package.
'@
    }

    & $iscc "/DMyAppVersion=$version" "/DPublishDir=$publishRoot" "/DOutputDir=$releaseRoot" 'installer\QuadDesk.iss'
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }

    $installerPath = Join-Path $releaseRoot "QuadDesk-v$version-win-x64-setup.exe"
    if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
        throw "Installer was not created: $installerPath"
    }
}

$checksumTargets = @($portableZip)
if ($installerPath) {
    $checksumTargets += $installerPath
}

$checksumFile = Join-Path $releaseRoot 'SHA256SUMS.txt'
$checksumLines = foreach ($target in $checksumTargets) {
    $hash = Get-FileHash -LiteralPath $target -Algorithm SHA256
    '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), (Split-Path $target -Leaf)
}
[System.IO.File]::WriteAllLines($checksumFile, $checksumLines, [System.Text.Encoding]::ASCII)

Write-Host ''
Write-Host '=== RELEASE READY ==='
Write-Host "Portable : $portableZip"
if ($installerPath) {
    Write-Host "Installer: $installerPath"
}
Write-Host "Checksums: $checksumFile"
Write-Host 'Version source: Directory.Build.props'
