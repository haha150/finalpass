[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath
)

$ErrorActionPreference = 'Stop'
$installer = (Resolve-Path $InstallerPath).Path
$temporaryRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$installDirectory = Join-Path $temporaryRoot "Finalpass-installer-smoke-$([Guid]::NewGuid().ToString('N'))"
$sentinelDirectory = Join-Path $temporaryRoot "Finalpass-vault-preservation-$([Guid]::NewGuid().ToString('N'))"
$sentinel = Join-Path $sentinelDirectory 'preserve.fpass'
$uninstaller = Join-Path $installDirectory 'unins000.exe'

New-Item -ItemType Directory -Path $sentinelDirectory | Out-Null
Set-Content -Path $sentinel -Value 'installer must not remove user vaults' -Encoding utf8

try {
    $arguments = @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        '/SP-',
        "/DIR=$installDirectory"
    )
    foreach ($pass in 1..2) {
        $process = Start-Process -FilePath $installer -ArgumentList $arguments -Wait -PassThru
        if ($process.ExitCode -ne 0) {
            throw "Installer pass $pass failed with exit code $($process.ExitCode)."
        }

        if (-not (Test-Path (Join-Path $installDirectory 'Finalpass.exe'))) {
            throw "Installer pass $pass did not create Finalpass.exe."
        }
    }

    $associationKey = Get-Item -Path 'HKCU:\Software\Classes\.fpass'
    $association = $associationKey.GetValue('')
    if ($association -ne 'Finalpass.Vault') {
        throw 'The current-user .fpass association was not registered.'
    }

    if (-not (Test-Path $uninstaller)) {
        throw 'The current-user uninstaller was not created.'
    }

    $uninstall = Start-Process -FilePath $uninstaller `
        -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') `
        -Wait -PassThru
    if ($uninstall.ExitCode -ne 0) {
        throw "Uninstall failed with exit code $($uninstall.ExitCode)."
    }

    if (Test-Path (Join-Path $installDirectory 'Finalpass.exe')) {
        throw 'Uninstall left the application executable behind.'
    }

    if (-not (Test-Path $sentinel)) {
        throw 'Uninstall removed a vault outside the application directory.'
    }

    Write-Host 'Installer install/update/file-association/uninstall smoke test passed.'
}
finally {
    if (Test-Path $uninstaller) {
        Start-Process -FilePath $uninstaller `
            -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') `
            -Wait | Out-Null
    }

    if (Test-Path $installDirectory) {
        Remove-Item -LiteralPath $installDirectory -Recurse -Force
    }

    if (Test-Path $sentinelDirectory) {
        Remove-Item -LiteralPath $sentinelDirectory -Recurse -Force
    }
}
