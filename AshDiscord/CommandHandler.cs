using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Discord;
using Discord.WebSocket;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualBasic;

namespace Ash3.AshDiscord {
    public class CommandHandler {
        public readonly Dictionary<string, IDiscordCommand> Commands = new();

        public CommandHandler() {
            // we add commands in a separate thread for quicker startup time
            new Thread(() => {
                foreach (var command in AppDomain.CurrentDomain.GetAssemblies().SelectMany(v => v.GetTypes()).Where(v => typeof(IDiscordCommand).IsAssignableFrom(v) && !v.IsInterface && !v.IsAbstract)) {
                    var instance = (IDiscordCommand) Activator.CreateInstance(command)!;
                    Commands.Add(instance.Name, instance);
                    // force static fields to be initialized (such as Activities)
                    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(command.TypeHandle);
                }
                Program.Log("DISCORD", "Command Handler Ready", ConsoleColor.DarkBlue);
            }).Start();

            Bot.Client.MessageReceived += async message => {
                if (message.Author.IsBot) return;
                if (!message.Content.StartsWith(Bot.Configuration.GetString("Prefix"))) return;

                // update user cache if message sent in stormlands guild (should probably make this a configuration option)
                if (message.Channel is IGuildChannel channel && channel.GuildId == 1350590619503955968uL) {
                    User.FromDiscordId((long) message.Author.Id)?.UpdateFromDiscord((SocketGuildUser) message.Author);
                }

                var messageText = message.Content[Bot.Configuration.GetString("Prefix").Length..] + ' ';
                var commandName = messageText[..messageText.IndexOf(' ')];
                var fullText = messageText[(commandName.Length + 1)..];

                var command = Commands.Values.FirstOrDefault(v => v.Name == commandName || v.Aliases.Contains(commandName));
                if (command == null) return;

                if (command is IRestrictedDiscordCommand restricted) {
                    if (restricted.AdminOnly && !message.Author.IsAdmin()) return;
                    if (restricted.StaffOnly && !message.Author.IsStaff()) return;
                }

                if (fullText.Length == 0 && command.Args.Any(arg => arg.Required)) {
                    Commands["help"].Execute(message, [new CommandArgumentString("CommandName") { Value = command.Name }]);
                    return;
                }
                
                var parser = new Parser();
                var collection = new CommandArgumentCollection() { FullText = fullText };
                
                try {
                    // todo this should be static and one pass instead of two
                    parser.Tokenize(fullText);
                    parser.FillAgainst(collection, command.Args);
                } catch (Exception e) {
                    switch (e) {
                        case ArgumentNullException:
                            _ = message.Channel.SendMessageAsync($"{Bot.Configuration.GetString("EmojiWarning")} One or more arguments required by the command `{command.Name}` are missing. Please see below for details.\n*Run `{Bot.Configuration.GetString("Prefix")}help {command.Name}` for syntax information.*```\n{message.Content}\n{Bot.Configuration.GetString("Prefix")}{command.Name} {command.Args}\n{e.Message}```");
                            return;
                        case ArgumentException:
                            var i = (int) e.Data["TokenIndex"]!;
                            var spacing = new string(' ', message.Content.Length - fullText.Length + parser.Tokens[..i].Aggregate(0, (sum, token) => sum + token.Value.Length + 1) + 1);
                            _ = message.Channel.SendMessageAsync($"{Bot.Configuration.GetString("EmojiWarning")} One or more arguments are not accepted by the command `{command.Name}`. Please see below for details.\n*Run `{Bot.Configuration.GetString("Prefix")}help {command.Name}` for syntax information.*```\n{message.Content}\n{spacing}{new string('^', parser.Tokens[i].Value.Length)}\n{spacing}{e.Message}```");
                            return;
                        default:
                            _ = message.Channel.SendMessageAsync($"{Bot.Configuration.GetString("EmojiWarning")} An error occurred during the `{command.Name}` argument parsing.\n*You should not see this message under normal circumstances. Please let the developer know.*```hs\n{e.GetType().Name}\n{e.Message}\n{fullText}```");
                            Console.Error.WriteLine(e);
                            return;
                    }
                }

                _ = Task.Run(async () => {
                    await command.Execute(message, collection);
                }).ContinueWith(t => {
                    if (t.IsFaulted && t.Exception.InnerException != null) {
                        var e = t.Exception.InnerException;
                        switch (e) {
                            case ArgumentException a:
                                var i = command.Args.FindIndex(definition => definition.Name == a.ParamName);
                                var spacing = new string(' ', message.Content.Length - fullText.Length + parser.Tokens[..i].Aggregate(0, (sum, token) => sum + token.Value.Length + 1) + 1);
                                _ = message.Channel.SendMessageAsync($"{Bot.Configuration.GetString("EmojiWarning")} One or more arguments are not accepted by the command `{command.Name}`. Please see below for details.\n*Run `{Bot.Configuration.GetString("Prefix")}help {command.Name}` for syntax information.*```\n{message.Content}\n{spacing}{new string('^', parser.Tokens[i].Value.Length)}\n{spacing}{e.Message}```");
                                return;
                            case InvalidOperationException:
                                _ = message.Channel.SendMessageAsync($"{Bot.Configuration.GetString("EmojiWarning")} The command `{command.Name}` cannot continue.```\n{e.Message}```");
                                return;
                            default:
                                _ = message.Channel.SendMessageAsync($"{Bot.Configuration.GetString("EmojiWarning")} An error occurred during the `{command.Name}` command execution.\n*You should not see this message under normal circumstances. Please let the developer know.*```hs\n{e.GetType().Name}\n{e.Message}```");
                                Console.Error.WriteLine(e);
                                return;
                        }
                    }
                });
            };
        }
    }

    public interface IDiscordCommand {
        string Name { get; }

        string Description { get; }

        string Category { get; }

        CommandArgumentBuilder Args { get; }

        string[] Aliases { get; }

        Task Execute(SocketMessage message, CommandArgumentCollection args);
    }
    internal interface IRestrictedDiscordCommand : IDiscordCommand {
        bool AdminOnly { get; }
        bool StaffOnly { get; }
    }

    public class CommandArgumentDefinition(string name, params CommandArgumentType[] accepted) {
        public string Name { get; set; } = name;
        public bool Required { get; set; } = true;
        public CommandArgumentType[] Accepted { get; set; } = accepted;
    }
    public class CommandArgumentBuilder : List<CommandArgumentDefinition> { // maybe this can just be replaced with the List ? Declare is not used
        public CommandArgumentDefinition Declare(string name, params CommandArgumentType[] types) {
            var def = new CommandArgumentDefinition(name, types);
            Add(def);
            return def;
        }

        public override string ToString() => string.Join(' ', this.Select(a => a.Required ? $"[{a.Name}]" : $"<{a.Name}>"));
    }

    public class CommandArgumentCollection : List<CommandArgument> {
        public string FullText { get; set; } = "";

        public T? Get<T>(string name) {
            if (!Has(name)) return default;
            return Get(name) switch {
                CommandArgumentString arg => (T)(object)arg.Value,
                CommandArgumentInt arg => (T)(object)arg.Value,
                CommandArgumentDouble arg => (T)(object)arg.Value,
                CommandArgumentLong arg => (T)(object)arg.Value,
                CommandArgumentAccount arg => (T)(object)arg.Value,
                CommandArgumentUser arg => (T)(object)arg.Value,
                CommandArgumentFaction arg => (T)(object)arg.Value,
                CommandArgumentNation arg => (T)(object)arg.Value,
                _ => default,
            };
        } // todo have a version of this that works on index too ... or just deprecate by index altogether

        public CommandArgument Get(string name) => this.First(arg => arg.Name == name);
        //public CommandArgument Get(int index) => this[index];

        public CommandArgumentType GetType(string name) => this.Has(name) ? this.First(arg => arg.Name == name).Type : CommandArgumentType.None;
        //public CommandArgumentType GetType(int index) => this.Has(index) ? this[index].Type : CommandArgumentType.None;

        public bool Has(string name) => this.Exists(arg => arg.Name == name);
        //public bool Has(int index) => index < this.Count && this[index].Type != CommandArgumentType.None;
    }
}
