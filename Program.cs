using Ash3.Economy;
using Ash3.Groups;
using Ash3.ArsTurdgeBop;
using Ash3.AshDiscord;
using Ash3.SWLink;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Ash3 {
    internal class Program {
        public static void Log(string message) => Console.WriteLine($"[{DateTime.Now:MM/dd/yy HH:mm:ss:fff}]: {message}");
        public static void Log(string code, string message) => Log($"[{code}]: {message}");
        public static void Log(string code, string message, ConsoleColor codeBackgroundColor) {
            Console.Write($"[{DateTime.Now:MM/dd/yy HH:mm:ss:fff}]: ");
            Console.Write("[");
            Console.BackgroundColor = codeBackgroundColor;
            Console.Write(code);
            Console.ResetColor();
            Console.Write("]: ");
            Console.WriteLine(message);
        }

        static async Task Main(string[] args) {
            Log("Starting Ash 3");

            //Console.Write("\e]9;4;3;0\x07"); // https://learn.microsoft.com/en-us/windows/terminal/tutorials/progress-bar-sequences

            Log("BOOT", "Start Database", ConsoleColor.DarkGreen);
            Database.Init(":memory:");
            //Database.Init("ash.db");

            Log("BOOT", "Start Configuration", ConsoleColor.DarkGreen);
            Configuration.Init();

            Log("BOOT", "Start User", ConsoleColor.DarkGreen);
            User.Init();
            Log("BOOT", "Start Account", ConsoleColor.DarkGreen);
            Account.Init();
            Log("BOOT", "Start Faction", ConsoleColor.DarkGreen);
            Faction.Init();
            Log("BOOT", "Start Nation", ConsoleColor.DarkGreen);
            Nation.Init();

            // perform firstrun here (redacted)
            // It needs a faction with ID 0; a nation with ID 0; and a user with ID 0.

            Log("BOOT", "Start Bot", ConsoleColor.DarkGreen);
            Bot.Init();
            IActivity.Init();

            //Console.Write("\e]9;4;0;0\x07");

            var exitEvent = new ManualResetEvent(false);

            Console.CancelKeyPress += (sender, e) => { e.Cancel = true; exitEvent.Set(); };

            await Bot.Start();

            exitEvent.WaitOne();

            Log("Stopping Ash 3");

            Log("EXIT", "Stop Bot", ConsoleColor.DarkRed);
            await Bot.Stop();

            //Log("EXIT", "Commit Configuration", ConsoleColor.DarkRed);
            //Configuration.Commit();

            Log("EXIT", "Close Database", ConsoleColor.DarkRed);
            Database.Connection.Close();

            Log("EXIT", "Done", ConsoleColor.DarkRed);
        }
    }
}
