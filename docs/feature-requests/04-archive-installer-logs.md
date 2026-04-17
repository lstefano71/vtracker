# Archive Installer Logs

## Status
Implemented.

## Goal
Preserve `msiexec` logs inside successful archives so support and diagnostics do not depend on a retained work directory.

## Agreed Direction
- Always include installer logs in the ZIP archive.
- Place them under a reserved archive root such as `_logs\`.
- Keep them out of the manifest file-entry array.
- Preserve stable log names like `01-admin-image.log` and `02-patch-001.log`.
- Keep the existing failure behavior where work directories and logs are retained on failure.

## Implementation Notes
- `_manifest.json` should continue to describe only the extracted product tree.
- Compare should continue to operate from manifests, so archived logs do not affect add/remove/update classification.

## Validation Expectations
- Verify successful archives contain `_logs\...` entries.
- Verify `_manifest.json` does not describe archived logs.
- Verify failure paths still preserve on-disk logs and work directories.
