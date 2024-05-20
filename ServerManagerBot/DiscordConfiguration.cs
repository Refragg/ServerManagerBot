using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServerManagerBot;

public class DiscordConfiguration
{
    public const string DiscordConfigurationFileName = "discord-conf.json";
    
    public List<ulong> LogChannelsIds { get; set; }
    
    public string Token { get; set; }

    public static DiscordConfiguration? Load()
    {
        try
        {
            string json = File.ReadAllText(DiscordConfigurationFileName);
            return JsonSerializer.Deserialize<DiscordConfiguration>(json);
        }
        catch
        {
            return default;
        }
    }
    
    public bool Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DiscordConfigurationFileName, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}