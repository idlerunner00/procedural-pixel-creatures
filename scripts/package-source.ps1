# Create a clean source folder and ZIP, including dotfiles. Existing output is never overwritten.
[CmdletBinding()]
param([string]$Destination = '')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'source-files.ps1')
$rootPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\', '/')
if (-not $Destination) { $Destination = Join-Path $rootPath 'build/source-release' }
$destinationPath = [IO.Path]::GetFullPath($Destination).TrimEnd('\', '/')
if (Test-Path -LiteralPath $destinationPath) { throw "Destination already exists; choose a new folder: $destinationPath" }
& (Join-Path $PSScriptRoot 'audit-source.ps1') -Root $rootPath
$files = @(Get-SourceFiles $rootPath)
$folderName = 'procedural-pixel-creatures'
$sourcePath = Join-Path $destinationPath $folderName
New-Item -ItemType Directory -Path $sourcePath -Force | Out-Null
foreach ($file in $files) {
    $relative = $file.FullName.Substring($rootPath.Length + 1)
    $target = Join-Path $sourcePath $relative
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
}
& (Join-Path $sourcePath 'scripts/audit-source.ps1') -Root $sourcePath
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = Join-Path $destinationPath 'procedural-pixel-creatures-source.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($sourcePath, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $true)
$manifest = foreach ($file in Get-SourceFiles $sourcePath | Sort-Object FullName) {
    $relative = $file.FullName.Substring($sourcePath.Length + 1).Replace('\', '/')
    '{0}  {1}/{2}' -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $folderName, $relative
}
$manifest += '{0}  {1}' -f (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($zipPath)
[IO.File]::WriteAllLines((Join-Path $destinationPath 'SHA256SUMS.txt'), $manifest, (New-Object Text.UTF8Encoding($false)))
Write-Host "Source folder: $sourcePath"
Write-Host "Source ZIP:    $zipPath"
