using Terminal.Gui;

namespace ServerManagerBot;

public partial class ServerShell
{
    private int _lines;
    private bool _paused;
    private readonly List<string> _pausedLinesBuffer = new();

    public event EventHandler<CommandEventArgs>? CommandSent;
    public event EventHandler<CommandEventArgs>? SpecialCommandSent;
    
    public ServerShell() 
    {
        InitializeComponent();

        inputField.KeyDown += (e) =>
        {
            if (e.KeyEvent.Key == Key.Enter && inputField.Text != "")
            {
                HandleCommand(inputField.Text.ToString()!);
                inputField.Text = "";
            }
        };
    }

    public void AppendLine(string text)
    {
        if (_paused)
        {
            _pausedLinesBuffer.Add(text);
            return;
        }
        
        if (_lines > 100)
            logsView.Text = logsView.Text.Substring(logsView.Text.IndexOf('\n') + 1) + ('\n' + text);
        else
        {
            logsView.Text += '\n' + text;
            _lines++;
        }
        
        logsView.MoveEnd();
    }

    private void TogglePause()
    {
        if (!_paused)
        {
            _paused = true;
            statusLabel.Text = "PAUSED";
            statusLabel.Visible = true;
            return;
        }
        
        _paused = false;
        foreach (string line in _pausedLinesBuffer)
            AppendLine(line);
            
        _pausedLinesBuffer.Clear();
        
        statusLabel.Visible = false;
    }

    private void HandleCommand(string command)
    {
        if (command.StartsWith('@'))
        {
            HandleSpecialCommand(command.Substring(1));
            return;
        }
        
        CommandSent?.Invoke(this, new CommandEventArgs(command));
    }

    private void HandleSpecialCommand(string specialCommand)
    {
        if (specialCommand.StartsWith("quit"))
        {
            RequestStop();
            return;
        }

        if (specialCommand.StartsWith("pause"))
        {
            TogglePause();
            return;
        }
        
        SpecialCommandSent?.Invoke(this, new CommandEventArgs(specialCommand));
    }
}