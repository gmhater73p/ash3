using Ash3.AshDiscord;
using Ash3.Economy;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Ash3.SWLink {
    public static class Verification {

        public readonly static Dictionary<string, long> LinkCodes = new(); // ephemeral. link code / discord id

        public readonly static Event<User> OnUserVerify = new();

        public static User Verify(long discordId, long steamId) {
            var user = User.FromDiscordId(discordId) ?? User.Create();

            user.DiscordId = discordId;
            user.SteamId = steamId;

            user.VerifiedTimestamp = DateTime.UtcNow;

            var discordUser = Bot.Client?.GetGuild(747672173073924117uL).GetUser((ulong)discordId);
            if (discordUser != null) user.UpdateFromDiscord(discordUser);

            if (user.PrimaryAccount == null) {
                user.PrimaryAccount = PersonalAccount.Create(user);
                user.PrimaryAccount.Name = $"{user.DisplayName}'s Personal Account";
                user.PrimaryAccount.Balance = 500; // starting balance should be configurable
                Account.Get(0)!.Balance -= 500; // temporary: deduct from pool
            }

            OnUserVerify.Fire(user);

            return user;
        }

        public static User Verify(string code, long steamId) {
            if (!LinkCodes.TryGetValue(code, out long discordId)) throw new InvalidOperationException("No Discord ID associated with link code");
            LinkCodes.Remove(code);
            return Verify(discordId, steamId);
        }

        public static string GenerateLinkCode(long discordId) {
            if (LinkCodes.ContainsValue(discordId)) return LinkCodes.First(x => x.Value == discordId).Key; // should use .Single here
            var code = NanoidDotNet.Nanoid.Generate("0123456789abcdefghijklmnopqrstuvwxyz", 4);
            LinkCodes[code] = discordId;
            return code;
        }

        public readonly static Activity<Presentation> NotLinkedActivity = new() {
            Id = "NotLinked",
            Args = [],
            Execute = delegate (ActivityContext context, CommandArgumentCollection args) {
                return new Presentation().WithEmbed(new EmbedBuilder() {
                    Title = "Not Linked",
                    Description = $"You must link your account to Stormworks to continue.\n\nTo link your account, run {Bot.Configuration.GetString("Prefix")}link."
                }.Build());
            }
        };

    }
}
