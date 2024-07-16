using System.Text.RegularExpressions;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ServerManagerBot;

public static class CustomCommands
{
    private static bool _noCommands = true;

    private static CustomCommandsConfiguration _configuration;

    private static Timer _cooldownTimer;

    private static bool _onCooldown;

    private static Regex _commandStartRegex;
    
    public static void Initialize(CustomCommandsConfiguration? configuration)
    {
        if (configuration is null)
            return;

        _configuration = configuration;
        _cooldownTimer = new Timer(configuration.Cooldown * 1000);
        _cooldownTimer.Elapsed += CooldownTimerOnElapsed;
        _commandStartRegex = new Regex(configuration.CommandStartRegex, RegexOptions.Compiled | RegexOptions.Multiline);

        // Initialization successful, we can activate the commands now
        _noCommands = false;
    }

    private static void CooldownTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        _onCooldown = false;
        _cooldownTimer.Stop();
    }

    private static void StartCooldown()
    {
        _onCooldown = true;
        _cooldownTimer.Start();
    }

    public static string? ProcessCommands(string input)
    {
        if (_noCommands || _onCooldown)
            return default;

        Match match = _commandStartRegex.Match(input);

        if (!match.Success)
            return default;

        input = input.Substring(match.Index + match.Length);
        
        int indexOfSpace = input.IndexOf(' ');
        if (indexOfSpace != -1)
            input = input[..indexOfSpace];
        
        string? output = _configuration.Commands.GetValueOrDefault(input);

        if (output is not null)
            StartCooldown();
            
        return output;
    }
}