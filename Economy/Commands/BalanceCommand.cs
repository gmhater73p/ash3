using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ash3.AshDiscord;
using Ash3.Groups;
using Ash3.SWLink;
using Discord;
using Discord.WebSocket;

namespace Ash3.Economy.Commands {
    internal class BalanceCommand : IDiscordCommand {
        public string Name { get; } = "balance";

        public string Description { get; } = "View the balance of a user, faction, or nation.";

        public string Category { get; } = "Economy";

        public CommandArgumentBuilder Args { get; } = [
            new ("Target", CommandArgumentType.User, CommandArgumentType.Faction, CommandArgumentType.Nation) { Required = false }
        ];

        public string[] Aliases { get; } = ["bal", "b"];

        public Task Execute(SocketMessage message, CommandArgumentCollection args) {
            var thisUser = User.FromDiscordId((long) message.Author.Id);
            if (thisUser == null) {
                _ = Verification.NotLinkedActivity.Present(message.Author, message.Channel, []);
                return Task.CompletedTask;
            }

            //BalanceActivity.Run(message, args.Count > 0 ? args : [new CommandArgumentUser("Target") { Value = thisUser }]);
            _ = BalanceActivity.Present(message.Author, message.Channel, args.Count > 0 ? args : [new CommandArgumentUser("Target") { Value = thisUser }]);
            return Task.CompletedTask;
        }

        public readonly static Activity<Presentation> BalanceActivity = new() {
            Id = "Balance1",
            Args = [
                new ("Target", CommandArgumentType.User, CommandArgumentType.Faction, CommandArgumentType.Nation)
            ],
            Execute = delegate(ActivityContext context, CommandArgumentCollection args) {
                var user = args.GetType("Target") == CommandArgumentType.User ? args.Get<User>("Target") : null;
                var faction = args.GetType("Target") == CommandArgumentType.Faction ? args.Get<Faction>("Target") : null;
                var nation = args.GetType("Target") == CommandArgumentType.Nation ? args.Get<Nation>("Target") : null;

                var group = (Group)faction! ?? (Group)nation!;

                var accounts = (user != null ? user.GetAccounts() : group.Accounts)
                    // filter out system accounts unless viewing system
                    .Where(account => !(account is FactionAccount f && f.Owner.Id == 0) || faction != null && faction.Id == 0);

                var embed = new EmbedBuilder {
                    Author = new EmbedAuthorBuilder {
                        Name = user != null ? $"{user.DisplayName}'s Economy Accounts" : $"{group.Name} Economy Accounts",
                        IconUrl = user != null ? user.CachedDiscordAvatar : "attachment://group.png"
                    },
                    Color = user != null ? new Discord.Color((uint) user.Color.ToInt()) : new Discord.Color((uint) group.Color.ToInt()),
                    Description = $"Total: {accounts.Aggregate(0, (s, acc) => s += acc.Balance):N0} {Bot.Configuration.GetString("CurrencySymbol")}",
                    Footer = new EmbedFooterBuilder {
                        Text = $"Economy | {(context.User is SocketGuildUser u ? u.DisplayName : User.FromDiscordId((long)context.User.Id)!.DisplayName)}"
                    }
                };

                foreach (var account in accounts) embed.AddField($"{(account.Equals(user != null ? user.PrimaryAccount : faction != null ? faction.PrimaryAccount : nation.PrimaryAccount) ? "⭐" : "")} [ {account.Id} ] {account.Name}", $"{((account is PersonalAccount a && a.Owner.Equals(user)) || (account is NationAccount b && b.Owner.Equals(nation)) || (account is FactionAccount c && c.Owner.Equals(faction)) ? "" : $"Owner: {account switch { PersonalAccount p => p.Owner.DiscordId != null ? $"<@{p.Owner.DiscordId}>" : p.Owner.DisplayName, NationAccount n => n.Owner.Name, FactionAccount n => n.Owner.Name }}\n")}Balance: {account.Balance:N0} {Bot.Configuration.GetString("CurrencySymbol")}");

                var presentation = new Presentation().WithEmbed(embed.Build());

                if (group != null && group.DisplayImage != null) presentation.WithFileAttachment(new FileAttachment(group.DisplayImage, "group.png"));

                return presentation;
            }
        };
    }
}
