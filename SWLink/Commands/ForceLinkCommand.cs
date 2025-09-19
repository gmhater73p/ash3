using Ash3.AshDiscord;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ash3.SWLink.Commands {
    internal class ForceLinkCommand : IRestrictedDiscordCommand {
        public string Name { get; } = "forcelink";

        public string Description { get; } = "Force associate a Steam ID with a user. If the user does not exist, it will be created with the given Discord ID.";

        public string Category { get; } = "Stormworks";

        public CommandArgumentBuilder Args { get; } = [
            new ("SteamId", CommandArgumentType.Long),
            new ("User", CommandArgumentType.User, CommandArgumentType.Long)
        ];

        public string[] Aliases { get; } = [];

        public bool AdminOnly { get; } = false;
        public bool StaffOnly { get; } = true;

        public Task Execute(SocketMessage message, CommandArgumentCollection args) {
            var steamId = args.Get<long>("SteamId")!;
            if (steamId.ToString().Length != 17 || !steamId.ToString().StartsWith("765")) throw new ArgumentException("Invalid Steam ID: must be 17 digit Steam64", "SteamId");

            switch (args.GetType("User")) {

                case CommandArgumentType.User:
                    var user = args.Get<User>("User")!;
                    user.SteamId = steamId;
                    user.VerifiedTimestamp = DateTime.UtcNow;
                    message.Channel.SendMessageAsync($"{Bot.Configuration.GetString("EmojiSuccess")} User " + user.Id + " changed Steam ID");
                    break;

                case CommandArgumentType.Long:
                    var discordId = args.Get<long>("User")!;
                    if (discordId.ToString().Length < 17 || discordId < 0) throw new ArgumentException("Invalid Discord ID", "User");
                    message.Channel.SendMessageAsync($"{Bot.Configuration.GetString("EmojiSuccess")} User " + Verification.Verify(discordId, steamId).Id + " created with Steam ID");
                    break;

            }
            return Task.CompletedTask;
        }
    }
}
