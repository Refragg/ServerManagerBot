namespace ServerManagerBot;

public class LogChannelLoadFailedEventArgs : EventArgs
{
    public enum FailCode
    {
        NotFound,
        ApplicationError,
        DiscordError
    }
    
    public ulong LogChannelId { get; }
    
    public FailCode Code { get; }
    
    public int RetriesLeft { get; }

    public LogChannelLoadFailedEventArgs(ulong logChannelId, FailCode code, int retriesLeft)
    {
        LogChannelId = logChannelId;
        Code = code;
        RetriesLeft = retriesLeft;
    }
}

public class DiscordMessageSendFailureEventArgs : EventArgs
{
    public Exception Exception { get; }

    public DiscordMessageSendFailureEventArgs(Exception exception)
    {
        Exception = exception;
    }
}