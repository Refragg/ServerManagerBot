using DSharpPlus;
using DSharpPlus.Entities;
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
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Stopping the server..."));
            
            Task.Run(() => Program.Stop());
        }
        
        [SlashCommand("quit", "Quits the server manager")]
        public async Task Quit(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Quiting the server manager..."));
            
            Task.Run(() => Program.Shutdown());
        }
        
        [SlashCommand("send", "Send a command to the server")]
        public async Task Send(InteractionContext ctx,
            [Option("Command", "The command to send to the server")] string command)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Sending the input to the process..."));
            
            Task.Run(() => DiscordBot.SendCommand(command));
        }
    }
}