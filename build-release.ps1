param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$stage = Join-Path $root "obj\release-stage\$Version"
$release = Join-Path $root "release"

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
dotnet publish (Join-Path $root "ObeliskPanel.csproj") -p:PublishProfile=win-x64 -p:Version=$Version -p:PublishDir="$stage\"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Copy-Item (Join-Path $root "release-readme.txt") (Join-Path $stage "README.txt") -Force

New-Item -ItemType Directory -Force $release | Out-Null
$zip = Join-Path $release "ObeliskPanel-$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
Write-Host "Release ready: $zip"

$publish = Join-Path $root "publish"
try {
    New-Item -ItemType Directory -Force $publish | Out-Null
    Copy-Item (Join-Path $stage "*") $publish -Recurse -Force
    Write-Host "publish\ updated"
} catch {
    Write-Warning "publish\ not updated - close ObeliskPanel and run this script again."
}
