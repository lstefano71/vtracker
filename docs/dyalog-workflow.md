# Tracking Dyalog APL Releases and Patches with VTracker

This guide describes how to use VTracker to understand exactly which files change when Dyalog releases a new version of the interpreter or a patch, and how to use that information to maintain a development environment.

## The problem

When Dyalog ships a patch (typically an `.msd` file), the naive integrated patching mechanism updates the main executables and DLLs — `dyalog.exe`, the core runtime DLL — but does not always update every support DLL. Libraries for the HTMLRenderer (CEF/Chromium dependencies like `libcef.dll`, locale data, shader libraries), Conga networking DLLs, and similar satellite files are covered by the `.msd` patch file but are not always surfaced by the standard update path.

This creates a real risk: you apply a patch, the main binaries update, and everything looks correct — but a subset of support DLLs are stale. In a development environment where you maintain a curated copy of the interpreter tree, the only reliable way to know what actually changed is to compare the full extracted file tree before and after patching.

VTracker solves this by extracting the complete administrative image of the MSI, applying the patch against it, and producing a deterministic manifest of every file. Comparing two manifests reveals exactly which files changed, down to the SHA-256 hash.

## Prerequisites

- VTracker (win-x64 Native AOT binary from [GitHub Releases](https://github.com/lstefano71/vtracker/releases) or built from source)
- The base MSI installer for the Dyalog version (e.g. `setup_64_unicode.msi`)
- Any patch files (`.msd`) to apply
- A catalog CSV file if you want per-module classification (optional but recommended)

## Step 1 — Archive the base release

Extract the base MSI without any patches to establish a baseline:

```powershell
vtracker extract `
  --msi "L:\Dyalog20.0\windows_64_20.0.50000_unicode\setup_64_unicode.msi" `
  --emit-manifest
```

This produces:
- `windows_64_20.0.50000_unicode.zip` — the full extracted file tree plus `_manifest.json`
- `windows_64_20.0.50000_unicode.manifest.json` — a standalone copy of the manifest

The ZIP name is derived automatically from the parent directory of the MSI.

## Step 2 — Archive the patched release

Run the same extraction but include the patch:

```powershell
vtracker extract `
  --msi "L:\Dyalog20.0\windows_64_20.0.50000_unicode\setup_64_unicode.msi" `
  --patch "L:\Dyalog20.0\windows_64_20.0.50000_unicode\patch_20.0.50000.0_64_unicode_2025.03.15.msd" `
  --out "windows_64_20.0.50000_unicode_patched.zip" `
  --emit-manifest
```

Use `--out` to give the patched archive a distinct name — otherwise it would overwrite the base archive since both derive from the same parent directory.

Multiple patches can be applied in sequence by repeating `--patch`. The order you specify is the order they are applied:

```powershell
vtracker extract `
  --msi "L:\Dyalog20.0\windows_64_20.0.50000_unicode\setup_64_unicode.msi" `
  --patch "L:\Dyalog20.0\patches\patch_001.msd" `
  --patch "L:\Dyalog20.0\patches\patch_002.msd" `
  --out "windows_64_20.0.50000_unicode_patched_002.zip" `
  --emit-manifest
```

## Step 3 — Compare base vs. patched

See exactly what the patch changed:

```powershell
vtracker compare `
  --left "windows_64_20.0.50000_unicode.zip" `
  --right "windows_64_20.0.50000_unicode_patched.zip"
```

In an interactive terminal this produces colour-coded output showing added, removed, and updated files with their sizes and version numbers.

To focus on DLLs and EXEs only:

```powershell
vtracker compare `
  --left "windows_64_20.0.50000_unicode.zip" `
  --right "windows_64_20.0.50000_unicode_patched.zip" `
  --include "**/*.dll" `
  --include "**/*.exe"
```

For machine-readable output (e.g. to feed into a script):

```powershell
vtracker compare `
  --left "windows_64_20.0.50000_unicode.manifest.json" `
  --right "windows_64_20.0.50000_unicode_patched.manifest.json" `
  --format json
```

The compare output shows you, for instance, that the patch updated not just `dyalog.exe` and the core runtime DLL but also `libcef.dll`, locale data files under `locales/`, and `htmlrenderer.dll` — files the standard patching UI might not make obvious.

## Step 4 — Classify files with a catalog (recommended)

A flat list of changed files is useful but can be overwhelming when the product contains over a thousand files. A catalog CSV classifies files into named categories so that the comparison output tells you *which modules* were affected.

### Initialise the catalog

Start from the base manifest to get a skeleton:

```powershell
vtracker catalog init `
  --manifest "windows_64_20.0.50000_unicode.zip" `
  --out "dyalog.catalog.csv"
```

This creates a CSV with one row per file, all assigned to `Unclassified`. Open it in a text editor or spreadsheet and replace exact paths with broader glob patterns, assigning each to a meaningful category:

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

Rows are evaluated in order; the first match wins. Files matching no row are `Unclassified`.

The `compact` subcommand can help you interactively consolidate exact paths into broader patterns:

```powershell
vtracker catalog compact `
  --catalog "dyalog.catalog.csv" `
  --manifest "windows_64_20.0.50000_unicode.zip"
```

### Validate the catalog against a new release

When a new release arrives, check for dead patterns (files that were removed or renamed):

```powershell
vtracker catalog check `
  --catalog "dyalog.catalog.csv" `
  --manifest "windows_64_20.0.50100_unicode.zip"
```

**Important:** also review the compare output for any `Unclassified` files. New support DLLs introduced by a release will not match existing catalog patterns and will appear as `Unclassified` — this is exactly the blind spot you want to catch early. Update the catalog to classify them before relying on per-module conclusions.

## Step 5 — Compare with per-category breakdown

Once the catalog is in place, pass it to `compare` with an explicit `--catalog` path:

```powershell
vtracker compare `
  --left "windows_64_20.0.50000_unicode.zip" `
  --right "windows_64_20.0.50000_unicode_patched.zip" `
  --catalog "dyalog.catalog.csv"
```

The output now groups changes by category. You can immediately see whether the patch touched only `Core` files or also affected `HTMLRenderer`, `Conga`, `Certificates`, or other modules — and whether any updated files fell through as `Unclassified`.

Extractions can also embed the classification directly into the manifest:

```powershell
vtracker extract `
  --msi "L:\Dyalog20.0\windows_64_20.0.50000_unicode\setup_64_unicode.msi" `
  --patch "L:\Dyalog20.0\windows_64_20.0.50000_unicode\patch_20.0.50000.0_64_unicode_2025.03.15.msd" `
  --catalog "dyalog.catalog.csv" `
  --out "windows_64_20.0.50000_unicode_patched.zip" `
  --emit-manifest
```

When `--catalog` is used during extraction, each file entry in the manifest includes its `category` and the manifest schema version is `2`. This means the classification is baked into the archive and does not depend on the catalog file being available at compare time.

## Step 6 — Selective extraction with `unpack` (experimental)

> **Status:** The `unpack` command is implemented and functional, but the workflow for automatically deploying extracted files into a development environment is not yet fully proven. Use `--dry-run` first to verify the extraction plan before writing files.

Once you know which modules were updated, `unpack` can extract just the files belonging to a specific category:

```powershell
# Preview what would be extracted
vtracker unpack `
  --from "windows_64_20.0.50000_unicode_patched.zip" `
  --catalog "dyalog.catalog.csv" `
  --category HTMLRenderer `
  --out "D:\Staging\htmlrenderer" `
  --dry-run
```

```powershell
# Extract for real
vtracker unpack `
  --from "windows_64_20.0.50000_unicode_patched.zip" `
  --catalog "dyalog.catalog.csv" `
  --category HTMLRenderer `
  --out "D:\Staging\htmlrenderer"
```

In an interactive terminal, VTracker detects the longest common path prefix and offers to strip it so that the output directory contains just the files rather than the full MSI directory structure.

You can also specify the prefix explicitly:

```powershell
vtracker unpack `
  --from "windows_64_20.0.50000_unicode_patched.zip" `
  --catalog "dyalog.catalog.csv" `
  --category HTMLRenderer `
  --out "D:\Staging\htmlrenderer" `
  --strip-prefix "ProgramFiles64Folder/Dyalog/Dyalog APL-64 20.0 Unicode"
```

**Limitations to be aware of:**
- `unpack` requires a ZIP archive as source, not a standalone manifest (manifests contain metadata only, no file content).
- Classification uses the catalog provided at unpack time. If you change the catalog between extraction and unpacking, the category assignments may differ from what is recorded in the manifest.
- The extracted files are a raw dump. Copying them into the right locations in your development environment is still a manual step for now.

## Practical tips

- **Always use explicit `--catalog` paths** rather than relying on auto-discovery from the current directory. This avoids accidental misclassification if you run commands from different directories.
- **Keep the catalog file under version control** alongside your development environment setup. It evolves with each Dyalog release as files are added or renamed.
- **Run `catalog check` after every new release** to catch patterns that no longer match any files. Dead patterns are harmless but indicate that the product layout has changed.
- **Watch for `Unclassified` entries** in compare output — they are the signal that new files were introduced and need to be added to the catalog.
- **Store archives and manifests** for each release and patch level. They are your ground truth for what was actually delivered, independent of what the installer UI reports.

## Typical workflow summary

```
1. Receive new Dyalog release or patch
2. Extract base MSI                       → base.zip + base.manifest.json
3. Extract patched MSI                    → patched.zip + patched.manifest.json
4. Compare base vs. patched               → see all changed files
5. Validate catalog against new release   → catch dead patterns and unclassified files
6. Compare with catalog                   → see per-module impact
7. Review Unclassified changes            → update catalog if needed
8. (Future) Unpack specific modules       → stage files for integration
```
