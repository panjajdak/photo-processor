namespace PhotoProcessor.Console.Cli;

using PhotoProcessor.Core.Config;

internal static class Errors
{
    public static int Report(Exception ex, ConsoleReporter reporter)
    {
        switch (ex)
        {
            case UsageException or InvalidConfigurationException:
                reporter.Error(ex.Message);
                return ExitCodes.InvalidUsage;
            default:
                reporter.Error(ex);
                return ExitCodes.Error;
        }
    }
}
