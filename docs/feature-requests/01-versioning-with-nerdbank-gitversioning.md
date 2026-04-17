# Versioning with Nerdbank.GitVersioning

## Status
Implemented.

## Goal
Give every build a bug-reportable identity while keeping human control over major and minor version numbers.

## Agreed Direction
- Use `Nerdbank.GitVersioning`.
- Control `major.minor` through `version.json`.
- Let git height provide the automated build number.
- Use the resulting informational version as the source of truth for branch and short-commit identity.
- Keep official human-managed release tags on `main` only.

## Implementation Notes
- Prefer one repo-level `version.json`.
- Feed the resolved version into CLI startup output, manifest tool metadata, and release asset naming.
- Keep the top-level manifest `tool` object as the place where tool version is recorded.

## Validation Expectations
- Build and test with versioning enabled.
- Verify the produced version reflects `major.minor`, git height, branch, and short SHA.
- Validate Native AOT publish after the package is introduced.
