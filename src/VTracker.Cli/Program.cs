using System.Text;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using VTracker.Cli;
using VTracker.Core;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// Show the banner in any interactive session that is not a machine-consumption
// invocation (piped output or JSON format) and not a direct command run.
// "Batch mode" = a real command verb (extract / compare) without --help.
if (!Console.IsOutputRedirected && !IsJsonFormat(args) && !IsBatchMode(args))
{
    PrintBanner();
}

try
{
    var toolIdentity = new ToolIdentity(
        "vtracker",
        ThisAssembly.AssemblyInformationalVersion);

    var app = ConsoleApp.Create()
        .ConfigureServices(services =>
        {
            services.AddSingleton(toolIdentity);
            services.AddSingleton<PathNormalizer>();
            services.AddSingleton<PathCollisionValidator>();
            services.AddSingleton<OutputPathResolver>();
            services.AddSingleton<WorkspaceManager>();
            services.AddSingleton<HashService>();
            services.AddSingleton<PeVersionService>();
            services.AddSingleton<MsiexecRunner>();
            services.AddSingleton<CatalogParser>();
            services.AddSingleton<CatalogClassifier>();
            services.AddSingleton<CatalogDiscovery>();
            services.AddSingleton<CatalogWriter>();
            services.AddSingleton<CatalogInitService>();
            services.AddSingleton<CatalogCheckService>();
            services.AddSingleton<ManifestBuilder>();
            services.AddSingleton<ManifestRepository>();
            services.AddSingleton<ArchiveBuilder>();
            services.AddSingleton<ManifestComparator>();
            services.AddSingleton<CompareService>();
            services.AddSingleton<ExtractService>();
            services.AddSingleton<UnpackService>();

            // Use the richer reporter for interactive terminals; plain text otherwise
            if (!Console.IsOutputRedirected)
            {
                services.AddSingleton<IAnsiConsole>(_ => AnsiConsole.Console);
                services.AddSingleton<IExtractProgressReporter, SpectreExtractProgressReporter>();
            }
            else
            {
                services.AddSingleton<IExtractProgressReporter, PlainExtractProgressReporter>();
            }
        });

    app.UseFilter<UserFacingErrorFilter>();
    app.Run(args);
    return Environment.ExitCode;
}
catch (VTrackerException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static void PrintBanner()
{
    AnsiConsole.Write(new FigletText("vtracker").Color(Color.Cyan1));
    AnsiConsole.Write(
        new Rule($"[dim]MSI administrative image capture & compare[/]  [grey]v{ThisAssembly.AssemblyInformationalVersion}[/]")
            .LeftJustified()
            .RuleStyle(Style.Parse("cyan1 dim")));
    AnsiConsole.WriteLine();
}

// True when the user passed a real command verb (first non-flag arg) and is
// NOT asking for help — i.e. an automated / scripted invocation.
static bool IsBatchMode(string[] a)
{
    if (a.Length == 0) return false;
    if (a[0].StartsWith('-')) return false;
    foreach (var arg in a)
        if (arg is "--help" or "-h") return false;
    return true;
}

static bool IsJsonFormat(string[] a)
{
    for (var i = 0; i < a.Length; i++)
    {
        if (a[i].Equals("--format", StringComparison.OrdinalIgnoreCase)
            && i + 1 < a.Length
            && a[i + 1].Equals("json", StringComparison.OrdinalIgnoreCase))
            return true;

        if (a[i].StartsWith("--format=", StringComparison.OrdinalIgnoreCase)
            && a[i][9..].Equals("json", StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}
