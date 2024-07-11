using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Timer = System.Timers.Timer;

namespace ServerManagerBot;

public static class DiscordBot
{
    private static DiscordClient _client;

    private static DiscordConfiguration _configuration;

    private static List<DiscordChannel> _logChannels = new ();
    
    private static SlashCommandsExtension _slashCommands;

    private static bool _logChannelsLoaded;

    public static event EventHandler<LogChannelLoadFailedEventArgs>? LogChannelLoadFailed;
    public static event EventHandler<DiscordMessageSendFailureEventArgs>? DiscordMessageSendFailure;
    public static event EventHandler<CommandEventArgs>? CommandSent;

    private const int RetryCount = 5;
    private const int RetryDelay = 2000;

    private const int MessageBufferDelay = 10000;
    private static List<Message> _messageBuffer = new (128);
    private static Timer _messageBufferTimer = new(MessageBufferDelay);

    private static bool _logStarted = false;
    
    public static async void Initialize(DiscordConfiguration configuration)
    {
        _client = new DiscordClient(new DSharpPlus.DiscordConfiguration
        {
            Intents = DiscordIntents.AllUnprivileged,
            TokenType = TokenType.Bot,
            Token = configuration.Token,
            LoggerFactory = new NullLoggerFactory() // FIXME: Have a proper log system
        });

        _configuration = configuration;
        
        _slashCommands = _client.UseSlashCommands(new SlashCommandsConfiguration());
        _slashCommands.RegisterCommands<DiscordBotCommands.ServerManagerCommands>();
        
        _client.GuildDownloadCompleted += ClientOnGuildDownloadCompleted;
        
        _messageBufferTimer.Elapsed += (_, _) => FlushMessageBuffer();

        await _client.ConnectAsync();
    }

    public static void SendCommand(string command) => CommandSent?.Invoke(null, new CommandEventArgs(command));

    private static bool ShouldLog(Message message)
    {
        if (!_logStarted)
        {
            _logStarted = message.Text.Contains(_configuration.LogStarter);
            return _logStarted;
        }

        foreach (string ignoredLog in _configuration.IgnoredLogs)
            if (message.Text.Contains(ignoredLog))
                return false;

        return true;
    }
    
    public static void QueueLogMessage(Message message)
    {
        if (!ShouldLog(message))
            return;
        
        lock (_messageBuffer)
            _messageBuffer.Add(message);
        
        _messageBufferTimer.Start();
    }

    public static void Shutdown()
    {
        FlushMessageBuffer();
        _client.Dispose();
    }

    private static async Task ClientOnGuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs args)
    {
        Task.Run(LoadLogChannels);
        
    }

    private static void FlushMessageBuffer()
    {
        _messageBufferTimer.Stop();

        while (!_logChannelsLoaded)
            Thread.Sleep(2000);

        List<DiscordMessageBuilder> messageBuilders;
        
        lock (_messageBuffer)
        {
            messageBuilders = BuildDiscordMessages(_messageBuffer);
            _messageBuffer.Clear();
        }

        foreach (DiscordChannel channel in _logChannels)
        {
            foreach (DiscordMessageBuilder messageBuilder in messageBuilders)
            {
                try
                {
                    channel.SendMessageAsync(messageBuilder).Wait();
                }
                catch (Exception exception)
                {
                    DiscordMessageSendFailure?.Invoke(null, new DiscordMessageSendFailureEventArgs(exception));
                }
            }
        }
    }

    private static List<DiscordMessageBuilder> BuildDiscordMessages(List<Message> messages)
    {
        void StartBuilder(StringBuilder sb) => sb.AppendLine("```ansi");
        
        void EndBuilder(StringBuilder sb) => sb.AppendLine("```");
        
        List<DiscordMessageBuilder> builders = new List<DiscordMessageBuilder>();

        List<Message> copiedMessages = new List<Message>(messages);

        StringBuilder currentBuilder = new StringBuilder();
        
        StartBuilder(currentBuilder);
        
        for (var i = 0; i < copiedMessages.Count; i++)
        {
            var message = copiedMessages[i];
            string messageString = message.Level switch
            {
                LogLevel.Trace => GetGreyText(message.Text),
                LogLevel.Debug => GetGreyText(message.Text),
                LogLevel.Information => message.Text,
                LogLevel.Warning => GetYellowText(message.Text),
                LogLevel.Error => GetRedText(message.Text),
                LogLevel.Critical => GetRedText(message.Text),
                LogLevel.None => message.Text
            };

            if (currentBuilder.Length + messageString.Length >= 396)
            {
                EndBuilder(currentBuilder);
                builders.Add(new DiscordMessageBuilder().WithContent(currentBuilder.ToString()));
                
                currentBuilder = new StringBuilder();
                StartBuilder(currentBuilder);
            }

            currentBuilder.AppendLine(messageString);
        }
        
        EndBuilder(currentBuilder);
        builders.Add(new DiscordMessageBuilder().WithContent(currentBuilder.ToString()));

        return builders;
    }

    private static string GetBlueText(string text) => $"\u001b[0;34m{text}\u001b[0;0m";
    
    private static string GetYellowText(string text) => $"\u001b[0;33m{text}\u001b[0;0m";
    
    private static string GetRedText(string text) => $"\u001b[0;31m{text}\u001b[0;0m";
    
    private static string GetGreyText(string text) => $"\u001b[0;30m{text}\u001b[0;0m";

    private static async Task LoadLogChannels()
    {
        foreach (ulong channelId in _configuration.LogChannelsIds)
        {
            int retriesLeft = RetryCount;

            while (true)
            {
                try
                {
                    DiscordChannel channel = await _client.GetChannelAsync(channelId);
                    _logChannels.Add(channel);
                    break;
                }
                catch (NotFoundException _)
                {
                    RemoveLogChannel(channelId);
                    LogChannelLoadFailed?.Invoke(null, new LogChannelLoadFailedEventArgs(channelId, LogChannelLoadFailedEventArgs.FailCode.NotFound, 0));
                    break;
                }
                catch (BadRequestException _)
                {
                    RemoveLogChannel(channelId);
                    LogChannelLoadFailed?.Invoke(null, new LogChannelLoadFailedEventArgs(channelId, LogChannelLoadFailedEventArgs.FailCode.ApplicationError, 0));
                    break;
                }
                catch
                {
                    if (retriesLeft == 0)
                    {
                        // Let it be in the configuration file, maybe next time the channel will be found
                        LogChannelLoadFailed?.Invoke(null, new LogChannelLoadFailedEventArgs(channelId, LogChannelLoadFailedEventArgs.FailCode.DiscordError, 0));
                        break;
                    }
                    
                    LogChannelLoadFailed?.Invoke(null, new LogChannelLoadFailedEventArgs(channelId, LogChannelLoadFailedEventArgs.FailCode.DiscordError, retriesLeft));
                }
                
                retriesLeft--;
                Thread.Sleep(RetryDelay);
            }
        }
        
        _logChannelsLoaded = true;
    }

    private static void RemoveLogChannel(ulong channelId)
    {
        _logChannels.Remove(_logChannels.FirstOrDefault(x => x.Id == channelId));
        if (_configuration.LogChannelsIds.Remove(channelId))
            _configuration.Save();
    }
}