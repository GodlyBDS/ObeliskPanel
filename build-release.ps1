param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$publish = Join-Path $root "publish"
$release = Join-Path $root "release"

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish (Join-Path $root "ObeliskPanel.csproj") -p:PublishProfile=win-x64 -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Copy-Item (Join-Path $root "release-readme.txt") (Join-Path $publish "README.txt") -Force

New-Item -ItemType Directory -Force $release | Out-Null
$zip = Join-Path $release "ObeliskPanel-$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $zip
Write-Host "Release ready: $zip"
