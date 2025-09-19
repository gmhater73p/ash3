using Discord;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.Loader;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Discord.WebSocket;

namespace Ash3.AshDiscord.Commands {
    internal class EvalCommand : IRestrictedDiscordCommand {
        public string Name { get; } = "eval";

        public string Description { get; } = "Evaluates C#.";

        public string Category { get; } = "System";

        public CommandArgumentBuilder Args { get; } = [
            new ("Code", CommandArgumentType.String)
        ];

        public string[] Aliases { get; } = ["e"];

        public bool AdminOnly { get; } = true;
        public bool StaffOnly { get; } = false;

        public async Task Execute(SocketMessage message, CommandArgumentCollection args) {
            var msg = message.Channel.SendMessageAsync($"{Bot.Configuration.GetString("EmojiLoading")} Please wait...");

            var options = ScriptOptions.Default
                .WithImports(
                    "System",
                    "System.Math",
                    "System.Collections.Generic",
                    "System.Linq",
                    "Discord",
                    "Ash3",
                    "Ash3.ArsTurdgeBop",
                    "Ash3.Cargo",
                    "Ash3.AshDiscord",
                    "Ash3.Economy",
                    "Ash3.Groups",
                    "Ash3.SWLink"
                )
                .AddReferences(
                    "System.Linq.dll",
                    "Ash3.dll",
                    "Discord.Net.Core.dll",
                    "Discord.Net.WebSocket.dll"
                );

            var timer = Stopwatch.StartNew();

            object? result;

            // todo pass 'message' to the script (globals dont work though)
            try {
                result = await CSharpScript.EvaluateAsync(args.FullText, options);
            } catch (Exception e) {
                //Console.Error.WriteLine(e);
                _ = msg.Result.ModifyAsync(msg => msg.Content = $"{timer.Elapsed.TotalMilliseconds:F} ms```js\n{e.Message}```");
                return;
            }

            timer.Stop();

            _ = msg.Result.ModifyAsync(msg => msg.Content = $"{timer.Elapsed.TotalMilliseconds:F} ms```js\n{((Func<string>) (() => {
                if (result == null) return "[ no return value ]";
                if (result is IEnumerable<object> enumerable) return $"[{string.Join(", ", enumerable.Select(o => {
                    if (o.GetType() == typeof(string)) return $"'{o}'";
                    if (o.GetType().ToString() == o.ToString()) return $"[{o}]";
                    return o.ToString();
                }))}]";
                if (result.GetType().ToString() == result.ToString()) return $"{result} {{\n{TypeDescriptor.GetProperties(result).Cast<PropertyDescriptor>().Select(descriptor => $"    {descriptor.Name}: {(descriptor.PropertyType == typeof(string) ? $"'{descriptor.GetValue(result)}'" : descriptor.GetValue(result))}").Aggregate((a, b) => $"{a},\n{b}")}\n}}";
                return result.ToString()!;
            }))()}```");
        }
    }
}
