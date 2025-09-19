using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ash3.Economy;
using Ash3.Groups;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading.Channels;
using System.Reflection;
using Ash3.Economy.Commands;
using System.Windows.Forms;

namespace Ash3.AshDiscord {

    internal interface IActivity {
        internal static readonly List<IActivity> _activities = new();

        CommandArgumentBuilder Args { get; init; }
        string Id { get; init; }
        string[]? Aliases { get; init; }

        internal static void Init() {
            Bot.Client.InteractionCreated += async interaction => {
                var args = new Stack<string>((interaction switch { SocketMessageComponent c => c.Data.CustomId, SocketModal m => m.Data.CustomId, _ => "" }).Split(';').Reverse());

                if (args.Count == 0) return;

                if (ulong.TryParse(args.Peek(), out var userId)) {
                    args.Pop();
                    if (userId != interaction.User.Id && !Bot.IsAdmin(interaction.User)) {
                        _ = interaction.RespondAsync($"{Bot.Configuration.GetString("EmojiWarning")} Please run the command to use this interaction.", ephemeral: true);
                        return;
                    }
                }

                var activityId = args.Pop();

                if (activityId == ComponentYieldTicket._identifier) {
                    var id = args.Pop();
                    var ticket = ComponentYieldTicket._tickets.FirstOrDefault(v => v.Id == id);
                    if (ticket == null) {
                        _ = interaction.RespondAsync($"{Bot.Configuration.GetString("EmojiWarning")} This interaction is no longer available.```\n{id}: No activity to resume (please start a new interaction)```", ephemeral: true);
                        return;
                    }
                    ticket._taskCompletionSource.SetResult(interaction);
                    ComponentYieldTicket._tickets.Remove(ticket);
                    return;
                }

                var activity = _activities.FirstOrDefault(v => v.Id == activityId || (v.Aliases?.Contains(activityId) ?? false));

                if (activity == null) {
                    _ = interaction.RespondAsync($"{Bot.Configuration.GetString("EmojiWarning")} This interaction is no longer available.```\nActivity '{activityId}' does not exist (outdated?) (please start a new interaction)```", ephemeral: true);
                    return;
                }

                var collection = new CommandArgumentCollection();

                var providedByModal = new List<string>();
                if (interaction is SocketModal modal) { // strings only for now
                    modal.Data.Components.ToList().ForEach(textInput => {
                        collection.Add(new CommandArgumentString(textInput.CustomId) { Value = textInput.Value });
                        providedByModal.Add(textInput.CustomId);
                    });
                }

                foreach (var argDef in activity.Args.Where(def => !providedByModal.Contains(def.Name))) {
                    if (!args.TryPop(out var arg)) {
                        if (argDef.Required) {
                            _ = interaction.RespondAsync($"{Bot.Configuration.GetString("EmojiWarning")} The activity `{activity.Id}` failed to validate.\n*You should not see this message under normal circumstances. Please let the developer know.*```\nMissing argument: {argDef.Name}```", ephemeral: true);
                            return;
                        }
                        break;
                    }

                    var type = (CommandArgumentType)int.Parse(arg[0].ToString());
                    arg = arg[1..];

                    if (arg == "%S%" && interaction is SocketMessageComponent selectMenu) {
                        if (selectMenu.Data.Values.Count > 0) arg = selectMenu.Data.Values.First();
                    }

                    try {
                        switch (type) {
                            case CommandArgumentType.String:
                                collection.Add(new CommandArgumentString(argDef.Name) { Value = arg });
                                break;
                            case CommandArgumentType.Int:
                                collection.Add(new CommandArgumentInt(argDef.Name) { Value = int.Parse(arg) });
                                break;
                            case CommandArgumentType.Long:
                                collection.Add(new CommandArgumentLong(argDef.Name) { Value = long.Parse(arg) });
                                break;
                            case CommandArgumentType.Double:
                                collection.Add(new CommandArgumentDouble(argDef.Name) { Value = double.Parse(arg) });
                                break;
                            case CommandArgumentType.Account:
                                var account = Account.Get(int.Parse(arg));
                                if (account == null) {
                                    if (argDef.Required) throw new ArgumentNullException(null, $"Account {arg} not found (no longer exists?)");
                                } else collection.Add(new CommandArgumentAccount(argDef.Name) { Value = account });
                                break;
                            case CommandArgumentType.User:
                                var user = User.Get(int.Parse(arg));
                                if (user == null) {
                                    if (argDef.Required) throw new ArgumentNullException(null, $"User {arg} not found (no longer exists?)");
                                } else collection.Add(new CommandArgumentUser(argDef.Name) { Value = user });
                                break;
                            case CommandArgumentType.Faction:
                                var faction = Faction.Get(int.Parse(arg));
                                if (faction == null) {
                                    if (argDef.Required) throw new ArgumentNullException(null, $"Faction {arg} not found (no longer exists?)");
                                } else collection.Add(new CommandArgumentFaction(argDef.Name) { Value = faction });
                                break;
                            case CommandArgumentType.Nation:
                                var nation = Nation.Get(int.Parse(arg));
                                if (nation == null) {
                                    if (argDef.Required) throw new ArgumentNullException(null, $"Nation {arg} not found (no longer exists?)");
                                } else collection.Add(new CommandArgumentNation(argDef.Name) { Value = nation });
                                break;
                        }
                    } catch (Exception e) {
                        _ = interaction.RespondAsync($"{Bot.Configuration.GetString("EmojiWarning")} The activity `{activityId}` cannot continue.```\n{e.Message}```", ephemeral: true);
                        return;
                    }
                }

                _ = Task.Run(async () => {
                    if (activity is not Activity<Presentation> and not Activity<Task<Presentation>>) {
                        var result = activity.GetType().GetMethod("Run", [typeof(SocketInteraction), typeof(CommandArgumentCollection)])?.Invoke(activity, [interaction, collection]);
                        if (result is Task task) await task;
                    } else {
                        var presentation = activity switch {
                            Activity<Presentation> p => p.Run(interaction, collection),
                            Activity<Task<Presentation>> t => await t.Run(interaction, collection)
                        };
                        _ = presentation.Attachments != null
                            ? interaction.RespondWithFilesAsync(text: presentation.Text, attachments: presentation.Attachments?.ToArray(), embeds: presentation.Embeds?.ToArray(), components: presentation.Components, ephemeral: presentation.Ephemeral)
                            : interaction.RespondAsync(presentation.Text, embeds: presentation.Embeds?.ToArray(), components: presentation.Components, ephemeral: presentation.Ephemeral);
                    }
                }).ContinueWith(t => {
                    if (t.IsFaulted && t.Exception.InnerException != null) {
                        var e = t.Exception.InnerException;
                        switch (e) {
                            case ArgumentException or InvalidOperationException:
                                _ = interaction.HasResponded ?
                                interaction.FollowupAsync($"{Bot.Configuration.GetString("EmojiWarning")} The activity `{activityId}` cannot continue.```\n{e.Message}```", ephemeral: true) :
                                interaction.RespondAsync($"{Bot.Configuration.GetString("EmojiWarning")} The activity `{activityId}` cannot continue.```\n{e.Message}```", ephemeral: true);
                                return;
                            default:
                                _ = interaction.HasResponded ?
                                interaction.FollowupAsync($"{Bot.Configuration.GetString("EmojiWarning")} An error occurred in the `{activityId}` activity.\n*You should not see this message under normal circumstances. Please let the developer know.*```hs\n{e.GetType().Name}\n{e.Message}```") :
                                interaction.RespondAsync($"{Bot.Configuration.GetString("EmojiWarning")} An error occurred in the `{activityId}` activity.\n*You should not see this message under normal circumstances. Please let the developer know.*```hs\n{e.GetType().Name}\n{e.Message}```");
                                Console.Error.WriteLine(e);
                                return;
                        }
                    }
                });
            };
        }
    }
    public class Activity<T> : IActivity {
        public required CommandArgumentBuilder Args { get; init; }

        public required string Id { get; init; }
        public string[]? Aliases { get; init; }

        public delegate T ExecuteDelegate(ActivityContext context, CommandArgumentCollection args);
        public required ExecuteDelegate Execute { get; init; }

        public Activity() => IActivity._activities.Add(this);

        public T Run(SocketMessage message, CommandArgumentCollection args, out ActivityContext context) {
            context = new ActivityContext { User = message.Author, Channel = message.Channel, PreviousMessage = message };
            return Execute(context, args);
        }
        public T Run(SocketMessage message, CommandArgumentCollection args) => Run(message, args, out _);
        public T Run(SocketInteraction interaction, CommandArgumentCollection args, out ActivityContext context) {
            context = new ActivityContext { User = interaction.User, Channel = interaction.Channel, PreviousInteraction = interaction };
            return Execute(context, args);
        }
        public T Run(SocketInteraction interaction, CommandArgumentCollection args) => Run(interaction, args, out _);
        
        public Task<IUserMessage> Present(ActivityContext context, CommandArgumentCollection args) {
            var result = Execute(context, args);
            var presentation = result switch { Presentation p => p, Task<Presentation> r => r.Result, _ => throw new InvalidOperationException($"Activity '{Id}' does not return a Presentation") };
            return context.Reply(presentation);
        }
        public Task<IUserMessage> Present(SocketUser user, ISocketMessageChannel channel, CommandArgumentCollection args) => Present(new ActivityContext { User = user, Channel = channel }, args);

        public string BindToComponent(CommandArgumentCollection args, long discordId = 0) {
            var str = $"{Id};{string.Join(';', args.Select(arg => ((int)arg.Type).ToString() + arg))}";
            if (discordId != 0) str = $"{discordId};" + str;
            return str;
        }

        public string BindToSelectMenu(CommandArgumentCollection args, string argNameToSubstitute, long discordId = 0) {
            var str = $"{Id};{string.Join(';', args.Select(arg => ((int)arg.Type).ToString() + (arg.Name == argNameToSubstitute ? "%S%" : arg.ToString())))}";
            if (discordId != 0) str = $"{discordId};" + str;
            return str;
        }
    }

    public class ActivityContext {
        public required SocketUser User { get; set; }
        public required ISocketMessageChannel Channel { get; set; }
        public SocketInteraction? PreviousInteraction { get; set; }
        public IMessage? PreviousMessage { get; set; }

        public async Task<IUserMessage> Send(Presentation presentation) {
            if (PreviousInteraction != null && PreviousMessage == null) {
                if (presentation.Attachments != null) {
                    await PreviousInteraction.RespondWithFilesAsync(
                        text: presentation.Text,
                        embeds: presentation.Embeds?.ToArray(),
                        components: presentation.Components,
                        attachments: presentation.Attachments,
                        ephemeral: presentation.Ephemeral
                    );
                } else await PreviousInteraction.RespondAsync(
                    text: presentation.Text,
                    embeds: presentation.Embeds?.ToArray(),
                    components: presentation.Components,
                    ephemeral: presentation.Ephemeral
                );
                PreviousMessage = await PreviousInteraction.GetOriginalResponseAsync();
            } else {
                PreviousMessage = presentation.Attachments == null
                    ? await Channel.SendMessageAsync(
                        text: presentation.Text,
                        embeds: presentation.Embeds?.ToArray(),
                        components: presentation.Components,
                        messageReference: presentation.MessageReference
                    )
                    : await Channel.SendFilesAsync(
                        text: presentation.Text,
                        embeds: presentation.Embeds?.ToArray(),
                        components: presentation.Components,
                        attachments: presentation.Attachments,
                        messageReference: presentation.MessageReference
                    );
            }
            return (IUserMessage) PreviousMessage;
        }

        public Task<IUserMessage> Reply(Presentation presentation) {
            if (PreviousMessage != null) presentation.MessageReference = new MessageReference(PreviousMessage.Id);
            return Send(presentation);
        }

        public Task Edit(Presentation presentation) {
            if (PreviousMessage is IUserMessage message) {
                return message.ModifyAsync(msg => {
                    msg.Content = presentation.Text;
                    msg.Embeds = presentation.Embeds?.ToArray();
                    msg.Components = presentation.Components;
                    msg.Attachments = presentation.Attachments;
                });
            } else if (PreviousInteraction is SocketMessageComponent component) {
                PreviousMessage = component.Message;
                return component.UpdateAsync(msg => {
                    msg.Content = presentation.Text;
                    msg.Embeds = presentation.Embeds?.ToArray();
                    msg.Components = presentation.Components;
                    if (presentation.Attachments != null) msg.Attachments = presentation.Attachments;
                });
            } else return Reply(presentation);
        }

        public async Task<SocketInteraction> WaitingFor(ComponentYieldTicket ticket) {
            PreviousInteraction = await ticket.Wait();
            PreviousMessage = null;
            return PreviousInteraction;
        }
    }

    public record Presentation {
        public string Text { get; set; } = "";
        public List<Embed>? Embeds { get; set; }
        public MessageComponent? Components { get; set; }
        public List<FileAttachment>? Attachments { get; set; }
        public MessageReference? MessageReference { get; set; }
        public bool Ephemeral { get; set; } = false;
        public Presentation WithEmbed(Embed embed) {
            Embeds ??= [];
            Embeds.Add(embed);
            return this;
        }
        public Presentation WithFileAttachment(FileAttachment attachment) {
            Attachments ??= [];
            Attachments.Add(attachment);
            return this;
        }
    }

    public class ComponentYieldTicket {
        internal const string _identifier = "yieldticket";
        internal static readonly List<ComponentYieldTicket> _tickets = new();

        public readonly string Id = NanoidDotNet.Nanoid.Generate(size: 8);

        public readonly string CustomId;

        internal TaskCompletionSource<SocketInteraction> _taskCompletionSource = new();

        public ComponentYieldTicket(long discordId = 0) => CustomId = (discordId != 0 ? $"{discordId};" : "") + $"{_identifier};{Id};";

        public Task<SocketInteraction> Wait() {
            _tickets.Add(this);
            return _taskCompletionSource.Task;
        }
    }
}
