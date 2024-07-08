using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;

namespace ServerManagerBot;

public class DiscordBotCommands
{
    private const Permissions AddonsManagementPermissions = Permissions.ModerateMembers;

    [SlashCommandPermissions(AddonsManagementPermissions)]
    [SlashCommandGroup("ServerManager", "Manage your server")]
    [SlashRequireGuild]
    public class ServerManagerCommands : ApplicationCommandModule
    {
        [SlashCommand("stop", "Stops the server from running")]
        public async Task Stop(InteractionContext ctx)
        {
            Task.Run(() => Program.Stop());
        }
        
        [SlashCommand("quit", "Quits the server manager")]
        public async Task Quit(InteractionContext ctx)
        {
            Task.Run(() => Program.Shutdown());
        }
        
        [SlashCommand("send", "Send a command to the server")]
        public async Task Send(InteractionContext ctx,
            [Option("Command", "The command to send to the server")] string command)
        {
            Task.Run(() => DiscordBot.SendCommand(command));
        }
    }
}