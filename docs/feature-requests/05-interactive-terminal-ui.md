# Interactive Terminal UI

## Status
Accepted future request.

## Goal
Give VTracker a more modern terminal experience without turning it into a full-screen terminal application.

## Agreed Direction
- Use `Spectre.Console` for the interactive presentation layer.
- Auto-enable the richer UI in interactive terminals.
- Scope the richer UI to `extract` and `compare`.
- Keep a plain-output fallback for non-interactive runs, redirected output, or feature-specific failures.
- For `extract`, show the major workflow steps and a best-effort live view of installer log output.
- If live tailing is unavailable because of file-sharing behavior, fall back gracefully to step/status output.

## Implementation Notes
- Keep `ConsoleAppFramework` for command parsing.
- Use the richer UI for rendering only.
- Validate the chosen `Spectre.Console` feature set under Native AOT before relying on it by default.

## Validation Expectations
- Verify interactive runs show the richer UI.
- Verify redirected or non-interactive runs fall back to plain output.
- Verify a log-tail failure does not fail extraction.
- Verify Native AOT publish still succeeds with the chosen UI features.
