namespace ServerManagerBot;

public static class LogHelpers
{
    public static string GetTimeString(DateTime? logTime = default)
    {
        logTime ??= DateTime.Now;
        return $"[{logTime.Value.ToShortDateString()} - {logTime.Value.ToLongTimeString()}]";
    }
}