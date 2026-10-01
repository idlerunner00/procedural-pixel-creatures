# Shared source manifest. Deliberately excludes toolchains, recordings, user data and build caches.
$ErrorActionPreference = 'Stop'

function Get-SourceFiles([string]$Root) {
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $rootFiles = @('.editorconfig', '.gitattributes', '.gitignore', 'global.json', 'icon.svg',
        'LICENSE', 'THIRD_PARTY_NOTICES.md', 'ProceduralPixelCreatures.csproj',
        'ProceduralPixelCreatures.sln', 'project.godot', 'README.md', 'Start.cmd', 'Start.ps1')
    foreach ($name in $rootFiles) {
        $path = Join-Path $rootPath $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing source file: $name" }
        Get-Item -LiteralPath $path
    }
    $pending = New-Object 'System.Collections.Generic.Stack[string]'
    foreach ($name in @('.github', 'addons', 'docs', 'examples', 'scripts', 'tests', 'tools', 'workshop')) {
        $path = Join-Path $rootPath $name
        if (-not (Test-Path -LiteralPath $path -PathType Container)) { throw "Missing source directory: $name" }
        $pending.Push($path)
    }
    while ($pending.Count -gt 0) {
        foreach ($item in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Source contains a link: $($item.FullName)" }
            if ($item.PSIsContainer) {
                if ($item.Name -notin @('bin', 'obj', '__pycache__', '.venv', 'venv', '.godot', '.git', 'out')) {
                    $pending.Push($item.FullName)
                }
            } elseif ($item.Name -notmatch '(?i)(\.(import|pyc|pyo|log|tmp|mp4|mov|avi|zip|7z|tpz|pfx|p12|pem|user|suo)$|^\.env($|\.)|^(Thumbs\.db|Desktop\.ini|\.DS_Store)$)') {
                $item
            }
        }
    }
}
