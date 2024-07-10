using System.Diagnostics;
using Pty.Net;

namespace ServerManagerBot;

public class ServerProcess
{
    public enum Status
    {
        Ready,
        Running,
        Ended,
    }

    public enum StartResult
    {
        Ok,
        AlreadyStarted,
        BadPath,
        Error
    }

    public enum SendInputResult
    {
        Ok,
        NotStarted,
        Error
    }
    
    public enum StopResult
    {
        Ok,
        NotStarted,
        Error
    }

    public Status ProcessStatus { get; private set; }

    public event EventHandler<ExitedEventArgs>? Exited;
    public event EventHandler<DataEventArgs>? OutputDataSent;
    public event EventHandler<DataEventArgs>? ErrorDataSent;

    private bool _usePty = GetUsePtyOption();
    
    private Process _process;

    private PtyOptions _ptyOptions;
    private IPtyConnection _ptyConnection;
    private string? _ptyLastCommand;

    private string _processPath;
    private string _processWorkingDirectory;
    private IEnumerable<string> _processArguments;

    public ServerProcess(string path, string workingDirectory, IEnumerable<string> arguments)
    {
        _processPath = path;
        _processWorkingDirectory = workingDirectory;
        _processArguments = arguments;
        InitProcess();
    }

    private static bool GetUsePtyOption()
    {
        string? usePtyRaw = Environment.GetEnvironmentVariable("ServerManagerBot_UsePty");
        return usePtyRaw?.ToLower() is "1" or "yes" or "true";
    }

    private void InitProcess()
    {
        ProcessStatus = Status.Ready;
        
        if (_usePty)
        {
            _ptyOptions = new PtyOptions
            {
                Name = AppDomain.CurrentDomain.FriendlyName,
                App = _processPath,
                Cwd = _processWorkingDirectory,
                CommandLine = _processArguments.ToArray(),
                Environment = new Dictionary<string, string>()
            };
            
            return;
        }
        
        _process = new Process
        {
            StartInfo = new ProcessStartInfo(_processPath)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _processWorkingDirectory,
                Arguments = string.Join(' ', _processArguments)
            }
        };
        _process.EnableRaisingEvents = true;
        _process.Exited += OnProcessExited;
        _process.OutputDataReceived += OnProcessOutputDataReceived;
        _process.ErrorDataReceived += OnProcessErrorDataReceived;
    }

    private void ResetProcess()
    {
        if (_usePty)
            try { _ptyConnection.Dispose(); } catch { }
        else
            _process.Dispose();
        
        InitProcess();
    }
    
    private void OnProcessExited(object? _, EventArgs e)
    {
        ProcessStatus = Status.Ended;
        Exited?.Invoke(this, new ExitedEventArgs(_process.ExitCode, _process.ExitTime));
    }

    private void OnPtyExited(object? _, PtyExitedEventArgs e)
    {
        ProcessStatus = Status.Ended;
        Exited?.Invoke(this, new ExitedEventArgs(e.ExitCode, DateTime.Now));
    }

    private void OnProcessOutputDataReceived(object _, DataReceivedEventArgs e) =>
        OutputDataSent?.Invoke(this, new DataEventArgs(e.Data));
    
    private void OnProcessErrorDataReceived(object _, DataReceivedEventArgs e) =>
        ErrorDataSent?.Invoke(this, new DataEventArgs(e.Data));

    public SendInputResult SendInputToProcess(string input)
    {
        if (ProcessStatus != Status.Running)
            return SendInputResult.NotStarted;
        
        try
        {
            if (_usePty)
            {
                StreamWriter inputWriter = new StreamWriter(_ptyConnection.WriterStream);
                inputWriter.WriteLine(input);
                inputWriter.Flush();
                _ptyLastCommand = input;
            }
            else
                _process.StandardInput.WriteLine(input);
        }
        catch
        {
            return SendInputResult.Error;
        }

        return SendInputResult.Ok;
    }

    public StartResult Start()
    {
        if (!File.Exists(_processPath))
            return StartResult.BadPath;

        if (!Directory.Exists(_processWorkingDirectory))
            return StartResult.BadPath;

        if (ProcessStatus == Status.Running)
            return StartResult.AlreadyStarted;
        
        if (ProcessStatus == Status.Ended)
            ResetProcess();
        
        try
        {
            if (_usePty)
            {
                _ptyConnection = PtyProvider.SpawnAsync(_ptyOptions, CancellationToken.None).Result;
                _ptyConnection.ProcessExited += OnPtyExited;
                
                // Wait a little bit before starting to read from the pty, there are cases where the pty takes some time to start and messes up the entire reading pipe
                Thread.Sleep(1000);
                Task.Run(PtyOutputReader);
            }
            else
            {
                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
        }
        catch
        {
            return StartResult.Error;
        }

        ProcessStatus = Status.Running;
        return StartResult.Ok;
    }

    private void PtyOutputReader()
    {
        var reader = new StreamReader(_ptyConnection.ReaderStream);
        while (ProcessStatus == Status.Running)
        {
            var line = reader.ReadLine();
            if (line is null)
                break;
            
            OutputDataSent?.Invoke(this, new DataEventArgs(line));
        }
    }

    public StopResult Stop()
    {
        if (ProcessStatus != Status.Running)
            return StopResult.NotStarted;

        try
        {
            if(_usePty)
                _ptyConnection.Kill();
            else
                _process.Kill(true);
        }
        catch
        {
            return StopResult.Error;
        }

        ProcessStatus = Status.Ended;
        return StopResult.Ok;
    }
}