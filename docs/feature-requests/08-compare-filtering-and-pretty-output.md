# Compare Filtering and Pretty Output

## Status
Accepted future request.

## Goal
Make `compare` easier to use for focused binary reviews while preserving automation-friendly output modes.

## Agreed Direction
- Add repeatable include filters such as:
  - `--include "**/*.dll"`
  - `--include "**/*.exe"`
- Match globs against normalized manifest paths using `/` separators.
- Evaluate globs case-insensitively to match Windows expectations.
- Use `DotNet.Glob` for matching.
- Keep `text` and `json`, and add a richer `pretty` format.
- Default interactive compare sessions to `pretty`.
- Apply filters to JSON output too.
- Show full summary counts even when detail rows are filtered.
- In pretty output, show status colour, path, size, and version values when available.
- For updated files, show left-to-right size and version information.

## Implementation Notes
- Repeated include patterns should use OR semantics.
- When filters hide some detail rows, show enough context in the output to make the mismatch between total counts and displayed rows understandable.
- Keep the plain `text` mode available for simple and legacy terminal use.

## Validation Expectations
- Verify include filters work for text, pretty, and JSON output.
- Verify case-insensitive matching works for Windows-style comparisons.
- Verify summaries continue to show full counts while details respect the filter.
- Verify pretty output shows version values only when present.
