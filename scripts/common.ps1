# Shared build, import and launch path for every Windows entry point.
$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'bootstrap.ps1')

function Find-Godot {
    Initialize-Toolchain
    return $script:GodotExe
}

function Quote-ProcessArgument([string]$Argument) {
    # Windows CommandLineToArgvW quoting, including trailing backslashes and embedded quotes.
    '"' + ([regex]::Replace($Argument, '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
}

function Invoke-Godot([string[]]$GodotArgs, [switch]$NoWait) {
    $godot = Find-Godot
    $console = $godot -replace '(_console)?\.exe$', '_console.exe'
    if ($NoWait) {
        $quoted = ($GodotArgs | ForEach-Object { Quote-ProcessArgument $_ }) -join ' '
        $style = if ($GodotArgs -contains '--headless') { 'Hidden' } else { 'Normal' }
        Start-Process -FilePath $godot -ArgumentList $quoted -WindowStyle $style | Out-Null
        return 0
    }
    if (Test-Path -LiteralPath $console) { $godot = $console }
    & $godot @GodotArgs | Out-Host
    return [int]$LASTEXITCODE
}

function Build-CSharp {
    Initialize-Toolchain
    Push-Location -LiteralPath $RepoRoot
    try {
        & $script:DotnetExe build (Join-Path $RepoRoot 'ProceduralPixelCreatures.sln') -c Debug -nologo -v minimal | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'The C# build failed (see above). Run Start.cmd again after fixing the reported error.' }
    } finally { Pop-Location }
}

function Import-Project {
    Initialize-Toolchain
    $marker = Join-Path $RepoRoot ('.godot/workshop-import-' + $Toolchain.godot.version)
    $imported = Join-Path $RepoRoot '.godot/imported'
    if ((Test-Path -LiteralPath $marker) -and (Test-Path -LiteralPath $imported)) { return }
    Write-Host 'Importing the project for the first start ...'
    $code = Invoke-Godot @('--headless', '--quiet', '--language', 'en', '--audio-driver', 'Dummy', '--path', $RepoRoot, '--import')
    if ($code -ne 0 -or -not (Test-Path -LiteralPath $imported)) {
        throw 'Godot could not import the project. See the messages above; the next start will retry.'
    }
    Set-Content -LiteralPath $marker -Value $Toolchain.godot.version -Encoding ASCII
}
