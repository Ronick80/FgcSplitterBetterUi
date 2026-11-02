<#
.SYNOPSIS
Builds the release package: a self-contained single-file FgcKeyboardSplitter.exe with the engine files beside it,
zipped to artifacts\FgcSplitterBetterUi-<version>-win-x64.zip together with its SHA-256 file.
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts')
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$Output = [IO.Path]::GetFullPath($Output)
$version = (Get-Content (Join-Path $root 'version.txt') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Version '$version' in version.txt must look like 1.2.3" }

$name = "FgcSplitterBetterUi-$version-win-x64"
$stage = Join-Path $Output $name
$zip = Join-Path $Output "$name.zip"
if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

# Publishing also fetches the pinned engine release into core\ when it is missing (see build\FgcCore.targets).
dotnet publish (Join-Path $root 'src\FgcKeyboardSplitter.Wpf') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Remove-Item (Join-Path $stage '*.pdb') -ErrorAction SilentlyContinue

# FgcDriverSetup.exe needs these as real files next to the exe, so fail loudly if one went missing.
$required = 'FgcKeyboardSplitter.exe', 'FgcCore.dll', 'FgcDriverSetup.exe', 'drivers\ScpVBus\ScpVBus.inf',
    'drivers\ScpVBus\ScpVBus.sys', 'drivers\ScpVBus\scpvbus.cat', 'drivers\ScpVBus\devcon.exe'
foreach ($file in $required) {
    if (-not (Test-Path (Join-Path $stage $file))) { throw "The package is missing $file" }
}
Copy-Item (Join-Path $root 'README.md') $stage

Compress-Archive -Path $stage -DestinationPath $zip
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zip.sha256", "$hash  $(Split-Path $zip -Leaf)`n")
Write-Host "Created $zip"
Write-Host "SHA-256 $hash"
