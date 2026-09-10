$ErrorActionPreference = 'Stop'

$version = '6.7.3'
$expectedSha256 = '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'
$uri = "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-$version.exe"
$dependencyRoot = Join-Path $PSScriptRoot '..\artifacts\dependencies\inno'
$installerPath = Join-Path $dependencyRoot "innosetup-$version.exe"
$installPath = Join-Path $dependencyRoot 'installed'

New-Item -ItemType Directory -Force -Path $dependencyRoot | Out-Null
if (-not (Test-Path $installerPath)) {
    Invoke-WebRequest -Uri $uri -OutFile $installerPath
}

$actualSha256 = (Get-FileHash -Algorithm SHA256 -Path $installerPath).Hash.ToLowerInvariant()
if ($actualSha256 -ne $expectedSha256) {
    throw "Inno Setup SHA-256 mismatch. Expected $expectedSha256, got $actualSha256."
}

if (Test-Path $installPath) {
    Remove-Item -LiteralPath $installPath -Recurse -Force
}

$arguments = @(
    '/VERYSILENT',
    '/SUPPRESSMSGBOXES',
    '/NORESTART',
    '/SP-',
    '/CURRENTUSER',
    "/DIR=$installPath"
)
$process = Start-Process -FilePath $installerPath -ArgumentList $arguments -PassThru -Wait
if ($process.ExitCode -ne 0) {
    throw "Inno Setup installation failed with exit code $($process.ExitCode)."
}

$compiler = Join-Path $installPath 'ISCC.exe'
if (-not (Test-Path $compiler)) {
    throw 'Inno Setup installed without ISCC.exe.'
}

Write-Host "Verified Inno Setup $version at $compiler"
