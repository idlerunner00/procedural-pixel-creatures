# Validate the public source manifest and local documentation links without requiring Godot.
[CmdletBinding()]
param([string]$Root = '')
if (-not $Root) { $Root = Join-Path $PSScriptRoot '..' }
. (Join-Path $PSScriptRoot 'source-files.ps1')
$rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
$files = @(Get-SourceFiles $rootPath)
$issues = New-Object 'System.Collections.Generic.List[string]'
$names = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($file in $files) {
    $relative = $file.FullName.Substring($rootPath.Length + 1).Replace('\', '/')
    if (-not $names.Add($relative)) { $issues.Add("Duplicate source path: $relative") }
    if ($file.Length -ge 25MB) { $issues.Add("File too large for a browser upload: $relative") }
    if ($file.Extension -in @('.cs', '.md', '.json', '.ps1', '.sh', '.py', '.txt', '.yml', '.godot', '.tscn')) {
        $content = [IO.File]::ReadAllText($file.FullName)
        if ($content -match '[A-Za-z]:[/\\]+Users[/\\]+[A-Za-z0-9]') {
            $issues.Add("Machine-specific user path: $relative")
        }
    }
}
foreach ($file in $files | Where-Object Extension -eq '.md') {
    $content = [IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches($content, '\]\(([^)\r\n]+)\)')) {
        $link = $match.Groups[1].Value.Trim().Trim('<', '>')
        if ($link -match '^(https?://|mailto:|#)') { continue }
        $link = [Uri]::UnescapeDataString(($link -split '#')[0])
        if (-not $link) { continue }
        $target = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $link))
        if (-not $target.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            $issues.Add("Link leaves the repository in $($file.Name): $link")
            continue
        }
        $relative = $target.Substring($rootPath.Length + 1).Replace('\', '/')
        $isDirectory = @( $names | Where-Object { $_.StartsWith($relative.TrimEnd('/') + '/', [StringComparison]::Ordinal) } ).Count -gt 0
        if (-not $names.Contains($relative) -and -not $isDirectory) {
            $issues.Add("Broken or wrongly cased source link in $($file.Name): $link")
        }
    }
}
if ($issues.Count -gt 0) { throw ($issues -join [Environment]::NewLine) }
$size = ($files | Measure-Object Length -Sum).Sum
Write-Host ('Source audit passed: {0} files, {1:N2} MiB; local links and file sizes checked.' -f $files.Count, ($size / 1MB))
