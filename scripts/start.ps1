# Compatibility entry point; shares the automatic setup used by Start.cmd.
param(
    [switch]$TestArea, [switch]$Overview, [switch]$Editor,
    [switch]$PrepareOnly, [switch]$NoBuild, [switch]$NoWait,
    [string[]]$GodotArguments = @()
)
& (Join-Path $PSScriptRoot '../Start.ps1') @PSBoundParameters
exit $LASTEXITCODE
