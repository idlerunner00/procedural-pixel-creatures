# Full verification: C# build, core tests (console harness, needs the .NET 8 SDK) and the Godot
# self test (headless). Exit code != 0 if anything fails.
#   .\scripts\check.ps1 [-Quick]
param([switch]$Quick)
. (Join-Path $PSScriptRoot 'common.ps1')
$reports = Join-Path $RepoRoot 'reports'
New-Item -ItemType Directory -Force -Path $reports | Out-Null
$status = 0

Write-Host '== 1/3 C# build =='
try { Build-CSharp; Import-Project } catch { Write-Host $_; exit 1 }

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    Write-Host '== 2/3 Core tests (console harness, without Godot) =='
    $seeds = if ($Quick) { '10' } else { '100' }
    & $script:DotnetExe run -c Release --project (Join-Path $RepoRoot 'tools/core_harness/CoreHarness.csproj') -- test $reports all $seeds | Out-Host
    if ($LASTEXITCODE -ne 0) { $status = 1 }
} else {
    Write-Host '== 2/3 Core tests skipped (no .NET SDK; the Godot self-test contains the same tests) =='
}

Write-Host '== 3/3 Godot self-test (headless) =='
$godotArgs = @('--headless', '--audio-driver', 'Dummy', '--path', $RepoRoot, '--', '--self-test', "--report=$(Join-Path $reports 'self_test_report.json')")
if ($Quick) { $godotArgs += '--quick' }
$code = Invoke-Godot $godotArgs
if ($code -ne 0) { $status = 1 }

if ($status -eq 0) { Write-Host 'ALL CHECKS PASSED' } else { Write-Host "CHECKS FAILED (see above and $reports)" }
exit $status
