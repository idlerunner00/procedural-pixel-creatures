# Project-local Windows dependencies. No administrator rights or persistent PATH changes.
$Toolchain = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
$ToolsRoot = Join-Path $RepoRoot '.tools'
$script:ToolchainReady = $false

function Assert-ToolPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = [IO.Path]::GetFullPath($ToolsRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Tool operation outside the project's .tools folder: $full"
    }
}

function Install-ToolArchive($Spec, [string]$RequiredFile, [switch]$Nested) {
    $destination = Join-Path $ToolsRoot $Spec.name
    $downloads = Join-Path $ToolsRoot 'downloads'
    New-Item -ItemType Directory -Path $downloads -Force | Out-Null
    $archive = Join-Path $downloads ($Spec.name + '.zip')
    if ((Test-Path -LiteralPath $archive) -and (Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $Spec.sha512) {
        Remove-Item -LiteralPath $archive -Force
    }
    if (-not (Test-Path -LiteralPath $archive)) {
        $partial = $archive + '.partial'
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            try {
                Write-Host "Downloading $($Spec.name) (attempt $attempt/3) ..."
                Invoke-WebRequest -Uri $Spec.url -OutFile $partial -UseBasicParsing -TimeoutSec 1200
                if ((Get-FileHash -LiteralPath $partial -Algorithm SHA512).Hash -ne $Spec.sha512) {
                    throw 'The download checksum does not match the official release.'
                }
                Move-Item -LiteralPath $partial -Destination $archive -Force
                break
            } catch {
                if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Force }
                if ($attempt -eq 3) { throw "Could not download $($Spec.name). Check the internet connection and run Start.cmd again. $($_.Exception.Message)" }
            }
        }
    }
    Write-Host "Unpacking $($Spec.name) ..."
    $stage = Join-Path $ToolsRoot ('staging-' + [Guid]::NewGuid().ToString('N'))
    $previous = Join-Path $ToolsRoot ('previous-' + [Guid]::NewGuid().ToString('N'))
    Assert-ToolPath $stage
    Assert-ToolPath $destination
    Assert-ToolPath $previous
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory($archive, $stage)
        $unpacked = if ($Nested) { Join-Path $stage $Spec.name } else { $stage }
        if (-not (Test-Path -LiteralPath (Join-Path $unpacked $RequiredFile))) { throw 'The tool archive is incomplete.' }
        if (Test-Path -LiteralPath $destination) { Move-Item -LiteralPath $destination -Destination $previous }
        try { Move-Item -LiteralPath $unpacked -Destination $destination }
        catch {
            if (Test-Path -LiteralPath $previous) { Move-Item -LiteralPath $previous -Destination $destination }
            throw
        }
        if (Test-Path -LiteralPath $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }
    } finally {
        if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    }
}

function Test-Godot([string]$Path) {
    if (-not $Path -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    $folder = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath (Join-Path $folder 'GodotSharp/Tools/nupkgs'))) { return $false }
    $console = $Path -replace '(_console)?\.exe$', '_console.exe'
    if (-not (Test-Path -LiteralPath $console)) { $console = $Path }
    try {
        $version = (& $console --version 2>$null | Out-String).Trim()
        return $LASTEXITCODE -eq 0 -and $version -match ('^' + [regex]::Escape($Toolchain.godot.version) + '\.stable\.mono\.')
    } catch { return $false }
}

function Test-DotnetSdk([string]$Path) {
    if (-not $Path -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    try {
        $sdks = @(& $Path --list-sdks 2>$null)
        if ($LASTEXITCODE -ne 0 -or -not @($sdks | Where-Object { $_ -match '^(\d+)\.\d+\.\d+ \[' -and [int]$Matches[1] -ge 8 }).Count) { return $false }
        if ($env:OS -eq 'Windows_NT') {
            $info = (& $Path --info 2>$null | Out-String)
            if ($LASTEXITCODE -ne 0 -or $info -notmatch '(?m)^\s*Architecture:\s*x64\s*$') { return $false }
        }
        # The console harness targets net8.0; a newer SDK alone does not supply that runtime.
        $runtimes = @(& $Path --list-runtimes 2>$null)
        return $LASTEXITCODE -eq 0 -and @($runtimes | Where-Object { $_ -match '^Microsoft.NETCore.App 8\.0\.' }).Count -gt 0
    } catch { return $false }
}

function Initialize-Toolchain {
    if ($script:ToolchainReady) { return }
    # Captures and reports are output files, not runtime assets; do not import thousands of frames.
    $reportsDirectory = Join-Path $RepoRoot 'reports'
    New-Item -ItemType Directory -Path $reportsDirectory -Force | Out-Null
    if (-not (Test-Path -LiteralPath (Join-Path $reportsDirectory '.gdignore'))) {
        New-Item -ItemType File -Path (Join-Path $reportsDirectory '.gdignore') | Out-Null
    }
    $env:DOTNET_CLI_UI_LANGUAGE = 'en'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    if ($env:OS -ne 'Windows_NT') {
        $script:GodotExe = $env:GODOT
        if (-not $script:GodotExe) {
            foreach ($name in @('godot', 'godot4', 'Godot_v4.5.2-stable_mono_linux.x86_64')) {
                $command = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue
                if ($command) { $script:GodotExe = $command.Source; break }
            }
        }
        $command = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue
        if (-not $script:GodotExe -or -not $command) { throw 'Install Godot .NET 4.5.2 and the .NET 8 SDK; automatic downloads are supported on Windows x64.' }
        $script:DotnetExe = $command.Source
        $script:ToolchainReady = $true
        return
    }
    $ProgressPreference = 'SilentlyContinue'
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $localEngine = Join-Path (Join-Path $ToolsRoot $Toolchain.godot.name) ($Toolchain.godot.name + '.exe')
    $savedFile = Join-Path $RepoRoot '.godot-path'
    $saved = if (Test-Path -LiteralPath $savedFile) { (Get-Content -LiteralPath $savedFile -Raw -Encoding UTF8).Trim().Trim('"') } else { '' }
    $candidates = @($env:GODOT, $localEngine, $saved)
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Godot $candidate.Trim('"'))) {
            $script:GodotExe = [IO.Path]::GetFullPath($candidate.Trim('"'))
            break
        }
    }
    if (-not $script:GodotExe) {
        Install-ToolArchive $Toolchain.godot ($Toolchain.godot.name + '.exe') -Nested
        if (-not (Test-Godot $localEngine)) { throw 'The downloaded Godot .NET executable could not run.' }
        $script:GodotExe = $localEngine
    }
    $script:GodotExe = $script:GodotExe -replace '_console\.exe$', '.exe'
    $localDotnet = Join-Path (Join-Path $ToolsRoot $Toolchain.dotnet.name) 'dotnet.exe'
    $command = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue
    $candidates = @($localDotnet)
    if ($command) { $candidates += $command.Source }
    if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles 'dotnet/dotnet.exe' }
    foreach ($candidate in $candidates) {
        if (Test-DotnetSdk $candidate) { $script:DotnetExe = $candidate; break }
    }
    if (-not $script:DotnetExe) {
        Write-Host 'No compatible SDK/runtime pair found; preparing a private .NET 8 SDK ...'
        Install-ToolArchive $Toolchain.dotnet 'dotnet.exe'
        if (-not (Test-DotnetSdk $localDotnet)) { throw 'The downloaded .NET SDK could not run.' }
        $script:DotnetExe = $localDotnet
    }
    $dotnetFolder = Split-Path -Parent $script:DotnetExe
    $env:DOTNET_ROOT = $dotnetFolder
    $env:DOTNET_ROOT_X64 = $dotnetFolder
    $env:PATH = "$dotnetFolder;$env:PATH"
    # Keep the bundled Godot packages available to a fresh SDK without changing global NuGet settings.
    $feed = Join-Path $ToolsRoot 'godot-nuget'
    New-Item -ItemType Directory -Path $feed -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path (Split-Path -Parent $script:GodotExe) 'GodotSharp/Tools/nupkgs') -Filter '*.nupkg' |
        Copy-Item -Destination $feed -Force
    $config = Join-Path $RepoRoot 'NuGet.Config'
    if (-not (Test-Path -LiteralPath $config)) {
        @'
<?xml version="1.0" encoding="utf-8"?>
<!-- Generated by the workshop launcher. Local packages travel with this project. -->
<configuration>
  <packageSources>
    <clear />
    <add key="Godot bundled" value=".tools/godot-nuget" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
'@ | Set-Content -LiteralPath $config -Encoding UTF8
    }
    # Other tools also read this file; UTF-8 without a BOM keeps it portable to Python and shells.
    [IO.File]::WriteAllText($savedFile, $script:GodotExe, [Text.UTF8Encoding]::new($false))
    Write-Host "Godot: $script:GodotExe"
    Write-Host ".NET:  $script:DotnetExe"
    $script:ToolchainReady = $true
}
