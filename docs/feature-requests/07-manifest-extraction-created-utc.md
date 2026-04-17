# Manifest Extraction CreatedUtc

## Status
Accepted future request.

## Goal
Record when a manifest was produced without changing the current ownership of tool metadata.

## Agreed Direction
- Add `createdUtc` to the manifest `extraction` object.
- Do not add a duplicate tool-version field under `extraction`.
- Keep the existing top-level `tool` metadata as the authoritative place for tool version.

## Implementation Notes
- Treat `createdUtc` as provenance metadata only.
- Compare must continue to separate provenance differences from file-content changes.
- Preserve compatibility with existing manifest consumers when evolving the schema.

## Validation Expectations
- Verify newly written manifests include `extraction.createdUtc` in UTC.
- Verify compare continues to classify file changes strictly by SHA-256.
- Verify provenance reporting remains separate from add/remove/update output.
