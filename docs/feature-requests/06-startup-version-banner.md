# Startup Version Banner

## Status
Implemented.

## Goal
Surface the exact build identity immediately when a human runs the tool.

## Agreed Direction
- Show a startup banner for interactive human-readable sessions only.
- Suppress the banner for JSON output and redirected stdout.
- Include:
  - tool version
  - branch name
  - truncated commit SHA

## Implementation Notes
- Use the version resolved by the git-versioning layer as the source of truth.
- Keep the banner lightweight so it does not overshadow the actual command output.
- Apply the rule consistently across interactive commands.

## Validation Expectations
- Verify the banner appears for interactive `extract` and `compare`.
- Verify it does not contaminate machine-readable output.
