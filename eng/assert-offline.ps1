[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$patterns = @(
    '\bSystem\.Net\b',
    '\bHttpClient\b',
    '\bWebRequest\b',
    '\bTcpClient\b',
    '\bUdpClient\b',
    '\bSocket\b',
    '\bWindows\.Web\b',
    '<WebView2?\b'
)
$sourceFiles = Get-ChildItem -Path src -Recurse -File |
    Where-Object {
        $_.Extension -in '.cs', '.xaml' -and
        $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
    }
$matches = $sourceFiles | Select-String -Pattern $patterns
if ($matches) {
    $matches | ForEach-Object { Write-Error "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
    throw 'Networking API usage was found in the offline application source.'
}

Write-Host 'Offline-source check passed: no networking API usage found.'
