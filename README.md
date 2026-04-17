# VTracker

VTracker is a Windows-only .NET 10 CLI for archiving and comparing MSI-delivered file trees without installing the product. It creates a Windows Installer administrative image, optionally applies patches in caller-defined order, builds a deterministic manifest for the extracted files, packages the result into a ZIP, and compares two manifests or archives.

## What it does

- Extracts the effective file tree of a base MSI by using `msiexec /a`
- Applies zero or more patches in the exact order you provide
- Generates a deterministic manifest with normalized relative paths, file sizes, SHA-256 hashes, timestamps, and PE version metadata for `.dll` and `.exe` files
- Writes `_manifest.json` at the ZIP root and can also emit a standalone `.manifest.json`
- Compares two manifests or archives and reports added, removed, updated, and provenance-difference findings

## Requirements

- Windows
- .NET 10 SDK to build from source
- Windows Installer (`msiexec.exe`), available on supported Windows systems

## Build, test, publish

```powershell
dotnet build .\VTracker.slnx
dotnet test .\VTracker.slnx
dotnet publish .\src\VTracker.Cli\VTracker.Cli.csproj -c Release -r win-x64 -p:PublishAot=true
```

The Native AOT publish output is written under:

```text
src\VTracker.Cli\bin\Release\net10.0\win-x64\publish\
```

## Usage

### Extract an MSI and emit a standalone manifest

```powershell
vtracker extract `
  --msi "L:\Dyalog19.0\windows_64_19.0.49959_unicode\setup_64_unicode.msi" `
  --patch "L:\Dyalog19.0\windows_64_19.0.49959_unicode\patch_19.0.49959.0_64_unicode_2024.08.06.msd" `
  --emit-manifest
```

Key `extract` options:

- `--msi <path>`: required base MSI
- `--patch <path>`: repeatable, order-preserving patch input
- `--out <zip-path>`: explicit ZIP output path
- `--work-dir <path>`: explicit work directory
- `--keep-work-dir`: retain the work directory on success
- `--emit-manifest`: also write `<archive-name>.manifest.json`
- `--max-parallelism <n>`: override hashing and metadata concurrency

Default `extract` behavior:

- If `--out` is omitted, the ZIP name is derived from the parent directory of the MSI
- If `--work-dir` is omitted, VTracker creates a temporary work directory
- Work directories are deleted only on success unless you request `--keep-work-dir`
- Work directories and installer logs are retained automatically on failure

The generated ZIP contains:

- the extracted product tree
- `_manifest.json` at the ZIP root

### Compare two releases

```powershell
vtracker compare `
  --left "D:\Archives\windows_64_19.0.49959_unicode.zip" `
  --right "D:\Archives\windows_64_19.0.50000_unicode.zip"
```

JSON output for automation:

```powershell
vtracker compare `
  --left "D:\Archives\windows_64_19.0.49959_unicode.manifest.json" `
  --right "D:\Archives\windows_64_19.0.50000_unicode.manifest.json" `
  --format json
```

`compare` accepts either ZIP archives or standalone manifest JSON files. A successful comparison returns exit code `0` even when differences are found; nonzero exit codes are reserved for actual errors.

## Manifest and compare semantics

- Manifest paths are stored as relative `/`-separated paths
- Path matching is case-insensitive
- A file is considered **updated** only when its SHA-256 changes
- `lastWriteTimeUtc` is informational only
- `fileVersion` and `productVersion` are captured for `.dll` and `.exe` when available, otherwise `null`
- Patch order is preserved exactly as provided by the caller
- Path-collision and file-read/hash failures are treated as hard errors

## Repository layout

```text
src\VTracker.Cli   CLI entry point and command surface
src\VTracker.Core  extraction, manifest, archive, compare, and utility services
tests\VTracker.Tests  unit and integration coverage
docs\              product and implementation reference documents
```

## License

See [LICENSE](LICENSE).
