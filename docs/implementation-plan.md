# Detailed Implementation Plan

## Objective
Implement a Windows CLI in C# 14 on .NET 10 that:

1. Creates an administrative image from a base MSI.
2. Applies zero or more patches in explicit order.
3. Builds a manifest for the resulting extracted file tree.
4. Packages the extracted tree and embedded manifest into a ZIP.
5. Optionally emits a standalone manifest file.
6. Compares two manifests or archives and reports adds, removes, and updates.

The implementation should be Native AOT-friendly and should parallelize hashing and file metadata collection where it matters.

## Recommended Solution Shape

### Project Layout
Use a small solution with clear boundaries:

- `src\VTracker.Cli`
- `src\VTracker.Core`
- `tests\VTracker.Tests`

If the repository already has a preferred layout, follow it. The important split is:

- CLI and command binding in one project
- Extraction, manifest, packaging, and comparison logic in a reusable core project

Recommended dependency choice:

- `VTracker.Cli` uses `ConsoleAppFramework` for command parsing and binding

### High-Level Architecture

#### CLI Layer
Responsibilities:

- Parse arguments
- Resolve defaults
- Validate command combinations
- Invoke application services
- Render human-readable or JSON output
- Map exceptions to exit codes

#### Application Layer
Responsibilities:

- Orchestrate extract and compare workflows
- Create and clean up workspaces
- Coordinate logging and progress reporting

#### Domain and Infrastructure Layer
Responsibilities:

- Run `msiexec`
- Hash files
- Read PE version info
- Normalize paths
- Read and write manifests
- Build ZIP archives
- Compare manifests

## Command Design

The CLI executable name is `vtracker`.

## `extract`
Recommended options:

- `--msi <path>` (required)
- `--patch <path>` (repeatable, optional, order-preserving)
- `--out <zip-path>` (optional)
- `--work-dir <path>` (optional)
- `--keep-work-dir` (optional)
- `--emit-manifest` (optional)
- `--max-parallelism <n>` (optional)
- `--catalog <path>` (optional) — path to a catalog CSV for file classification

Default behavior:

- If `--out` is omitted, derive the base name from the parent directory of the MSI.
- If `--work-dir` is omitted, create a temp work directory and delete it on success.
- If `--work-dir` is supplied, delete it only when the caller does not also ask to keep it.
- Keep the work directory automatically on failure.

## `compare`
Recommended options:

- `--left <path>` (required)
- `--right <path>` (required)
- `--format <text|json>` (optional, default `text`)
- `--catalog <path>` (optional) — path to a catalog CSV for per-category breakdown

Behavior:

- Accept ZIPs and standalone manifest files.
- Return `0` on a successful comparison, even when differences exist.
- Return nonzero only on actual errors.
- When a catalog is active, output includes a per-category breakdown of added/removed/updated files. Category-only differences (same SHA-256, different stored category) are reported as provenance differences.

## `unpack`
Options:

- `--from <path>` (required) — source ZIP archive
- `--catalog <path>` (optional) — auto-discovers `vtracker.catalog.csv` in CWD when omitted
- `--category <name>` (required) — category name to extract (case-insensitive)
- `--out <dir>` (required) — output directory
- `--strip-prefix <prefix>` (optional) — path prefix to strip from extracted paths
- `--dry-run` (optional) — print extraction plan without writing files

Behavior:

- Requires a catalog (explicit or auto-discovered).
- Loads `_manifest.json` from the ZIP, classifies files by catalog, filters to the requested category.
- Computes the longest common directory prefix across matching files.
- In interactive mode, prompts to strip the detected common prefix when `--strip-prefix` is omitted.
- `--dry-run` shows a source → destination mapping table without writing files.
- Rejects `.json` paths with a clear error directing the user to use the corresponding ZIP.

## `catalog` Subcommand Group

### `catalog init`
- `--manifest <path>` (required) — manifest or ZIP to seed from
- `--out <path>` (required) — output catalog CSV

Creates one exact-path glob row per file, all assigned to `Unclassified`, sorted by path.

### `catalog check`
- `--catalog <path>` (required) — catalog CSV to validate
- `--manifest <path>` (required) — manifest or ZIP to check against

Reports catalog patterns that match zero files in the manifest.

### `catalog compact`
- `--catalog <path>` (required) — catalog CSV to compact
- `--manifest <path>` (optional) — manifest to validate replacement patterns against

Interactive TTY command. Groups exact-path entries by category, prompts for a replacement glob pattern per group, and rewrites the catalog.

### `catalog export`
- `--catalog <path>` (required) — source catalog CSV
- `--out <path>` (required) — output CSV path

Re-serialises the catalog through the writer to normalise quoting and escaping.

### `catalog show`
- `--catalog <path>` (required) — catalog CSV to display
- `--category <name>` (optional) — filter to a specific category

Renders catalog entries as a Spectre.Console table in interactive mode, or plain CSV in piped mode.

## Native AOT Strategy

### Design Rules
- Prefer static typing and source-generated serializers.
- Avoid reflection-heavy frameworks where possible.
- Keep serialization contracts explicit.
- Validate AOT publish early, not after the full feature set is implemented.

### Serialization
Use `System.Text.Json` with source generation.

Recommended pattern:

- Create DTOs for manifest and compare output.
- Add a `JsonSerializerContext` for all serialized models.
- Avoid dynamic object graphs.

### CLI Parsing
Use `Cysharp.ConsoleAppFramework` as the CLI layer.

Why this is the best fit here:

- It is explicitly positioned as AOT-safe.
- It is source-generator based, which aligns well with Native AOT and trimming.
- It avoids reflection-heavy command binding.
- It is optimized for low startup overhead, which is valuable for a CLI utility.
- The command surface for this tool is small and maps cleanly to method-based command handlers.

Implementation notes:

- Keep command handlers thin and delegate real work to `VTracker.Core`.
- Model command options with explicit parameter types instead of dynamic parsing.
- Validate the exact package version against `.NET 10` and Native AOT early in Phase 1.
- Lock the package version once publish validation succeeds.

## Core Data Contracts

### Manifest Root
Suggested shape:

```json
{
  "schemaVersion": 2,
  "tool": {
    "name": "vtracker",
    "version": "1.0.0"
  },
  "source": {
    "msiPath": "L:\\Dyalog19.0\\windows_64_19.0.49959_unicode\\setup_64_unicode.msi",
    "msiSha256": "..."
  },
  "patches": [
    {
      "sequence": 1,
      "path": "L:\\Dyalog19.0\\windows_64_19.0.49959_unicode\\patch_19.0.49959.0_64_unicode_2024.08.06.msd",
      "sha256": "..."
    }
  ],
  "extraction": {
    "mode": "administrative-image",
    "workDirKept": false,
    "compression": "Optimal"
  },
  "files": [
    {
      "path": "bin/aplcore.dll",
      "lastWriteTimeUtc": "2026-04-17T00:00:00Z",
      "size": 123456,
      "sha256": "...",
      "fileVersion": "19.0.49959.0",
      "productVersion": "19.0",
      "category": "Runtime"
    }
  ]
}
```

Schema version 1 manifests have no `category` field. Schema version 2 manifests include `category` on every file entry (set when a catalog is active during `extract`). Supported versions: 1 and 2. The `category` field is omitted from JSON when null (`JsonIgnoreCondition.WhenWritingNull`).

### File Entry Rules
- `path` is relative, normalized to `/`, and compared case-insensitively.
- `lastWriteTimeUtc` is informational only.
- `size` is byte size as a 64-bit integer.
- `sha256` is lowercase hex.
- `fileVersion` and `productVersion` are nullable strings.
- Per-step timing data is intentionally omitted from manifest provenance in v1.

### Compare Result Shape
Suggested model:

```json
{
  "summary": {
    "added": 12,
    "removed": 3,
    "updated": 27,
    "provenanceDifferences": 2,
    "categoryBreakdown": [
      { "category": "Runtime", "added": 8, "removed": 1, "updated": 20 },
      { "category": "Unclassified", "added": 4, "removed": 2, "updated": 7 }
    ]
  },
  "added": [
    { "path": "bin/new.dll", "category": "Runtime" }
  ],
  "removed": [
    { "path": "bin/old.dll", "category": "Runtime" }
  ],
  "updated": [
    {
      "path": "bin/aplcore.dll",
      "category": "Runtime",
      "left": {
        "sha256": "...",
        "size": 123456,
        "fileVersion": "19.0.49959.0",
        "productVersion": "19.0"
      },
      "right": {
        "sha256": "...",
        "size": 125000,
        "fileVersion": "19.0.50000.0",
        "productVersion": "19.0"
      }
    }
  ],
  "provenanceDifferences": [
    "Patch sequence differs"
  ]
}
```

The `categoryBreakdown` and `category` fields are present only when a catalog is active. Without a catalog, `added` and `removed` entries still use the object form (`{ path, category }`) but `category` is null and omitted from JSON.

## Extraction Workflow

### Step 1: Resolve Paths and Names
Inputs:

- Base MSI path
- Ordered patch list
- Optional output path
- Optional work directory

Rules:

- Validate file existence before doing expensive work.
- If no output path is supplied, derive the output base name from the parent directory of the MSI.
- Choose a temp work directory when no work directory is provided.
- Create a log directory under the work directory.

### Step 2: Create Administrative Image
Run:

```powershell
msiexec.exe /a "L:\Dyalog19.0\windows_64_19.0.49959_unicode\setup_64_unicode.msi" /qn TARGETDIR="D:\Temp\windows_64_19.0.49959_unicode\image" /l*vx "D:\Temp\windows_64_19.0.49959_unicode\logs\01-admin-image.log"
```

Implementation notes:

- Use `ProcessStartInfo`.
- Set `UseShellExecute = false`.
- Capture exit code.
- Keep the log path stable and predictable.
- Fail immediately on nonzero exit.

### Step 3: Locate the MSI Inside the Administrative Image
After the admin image is created, locate the MSI inside the work image. Prefer:

- Same file name as the source MSI under the image root, if present
- Otherwise fail explicitly with a clear error message

Do not guess across multiple MSI candidates.

### Step 4: Apply Patches Sequentially
For each patch path, in input order, run:

```powershell
msiexec.exe /p "L:\Dyalog19.0\windows_64_19.0.49959_unicode\patch_19.0.49959.0_64_unicode_2024.08.06.msd" /a "D:\Temp\windows_64_19.0.49959_unicode\image\setup_64_unicode.msi" /qn /l*vx "D:\Temp\windows_64_19.0.49959_unicode\logs\02-patch-001.log"
```

Implementation notes:

- Preserve caller order exactly.
- Do not attempt automatic sequencing or validation heuristics in v1.
- Fail on the first nonzero exit code.

## File Enumeration and Manifest Building

### Enumeration Rules
- Enumerate files recursively under the extracted image root.
- Exclude installer log files and any work-area artifacts not part of the extracted product tree.
- Compute a relative path from the image root.
- Normalize separators to `/`.

### Path-Collision Detection
Create a dictionary keyed by normalized path using `StringComparer.OrdinalIgnoreCase`.

If insertion finds an existing key with a different original path, fail the run immediately. This prevents ambiguous compare behavior later.

### Parallel Metadata Pipeline
Recommended implementation:

1. Enumerate files into a lightweight work-item sequence.
2. Process files with `Parallel.ForEachAsync`.
3. Use a bounded degree of parallelism.
4. For each file:
   - Read length
   - Read last write time in UTC
   - Compute SHA-256
   - If extension is `.dll` or `.exe`, read file version info

Suggested default degree:

```csharp
var degree = requestedDegree > 0
    ? requestedDegree
    : Math.Max(2, Environment.ProcessorCount - 1);
```

### Hashing Strategy
Use streaming SHA-256 over a `FileStream` opened with `FileOptions.SequentialScan`.

Recommended `FileStreamOptions`:

- `Mode = FileMode.Open`
- `Access = FileAccess.Read`
- `Share = FileShare.Read`
- `Options = FileOptions.SequentialScan`

Keep hashing single-pass per file. Do not read the same file twice just to gather metadata.

### PE Version Strategy
For `.dll` and `.exe`:

- Use `FileVersionInfo.GetVersionInfo(path)`
- Store `FileVersion` and `ProductVersion`
- If unavailable or unreadable, store `null`

Do not fail the run because version metadata is missing.

## Packaging

### Embedded Manifest
Write `_manifest.json` at the root of the ZIP.

### ZIP Contents
The ZIP must contain:

- Every extracted file, preserving relative layout
- `_manifest.json`

The manifest file should not be included in the file-entry array, because the manifest describes the extracted product tree, not the archive wrapper.

### Compression
Default to `CompressionLevel.Optimal`.

### Packaging Order
Recommended order:

1. Complete extraction and patch application.
2. Build the manifest from the extracted image.
3. Optionally write the external manifest.
4. Create the ZIP.
5. Delete the temp work directory only on success.

This avoids packaging partial outputs.

## Compare Workflow

### Input Resolution
Support:

- `.zip`
- `.json`

Rules:

- If input is `.zip`, extract only `_manifest.json` from the archive.
- If input is `.json`, read it directly.
- Reject other file types with a clear message.

### Comparison Algorithm
1. Load both manifests.
2. Validate `schemaVersion`.
3. Build dictionaries of file entries keyed by normalized path using case-insensitive comparison.
4. Compute:
   - Added: in right only
   - Removed: in left only
   - Updated: present in both, SHA differs
5. Compare provenance separately and record differences as informational findings.

### Text Output
Suggested text layout:

```text
Added: 12
Removed: 3
Updated: 27
Provenance differences: 2

+ bin/new.dll
- bin/old.dll
~ bin/aplcore.dll
```

### JSON Output
Serialize the compare result with the source-generated JSON context.

## Error Handling

### Hard Failures
Fail the run when:

- The MSI path is missing or unreadable
- A patch path is missing or unreadable
- `msiexec` returns nonzero
- The MSI inside the admin image cannot be found unambiguously
- A file cannot be read
- A SHA cannot be computed
- Two files collapse to the same normalized compare key
- The ZIP cannot be written

### Non-Fatal Conditions
Do not fail when:

- `FileVersion` is absent
- `ProductVersion` is absent
- Provenance differs during compare

### Failure Retention
On failure:

- Keep the work directory
- Keep all logs
- Print the key paths needed for investigation

## Logging and Diagnostics

### Installer Logs
Create per-step logs with stable names:

- `01-admin-image.log`
- `02-patch-001.log`
- `02-patch-002.log`

### Application Logging
For v1, simple structured console output is sufficient:

- Step started
- Step finished
- File counts
- Output paths
- Failure locations

Avoid introducing a heavy logging framework unless the repository already uses one.

## Suggested Types and Components

### Core Types
- `ExtractOptions`
- `CompareOptions`
- `ManifestDocument`
- `ManifestFileEntry`
- `CompareResult`
- `CompareAddedFile`
- `CompareRemovedFile`
- `CompareUpdatedFile` (was `UpdatedFileEntry`)
- `CompareCategoryBreakdown`
- `CatalogRow`, `CatalogRowType`, `CompiledCatalogEntry`, `CatalogFile`
- `UnpackRequest`, `UnpackFileMapping`, `UnpackResult`
- `CatalogCheckResult`, `CatalogCheckDeadEntry`

### Services
- `MsiexecRunner`
- `AdministrativeImageService`
- `PatchApplicationService`
- `ManifestBuilder`
- `HashService`
- `PeVersionService`
- `ZipPackagingService`
- `ManifestRepository`
- `ManifestComparator`
- `WorkspaceManager`
- `PathNormalizer`
- `CatalogParser` — RFC 4180 CSV parsing with `Sep`
- `CatalogClassifier` — First-match-wins file classification
- `CatalogDiscovery` — Resolves explicit `--catalog` or auto-discovers `vtracker.catalog.csv`
- `CatalogWriter` — RFC 4180 CSV writing with proper quoting
- `CatalogInitService` — Seeds a catalog from a manifest
- `CatalogCheckService` — Reports dead catalog patterns
- `UnpackService` — Category-filtered extraction from ZIP archives

### Utility Helpers
- `OutputNameResolver`
- `ProcessFailureException`
- `NormalizedPathCollisionException`

## Incremental Delivery Plan

### Phase 1: Skeleton and Contracts
- Create solution and project structure
- Add `ConsoleAppFramework` and wire the command entry points
- Define options models
- Define manifest DTOs
- Add source-generated JSON context

Deliverable: buildable CLI that validates arguments and can serialize an empty manifest shape.

### Phase 2: Extraction Foundation
- Implement workspace creation
- Implement `msiexec /a`
- Implement installer log creation
- Locate MSI inside admin image

Deliverable: successful admin-image creation for a base MSI.

### Phase 3: Patch Application
- Implement ordered patch application
- Add per-patch logs
- Fail fast on patch errors

Deliverable: patched admin-image workflow.

### Phase 4: Manifest Builder
- Enumerate files
- Normalize paths
- Detect collisions
- Implement parallel hashing
- Read PE version info
- Produce deterministic sorted manifest

Deliverable: complete manifest JSON for extracted content.

### Phase 5: Packaging
- Write `_manifest.json`
- Produce ZIP with extracted tree and manifest
- Optionally emit external manifest
- Implement cleanup rules

Deliverable: archive output ready for future inspection.

### Phase 6: Compare
- Read manifest from ZIP or JSON
- Compare file entries
- Compare provenance separately
- Implement text and JSON output

Deliverable: usable release-to-release diff tool.

### Phase 7: AOT Hardening
- Publish with Native AOT
- Resolve trimming warnings
- Replace any AOT-hostile dependencies

Deliverable: publishable AOT binary for Windows.

### Phase 8: Test Coverage and Polish
- Add unit and integration tests
- Improve error messages
- Add sample command documentation

Deliverable: production-ready v1.

## Testing Strategy

### Unit Tests
Focus on pure logic:

- Output name resolution
- Path normalization
- Path collision detection
- Manifest sorting
- Compare classification
- Provenance diff reporting

### Integration Tests
Focus on real file-system behavior:

- Manifest generation on synthetic directory trees
- ZIP creation and manifest round-trip
- Compare on known manifests

### Installer Integration Tests
If stable sample packages are available:

- Admin-image creation from a test MSI
- Patch application order
- Failure behavior and retained logs

Keep these tests optional if they depend on large or proprietary packages.

### AOT Validation
Add a publish validation step early:

```powershell
dotnet publish .\src\VTracker.Cli\VTracker.Cli.csproj -c Release -r win-x64 -p:PublishAot=true
```

After publishing, smoke-test at least:

- `--help`
- `extract --help`
- `compare --help`

This should be part of regular validation once the core command set is in place.

## Performance Notes

### What to Parallelize
Parallelize:

- SHA-256 computation
- File metadata collection
- PE version inspection

Do not try to parallelize:

- Patch application
- ZIP writing through a single `ZipArchive`

### Expected Bottlenecks
Most likely bottlenecks:

- Disk throughput during hashing
- ZIP creation with `Optimal` compression
- `msiexec` work during admin-image creation and patch application

### Tuning Hooks
Provide:

- `--max-parallelism`

This is enough for v1. Avoid overdesigning a tuning surface before real measurements exist.

## Key Risks and Mitigations

### Risk: Native AOT Integration Issues
Mitigation:

- Validate AOT in Phase 1 or 2
- Use `ConsoleAppFramework` for the CLI layer because it is designed for AOT-safe command binding
- Keep serialization source-generated and avoid adding reflection-heavy dependencies

### Risk: Ambiguous Admin-Image MSI Location
Mitigation:

- Require a clear match by original MSI name
- Fail explicitly if not found

### Risk: False Change Signals from Metadata
Mitigation:

- Use SHA-256 as the update signal
- Keep timestamps informational only

### Risk: Performance Regressions on Large Trees
Mitigation:

- Parallelize hashing
- Measure file counts, bytes processed, and elapsed time
- Keep the pipeline streaming where practical

## Done Criteria
The implementation is done when:

1. `extract` produces a ZIP with extracted content and `_manifest.json`.
2. Optional external manifest emission works with the same base name.
3. Zero or more patches can be applied in caller order.
4. The manifest captures normalized paths, timestamps, sizes, SHA-256, and PE version fields.
5. `compare` accepts ZIPs and standalone manifests.
6. `compare` reports adds, removes, updates, and provenance differences.
7. Work directories and logs are retained on failure.
8. The CLI publishes successfully with Native AOT on Windows.
9. `extract --catalog` classifies files and produces schema version 2 manifests with categories.
10. `compare --catalog` produces per-category breakdown and annotates added/removed/updated with categories.
11. `catalog init` seeds a catalog from a manifest with all entries as `Unclassified`.
12. `catalog check` reports dead patterns matching zero files.
13. `catalog compact` interactively replaces exact-path entries with broader patterns.
14. `catalog export` re-serialises a catalog with normalised quoting.
15. `catalog show` displays catalog entries as a table, optionally filtered by category.
16. `unpack` extracts files by category from a ZIP, with optional prefix stripping and dry-run support.
