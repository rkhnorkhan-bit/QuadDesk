[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TargetDirectory
)

$ErrorActionPreference = 'Stop'
$taskTarget = (Resolve-Path -LiteralPath $TargetDirectory).Path
$taskExe = Join-Path $taskTarget 'QuadDesk.exe'

if (-not (Test-Path -LiteralPath $taskExe -PathType Leaf)) {
    throw "QuadDesk.exe was not found in: $taskTarget"
}

if (Get-Process -Name QuadDesk -ErrorAction SilentlyContinue) {
    throw 'Close QuadDesk from the tray and run this command again.'
}

Set-Location $PSScriptRoot
& (Join-Path $PSScriptRoot 'publish.ps1') -SkipInstaller

$taskCandidate = Join-Path $PSScriptRoot 'artifacts\publish\QuadDesk\QuadDesk.exe'
if (-not (Test-Path -LiteralPath $taskCandidate -PathType Leaf)) {
    throw 'Built QuadDesk.exe was not found in artifacts\publish\QuadDesk.'
}

if (Get-Process -Name QuadDesk -ErrorAction SilentlyContinue) {
    throw 'QuadDesk was started during the build. Close it and run the update again.'
}

$taskBackup = $taskExe + '.backup-' + (Get-Date -Format 'yyyyMMdd-HHmmssfff')
Copy-Item -LiteralPath $taskExe -Destination $taskBackup
$taskIncoming = Join-Path $taskTarget ('QuadDesk.update-' + [Guid]::NewGuid().ToString('N') + '.exe')

try {
    Copy-Item -LiteralPath $taskCandidate -Destination $taskIncoming

    $sourceHash = (Get-FileHash -LiteralPath $taskCandidate -Algorithm SHA256).Hash
    $incomingHash = (Get-FileHash -LiteralPath $taskIncoming -Algorithm SHA256).Hash
    if ($sourceHash -ne $incomingHash) {
        throw 'The copied executable failed SHA-256 verification.'
    }

    Move-Item -LiteralPath $taskIncoming -Destination $taskExe -Force
}
finally {
    if (Test-Path -LiteralPath $taskIncoming) {
        Remove-Item -LiteralPath $taskIncoming -Force
    }
}

Write-Host "Updated: $taskExe"
Write-Host "Backup : $taskBackup"
Write-Host 'Settings, layouts, workspace and logs were preserved.'
Start-Process -FilePath $taskExe -WorkingDirectory $taskTarget
