using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using VTracker.Cli;
using VTracker.Core;

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
            services.AddSingleton<ManifestBuilder>();
            services.AddSingleton<ManifestRepository>();
            services.AddSingleton<ArchiveBuilder>();
            services.AddSingleton<ManifestComparator>();
            services.AddSingleton<CompareService>();
            services.AddSingleton<ExtractService>();
        });

    app.UseFilter<StartupBannerFilter>();
    app.UseFilter<UserFacingErrorFilter>();
    app.Run(args);
    return Environment.ExitCode;
}
catch (VTrackerException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
