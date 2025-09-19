using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ash3.AshDiscord {
    public static class Bot {
        public static DiscordSocketClient Client;

        public static CommandHandler CommandHandler;

        public static Configuration Configuration;

        internal static void Init() {
            Configuration = new("Bot");

            Configuration.Declare("Prefix", "-");

            Configuration.Declare("EmojiLoading", ":thinking;");
            Configuration.Declare("EmojiLoading2", ":thinking;");
            Configuration.Declare("EmojiSuccess", ":white_check_mark:");
            Configuration.Declare("EmojiWarning", ":warning:");

            Configuration.Declare("CurrencySymbol", "SR");

            Client = new(new DiscordSocketConfig {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent | GatewayIntents.DirectMessages
            });

            CommandHandler = new();
        }

        internal static async Task Start() {

            Client.Log += msg => {
                Program.Log("DISCORD", msg.Message);
                return Task.CompletedTask;
            };

            Client.Ready += async () => Console.Write("•"); // console bell

            Program.Log("DISCORD", "Start Client", ConsoleColor.DarkBlue);

            throw new NotImplementedException("Bot token redacted");

            //await Client.LoginAsync(TokenType.Bot, "");
            //await Client.StartAsync();

            //await Client.SetActivityAsync(new Game($"for {Configuration.GetString("Prefix")}help", ActivityType.Watching));

        }

        internal static Task Stop() => Client.StopAsync();

        internal static readonly ulong[] AdminIds = {
            // redacted
        };
        internal static readonly ulong[] StaffIds = {
            
        };

        internal static bool IsAdmin(this IUser user) => IsAdmin(user.Id);
        internal static bool IsAdmin(ulong id) {
            return AdminIds.Contains(id);
        }

        internal static bool IsStaff(this IUser user) => IsStaff(user.Id);
        internal static bool IsStaff(ulong id) {
            return StaffIds.Contains(id) || IsAdmin(id);
        }
    }
}
