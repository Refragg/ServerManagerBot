namespace ServerManagerBot;

public class ExitedEventArgs : EventArgs
{
    public int ExitCode { get; }
        
    public DateTime ExitTime { get; }

    public ExitedEventArgs(int exitCode, DateTime exitTime)
    {
        ExitCode = exitCode;
        ExitTime = exitTime;
    }
}

public class DataEventArgs : EventArgs
{
    public string? Text { get; }

    public DataEventArgs(string? text)
    {
        Text = text;
    }
}