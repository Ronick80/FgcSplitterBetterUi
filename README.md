# FgcSplitterBetterUi

A Windows desktop front end for [FGC Keyboard Splitter](https://github.com/Ronick80/FgcKeyboardSplitter). The engine
(`FgcCore.dll`, the keyboard capture and virtual Xbox controllers) lives in that repository; this one only holds the
user interface and does not contain any engine code.

Status: the interop layer is done and tested. The WPF app is not started yet.

## How it fits together

- `core.lock.json` pins one release of the engine repository (version, download URL, SHA-256).
- `scripts/Get-FgcCore.ps1` downloads that release zip, checks the SHA-256 before extracting anything, and unpacks it
  to `core/` (git-ignored). `core/` then holds `FgcCore.dll`, `FgcDriverSetup.exe` and the `drivers` folder.
  Building any project runs the script automatically when `core/` is empty.
- `src/Fgc.Interop` wraps the engine's C API (`core/include/fgc_core.h` in the engine repository). It checks
  `fgc_get_api_version()` and refuses to run against an engine with another API version.
- `tests/Fgc.Interop.Tests` tests struct layouts and calls the real `FgcCore.dll`. No test starts a session, so no
  keyboard is captured.

## Building

Needs Windows x64 and the .NET 9 SDK.

```powershell
dotnet build
dotnet test
```

To build against your own engine build, point at a folder that contains `FgcCore.dll`:

```powershell
dotnet test -p:FgcCoreDir=C:\coding\FgcKeyboardSplitter\build\bin\Release
```

(or set the `FGC_CORE_DIR` environment variable).

If PowerShell refuses to run the download script, run it as
`powershell -ExecutionPolicy Bypass -File scripts\Get-FgcCore.ps1`.

## Updating the engine version

Take the URL of the release zip and the SHA-256 from the release's `.sha256` file, put them in `core.lock.json`, then
run `scripts\Get-FgcCore.ps1`. It replaces `core/` only when the pinned version or checksum changed.
