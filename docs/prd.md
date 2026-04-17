# Product Requirements Document

## Title
MSI Content Archiver and Manifest Diff Tool

## Status
Draft v1

## Summary
Build a Windows CLI tool that extracts the effective file content of an MSI package, optionally after applying one or more patches, without installing the product. The tool must create a manifest for the extracted content, package the extracted files and embedded manifest into a ZIP archive, optionally emit the manifest as a standalone file, and compare two manifests or archives to report additions, removals, and updates.

The primary use case is archival, inspection, and release-to-release comparison of MSI-based software containing thousands of files.

## Problem Statement
The current workflow for inspecting MSI-delivered content is manual and fragile. It is difficult to answer questions such as:

- What exact files are present in a given release?
- What changes after applying one or more patches?
- Which files were added, removed, or updated between two releases?
- How can a release be archived in a form that is easy to inspect later without rerunning installer tooling?

The solution must avoid installing the product and must produce deterministic, reviewable artifacts.

## Goals
1. Extract the effective file tree of a base MSI using administrative-image semantics.
2. Apply patches in a caller-defined order and capture the patched file tree.
3. Generate a deterministic manifest containing file metadata for all extracted files.
4. Package the extracted content and embedded manifest into a ZIP archive.
5. Optionally emit an external manifest file with the same base name as the ZIP.
6. Compare two manifests or archives and report adds, removes, and updates.
7. Scale to thousands of files and parallelize the expensive parts, especially hashing.
8. Be suitable for Native AOT publication on Windows.

## Non-Goals
- Installing or repairing the product on the local machine.
- Parsing MSI or MSP internals directly without Windows Installer.
- Supporting MST transforms in v1.
- Supporting cross-platform execution in v1.
- Tracking directory entries in the manifest.
- Detecting renames in v1.
- Treating timestamp-only changes as content updates.

## Users
- Release engineering
- Build and packaging maintainers
- Support and diagnostics teams
- Developers investigating content drift between releases

## Assumptions
- The tool runs on Windows.
- A human has already unpacked any outer delivery ZIP, if one exists.
- The directory containing the MSI has a meaningful name and that parent directory name identifies the release.
- Patch order is provided explicitly by the caller and must be preserved.
- Patch files may use nonstandard extensions such as `.msd`; the tool passes the provided file path to `msiexec` and lets Windows Installer decide whether it is valid.

## Key Product Decisions

### Extraction Semantics
The tool will use Windows Installer administrative-image semantics. It will not simulate feature selection or custom installation properties.

### Patch Workflow
The tool will always use a two-step workflow:

1. Create an administrative image from the source MSI in a work directory.
2. Apply each patch, in order, to the MSI located inside that work directory.

This preserves the original MSI and makes the mutation target explicit.

### Naming
If the caller does not explicitly provide an output path, the default output base name is the parent directory name of the source MSI. For example:

- Source MSI: `L:\Dyalog19.0\windows_64_19.0.49959_unicode\setup_64_unicode.msi`
- Default output base name: `windows_64_19.0.49959_unicode`

### Manifest Semantics
- Relative paths are stored with `/` separators.
- Path comparison is case-insensitive.
- `LastWriteTimeUtc` is recorded for information only.
- A file is considered updated only when its SHA-256 changes.
- PE version fields are captured for `.dll` and `.exe` files when available.
- Missing PE version info is represented as `null`.

## User Stories

### Extraction and Archival
As a release engineer, I want to point the tool at an MSI and an ordered list of patches so that I can archive the exact resulting file content without installing the product.

### Offline Inspection
As a support engineer, I want a ZIP that contains the extracted files and manifest so that I can inspect a release later without rerunning the extraction.

### Change Review
As a developer, I want to compare two releases and see which files were added, removed, or updated so that I can understand the impact of a patch or release increment.

### Automation
As a build engineer, I want a CLI and machine-readable compare output so that I can integrate the tool into scripts and pipelines.

## Functional Requirements

### FR-1: Input Handling
The tool must support:

- A base MSI path
- Zero or more patch paths, in explicit order
- An optional work directory
- An optional explicit ZIP output path
- An optional request to emit an external manifest
- A compare command that accepts ZIP files and standalone manifests

The tool does not need to ingest outer delivery ZIP files in v1.

### FR-2: Administrative Image Extraction
The tool must:

1. Create a work directory.
2. Run `msiexec /a` against the source MSI with `TARGETDIR` set to the work directory.
3. Produce a Windows Installer log for the extraction step.
4. Fail if `msiexec` returns a nonzero exit code.

### FR-3: Patch Application
For each patch path, in caller-defined order, the tool must:

1. Target the MSI inside the work directory.
2. Run `msiexec /p <patch> /a <workdir-msi>`.
3. Produce a Windows Installer log for the patch step.
4. Fail if any patch step returns a nonzero exit code.

### FR-4: Manifest Generation
The tool must generate a JSON manifest containing:

- `schemaVersion`
- Tool metadata
- Input and provenance metadata
- Extraction metadata
- A sorted file-entry array

Each file entry must contain:

- `path`
- `lastWriteTimeUtc`
- `size`
- `sha256`
- `fileVersion`
- `productVersion`

File entries must be sorted by normalized path.

### FR-5: Path Normalization and Collision Handling
The tool must:

- Normalize manifest paths to `/`-separated relative paths
- Compare and index paths case-insensitively
- Fail the run if two files collapse to the same normalized comparison key

### FR-6: ZIP Packaging
The tool must create a ZIP archive whose root contains:

- The extracted files and directories
- `_manifest.json`

Compression mode defaults to `Optimal`.

### FR-7: Optional External Manifest
When requested, the tool must write an external manifest file beside the ZIP using the same base name, for example:

- `windows_64_19.0.49959_unicode.zip`
- `windows_64_19.0.49959_unicode.manifest.json`

### FR-8: Compare
The compare command must:

- Accept ZIP archives and standalone manifest files
- Ignore provenance differences for add/remove/update classification
- Report provenance differences separately
- Classify differences by normalized path as:
  - Added
  - Removed
  - Updated

A file is updated when the SHA-256 differs.

### FR-9: Compare Output
The compare command must provide:

- Human-readable text output by default
- Optional JSON output for automation

### FR-10: Exit Codes
The compare command must return:

- `0` when the comparison succeeds, even if differences are found
- Nonzero only when an actual error occurs

### FR-11: Failure Behavior
On failure, the tool must:

- Keep the work directory
- Keep generated Windows Installer logs
- Fail the run if any file cannot be read or hashed
- Continue when PE version info is unavailable by storing `null`

### FR-12: Performance
The tool must:

- Auto-tune worker counts for hashing and metadata collection
- Allow CLI overrides for concurrency
- Be practical for archives containing thousands of files

## Non-Functional Requirements

### NFR-1: Determinism
Given the same inputs and patch order, the manifest and compare classification must be deterministic.

### NFR-2: Reliability
The tool must fail explicitly on partial or ambiguous results rather than silently skipping problems.

### NFR-3: Observability
The tool must retain sufficient logs and work artifacts on failure to diagnose extraction and patching problems.

### NFR-4: Performance
Hashing must be parallelized with bounded concurrency. Manifest generation must remain responsive with large file counts.

### NFR-5: AOT Compatibility
The implementation should avoid reflection-heavy patterns and use libraries and APIs that are compatible with trimming and Native AOT on Windows.

## Proposed CLI Surface

The exact executable name is TBD. Examples below use `vtracker`.

### Extract
```powershell
vtracker extract `
  --msi "L:\Dyalog19.0\windows_64_19.0.49959_unicode\setup_64_unicode.msi" `
  --patch "L:\Dyalog19.0\windows_64_19.0.49959_unicode\patch_19.0.49959.0_64_unicode_2024.08.06.msd" `
  --emit-manifest
```

### Extract with explicit output and retained work directory
```powershell
vtracker extract `
  --msi "L:\Dyalog19.0\windows_64_19.0.49959_unicode\setup_64_unicode.msi" `
  --patch "L:\Dyalog19.0\windows_64_19.0.49959_unicode\patch_19.0.49959.0_64_unicode_2024.08.06.msd" `
  --out "D:\Archives\windows_64_19.0.49959_unicode.zip" `
  --work-dir "D:\Temp\windows_64_19.0.49959_unicode" `
  --keep-work-dir `
  --emit-manifest
```

### Compare
```powershell
vtracker compare `
  --left "D:\Archives\windows_64_19.0.49959_unicode.zip" `
  --right "D:\Archives\windows_64_19.0.50000_unicode.zip"
```

### Compare with JSON output
```powershell
vtracker compare `
  --left "D:\Archives\windows_64_19.0.49959_unicode.manifest.json" `
  --right "D:\Archives\windows_64_19.0.50000_unicode.manifest.json" `
  --format json
```

## Acceptance Criteria
1. The tool extracts an administrative image from a valid MSI without installing the product.
2. The tool applies one or more patches in the provided order to the administrative image.
3. The resulting ZIP contains the extracted tree and `_manifest.json`.
4. The optional external manifest matches the embedded manifest content.
5. The manifest contains normalized paths, SHA-256, file size, UTC timestamp, and PE version fields.
6. The tool fails if any extracted file cannot be read or hashed.
7. The compare command correctly reports adds, removes, and updates for two manifests or archives.
8. The compare command reports provenance differences separately.
9. The tool handles releases with thousands of files without serial hashing.

## Risks
- Windows Installer behavior can vary depending on package authoring and patch applicability rules.
- Some package layouts may produce unexpected file timestamps even when content is unchanged.
- ZIP compression can dominate wall-clock time after hashing is optimized.
- Native AOT compatibility must be validated early for any chosen CLI parsing library.

## Open Implementation Questions
- Final executable name
- Exact JSON shape for compare output
- Whether to include per-step timing data in the manifest provenance
