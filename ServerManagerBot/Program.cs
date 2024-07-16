using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using Terminal.Gui;

namespace ServerManagerBot;

internal static class Program
{
    private static ServerShell _shell;

    private static ServerProcess _process;

    private static Task? _httpManagementListenerTask;
    private static CancellationTokenSource _httpManagementListenerTaskCanceller = new ();
    
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
        DiscordBot.DiscordMessageSendFailure += DiscordBotOnDiscordMessageSendFailure;
        DiscordBot.CommandSent += OnCommandSent;
        DiscordBot.Initialize(discordConfiguration!);
        
        StartHttpManagementListener();
        InitializeCustomCommands();
        
        Task.Run(() =>
        {
            Application.MainLoop.Invoke(() =>
            {
                Message logMessage = GetStartResultLogMessage(_process.Start());
                
                _shell.AppendLine(logMessage.Text);
                DiscordBot.QueueLogMessage(logMessage);
            });
        });
        
        Application.Run(_shell);
        Application.Shutdown();
    }

    private static void InitializeCustomCommands()
    {
        CustomCommandsConfiguration? customCommandsConfiguration = CustomCommandsConfiguration.Load();
        if (customCommandsConfiguration is not null)
        {
            try
            {
                CustomCommands.Initialize(customCommandsConfiguration);
                return;
            }
            catch { } // Commands failed to initialize, fallthrough the warning log
        }
        
        Message noCustomCommandsMessage =
            GetWarningMessage("Custom commands failed to load, the custom commands will not be available");

        _shell.AppendLine(noCustomCommandsMessage.Text);
        DiscordBot.QueueLogMessage(noCustomCommandsMessage);
        CustomCommands.Initialize(default);
    }
    
    private static void StartHttpManagementListener()
    {
        string? managementPortRaw = Environment.GetEnvironmentVariable("ServerManagerBot_ManagementPort");

        if (!int.TryParse(managementPortRaw, out int managementPort))
        {
            Message noHttpMessage = GetWarningMessage(
                    "Couldn't parse the HTTP port for the management listener, the management interface will not be available");
            
            _shell.AppendLine(noHttpMessage.Text);
            DiscordBot.QueueLogMessage(noHttpMessage);
            return;
        }

        _httpManagementListenerTask = new Task(() => HttpManagementListenerLoop(managementPort, _httpManagementListenerTaskCanceller.Token), _httpManagementListenerTaskCanceller.Token);
        _httpManagementListenerTask.Start();
    }

    private static async Task HttpManagementListenerLoop(int port, CancellationToken token)
    {
        HttpListener httpListener = new HttpListener();
        
        httpListener.Prefixes.Add($"http://localhost:{port}/send/");
        httpListener.Start();

        while (!token.IsCancellationRequested)
        {
            HttpListenerContext context = await httpListener.GetContextAsync().WaitAsync(token);
            
            if (context.Request.HttpMethod != "POST")
            {
                context.Response.StatusCode = 405;
                context.Response.Close();
                continue;
            }
            
            using (MemoryStream bodyStream = new MemoryStream())
            {
                context.Request.InputStream.CopyTo(bodyStream);
                OnCommandSent(null, new CommandEventArgs(Encoding.UTF8.GetString(bodyStream.ToArray())));
            }
            
            context.Response.StatusCode = 200;
            context.Response.Close();
        }
    }

    private static void DiscordBotOnDiscordMessageSendFailure(object? sender, DiscordMessageSendFailureEventArgs e)
    {
        Application.MainLoop.Invoke(() =>
        {
            _shell.AppendLine(GetErrorMessage($"Failure to send discord message: {e.Exception}").Text);
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
                _shell.AppendLine(GetWarningMessage($"Couldn't get channel {e.LogChannelId} {errorCodeString}. Retrying {e.RetriesLeft} more times.").Text);
            else
                _shell.AppendLine(GetErrorMessage($"Couldn't get channel {e.LogChannelId} {errorCodeString}. Retries were exhausted.").Text);
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
        
        _shell = new ServerShell();
        _shell.CommandSent += OnCommandSent;
        _shell.SpecialCommandSent += OnSpecialCommandSent;
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
            
            _shell.AppendLine(logMessage.Text);
            DiscordBot.QueueLogMessage(logMessage);
        }
    }

    private static void OnSpecialCommandSent(object? sender, CommandEventArgs e)
    {
        if (e.Command == "start")
        {
            Start();
            return;
        }

        if (e.Command == "stop")
        {
            Stop();
            return;
        }

        if (e.Command == "quit")
        {
            Shutdown();
            return;
        }

        Message logMessage = GetWarningMessage($"Unknown command '{e.Command}'");

        _shell.AppendLine(logMessage.Text);
        DiscordBot.QueueLogMessage(logMessage);
    }

    private static void OnErrorDataSent(object? sender, DataEventArgs e)
    {
        Message logMessage = GetErrorMessage(e.Text);
        
        Application.MainLoop.Invoke(() =>
        {
            _shell.AppendLine(logMessage.Text);
        });
        DiscordBot.QueueLogMessage(logMessage);
    }

    private static void OnOutputDataSent(object? sender, DataEventArgs e)
    {
        string? response = CustomCommands.ProcessCommands(e.Text);
        if (response is not null)
            OnCommandSent(null, new CommandEventArgs(response));
        
        Message logMessage = GetLogMessage(e.Text);
        
        Application.MainLoop.Invoke(() =>
        {
            _shell.AppendLine(logMessage.Text);
        });
        DiscordBot.QueueLogMessage(logMessage);
    }

    private static void OnExited(object? sender, ExitedEventArgs e)
    {
        Message logMessage = GetLogMessage($"Server process exited with exit code {e.ExitCode}.", e.ExitTime);
        
        Application.MainLoop.Invoke(() =>
        {
            _shell.AppendLine(logMessage.Text);
        });
        DiscordBot.QueueLogMessage(logMessage);
        
        Shutdown();
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

    public static void Start()
    {
        Message logMessage = GetStartResultLogMessage(_process.Start());
            
        _shell.AppendLine(logMessage.Text);
        DiscordBot.QueueLogMessage(logMessage);
    }

    public static void Stop()
    {
        Message logMessage = GetStopResultLogMessage(_process.Stop());
            
        _shell.AppendLine(logMessage.Text);
        DiscordBot.QueueLogMessage(logMessage);
    }

    public static void Shutdown()
    {
        Message shutdownMessage = GetLogMessage("Shutting down");

        _httpManagementListenerTaskCanceller.Cancel();
        if (_httpManagementListenerTask is not null)
            _httpManagementListenerTask.Wait();
        
        if (_shell.Running)
            _shell.AppendLine(shutdownMessage.Text);
        
        DiscordBot.QueueLogMessage(shutdownMessage);
        DiscordBot.Shutdown();
        
        if (_shell.Running)
            _shell.RequestStop();
    }
}