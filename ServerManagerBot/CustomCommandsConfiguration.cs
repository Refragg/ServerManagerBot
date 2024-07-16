using System.Text.Json;

namespace ServerManagerBot;

public class CustomCommandsConfiguration
{
    private const string CustomCommandsConfigurationDefaultFileName = "commands-conf.json";

    public static readonly string CustomCommandsConfigurationFileName = GetCustomCommandsConfigurationFileName();
    
    public int Cooldown { get; set; }
    
    public string CommandStartRegex { get; set; }
    
    public Dictionary<string, string>  Commands { get; set; }
    
    public static CustomCommandsConfiguration? Load()
    {
        try
        {
            string json = File.ReadAllText(CustomCommandsConfigurationFileName);
            return JsonSerializer.Deserialize<CustomCommandsConfiguration>(json);
        }
        catch
        {
            return default;
        }
    }
    
    private static string GetCustomCommandsConfigurationFileName()
    {
        string? configFileName = Environment.GetEnvironmentVariable("ServerManagerBot_CommandsConfPath");
        return configFileName ?? CustomCommandsConfigurationDefaultFileName;
    }
}