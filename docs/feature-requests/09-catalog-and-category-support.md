# Catalog and Category Support

## Status
Pending.

## Goal
Allow files in a manifest or archive to be classified into named categories so that comparisons can be understood in terms of product modules, partial archives can be unpacked by category, and the classification survives across releases of the same product.

## Problem
MSI-delivered products can contain over a thousand files from many distinct modules (core runtime, networking, HTML renderer, samples, certificates, fonts, etc.). The current manifest and compare output treats every file uniformly. There is no way to filter a comparison to a specific module, to unpack only the files belonging to a component, or to understand at a glance which modules changed between two releases.

## Agreed Direction

### Catalog File Format
- The catalog is a three-column CSV file: `type`, `pattern`, `category`.
- `type` is either `G` (glob) or `R` (regex). No other values are valid.
- `G` patterns are matched using `DotNet.Glob` with case-insensitive evaluation, consistent with `GlobFilter`. Supports `*`, `**`, `?`, and `[...]`. A literal path with no wildcards is a valid `G` pattern and behaves as an exact match.
- `R` patterns are matched using `System.Text.RegularExpressions.Regex` with `RegexOptions.IgnoreCase | RegexOptions.NonBacktracking`. The pattern is matched against the full normalized path. Patterns are not automatically anchored; the caller writes `^` and `$` when strict anchoring is needed. `NonBacktracking` is required rather than `Compiled` because `RegexOptions.Compiled` is silently a no-op under Native AOT (the JIT is not available); `NonBacktracking` provides a guaranteed O(n) match and works correctly in AOT builds. `[GeneratedRegex]` cannot be used here because catalog patterns are not known at compile time.
- Both types are always case-insensitive. The user never needs to write `(?i)` or adjust casing.
- Rows are evaluated in file order. The first matching row wins. No type-based priority exists.
- Each file belongs to at most one category. Files matching no row are classified as `Unclassified`.
- The CSV must have a header row. Additional columns are ignored.
- The CSV parser must implement RFC 4180 quoting: any field value that contains a comma must be enclosed in double-quotes (e.g. `R,"^foo{1,3}\.dll$",Core`). A naive `Split(',')` parser is not sufficient. Use a standards-compliant CSV library or implement RFC 4180 field parsing to avoid silent column-boundary errors in regex patterns containing quantifiers such as `{1,4}` or character classes such as `[a,b]`.

Example:
```csv
type,pattern,category
G,FontsFolder/**,Fonts
G,**/locales/**,HTMLRenderer
G,**/chrome_*.pak,HTMLRenderer
G,**/libcef.dll,HTMLRenderer
G,**/libEGL.dll,HTMLRenderer
G,**/libGLESv2.dll,HTMLRenderer
G,**/vk_swiftshader*,HTMLRenderer
G,**/vulkan-1.dll,HTMLRenderer
G,**/v8_context_snapshot.bin,HTMLRenderer
G,**/icudtl.dat,HTMLRenderer
G,**/d3dcompiler_47.dll,HTMLRenderer
G,**/htmlrenderer.dll,HTMLRenderer
G,**/conga*.dll,Conga
G,ProgramFiles64Folder/Dyalog/*/PublicCACerts/**,Certificates
G,ProgramFiles64Folder/Dyalog/*/TestCertificates/**,Certificates
R,dyalog\d+_64.*unicode\.dll,Core
G,ProgramFiles64Folder/Dyalog/*/dyalog.exe,Core
```

### Catalog Discovery
- `--catalog <path>` is accepted by `extract`, `compare`, and `unpack`. Category support is always opt-in.
- If `--catalog` is omitted and a file named `vtracker.catalog.csv` exists in the current working directory, it is used automatically.
- Explicit `--catalog` always overrides auto-discovery.
- When no catalog is active, all commands behave exactly as before this feature.

### `catalog` Subcommand Group

#### `vtracker catalog init`
```
vtracker catalog init --manifest <path> --out <catalog.csv>
```
- Reads a manifest and emits a CSV with one `G` row per file path, all assigned to `Unclassified`, sorted by path.
- Intended as the starting point for first-time classification.

#### `vtracker catalog check`
```
vtracker catalog check --catalog <path> --manifest <path>
```
- Reads a catalog and a manifest.
- Reports every catalog row whose pattern matches zero files in the manifest.
- Dead entries indicate paths that have changed across releases and are candidates for glob conversion or deletion.
- Exit code is `0` whether or not dead entries are found; nonzero only on errors.

#### `vtracker catalog compact`
```
vtracker catalog compact --catalog <path> [--manifest <path>]
```
- Interactive command (requires a TTY). Groups exact-path `G` entries by category and presents them one category at a time.
- For each category, shows the grouped paths and prompts the user to enter a glob or regex pattern.
- Match count preview: if `--manifest` is provided, displays the count of files in that manifest matched by the candidate pattern. If `--manifest` is omitted, displays the count of exact catalog entries that the candidate pattern would replace. Either number is useful; `--manifest` gives a more realistic picture.
- On confirmation, replaces the selected exact entries with the new pattern row.
- Uses `Spectre.Console` for prompts and match-count display.
- Never modifies the catalog without confirmation.

#### `vtracker catalog export`
```
vtracker catalog export --catalog <path> --out <file.csv>
```
- Re-exports the catalog as a CSV (useful for re-sorting or copying to a spreadsheet tool).

#### `vtracker catalog show`
```
vtracker catalog show --catalog <path> [--category <name>]
```
- Prints the catalog contents in a readable table. If `--category` is given, shows only rows for that category.

### Enriched `extract`
- When a catalog is active, each file entry in the generated manifest gains a `category` field.
- Files matching no catalog row receive `"Unclassified"` as the category value.
- The manifest schema version is bumped from `1` to `2` to signal the presence of the optional `category` field.
- `ManifestRepository.NormalizeAndValidate` currently contains a hard equality check `SchemaVersion != 1` and must be updated to accept versions `1` and `2`. Version-1 manifests loaded for comparison must tolerate an absent `category` field (treat as `null`). The check should become a range check (`SchemaVersion < 1 || SchemaVersion > 2`) or equivalent.

### Enriched `compare`
- When a catalog is active, each added, removed, and updated entry in the compare output gains a `category` field (sourced from the right-side manifest for added/updated, left-side for removed).
- `CompareResult.Added` and `CompareResult.Removed` are currently `string[]` (bare paths). Adding `category` requires changing these to object arrays. Define two new types:
  - `CompareAddedFile { string Path; string? Category; }`
  - `CompareRemovedFile { string Path; string? Category; }`
  `CompareResult.Updated` already uses `CompareUpdatedFile`; add a `Category` property to that type as well.
- All three new or modified types must be registered in `VTrackerJsonContext` with `[JsonSerializable]` entries to satisfy Native AOT / source-generation requirements. Omitting this registration causes a runtime serialization failure under AOT.
- This is a breaking change to the JSON compare output schema. Text and pretty output formats are additive only and remain backwards-compatible.
- The compare summary gains a per-category breakdown showing counts of added, removed, and updated files per category.
- Files classified as `Unclassified` are surfaced explicitly in the summary so that newly added files that have not yet been cataloged are immediately visible.
- Category differences between left and right manifests (same path, different category because the catalog changed) are reported separately as provenance-level differences, not as content updates.
- All existing compare behavior is unchanged when no catalog is active.

### New `unpack` Command
```
vtracker unpack --from <zip> --catalog <path> --category <name>
                --out <dir> [--strip-prefix <prefix>] [--dry-run]
```
- Extracts all files belonging to the given category from a ZIP archive into `--out`.
- `--from` must point to a `.zip` file. A standalone manifest JSON (`.json`) cannot be used as the source for `unpack` because manifest files contain only metadata — no file content is embedded. Passing a `.json` path to `--from` is a hard error with a clear message directing the user to use the corresponding ZIP archive.
- `--catalog` is required (or auto-discovered from CWD).
- `--strip-prefix <prefix>`: removes a leading path prefix from every extracted path before writing. The prefix must match exactly (case-insensitive). If the prefix does not match a file's path the file is written with its full path unchanged (no error).
- When `--strip-prefix` is omitted and the session is interactive (TTY detected), VTracker computes the longest common path prefix across all files in the requested category and prompts: `Detected common prefix: "ProgramFiles64Folder/Dyalog/Dyalog APL-64 20.0 Unicode/". Strip it? [Y/n]`. In non-interactive mode, omitting `--strip-prefix` means no stripping.
- `--dry-run`: prints a table of `path → destination` without writing any files. Uses `Spectre.Console` for rendering. Always available regardless of TTY. `--dry-run` is the only valid mode for inspecting what a category contains when you have only the manifest and not the ZIP; in that case, pass the manifest to `catalog show` or `catalog check` instead.
- Exit code is `0` on success, nonzero on errors. Partial writes on failure retain whatever was written (no rollback).

## Implementation Notes
- `GlobFilter.cs` in `VTracker.Core` already provides the `G`-pattern matching infrastructure via `DotNet.Glob` (package `DotNet.Glob` 3.1.3, already in `VTracker.Core.csproj`). The catalog engine adds `R`-pattern support alongside it using `System.Text.RegularExpressions`.
- Catalog rows should be compiled once at load time (pre-compile `Glob` and `Regex` objects) rather than on every file evaluation.
- Regex instances must use `RegexOptions.IgnoreCase | RegexOptions.NonBacktracking`. Do not use `RegexOptions.Compiled` — it is silently ignored under Native AOT because the JIT is not available, providing no performance benefit while giving a false impression of optimization. `NonBacktracking` guarantees O(n) matching and is the correct choice for user-supplied runtime patterns in an AOT build.
- The serialization context `VTrackerJsonContext` uses `System.Text.Json` source generation for AOT safety. Every new serializable type (`CompareAddedFile`, `CompareRemovedFile`, and any modified `CompareUpdatedFile`) must have a corresponding `[JsonSerializable(typeof(...))]` entry added to `VTrackerJsonContext`. Omitting this causes a runtime serialization failure under Native AOT with no compile-time warning.
- `ManifestFileEntry` gains a nullable `string? Category` property. Manifests produced without a catalog write `null` or omit the field; the JSON deserializer must tolerate both (use `JsonIgnoreCondition.WhenWritingNull` and ensure the property has a default of `null`).
- `ManifestRepository.NormalizeAndValidate` contains a hard `SchemaVersion != 1` check that must be relaxed to a range check before this feature ships, or manifests written by `extract --catalog` (schema version 2) will be rejected by all subsequent reads.
- `Spectre.Console` is already present in `VTracker.Cli` and actively used by `SpectreExtractProgressReporter`. The `compact` interactive prompts and the `unpack` prefix-detection prompt follow the same existing pattern.
- The CSV parser must be RFC 4180-compliant. Do not use `string.Split(',')`. Use `Sep` (package `nietras.SepReader`) for CSV parsing — it is explicitly AOT-compatible, reflection-free, and handles quoted fields correctly. `CsvHelper` is not recommended because it relies on reflection and has known Native AOT friction. Proper quoted-field support is required to handle regex patterns containing commas (e.g. `{1,4}` quantifiers, character classes such as `[a,b]`).
- `catalog check` and `catalog compact` operate on the catalog file itself. No MSI, ZIP, or live extraction is required for those commands.
- Help text for every new command and every option must be complete. `ConsoleAppFramework` derives `--help` output from XML doc comments. Treat help text as a first-class deliverable, not an afterthought.

## Validation Expectations
- `catalog init` produces a valid CSV with one row per manifest file, all `Unclassified`.
- `catalog check` correctly identifies patterns that match zero files in a given manifest.
- `catalog compact` without `--manifest` shows the count of exact catalog entries the candidate pattern would replace.
- `catalog compact` with `--manifest` shows the count of files in that manifest matched by the candidate pattern.
- `G` patterns match case-insensitively and support `**` across path segments.
- `R` patterns match case-insensitively against the full normalized path without requiring `(?i)`.
- File-order first-match-wins is respected: an earlier row shadows a later row for the same path.
- A regex pattern containing a comma (e.g. `{1,4}`) round-trips correctly when the CSV field is quoted per RFC 4180.
- `extract --catalog` produces a manifest where every file entry has a `category` field; the manifest `schemaVersion` is `2`.
- Version-2 manifests are loaded successfully. Version-1 manifests are also loaded successfully with `category` defaulting to `null`.
- Passing a version-2 manifest to `compare` without `--catalog` succeeds and ignores the stored category values.
- `compare --catalog` produces a per-category summary and attaches `category` to each diff entry. JSON output uses the new `CompareAddedFile` / `CompareRemovedFile` object types, not bare strings.
- `Unclassified` files are explicitly surfaced in compare output when a catalog is active.
- `unpack --from` with a `.json` path produces a clear error directing the user to the corresponding ZIP.
- `unpack --dry-run` prints the destination table without writing files.
- `unpack` with TTY and no `--strip-prefix` prompts with the detected common prefix.
- `unpack` without TTY and no `--strip-prefix` writes full paths without stripping.
- Auto-discovery loads `vtracker.catalog.csv` from the CWD when `--catalog` is omitted.
- Explicit `--catalog` overrides auto-discovery.
- All commands behave identically to their pre-catalog behavior when no catalog is active.
- Native AOT publish succeeds with no new trimming warnings introduced by this feature.
