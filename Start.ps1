# Double-click Start.cmd: download missing tools, build, import, and open the workshop.
# All downloads stay in .tools; installed compatible tools may be reused.
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('workshop', 'overview', 'testarea', 'editor', 'help')]
    [string]$Mode = 'workshop',
    [switch]$Editor,
    [switch]$Overview,
    [switch]$TestArea,
    [switch]$PrepareOnly,
    [switch]$NoBuild,
    [switch]$NoWait,
    [string[]]$GodotArguments = @()
)
$ErrorActionPreference = 'Stop'
try {
    if ($Mode -eq 'help') {
        Write-Host @'
Start.cmd                       Download missing tools and open the workshop
Start.cmd overview              Open the 100-creature overview
Start.cmd testarea               Open the test area
Start.cmd editor                 Open the Godot editor
Start.cmd -PrepareOnly           Download, build and import without opening a window
Start.cmd -NoBuild               Reuse the existing build (builds once if missing)

-Editor, -Overview and -TestArea are also accepted.
Windows x64; first setup needs an internet connection. No administrator rights required.
'@
        exit 0
    }
    $selected = @()
    if ($Editor) { $selected += 'editor' }
    if ($Overview) { $selected += 'overview' }
    if ($TestArea) { $selected += 'testarea' }
    if ($selected.Count -gt 1 -or ($selected.Count -eq 1 -and $Mode -ne 'workshop' -and $Mode -ne $selected[0])) {
        throw 'Choose one start mode: workshop, overview, testarea or editor.'
    }
    if ($selected.Count -eq 1) { $Mode = $selected[0] }
    . (Join-Path $PSScriptRoot 'scripts/common.ps1')
    Set-Location -LiteralPath $RepoRoot
    Write-Host 'Procedural Pixel Creature Workshop'
    Write-Host '[1/3] Preparing Godot .NET and the C# SDK ...'
    Initialize-Toolchain
    $assembly = Join-Path $RepoRoot '.godot/mono/temp/bin/Debug/ProceduralPixelCreatures.dll'
    if (-not $NoBuild -or -not (Test-Path -LiteralPath $assembly)) {
        Write-Host '[2/3] Building the workshop ...'
        Build-CSharp
    } else { Write-Host '[2/3] Using the existing build.' }
    Import-Project
    if ($PrepareOnly) { Write-Host 'Setup complete. Start.cmd is ready.'; exit 0 }
    $arguments = @('--path', $RepoRoot) + $GodotArguments
    switch ($Mode) {
        'editor' { $arguments += '--editor' }
        'overview' { $arguments += @('--', '--overview') }
        'testarea' { $arguments += @('--', '--testarea') }
    }
    Write-Host "[3/3] Starting $Mode ..."
    $code = Invoke-Godot $arguments -NoWait:$NoWait
    if ($code -ne 0) { throw "Godot ended with exit code $code. Its log is in %APPDATA%/Godot/app_userdata/Procedural Pixel Creature Workshop/logs/godot.log." }
    exit 0
} catch {
    Write-Host ''
    Write-Host "The workshop could not start: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
