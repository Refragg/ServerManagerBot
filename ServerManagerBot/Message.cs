using Microsoft.Extensions.Logging;

namespace ServerManagerBot;

public class Message
{
    public string Text { get; }

    public LogLevel Level { get; }
    
    public DateTime Time { get; }

    public Message(string text, LogLevel level, DateTime? time = default)
    {
        Text = text;
        Level = level;

        Time = time ?? DateTime.Now;
        
    }
}