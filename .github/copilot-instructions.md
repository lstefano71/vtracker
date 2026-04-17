# Copilot instructions for VTracker

## Current repository state
- The repository currently contains planning documents only under `docs\`; there is no checked-in solution, project file, test project, or lint configuration yet.
- Treat `docs\prd.md` as the product contract and `docs\implementation-plan.md` as the implementation design. Keep them aligned with any code you add.

## Build, test, and lint
- `docs\implementation-plan.md` defines the planned Native AOT validation command for the future CLI:

```powershell
dotnet publish .\src\VTracker.Cli\VTracker.Cli.csproj -c Release -r win-x64 -p:PublishAot=true
```

## Planned architecture
- VTracker is intended to be a Windows-only .NET 10 CLI that extracts an MSI administrative image, optionally applies patches in caller-defined order, generates a manifest for the resulting file tree, packages the extracted tree and `_manifest.json` into a ZIP, and compares two manifests or archives.
- The planned solution split is:
  - `src\VTracker.Cli`: thin CLI layer using `Cysharp.ConsoleAppFramework` for command parsing, validation, and text/JSON output.
  - `src\VTracker.Core`: reusable extraction, hashing, PE version lookup, manifest I/O, ZIP packaging, path normalization, and compare logic.
  - `tests\VTracker.Tests`: unit and integration coverage for normalization, collision detection, manifest sorting, ZIP round-trips, compare classification, and file-system behavior.
- The `extract` workflow should be: resolve default output and work paths -> run `msiexec /a` -> locate the copied MSI inside the administrative image -> apply patches sequentially with `msiexec /p ... /a` -> enumerate extracted files -> collect hashes and metadata in parallel -> write a deterministic manifest -> optionally emit an external manifest -> create the ZIP -> delete the temp work directory only on success.
- The `compare` workflow should accept either ZIPs or standalone manifests, load `_manifest.json` when given a ZIP, compare entries by normalized path, classify added/removed/updated files from SHA-256 differences, and report provenance differences separately from file-content changes.

## Key conventions
- Windows-only behavior is intentional. Use Windows Installer administrative-image semantics and do not add cross-platform behavior or direct MSI/MSP parsing in v1.
- Patch order comes from the caller and must be preserved exactly; do not reorder patches or infer sequencing.
- When `--out` is omitted, derive the default output base name from the parent directory of the source MSI.
- Manifest paths are stored as relative `/`-separated paths, sorted by normalized path, and compared case-insensitively.
- A file counts as updated only when its SHA-256 changes. `LastWriteTimeUtc` is informational and timestamp-only differences are not updates.
- Capture `fileVersion` and `productVersion` for `.dll` and `.exe` when available; store `null` instead of failing when PE version info is missing.
- Fail explicitly on unreadable files, hashing failures, nonzero `msiexec` exits, ambiguous or missing MSI discovery inside the administrative image, and normalized-path collisions. Do not silently skip partial results.
- Keep the work directory and Windows Installer logs on failure. Use stable log names such as `01-admin-image.log` and `02-patch-001.log`.
- `_manifest.json` belongs at the ZIP root and describes the extracted product tree only; do not include the manifest file itself in the file-entry array.
- Prefer Native AOT-safe implementation choices: explicit models, `System.Text.Json` source generation, and avoiding reflection-heavy or dynamic patterns.
