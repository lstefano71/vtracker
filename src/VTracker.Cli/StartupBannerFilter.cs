using ConsoleAppFramework;

namespace VTracker.Cli;

internal sealed class StartupBannerFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
{
    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        if (!Console.IsOutputRedirected && !IsJsonFormat(context.Arguments))
        {
            Console.Out.WriteLine($"vtracker {ThisAssembly.AssemblyInformationalVersion}");
            Console.Out.WriteLine();
        }

        await Next.InvokeAsync(context, cancellationToken);
    }

    private static bool IsJsonFormat(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.Equals("--format", StringComparison.OrdinalIgnoreCase) &&
                i + 1 < args.Length &&
                args[i + 1].Equals("json", StringComparison.OrdinalIgnoreCase))
                return true;

            if (arg.StartsWith("--format=", StringComparison.OrdinalIgnoreCase) &&
                arg[9..].Equals("json", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
