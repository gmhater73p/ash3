using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ash3.AshDiscord.Commands {
    internal class PingCommand : IDiscordCommand {
        public string Name { get; } = "ping";

        public string Description { get; } = "Returns latency information.";

        public string Category { get; } = "System";

        public CommandArgumentBuilder Args { get; } = [];

        public string[] Aliases { get; } = [];

        public async Task Execute(SocketMessage message, CommandArgumentCollection args) {
            var newMsg = await message.Channel.SendMessageAsync($"{Bot.Configuration.GetString("EmojiLoading2")} Ping?");

            var content = $"Pong! `{(newMsg.Timestamp - message.Timestamp).TotalMilliseconds}ms`. API Latency: `{Bot.Client.Latency}ms`.";
            // iterate over servers here in the future when thats available, and add to ping
            _ = newMsg.ModifyAsync(msg => msg.Content = content);
        }
    }
}
