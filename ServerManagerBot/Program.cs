using Microsoft.Extensions.Logging;
using Terminal.Gui;

namespace ServerManagerBot;

internal static class Program
{
    public static ServerShell Shell;

    private static ServerProcess _process;
    
    private static void Main(string[] args)
    {
        var arguments = GetProgramArguments(args);
        
        DiscordConfiguration? discordConfiguration = DiscordConfiguration.Load();
        if (discordConfiguration is null)
            Exit(1,
                $"Discord configuration could not be loaded from {DiscordConfiguration.DiscordConfigurationFileName}",
                ConsoleColor.Red);
        
        InitShellApplication();
        InitProcess(arguments.ProcessPath, arguments.WorkingDirectory, arguments.ProcessArguments);
        
        DiscordBot.LogChannelLoadFailed += DiscordBotOnLogChannelLoadFailed;
        DiscordBot .DiscordMessageSendFailure += DiscordBotOnDiscordMessageSendFailure;
        DiscordBot.Initialize(discordConfiguration!);
        
        Task.Run(() =>
        {
            Application.MainLoop.Invoke(() =>
            {
                Message logMessage = GetStartResultLogMessage(_process.Start());
                
                Shell.AppendLine(logMessage.Text);
                DiscordBot.QueueLogMessage(logMessage);
            });
        });
        
        Application.Run(Shell);
        Application.Shutdown();
    }

    private static void DiscordBotOnDiscordMessageSendFailure(object? sender, DiscordMessageSendFailureEventArgs e)
    {
        Application.MainLoop.Invoke(() =>
        {
            Shell.AppendLine(GetErrorMessage($"Failure to send discord message: {e.Exception}").Text);
        });
    }

    private static void DiscordBotOnLogChannelLoadFailed(object? sender, LogChannelLoadFailedEventArgs e)
    {
        Application.MainLoop.Invoke(() =>
        {
            string errorCodeString = e.Code switch
            {
                LogChannelLoadFailedEventArgs.FailCode.NotFound => "because it couldn't be found",
                LogChannelLoadFailedEventArgs.FailCode.ApplicationError => "due to an application error",
                LogChannelLoadFailedEventArgs.FailCode.DiscordError => "due to an error on Discord side",
            };
            
            if (e.RetriesLeft > 0)
                Shell.AppendLine(GetWarningMessage($"Couldn't get channel {e.LogChannelId} {errorCodeString}. Retrying {e.RetriesLeft} more times.").Text);
            else
                Shell.AppendLine(GetErrorMessage($"Couldn't get channel {e.LogChannelId} {errorCodeString}. Retries were exhausted.").Text);
        });
    }

    private static (string ProcessPath, string WorkingDirectory, string[] ProcessArguments) GetProgramArguments(string[] args)
    {
        if (args.Length < 2)
            Exit(1,
                $"Syntax:\n\t{AppDomain.CurrentDomain.FriendlyName} \"path/to/server/process\" \"path/to/working/dir\" arg1 arg2 arg3...",
                ConsoleColor.Yellow);

        if (args.Length > 2)
            return (args[0], args[1], args[2..]);
        
        return (args[0], args[1], Array.Empty<string>());
    }

    private static void InitShellApplication()
    {
        Application.Init();
        Application.MainLoop.AddIdle(() =>
        {
            Thread.Sleep(16);
            return true;
        });
        
        Shell = new ServerShell();
        Shell.CommandSent += OnCommandSent;
        Shell.SpecialCommandSent += OnSpecialCommandSent;
    }
    
    private static void InitProcess(string processPath, string workingDirectory, string[] arguments)
    {
        _process = new ServerProcess(processPath, workingDirectory, arguments);
        _process.Exited += OnExited;
        _process.OutputDataSent += OnOutputDataSent;
        _process.ErrorDataSent += OnErrorDataSent;
    }

    private static void OnCommandSent(object? sender, CommandEventArgs e)
    {
        ServerProcess.SendInputResult result = _process.SendInputToProcess(e.Command);

        if (result != ServerProcess.SendInputResult.Ok)
        {
            Message logMessage = GetSendInputResultMessage(result);
            
            Shell.AppendLine(logMessage.Text);
            DiscordBot.QueueLogMessage(logMessage);
        }
    }

    private static void OnSpecialCommandSent(object? sender, CommandEventArgs e)
    {
        Message logMessage;
        
        if (e.Command == "start")
        {
            logMessage = GetStartResultLogMessage(_process.Start());
            
            Shell.AppendLine(logMessage.Text);
            DiscordBot.QueueLogMessage(logMessage);
            return;
        }

        if (e.Command == "stop")
        {
            logMessage = GetStopResultLogMessage(_process.Stop());
            
            Shell.AppendLine(logMessage.Text);
            DiscordBot.QueueLogMessage(logMessage);
            return;
        }

        logMessage = GetWarningMessage($"Unknown command '{e.Command}'");

        Shell.AppendLine(logMessage.Text);
        DiscordBot.QueueLogMessage(logMessage);
    }

    private static void OnErrorDataSent(object? sender, DataEventArgs e)
    {
        Message logMessage = GetErrorMessage(e.Text);
        
        Application.MainLoop.Invoke(() =>
        {
            Shell.AppendLine(logMessage.Text);
        });
        DiscordBot.QueueLogMessage(logMessage);
    }

    private static void OnOutputDataSent(object? sender, DataEventArgs e)
    {
        Message logMessage = GetLogMessage(e.Text);
        
        Application.MainLoop.Invoke(() =>
        {
            Shell.AppendLine(logMessage.Text);
        });
        DiscordBot.QueueLogMessage(logMessage);
    }

    private static void OnExited(object? sender, ExitedEventArgs e)
    {
        Message logMessage = GetLogMessage($"Server process exited with exit code {e.ExitCode}.", e.ExitTime);
        
        Application.MainLoop.Invoke(() =>
        {
            Shell.AppendLine(logMessage.Text);
        });
        DiscordBot.QueueLogMessage(logMessage);
    }

    private static Message GetStartResultLogMessage(ServerProcess.StartResult result)
    {
        return result switch
        {
            ServerProcess.StartResult.Ok => GetLogMessage("Server started"),
            ServerProcess.StartResult.AlreadyStarted => GetWarningMessage("Server was already started"),
            ServerProcess.StartResult.BadPath => GetErrorMessage("Couldn't start the server: Bad path on either file path or working dir"),
            ServerProcess.StartResult.Error => GetErrorMessage("Couldn't start the server: An internal error occured")
        };
    }
    
    private static Message GetStopResultLogMessage(ServerProcess.StopResult result)
    {
        return result switch
        {
            ServerProcess.StopResult.Ok => GetLogMessage("Server stopped"),
            ServerProcess.StopResult.NotStarted => GetWarningMessage("Server was already stopped"),
            ServerProcess.StopResult.Error => GetErrorMessage("Couldn't stop the server: An internal error occured")
        };
    }
    
    private static Message GetSendInputResultMessage(ServerProcess.SendInputResult result)
    {
        return result switch
        {
            ServerProcess.SendInputResult.Ok => GetLogMessage("Input sent to process"),
            ServerProcess.SendInputResult.NotStarted => GetWarningMessage("No process to send the input to"),
            ServerProcess.SendInputResult.Error => GetErrorMessage("Couldn't send input to the server: An internal error occured")
        };
    }

    private static Message GetLogMessage(string toLog, DateTime? logTime = default)
    {
        return new Message($"{LogHelpers.GetTimeString(logTime)} - {toLog}", LogLevel.None, logTime);
    }
    private static Message GetErrorMessage(string toLog, DateTime? logTime = default)
    {
        return new Message($"{LogHelpers.GetTimeString(logTime)} - ERR - {toLog}", LogLevel.Error, logTime);
    }
    private static Message GetWarningMessage(string toLog, DateTime? logTime = default)
    {
        return new Message($"{LogHelpers.GetTimeString(logTime)} - WARN - {toLog}", LogLevel.Warning, logTime);
    }

    private static void Exit(int exitCode, string message, ConsoleColor? messageColor = default)
    {
        if (messageColor is not null)
            Console.ForegroundColor = messageColor.Value;
        
        Console.WriteLine(message);
        Console.ResetColor();
        
        Environment.Exit(exitCode);
    }
}