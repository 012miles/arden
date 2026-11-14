param([string]$App, [string]$Out)
# Zips a macOS .app built on Windows, marking every file executable (0755) so the Mac can launch it.
# Windows files carry no Unix permissions, so we describe the archive with an mtree spec for bsdtar.
$ErrorActionPreference = 'Stop'
function Vis([string]$s) { ($s -replace '\\', '/') -replace ' ', '\040' }
$appItem = Get-Item $App
$base = $appItem.Parent.FullName
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('#mtree')
$lines.Add("$(Vis $appItem.Name) type=dir mode=0755")
foreach ($it in Get-ChildItem $App -Recurse -Force) {
    $rel = $it.FullName.Substring($base.Length + 1)
    if ($it.PSIsContainer) { $lines.Add("$(Vis $rel) type=dir mode=0755") }
    else { $lines.Add("$(Vis $rel) type=file mode=0755 contents=$(Vis $it.FullName)") }
}
$spec = Join-Path $env:TEMP 'rocket_mac.mtree'
[System.IO.File]::WriteAllLines($spec, $lines, (New-Object System.Text.UTF8Encoding($false)))
if (Test-Path $Out) { Remove-Item $Out -Force }
tar --format zip --options zip:compression-level=9 -cf $Out "@$spec"
if ($LASTEXITCODE -ne 0) { throw "tar failed ($LASTEXITCODE)" }
"{0} files, {1:N1} MB" -f ($lines.Count - 1), ((Get-Item $Out).Length / 1MB)
