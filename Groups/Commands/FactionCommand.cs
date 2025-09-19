using Ash3.AshDiscord;
using Ash3.Economy;
using Ash3.Groups;
using Ash3.SWLink;
using Discord;
using Discord.Interactions.Builders;
using Discord.WebSocket;
using FuzzySharp;
using System.Security.Principal;

namespace Ash3.Groups.Commands {
    internal class FactionCommand : IDiscordCommand {
        public string Name { get; } = "faction";

        public string Description { get; } = "View information about a faction.";

        public string Category { get; } = "Factions";

        public CommandArgumentBuilder Args { get; } = [
            new ("Target", CommandArgumentType.Faction, CommandArgumentType.String) { Required = false }
        ];

        public string[] Aliases { get; } = ["f"];

        public async Task Execute(SocketMessage message, CommandArgumentCollection args) {
            var thisUser = User.FromDiscordId((long)message.Author.Id);
            if (thisUser == null) {
                _ = Verification.NotLinkedActivity.Present(message.Author, message.Channel, []);
                return;
            }
            
            switch (args.GetType("Target")) {

                case CommandArgumentType.Faction:
                    _ = SummaryActivity.Present(message.Author, message.Channel, [new CommandArgumentFaction("Faction") { Value = args.Get<Faction>("Target")! }]);
                    break;

                case CommandArgumentType.String:
                    var result = Process.ExtractOne(new { Name = args.Get<string>("Target"), Id = -1 }, Faction.GetAll().Select(f => new { f.Name, f.Id })!, f => f.Name);
                    if (result.Score > 40) {
                        _ = SummaryActivity.Present(message.Author, message.Channel, [new CommandArgumentFaction("Faction") { Value = Faction.Get(result.Value.Id)! }]);
                    } else throw new ArgumentException($"Faction matching query \"{args.Get<string>("Target")}\" not found", "Target");
                    break;

                default:
                    var faction = await Faction.SelectActivity.Run(message, [new CommandArgumentString("Message") { Value = "Select a faction to view details." }], out var context) ?? throw new InvalidOperationException("Selected faction not found (no longer exists?)");
                    _ = SummaryActivity.Present(context, [new CommandArgumentFaction("Faction") { Value = faction }]);
                    break;

            }
        }

        public readonly static Activity<Presentation> SummaryActivity = new() {
            Id = "ViewFaction1",
            Args = [
                new ("Faction", CommandArgumentType.Faction)
            ],
            Execute = delegate (ActivityContext context, CommandArgumentCollection args) {
                var thisUser = User.FromDiscordId((long)context.User.Id) ?? throw new InvalidOperationException("No executing user (impossible or removed?)");

                var faction = args.Get<Faction>("Faction")!;

                var embed = new EmbedBuilder {
                    Title = $"{(faction.GetMember(thisUser) != null ? "⭐ " : "")} {faction.Name}",
                    ThumbnailUrl = "attachment://group.png",
                    Color = new Discord.Color((uint)faction.Color.ToInt()),
                    Description = faction.Description + $"\n\nCreated <t:{(int)faction.CreationTimestamp.Subtract(DateTime.UnixEpoch).TotalSeconds}:D> <t:{(int)faction.CreationTimestamp.Subtract(DateTime.UnixEpoch).TotalSeconds}:R>",
                    Fields = {
                        new EmbedFieldBuilder {
                            Name = "Owner",
                            Value = faction.Owner.User.DiscordId != null ? $"<@{faction.Owner.User.DiscordId}>" : faction.Owner.User.DisplayName,
                            IsInline = true
                        },
                        /*new EmbedFieldBuilder {
                            Name = "Nation",
                            Value = faction.Nation?.Name ?? "None",
                            IsInline = true
                        },*/
                    },
                    Footer = new EmbedFooterBuilder() {
                        Text = $"Factions | Faction ID: {faction.Id} | {(context.User is SocketGuildUser u ? u.DisplayName : thisUser.DisplayName)}"
                    }
                };

                if (faction.Accounts.Count > 0) {
                    embed.AddField(new EmbedFieldBuilder {
                        Name = $"Accounts ({faction.Accounts.Aggregate(0, (s, acc) => s += acc.Balance):N0} {Bot.Configuration.GetString("CurrencySymbol")})",
                        Value = string.Join('\n', faction.Accounts.Select(acc => $"`[ {acc.Id} ]` `{acc.Name}` `{acc.Balance:N0} {Bot.Configuration.GetString("CurrencySymbol")}`")),
                    });
                }

                var member = faction.GetMember(thisUser);

                var component = new ComponentBuilder();



                return new Presentation().WithEmbed(embed.Build());
            }
        };
    }
}
