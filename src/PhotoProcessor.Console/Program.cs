namespace PhotoProcessor.Console;

using System.Text.Json;
using PhotoProcessor.Core;
using PhotoProcessor.Core.Abstractions;
using PhotoProcessor.Core.Config;

internal class Program
{
    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");

    private static void Main(string[] args)
    {
        try
        {
            Configuration config = ReadConfig();
            string inputDirectory, outputDirectory;

            switch (args.Length)
            {
                case 0:
                    // E:\dev\data\photoin
                    inputDirectory = @"\\CARP\photos\_pipeline\in";
                    outputDirectory = @"\\CARP\photos\_pipeline\out";
                    //inputDirectory = @"G:\Pictures\Photos\--Pipeline\newout\2021";
                    //outputDirectory = @"G:\Pictures\Photos\--Pipeline\newout\2021";
                    //outputDirectory = inputDirectory;
                    break;
                case 1:
                    inputDirectory = args[0];
                    outputDirectory = inputDirectory;
                    break;
                case 2:
                    inputDirectory = args[0];
                    outputDirectory = args[1];
                    break;
                default:
                    System.Console.WriteLine("Invalid args");
                    System.Console.ReadKey();
                    return;
            }

            config.InputDirectory = inputDirectory;
            config.OutputDirectory = outputDirectory;

            DirectoryProcessor dp = new DirectoryProcessor(config, new ConsoleLog());
            dp.Process(inputDirectory, outputDirectory);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine(ex.Message);
            System.Console.ReadKey();
        }
    }

    private static Configuration ReadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            throw new Exception($"Config does not exist: {ConfigPath}");
        }

        string jsonString = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<Configuration>(jsonString)
            ?? throw new Exception($"Config is empty: {ConfigPath}");
    }
}

internal class ConsoleLog : ILog
{
    public void LogError(Exception ex)
    {
        System.Console.WriteLine(ex);
    }

    public void LogMessage(string message)
    {
        System.Console.WriteLine(message);
    }
}