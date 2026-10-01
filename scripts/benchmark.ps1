# Performance benchmark (populations 1/25/100) or soak test. Reports go to reports\.
#   .\scripts\benchmark.ps1              -> benchmark in a window (real GPU, V-Sync off)
#   .\scripts\benchmark.ps1 -Headless    -> CPU side only
#   .\scripts\benchmark.ps1 -Soak -Minutes 10
param([switch]$Headless, [switch]$Soak, [int]$Minutes = 10)
. (Join-Path $PSScriptRoot 'common.ps1')
Build-CSharp
Import-Project
$reports = Join-Path $RepoRoot 'reports'
New-Item -ItemType Directory -Force -Path $reports | Out-Null
$godotArgs = @()
if ($Headless) { $godotArgs += @('--headless', '--audio-driver', 'Dummy') }
$godotArgs += @('--path', $RepoRoot, '--')
if ($Soak) { $godotArgs += @('--soak', "--minutes=$Minutes", "--report=$(Join-Path $reports 'soak.json')") }
else { $godotArgs += @('--benchmark', "--report=$(Join-Path $reports 'benchmark.json')") }
exit (Invoke-Godot $godotArgs)
