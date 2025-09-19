using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace Ash3.AshDiscord.Commands {
    internal class HelpCommand : IDiscordCommand {
        public string Name { get; } = "help";

        public string Description { get; } = "Shows all commands available to you.";

        public string Category { get; } = "System";

        public CommandArgumentBuilder Args { get; } = [
            new ("CommandName", CommandArgumentType.String) { Required = false }
        ];

        public string[] Aliases { get; } = [];

        public Task Execute(SocketMessage message, CommandArgumentCollection args) {
            var commandName = args.Get<string>("CommandName");
            
            if (commandName == null) {
                var embed = new EmbedBuilder() {
                    ThumbnailUrl = Bot.Client.CurrentUser.GetAvatarUrl(),
                    Title = $"{Bot.Client.CurrentUser.Username} Help",
                    Description = $"Run `{Bot.Configuration.GetString("Prefix")}help [command name]` for details.",
                    Footer = new() { Text = $"The prefix is {Bot.Configuration.GetString("Prefix")}" },
                };

                foreach (var category in Bot.CommandHandler.Commands.Values
                    .Where(c => c.Category != "")
                    .GroupBy(c => c.Category)
                    .OrderBy(c => c.Key)) {
                    embed.AddField(category.Key, string.Join(' ', category.Select(c => $"`{c.Name}`")), true);
                }

                message.Channel.SendMessageAsync(embed: embed.Build());
            } else { // todo convert this into an activity
                var command = Bot.CommandHandler.Commands.Values.FirstOrDefault(v => v.Name == commandName || v.Aliases.Contains(commandName));
                if (command == null) {
                    message.Channel.SendMessageAsync($"`{commandName}` is not a command.");
                    return Task.CompletedTask;
                }

                var embed = new EmbedBuilder() {
                    Title = command.Name,
                    Description = command.Description,
                    Fields = {
                        new() { Name = "Usage", Value = $"{Bot.Configuration.GetString("Prefix")}{command.Name} {command.Args}" },
                    },
                    Footer = command is IRestrictedDiscordCommand restricted ? new() { Text = restricted.AdminOnly ? "This command can only be run by administrators." : restricted.StaffOnly ? "This command can only be run by staff." : "This command is restricted." } : null,
                };
                if (command.Category != "") embed.AddField("Category", command.Category, true);
                if (command.Aliases.Length > 0) embed.AddField("Aliases", string.Join(", ", command.Aliases), true);

                message.Channel.SendMessageAsync(embed: embed.Build());
            }

            return Task.CompletedTask;
        }
    }
}
