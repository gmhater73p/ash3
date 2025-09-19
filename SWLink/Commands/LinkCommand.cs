using Ash3.AshDiscord;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ash3.SWLink.Commands {
    internal class LinkCommand : IDiscordCommand {
        public string Name { get; } = "link";

        public string Description { get; } = "Generate a code to link your account to Stormworks.";

        public string Category { get; } = "Stormworks";

        public CommandArgumentBuilder Args { get; } = [];

        public string[] Aliases { get; } = [];

        public async Task Execute(SocketMessage message, CommandArgumentCollection args) {
            var existingUser = User.FromDiscordId((long)message.Author.Id);
            if (existingUser != null && existingUser.SteamId != null) {
                _ = message.Channel.SendMessageAsync("You are already linked to Stormworks. If you would like to unlink your account, please contact staff.");
                return;
            }

            var code = Verification.GenerateLinkCode((long)message.Author.Id);

            var dm = await message.Author.CreateDMChannelAsync();

            var embed = new EmbedBuilder {
                Title = "Link to Stormworks",
                Description = "Please follow these directions to link your Discord account to in-game servers.",
            };
            embed.AddField("Link Code", $"**{code}**\n*Do not share your link code with anyone.*");
            embed.AddField("Directions", $"Join the Stormworks server and type `?link {code}` in the **in-game chat.**");

            try {
                await dm.SendMessageAsync(embed: embed.Build());
            } catch {
                _ = message.Channel.SendMessageAsync("Unable to DM you. Please configure your settings to allow direct messages from this server or contact staff.", messageReference: new MessageReference(message.Id));
                return;
            }

            _ = message.Channel.SendMessageAsync("Please check your DMs.", messageReference: new MessageReference(message.Id));
        }
    }
}
