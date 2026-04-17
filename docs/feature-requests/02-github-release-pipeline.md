# GitHub Release Pipeline

## Status
Accepted future request.

## Goal
Publish official VTracker releases from GitHub Actions with stable assets and meaningful descriptions.

## Agreed Direction
- Add a GitHub Actions workflow for release publication.
- Trigger official releases from tags on `main`.
- Publish these assets:
  - win-x64 Native AOT ZIP
  - SHA-256 checksum file
- Generate release descriptions from git commit subjects instead of PR metadata.
- Build official notes from commits since the previous official tag.
- Do not depend on Release Drafter or PR labeling.

## Implementation Notes
- Use the official release tag as the immutable public release anchor.
- Generate release notes in the workflow from git history.
- Document the release process in the repository once the workflow exists.

## Validation Expectations
- Confirm the workflow publishes from a tagged `main` commit only.
- Verify release assets and checksums are attached correctly.
- Verify generated notes use the expected commit range.
