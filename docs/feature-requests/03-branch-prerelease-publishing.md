# Branch Prerelease Publishing

## Status
Implemented.

## Goal
Publish non-main builds in a way that is clearly distinct from official releases while still giving users a stable place to download the latest preview from a branch.

## Agreed Direction
- Publish prereleases from pushes on any non-main branch.
- Use rolling prereleases per branch, not one prerelease per commit.
- Let the workflow manage prerelease tags automatically; no manual branch tagging is required.
- Generate branch prerelease notes from commit subjects.
- Build branch prerelease notes from commits that are on the branch but not on `main`.

## Implementation Notes
- A rolling GitHub prerelease requires an automation-managed branch-specific tag or equivalent release anchor.
- Use branch-qualified naming so prereleases are visually distinct from official releases.
- Include branch identity and short SHA in the human-facing release name and asset naming.
- Ensure official releases remain separate and unaffected by branch publication.

## Validation Expectations
- Verify a non-main push updates the branch prerelease instead of creating an official release.
- Verify branch prerelease labels, names, and assets are clearly marked as preview output.
- Verify official tagged releases on `main` remain immutable and separate.
