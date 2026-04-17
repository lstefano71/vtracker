using ConsoleAppFramework;
using VTracker.Core;

namespace VTracker.Cli;

internal sealed class UserFacingErrorFilter : ConsoleAppFilter
{
    public UserFacingErrorFilter(ConsoleAppFilter next)
        : base(next)
    {
    }

    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        try
        {
            await Next.InvokeAsync(context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Environment.ExitCode = 1;
        }
        catch (VTrackerException exception)
        {
            Environment.ExitCode = 1;
            Console.Error.WriteLine(exception.Message);
        }
    }
}
