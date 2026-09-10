[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$version = '1.0.22'
$archiveSha256 = '3e03a726fac4bc09cb61d8f29d658ef7a5eca0811de59082130414f7ca2e4279'
$dllSha256 = 'f656aeb789bfc3a2ac587fae1d7cfe278bbe8ce73f28da73b4d08282ea8f9fe3'
$archiveUri = "https://github.com/jedisct1/libsodium/releases/download/$version-RELEASE/libsodium-$version-msvc.zip"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dependencyRoot = Join-Path $repositoryRoot 'artifacts/dependencies/libsodium'
$archivePath = Join-Path $dependencyRoot "libsodium-$version-msvc.zip"
$extractPath = Join-Path $dependencyRoot $version
$destinationDirectory = Join-Path $repositoryRoot 'src/Finalpass.Cryptography/runtimes/win-x64/native'
$destinationPath = Join-Path $destinationDirectory 'libsodium.dll'
$sourcePath = Join-Path $extractPath 'libsodium/x64/Release/v143/dynamic/libsodium.dll'

New-Item -ItemType Directory -Force -Path $dependencyRoot | Out-Null

if (-not (Test-Path $archivePath)) {
    Invoke-WebRequest -Uri $archiveUri -OutFile $archivePath
}

$actualSha256 = (Get-FileHash -Algorithm SHA256 -Path $archivePath).Hash.ToLowerInvariant()
if ($actualSha256 -ne $archiveSha256) {
    throw "libsodium archive hash mismatch. Expected $archiveSha256 but received $actualSha256."
}

if (Test-Path $extractPath) {
    Remove-Item -Recurse -Force $extractPath
}

Expand-Archive -Path $archivePath -DestinationPath $extractPath
if (-not (Test-Path $sourcePath)) {
    throw "The expected x64 release DLL was not present in the verified archive."
}

New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
Copy-Item -Force -Path $sourcePath -Destination $destinationPath

$dllHash = (Get-FileHash -Algorithm SHA256 -Path $destinationPath).Hash.ToLowerInvariant()
if ($dllHash -ne $dllSha256) {
    throw "libsodium DLL hash mismatch. Expected $dllSha256 but received $dllHash."
}

Write-Host "Restored libsodium $version x64 ($dllHash)."
