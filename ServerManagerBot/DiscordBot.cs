using System.Runtime.CompilerServices;
using System.Text;
using System.Timers;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Timer = System.Timers.Timer;

namespace ServerManagerBot;

public static class DiscordBot
{
    private static DiscordClient _client;

    private static DiscordConfiguration _configuration;

    private static List<DiscordChannel> _logChannels = new ();

    private static bool _logChannelsLoaded;

    public static event EventHandler<LogChannelLoadFailedEventArgs>? LogChannelLoadFailed;
    public static event EventHandler<DiscordMessageSendFailureEventArgs>? DiscordMessageSendFailure;

    private const int RetryCount = 5;
    private const int RetryDelay = 2000;

    private const int MessageBufferDelay = 10000;
    private static List<Message> _messageBuffer = new (128);
    private static Timer _messageBufferTimer = new(MessageBufferDelay);
    
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
        
        _client.GuildDownloadCompleted += ClientOnGuildDownloadCompleted;
        
        _messageBufferTimer.Elapsed += MessageBufferTimerElapsed;

        await _client.ConnectAsync();
    }

    private static async Task ClientOnGuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs args)
    {
        Task.Run(LoadLogChannels);
    }

    public static void QueueLogMessage(Message message)
    {
        lock (_messageBuffer)
            _messageBuffer.Add(message);
        
        _messageBufferTimer.Start();
    }
    
    private static void MessageBufferTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _messageBufferTimer.Stop();

        while (!_logChannelsLoaded)
        {
            Thread.Sleep(2000);
        }

        List<DiscordMessageBuilder> messageBuilders = BuildDiscordMessages();
        
        _messageBuffer.Clear();

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

    private static List<DiscordMessageBuilder> BuildDiscordMessages()
    {
        void StartBuilder(StringBuilder sb) => sb.AppendLine("```ansi");
        
        void EndBuilder(StringBuilder sb) => sb.AppendLine("```");
        
        List<DiscordMessageBuilder> builders = new List<DiscordMessageBuilder>();

        List<Message> messages = new List<Message>(_messageBuffer);

        StringBuilder currentBuilder = new StringBuilder();
        
        StartBuilder(currentBuilder);
        
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
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