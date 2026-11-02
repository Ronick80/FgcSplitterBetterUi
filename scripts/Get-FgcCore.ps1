<#
.SYNOPSIS
Downloads the FgcKeyboardSplitter release pinned in core.lock.json into .\core and checks its SHA-256.

.DESCRIPTION
The engine (FgcCore.dll), the driver setup tool and the ScpVBus driver files come from the release zip of
github.com/Ronick80/FgcKeyboardSplitter. Nothing from the zip is used before its checksum matches the lock file.
Pass -Force to download again even when .\core is already at the pinned version.
#>
param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\core'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

# Projects built in parallel each call this script; only one may download at a time.
$mutex = New-Object System.Threading.Mutex($false, 'FgcCoreFetch')
[void]$mutex.WaitOne()
try {
    $lock = Get-Content (Join-Path $PSScriptRoot '..\core.lock.json') -Raw | ConvertFrom-Json
    $Destination = [IO.Path]::GetFullPath($Destination)
    $stamp = "$($lock.version) $($lock.sha256)"
    $marker = Join-Path $Destination '.fgc-core-version'

    if (-not $Force -and (Test-Path $marker) -and ((Get-Content $marker -Raw).Trim() -eq $stamp)) {
        Write-Host "FgcCore $($lock.version) is already in $Destination"
        return
    }

    # Only ever clear a folder this script filled before, so a wrong -Destination cannot delete anything else.
    if ((Test-Path $Destination) -and -not (Test-Path $marker) -and (Get-ChildItem $Destination -Force | Select-Object -First 1)) {
        throw "$Destination is not empty and was not created by this script. Choose another -Destination."
    }

    $work = Join-Path ([IO.Path]::GetTempPath()) ("fgccore-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    try {
        $zip = Join-Path $work 'core.zip'
        Write-Host "Downloading $($lock.url)"
        Invoke-WebRequest -Uri $lock.url -OutFile $zip -UseBasicParsing

        # .NET hashing: Get-FileHash is missing when this runs as Windows PowerShell under a PowerShell 7 parent.
        $sha = [Security.Cryptography.SHA256]::Create()
        $stream = [IO.File]::OpenRead($zip)
        try { $actual = ([BitConverter]::ToString($sha.ComputeHash($stream)) -replace '-', '').ToLowerInvariant() }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($actual -ne $lock.sha256.ToLowerInvariant()) {
            throw "Checksum mismatch for FgcCore $($lock.version). Expected $($lock.sha256), got $actual. Nothing was extracted."
        }

        $unpacked = Join-Path $work 'unpacked'
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $unpacked)
        $root = Get-ChildItem $unpacked -Directory | Select-Object -First 1
        if (-not $root -or -not (Test-Path (Join-Path $root.FullName 'FgcCore.dll'))) {
            throw 'The release zip does not have the expected layout (FgcCore.dll in its top-level folder).'
        }

        if (Test-Path $Destination) { Remove-Item $Destination -Recurse -Force }
        New-Item -ItemType Directory -Path $Destination | Out-Null
        Copy-Item (Join-Path $root.FullName '*') $Destination -Recurse
        Set-Content -Path $marker -Value $stamp -Encoding ascii
        Write-Host "FgcCore $($lock.version) is ready in $Destination"
    }
    finally {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}
finally {
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
