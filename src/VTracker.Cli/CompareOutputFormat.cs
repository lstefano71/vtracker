namespace VTracker.Cli;

/// <summary>
/// Output format choices for the <c>compare</c> command.
/// Kept in the CLI assembly so Core remains free of UI-rendering concepts.
/// </summary>
public enum CompareOutputFormat
{
    /// <summary>Plain text, one line per finding. Suitable for scripts and piped output.</summary>
    Text,

    /// <summary>Structured JSON. Suitable for automation and downstream processing.</summary>
    Json,

    /// <summary>
    /// Colour-rich Spectre.Console rendering. Default for interactive terminals.
    /// Falls back to <see cref="Text"/> when the terminal does not support ANSI.
    /// </summary>
    Pretty,
}
