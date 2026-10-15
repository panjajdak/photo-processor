namespace PhotoProcessor.Console.Cli;

using PhotoProcessor.Core.Config;

internal static class Errors
{
    /// <summary>
    /// Reports an exception that ended a command and returns the matching exit code.
    /// </summary>
    public static int Report(Exception ex, ConsoleReporter reporter)
    {
        switch (ex)
        {
            case UsageException or InvalidConfigurationException:
                reporter.LogError(new Exception(ex.Message));
                return ExitCodes.InvalidUsage;
            default:
                reporter.LogError(ex);
                return ExitCodes.Error;
        }
    }
}
