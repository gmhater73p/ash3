using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ash3.AshDiscord;
using Ash3.Groups;
using Ash3.Groups.Commands;
using Ash3.SWLink;
using Discord;
using Discord.WebSocket;
using FuzzySharp;

namespace Ash3.Economy.Commands {
    internal class AccountCommand : IDiscordCommand {
        public string Name { get; } = "account";

        public string Description { get; } = "View details about an economy account.";

        public string Category { get; } = "Economy";

        public CommandArgumentBuilder Args { get; } = [
            new ("Target", CommandArgumentType.Account, CommandArgumentType.Faction, CommandArgumentType.Nation, CommandArgumentType.User, CommandArgumentType.String) { Required = false }
        ];

        public string[] Aliases { get; } = ["a"];

        public Task Execute(SocketMessage message, CommandArgumentCollection args) {
            var thisUser = User.FromDiscordId((long) message.Author.Id);
            if (thisUser == null) {
                _ = Verification.NotLinkedActivity.Present(message.Author, message.Channel, []);
                return Task.CompletedTask;
            }

            var account = // account if provided
                args.GetType("Target") == CommandArgumentType.Account ? args.Get<Account>("Target")
                // user's primary account if exists and is .
                : args.GetType("Target") == CommandArgumentType.String && args.Get<string>("Target") == "." ? thisUser.PrimaryAccount
                // implements fuzzy searching account name // implements rudimentary searching using Database.SearchColumn
                : args.GetType("Target") == CommandArgumentType.String && args.Get<string>("Target")!.Length >= 3 ? Account.Get(Process.ExtractOne(new { Name = args.Get<string>("Target"), Id = -1 }, Account.GetAll().Select(a => new { a.Name, a.Id })!, a => a.Name).Value.Id)//Account.Get(Account.Database.SearchColumn(args.Get<string>("Target")!, "Name").FirstOrDefault(-1))
                // null if no matches or no argument provided
                : null;
            
            if (account == null) {
                if (args.GetType("Target") == CommandArgumentType.String) throw new ArgumentException($"Account matching query \"{args.Get<string>("Target")}\" not found", "Target");

                List<Account> accounts;
                Account? primaryAccount;
                string targetName;
                
                switch (args.GetType("Target")) {
                    case CommandArgumentType.Faction:
                        var faction = args.Get<Faction>("Target")!;
                        accounts = faction.Accounts.Cast<Account>().ToList();
                        primaryAccount = faction.PrimaryAccount;
                        targetName = faction.Name;
                        break;
                    case CommandArgumentType.Nation:
                        var nation = args.Get<Nation>("Target")!;
                        accounts = nation.Accounts.Cast<Account>().ToList();
                        primaryAccount = nation.PrimaryAccount;
                        targetName = nation.Name;
                        break;
                    case CommandArgumentType.User:
                        var user = args.Get<User>("Target")!;
                        accounts = user.GetAccounts();
                        primaryAccount = user.PrimaryAccount;
                        targetName = user.DisplayName;
                        break;
                    default:
                        accounts = thisUser.GetAccounts();
                        primaryAccount = thisUser.PrimaryAccount;
                        targetName = thisUser.DisplayName;
                        break;
                }

                var selectMenu = new SelectMenuBuilder {
                    CustomId = SummaryActivity.BindToSelectMenu([new CommandArgumentAccount("Account") { Value = null }], "Account", (long)thisUser.DiscordId!),
                    Placeholder = accounts.Count + " account" + (accounts.Count != 1 ? "s" : ""),
                };

                foreach (var acc in accounts) selectMenu.AddOption($"[ {acc.Id} ] {acc.Name}", acc.Id.ToString(), $"{acc switch { PersonalAccount p => "Personal - " + p.Owner.DisplayName, NationAccount n => "Nation - " + n.Owner.Name, FactionAccount f => "Faction - " + f.Owner.Name }} - {acc.Balance:N0} {Bot.Configuration.GetString("CurrencySymbol")}", acc.Equals(primaryAccount) ? new Emoji("⭐") : null);

                if (accounts.Count == 0) {
                    selectMenu.IsDisabled = true;
                    selectMenu.AddOption("-", "-");
                }

                message.Channel.SendMessageAsync($"**{targetName}**: Select an account to view details.", components: new ComponentBuilder().WithSelectMenu(selectMenu).Build());
            } else {
                _ = SummaryActivity.Present(message.Author, message.Channel, [new CommandArgumentAccount("Account") { Value = account }]);
            }
            return Task.CompletedTask;
        }

        public readonly static Activity<Presentation> SummaryActivity = new() {
            Id = "ViewAccount1",
            Args = [
                new ("Account", CommandArgumentType.Account)
            ],
            Execute = delegate (ActivityContext context, CommandArgumentCollection args) {
                var thisUser = User.FromDiscordId((long)context.User.Id) ?? throw new InvalidOperationException("No executing user (impossible or removed?)");

                var account = args.Get<Account>("Account")!;

                Account? primaryAccount = account switch { PersonalAccount p1 => p1.Owner.PrimaryAccount, NationAccount n1 => n1.Owner.PrimaryAccount, FactionAccount f1 => f1.Owner.PrimaryAccount };

                var embed = new EmbedBuilder {
                    Title = $"{(account.Equals(primaryAccount) ? "⭐ " : "")}[ {account.Id} ] {account.Name}",
                    ThumbnailUrl = account switch { PersonalAccount p5 => p5.Owner.CachedDiscordAvatar, _ => "attachment://group.png" },
                    Color = new Discord.Color((uint)(account switch { PersonalAccount p2 => p2.Owner.Color, NationAccount n2 => n2.Owner.Color, FactionAccount f2 => f2.Owner.Color }).ToInt()),
                    Description = $"{account.Type} account{(account.Equals(primaryAccount) ? "\nPrimary account" : "")}\nCreated <t:{(int)account.CreationTimestamp.Subtract(DateTime.UnixEpoch).TotalSeconds}:D> <t:{(int)account.CreationTimestamp.Subtract(DateTime.UnixEpoch).TotalSeconds}:R>",
                    Fields = {
                        new EmbedFieldBuilder {
                            Name = "Balance",
                            Value = $"{account.Balance:N0} {Bot.Configuration.GetString("CurrencySymbol")}",
                            IsInline = true
                        },
                        new EmbedFieldBuilder {
                            Name = "Owner",
                            Value = account switch { PersonalAccount p3 => p3.Owner.DiscordId != null ? $"<@{p3.Owner.DiscordId}>" : p3.Owner.DisplayName, NationAccount n3 => n3.Owner.Name, FactionAccount f3 => f3.Owner.Name },
                            IsInline = true
                        }
                    },
                    Footer = new EmbedFooterBuilder {
                        Text = $"Economy | {(context.User is SocketGuildUser u ? u.DisplayName : thisUser.DisplayName)}"
                    }
                };

                if (account.UserHasPermission(thisUser, AccountPermission.Use)) {
                    embed.AddField($"History ({account.Transactions.Count})", account.Transactions.Count == 0 ? "No entries" : string.Join('\n', account.Transactions[..10].Select(t =>
                        $"<t:{(int)t.CreationTimestamp.Subtract(DateTime.UnixEpoch).TotalSeconds}:{(DateTime.UtcNow.Subtract(t.CreationTimestamp).Days == 0 ? "t" : "d")}> {t.FormatFor(account)}" + (t.Note != null ? $": \"{(t.Note.Length > 16 ? t.Note[..16] + "..." : t.Note)}\"" : ""))));
                }

                var allUsers = account.GetUsersWithPermission(AccountPermission.Use, AccountPermission.Delete, AccountPermission.Rename);
                if (account is PersonalAccount p && p.Users.Count > 0) {
                    embed.AddField($"Users ({p.Users.Count})", string.Join(", ", p.Users.Select(u => u.DiscordId != null ? $"<@{u.DiscordId}>" : u.DisplayName)));
                } else if (account.Type != AccountType.Personal && allUsers.Count > 0) {
                    embed.AddField($"Users ({allUsers.Count})", string.Join('\n', allUsers.Select(u => $"{(u.DiscordId != null ? $"<@{u.DiscordId}>" : u.DisplayName)} {(account.UserHasPermission(u, AccountPermission.Use) ? "💸" : "")} {(account.UserHasPermission(u, AccountPermission.Rename) ? "📝" : "")} {(account.UserHasPermission(u, AccountPermission.Delete) ? "🗑️" : "")}")));
                }

                var component = new ComponentBuilder();

                switch (account) {

                    case PersonalAccount p4:
                        if (thisUser.Equals(p4.Owner)) {
                            component.WithButton(new ButtonBuilder {
                                CustomId = "a",//UserSelectorActivity.BindToComponent([new CommandArgumentAccount("Account") { Value = account }], "Account"),
                                Label = "Add User",
                                Emote = new Emoji("📥"),
                                Style = ButtonStyle.Secondary,
                                IsDisabled = true
                            });
                            component.WithButton(new ButtonBuilder {
                                CustomId = "b",//UserSelectorActivity.BindToComponent([new CommandArgumentAccount("Account") { Value = account }], "Account"),
                                Label = "Remove User",
                                Emote = new Emoji("🚷"),
                                Style = ButtonStyle.Secondary,
                                IsDisabled = true
                            });
                        }
                        break;

                    case FactionAccount f4:
                        component.WithButton(new ButtonBuilder {
                            CustomId = FactionCommand.SummaryActivity!.BindToComponent([new CommandArgumentFaction("Faction") { Value = f4.Owner }], (long)thisUser.DiscordId!),
                            Label = "View Faction",
                            Emote = new Emoji("🏛️"),
                            Style = ButtonStyle.Secondary
                        });
                        break;

                    case NationAccount n4:
                        component.WithButton(new ButtonBuilder {
                            CustomId = "d",//Nation.SummaryActivity.BindToComponent([new CommandArgumentAccount("Account") { Value = account }], "Account"),
                            Label = "View Nation",
                            Emote = new Emoji("🌎"),
                            Style = ButtonStyle.Secondary,
                            IsDisabled = true
                        });
                        break;

                }

                if (account.UserHasPermission(thisUser, AccountPermission.Rename)) {
                    component.WithButton(new ButtonBuilder {
                        CustomId = RenameActivity!.BindToComponent([new CommandArgumentAccount("Account") { Value = account }], (long)thisUser.DiscordId!),
                        Label = "Rename",
                        Emote = new Emoji("📝"),
                        Style = ButtonStyle.Secondary
                    });
                }

                if (account.UserHasPermission(thisUser, AccountPermission.Delete)) {
                    component.WithButton(new ButtonBuilder {
                        CustomId = DeleteActivity!.BindToComponent([new CommandArgumentAccount("Account") { Value = account }], (long)thisUser.DiscordId!),
                        Label = "Delete",
                        Emote = new Emoji("🗑️"),
                        Style = ButtonStyle.Secondary
                    });
                }

                var presentation = new Presentation {
                    Components = component.Build()
                }.WithEmbed(embed.Build());

                var group = (Group?) (account is FactionAccount f6 ? f6.Owner : account is NationAccount n6 ? n6.Owner : null);
                if (group != null && group.DisplayImage != null) presentation.WithFileAttachment(new FileAttachment(group.DisplayImage, "group.png"));

                return presentation;
            }
        };

        public readonly static Activity<Task> RenameActivity = new() {
            Id = "RenameAccount1",
            Args = [
                new ("Account", CommandArgumentType.Account)
            ],
            Execute = async delegate (ActivityContext context, CommandArgumentCollection args) {
                var thisUser = User.FromDiscordId((long)context.User.Id) ?? throw new InvalidOperationException("No executing user (impossible or removed?)");

                var account = args.Get<Account>("Account")!;

                if (!account.UserHasPermission(thisUser, AccountPermission.Rename)) throw new InvalidOperationException("User does not have permission to rename account");

                var ticket = new ComponentYieldTicket((long)thisUser.DiscordId!);
                var modal = new ModalBuilder {
                    CustomId = ticket.CustomId,
                    Title = $"Rename Account #{account.Id}",
                    Components = new ModalComponentBuilder().WithTextInput(new TextInputBuilder() {
                        CustomId = "Name",
                        Label = "New Name",
                        Placeholder = $"Enter a new name for \"{account.Name}\"",
                        Required = true,
                        MinLength = 4,
                        MaxLength = 64,
                    })
                };

                await ((SocketMessageComponent)context.PreviousInteraction!).RespondWithModalAsync(modal.Build());

                var result = (SocketModal) await ticket.Wait();
                var name = result.Data.Components.First().Value;

                var embed = new EmbedBuilder {
                    Title = "Account Renamed",
                    Color = new Discord.Color((uint)Color.FromHexCode("#32a852").ToInt()),
                    Description = $"Account #{account.Id} has been renamed to `{name}`.\n-# Previously: `{account.Name}`",
                    Footer = new EmbedFooterBuilder() {
                        Text = $"Economy | {(context.User is SocketGuildUser u ? u.DisplayName : thisUser.DisplayName)}"
                    }
                };

                account.Name = name;

                _ = result.RespondAsync(embed: embed.Build());
            }
        };

        public readonly static Activity<Task> DeleteActivity = new() {
            Id = "DeleteAccount1",
            Args = [
                new ("Account", CommandArgumentType.Account)
            ],
            Execute = async delegate (ActivityContext context, CommandArgumentCollection args) {
                var thisUser = User.FromDiscordId((long)context.User.Id) ?? throw new InvalidOperationException("No executing user (impossible or removed?)");

                var account = args.Get<Account>("Account")!;

                if (!account.UserHasPermission(thisUser, AccountPermission.Delete)) throw new InvalidOperationException("User does not have permission to delete account");

                var ticket = new ComponentYieldTicket((long)thisUser.DiscordId!);

                var embed = new EmbedBuilder {
                    Title = $"Delete Account #{account.Id}",
                    Color = new Discord.Color((uint)Color.FromHexCode("#f70c0c").ToInt()),
                    Description = $"Are you sure you want to delete `{account.Name}`?\n-# This account has `{account.Balance:N0} {Bot.Configuration.GetString("CurrencySymbol")}` which will be *permanently lost* upon deletion.",
                    Footer = new EmbedFooterBuilder() {
                        Text = $"Economy | {(context.User is SocketGuildUser u ? u.DisplayName : thisUser.DisplayName)}"
                    }
                };

                var components = new ComponentBuilder()
                    .WithButton(new ButtonBuilder {
                        CustomId = ticket.CustomId + "y",
                        Label = "Yes",
                        Style = ButtonStyle.Danger
                    }).WithButton(new ButtonBuilder {
                        CustomId = ticket.CustomId + "n",
                        Label = "No",
                        Style = ButtonStyle.Secondary
                    });

                var original = ((SocketMessageComponent)context.PreviousInteraction!).Message;
                await context.Reply(new Presentation { Components = components.Build() }.WithEmbed(embed.Build()));
                
                var result = (SocketMessageComponent)await context.WaitingFor(ticket);

                switch (result.Data.CustomId.Last()) {
                    case 'y':
                        if (!account.UserHasPermission(thisUser, AccountPermission.Delete)) throw new InvalidOperationException("User does not have permission to delete account");
                        var successEmbed = new EmbedBuilder {
                            Title = "Account Deleted",
                            Color = new Discord.Color((uint)Color.FromHexCode("#f70c0c").ToInt()),
                            Description = $"Account #{account.Id} `{account.Name}` has been deleted.",
                            Footer = new EmbedFooterBuilder {
                                Text = $"Economy | {(context.User is SocketGuildUser u2 ? u2.DisplayName : thisUser.DisplayName)}"
                            }
                        };
                        account.Delete();
                        _ = original.ModifyAsync(msg => { msg.Content = "";  msg.Components = null; });
                        _ = context.Edit(new Presentation().WithEmbed(successEmbed.Build()));
                        break;
                    case 'n':
                        _ = context.Edit(new Presentation().WithEmbed(new EmbedBuilder() {
                            Description = "Account deletion canceled.",
                            Footer = new EmbedFooterBuilder {
                                Text = $"Economy | {(context.User is SocketGuildUser u3 ? u3.DisplayName : thisUser.DisplayName)}"
                            }
                        }.Build()));
                        break;
                }
            }
        };

        public readonly static Activity<Task> AddUserActivity = new() {
            Id = "AddAccountUser1",
            Args = [
                new ("Account", CommandArgumentType.Account)
            ],
            Execute = async delegate (ActivityContext context, CommandArgumentCollection args) {
                var thisUser = User.FromDiscordId((long)context.User.Id) ?? throw new InvalidOperationException("No executing user (impossible or removed?)");

                var account = args.Get<Account>("Account") as PersonalAccount ?? throw new InvalidOperationException("Account is not Personal");
                if (!account.Owner.Equals(thisUser)) throw new InvalidOperationException("User does not have permission to add users (not the account owner)");

                await context.Reply(new Presentation { Text = $"Please list the users to be added to account #{account.Id}.\n\nType `cancel` to cancel." });

                // TODO: FINISH (no method for awaiting the next message in the channel)
            }
        };
    }
}
