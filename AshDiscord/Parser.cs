using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ash3.Economy;
using Ash3.Groups;

namespace Ash3.AshDiscord {
    public enum CommandArgumentType {
        None,
        String,
        Int,
        Long,
        Double,

        Account,
        User,
        Faction,
        Nation,
    }
    public class CommandArgument(string Name) { // never used. can probably retire CommandArgumentType.None
        public string Name { get; set; } = Name;
        public virtual CommandArgumentType Type { get => CommandArgumentType.None; }
    }
    public class CommandArgumentString(string Name) : CommandArgument(Name) {
        public override CommandArgumentType Type { get => CommandArgumentType.String; }
        public required string Value { get; set; }
        public override string ToString() => Value;
    }
    public class CommandArgumentInt(string Name) : CommandArgument(Name) {
        public override CommandArgumentType Type { get => CommandArgumentType.Int; }
        public required int Value { get; set; }
        public override string ToString() => Value.ToString();
    }
    public class CommandArgumentLong(string Name) : CommandArgument(Name) {
        public override CommandArgumentType Type { get => CommandArgumentType.Long; }
        public required long Value { get; set; }
        public override string ToString() => Value.ToString();
    }
    public class CommandArgumentDouble(string Name) : CommandArgument(Name) {
        public override CommandArgumentType Type { get => CommandArgumentType.Double; }
        public required double Value { get; set; }
        public override string ToString() => Value.ToString();
    }
    public class CommandArgumentAccount(string Name) : CommandArgument(Name) {
        public override CommandArgumentType Type { get => CommandArgumentType.Account; }
        public required Account Value { get; set; }
        public override string ToString() => Value.Id.ToString();
    }
    public class CommandArgumentUser(string Name) : CommandArgument(Name) {
        public override CommandArgumentType Type { get => CommandArgumentType.User; }
        public required User Value { get; set; }
        public override string ToString() => Value.Id.ToString();
    }
    public class CommandArgumentFaction(string Name) : CommandArgument(Name) {
        public override CommandArgumentType Type { get => CommandArgumentType.Faction; }
        public required Faction Value { get; set; }
        public override string ToString() => Value.Id.ToString();
    }
    public class CommandArgumentNation(string Name) : CommandArgument(Name) {
        public override CommandArgumentType Type { get => CommandArgumentType.Nation; }
        public required Nation Value { get; set; }
        public override string ToString() => Value.Id.ToString();
    }

    internal enum ParserTokenType {
        None,
        String,
        Number,
        Identifier
    }
    internal record Token(ParserTokenType Type) {
        public ParserTokenType Type { get; set; } = Type;
        public string Value { get; set; } = "";
    }

    internal class Parser {

        public List<Token> Tokens = new();
        
        public void Tokenize(string str) {
            str = str.Replace(Environment.NewLine, " ");

            var currentToken = new Token(ParserTokenType.None);

            void PushToken() {
                if (currentToken.Type == ParserTokenType.None) return;
                Tokens.Add(currentToken);
                currentToken = new(ParserTokenType.None);
            }

            for (int i = 0; i < str.Length; i++) {
                var character = str[i];

                char? nextCharacter = i < str.Length - 1 ? str[i+1] : null;

                switch (currentToken.Type) {

                    case ParserTokenType.None:
                        if (char.IsWhiteSpace(character)) continue;

                        if ("0123456789".Contains(character) || (nextCharacter != null && character == '-' && "0123456789".Contains((char)nextCharacter))) {
                            currentToken = new(ParserTokenType.Number);
                        } else {
                            currentToken = new(ParserTokenType.String);
                        }

                        currentToken.Value += character;

                        break;

                    case ParserTokenType.String:
                        var isExplicit = "\"\'".Contains(currentToken.Value[0]);

                        if ((isExplicit && character == currentToken.Value[0]) || (!isExplicit && char.IsWhiteSpace(character))) {
                            if (isExplicit) currentToken.Value = currentToken.Value[1..^0];
                            PushToken();
                        } else {
                            currentToken.Value += character;
                        }
                        break;

                    case ParserTokenType.Number:
                        if ("0123456789".Contains(character) || (character == '.' && !currentToken.Value.Contains('.'))) {
                            currentToken.Value += character;
                        } else if (char.IsWhiteSpace(character)) {
                            PushToken();
                        } else {
                            if ((nextCharacter == null || char.IsWhiteSpace((char)nextCharacter)) && !currentToken.Value.Contains('.') && !currentToken.Value.Contains('-')) {
                                currentToken.Type = ParserTokenType.Identifier;
                                currentToken.Value += character;
                                PushToken();
                            } else {
                                currentToken.Type = ParserTokenType.String;
                                currentToken.Value += character;
                            }
                        }
                        break;

                }
            }
            PushToken();
        }

        public void FillAgainst(CommandArgumentCollection collection, CommandArgumentBuilder args) {
            var i = 0;

            foreach (var definition in args) {

                var fulfilled = false;

                if (i < Tokens.Count) {
                    var token = Tokens[i++];

                    foreach (var accepted in definition.Accepted) {
                        var isExclusive = definition.Accepted.Length == 1;
                        var isPriority = accepted == definition.Accepted.First();

                        ArgumentException TokenException(string message) {
                            var e = new ArgumentException(message, definition.Name);
                            e.Data["TokenIndex"] = i - 1;
                            return e;
                        }

                        switch (accepted) {

                            case CommandArgumentType.String:
                                if (token.Type == ParserTokenType.String) {
                                    var argument = new CommandArgumentString(definition.Name) {
                                        Value = token.Value
                                    };
                                    while (true) {
                                        if (i >= Tokens.Count) break;
                                        var next = Tokens[i++];
                                        if (next.Type == ParserTokenType.String) { // this should probably continue regardless of token type if its the last argument definition
                                            argument.Value += " " + next.Value;
                                        } else {
                                            i--;
                                            break;
                                        }
                                    }
                                    collection.Add(argument);
                                    fulfilled = true;
                                } else {
                                    collection.Add(new CommandArgumentString(definition.Name) {
                                        Value = token.Value
                                    });
                                    fulfilled = true;
                                }
                                break;

                            case CommandArgumentType.Int:
                                if (token.Type == ParserTokenType.Number) {
                                    collection.Add(new CommandArgumentInt(definition.Name) {
                                        Value = int.Parse(token.Value)
                                    });
                                    fulfilled = true;
                                }
                                break;

                            case CommandArgumentType.Double:
                                if (token.Type == ParserTokenType.Number) {
                                    collection.Add(new CommandArgumentDouble(definition.Name) {
                                        Value = double.Parse(token.Value)
                                    });
                                    fulfilled = true;
                                }
                                break;

                            case CommandArgumentType.Long:
                                if (token.Type == ParserTokenType.Number) {
                                    collection.Add(new CommandArgumentLong(definition.Name) {
                                        Value = long.Parse(token.Value)
                                    });
                                    fulfilled = true;
                                } else if (token.Type == ParserTokenType.String && token.Value.StartsWith("<@") && token.Value.EndsWith('>')) {
                                    collection.Add(new CommandArgumentLong(definition.Name) {
                                        Value = long.Parse(new Regex("[^0-9]").Replace(token.Value, ""))
                                    });
                                    fulfilled = true;
                                }
                                break;

                            case CommandArgumentType.Account:
                                switch (token.Type) {
                                    case ParserTokenType.Number:
                                        var account = Account.Get(int.Parse(token.Value));
                                        if (account != null) {
                                            collection.Add(new CommandArgumentAccount(definition.Name) {
                                                Value = account
                                            });
                                            fulfilled = true;
                                        } else if (isExclusive || isPriority) throw TokenException($"Account {token.Value} not found");
                                        break;
                                    /*case ParserTokenType.String:
                                        collection.Add(new CommandArgumentString(definition.Name) {
                                            Value = token.Value
                                        });
                                        fulfilled = true;
                                        break;*/
                                    case ParserTokenType.Identifier:
                                        switch (token.Value[^1]) {
                                            case 'a': // account --> account
                                                var acc = Account.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"Account {token.Value[..^1]} not found");
                                                collection.Add(new CommandArgumentAccount(definition.Name) {
                                                    Value = acc
                                                });
                                                fulfilled = true;
                                                break;
                                            case 'u': // user --> primary account
                                                if (!definition.Accepted.Contains(CommandArgumentType.User)) {
                                                    var user = User.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"User {token.Value[..^1]} not found");
                                                    collection.Add(new CommandArgumentAccount(definition.Name) {
                                                        Value = user.PrimaryAccount ?? throw TokenException($"User '{user.DisplayName}' has no primary account")
                                                    });
                                                    fulfilled = true;
                                                }
                                                break;
                                            case 'f': // faction --> primary account
                                                if (!definition.Accepted.Contains(CommandArgumentType.Faction)) {
                                                    var faction = Faction.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"Faction {token.Value[..^1]} not found");
                                                    collection.Add(new CommandArgumentAccount(definition.Name) {
                                                        Value = faction.PrimaryAccount ?? throw TokenException($"Faction '{faction.Name}' has no primary account")
                                                    });
                                                    fulfilled = true;
                                                }
                                                break;
                                            case 'n': // nation --> primary account
                                                if (!definition.Accepted.Contains(CommandArgumentType.Nation)) {
                                                    var nation = Nation.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"Nation {token.Value[..^1]} not found");
                                                    collection.Add(new CommandArgumentAccount(definition.Name) {
                                                        Value = nation.PrimaryAccount ?? throw TokenException($"Nation '{nation.Name}' has no primary account")
                                                    });
                                                    fulfilled = true;
                                                }
                                                break;
                                            default:
                                                throw TokenException($"Identifier {token.Value[^1]} not accepted (expected a | u | f | n)");
                                        }
                                        break;
                                }
                                break;

                            case CommandArgumentType.User:
                                switch (token.Type) {
                                    case ParserTokenType.Number:
                                        var user = token.Value switch {
                                            var x when x.Length == 17 && x.StartsWith("765") => User.FromSteamId(long.Parse(x)),
                                            var x when x.Length >= 17 => User.FromDiscordId(long.Parse(x)),
                                            _ => User.Get(int.Parse(token.Value))
                                        };
                                        if (user != null) {
                                            collection.Add(new CommandArgumentUser(definition.Name) {
                                                Value = user
                                            });
                                            fulfilled = true;
                                        } else if (isExclusive || isPriority) throw TokenException($"User {token.Value} not found");
                                        break;
                                    case ParserTokenType.String:
                                        if (token.Value.StartsWith("<@") && token.Value.EndsWith('>')) {
                                            var userFromDiscordId = User.FromDiscordId(long.Parse(token.Value[2..^1]));
                                            if (userFromDiscordId != null) {
                                                collection.Add(new CommandArgumentUser(definition.Name) {
                                                    Value = userFromDiscordId
                                                });
                                                fulfilled = true;
                                            }
                                        }
                                        break;
                                    case ParserTokenType.Identifier:
                                        switch (token.Value[^1]) {
                                            case 'a': // personal account --> owner
                                                if (!definition.Accepted.Contains(CommandArgumentType.Account)) {
                                                    var account = Account.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"Account {token.Value[..^1]} not found");
                                                    if (account.Type != AccountType.Personal) {
                                                        if (isExclusive) throw TokenException($"Account '{account.Name}' is not a personal account");
                                                        else break;
                                                    }
                                                    collection.Add(new CommandArgumentUser(definition.Name) {
                                                        Value = ((PersonalAccount)account).Owner
                                                    });
                                                    fulfilled = true;
                                                }
                                                break;
                                            case 'u': // user --> user
                                                var usr = User.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"User {token.Value[..^1]} not found");
                                                collection.Add(new CommandArgumentUser(definition.Name) {
                                                    Value = usr
                                                });
                                                fulfilled = true;
                                                break;
                                            default:
                                                if (definition.Accepted.Contains(CommandArgumentType.Faction)) break; // no case for this, skip if accepted
                                                if (definition.Accepted.Contains(CommandArgumentType.Nation)) break; // no case for this, skip if accepted
                                                throw TokenException($"Identifier {token.Value[^1]} not accepted (expected a | u)");
                                        }
                                        break;
                                }
                                if (!fulfilled && isExclusive) throw TokenException($"User {token.Value} not found (maybe not verified?)");
                                break;

                            case CommandArgumentType.Faction:
                                switch (token.Type) {
                                    case ParserTokenType.Number:
                                        var faction = Faction.Get(int.Parse(token.Value));
                                        if (faction != null) {
                                            collection.Add(new CommandArgumentFaction(definition.Name) {
                                                Value = faction
                                            });
                                            fulfilled = true;
                                        } else if (isExclusive || isPriority) throw TokenException($"Faction {token.Value} not found");
                                        break;
                                    /*case ParserTokenType.String:
                                        collection.Add(new CommandArgumentString(definition.Name) {
                                            Value = token.Value
                                        });
                                        fulfilled = true;
                                        break;*/
                                    case ParserTokenType.Identifier:
                                        switch (token.Value[^1]) {
                                            case 'a': // faction account --> owner
                                                if (!definition.Accepted.Contains(CommandArgumentType.Account)) {
                                                    var account = Account.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"Account {token.Value[..^1]} not found");
                                                    if (account.Type != AccountType.Faction) {
                                                        if (isExclusive) throw TokenException($"Account '{account.Name}' is not a faction account");
                                                        else break;
                                                    }
                                                    collection.Add(new CommandArgumentFaction(definition.Name) {
                                                        Value = ((FactionAccount)account).Owner
                                                    });
                                                    fulfilled = true;
                                                }
                                                break;
                                            /*case 'u': // user --> SELECT faction
                                                if (!definition.Accepted.Contains(CommandArgumentType.User)) {
                                                    var user = User.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"User {token.Value[..^1]} not found");
                                                    collection.Add(new CommandArgumentUser(definition.Name) {
                                                        Value = user
                                                    });
                                                    fulfilled = true;
                                                }
                                                break;*/
                                            case 'f': // faction --> faction
                                                var fac = Faction.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"Faction {token.Value[..^1]} not found");
                                                collection.Add(new CommandArgumentFaction(definition.Name) {
                                                    Value = fac
                                                });
                                                fulfilled = true;
                                                break;
                                            default:
                                                if (definition.Accepted.Contains(CommandArgumentType.User)) break; // no case for this, skip if accepted
                                                if (definition.Accepted.Contains(CommandArgumentType.Nation)) break; // no case for this, skip if accepted
                                                throw TokenException($"Identifier {token.Value[^1]} not accepted (expected a | f)");
                                        }
                                        break;
                                }
                                break;

                            case CommandArgumentType.Nation:
                                switch (token.Type) {
                                    case ParserTokenType.Number:
                                        var nation = Nation.Get(int.Parse(token.Value));
                                        if (nation != null) {
                                            collection.Add(new CommandArgumentNation(definition.Name) {
                                                Value = nation
                                            });
                                            fulfilled = true;
                                        } else if (isExclusive || isPriority) throw TokenException($"Nation {token.Value} not found");
                                        break;
                                    /*case ParserTokenType.String:
                                        collection.Add(new CommandArgumentString(definition.Name) {
                                            Value = token.Value
                                        });
                                        fulfilled = true;
                                        break;*/
                                    case ParserTokenType.Identifier:
                                        switch (token.Value[^1]) {
                                            case 'a': // nation account --> owner
                                                if (!definition.Accepted.Contains(CommandArgumentType.Account)) {
                                                    var account = Account.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"Account {token.Value[..^1]} not found");
                                                    if (account.Type != AccountType.Nation) {
                                                        if (isExclusive) throw TokenException($"Account '{account.Name}' is not a nation account");
                                                        else break;
                                                    }
                                                    collection.Add(new CommandArgumentNation(definition.Name) {
                                                        Value = ((NationAccount)account).Owner
                                                    });
                                                    fulfilled = true;
                                                }
                                                break;
                                            /*case 'u': // user --> SELECT nation (if multi-citizenship)
                                                if (!definition.Accepted.Contains(CommandArgumentType.User)) {
                                                    var user = User.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"User {token.Value[..^1]} not found");
                                                    collection.Add(new CommandArgumentUser(definition.Name) {
                                                        Value = user
                                                    });
                                                    fulfilled = true;
                                                }
                                                break;*/
                                            /*case 'u': // user --> nation
                                                if (!definition.Accepted.Contains(CommandArgumentType.User)) {
                                                    var user = User.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"User {token.Value[..^1]} not found");
                                                    collection.Add(new CommandArgumentNation(definition.Name) {
                                                        Value = user.Nation ?? throw TokenException($"User '{user.DisplayName}' has no nation")
                                                    });
                                                    fulfilled = true;
                                                }
                                                break;*/
                                            case 'f': // faction --> nation
                                                if (!definition.Accepted.Contains(CommandArgumentType.Faction)) {
                                                    var faction = Faction.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"Faction {token.Value[..^1]} not found");
                                                    collection.Add(new CommandArgumentNation(definition.Name) {
                                                        Value = faction.Nation ?? throw TokenException($"Faction '{faction.Name}' has no nation")
                                                    });
                                                    fulfilled = true;
                                                }
                                                break;
                                            case 'n': // nation --> nation
                                                var nat = Nation.Get(int.Parse(token.Value[..^1])) ?? throw TokenException($"Nation {token.Value[..^1]} not found");
                                                collection.Add(new CommandArgumentNation(definition.Name) {
                                                    Value = nat
                                                });
                                                fulfilled = true;
                                                break;
                                            default:
                                                if (definition.Accepted.Contains(CommandArgumentType.User)) break; // no case for this, skip if accepted
                                                throw TokenException($"Identifier {token.Value[^1]} not accepted (expected a | f | n)");
                                        }
                                        break;
                                }
                                break;

                        }
                        if (fulfilled) break;
                    }
                }

                if (!fulfilled) {
                    if (i - 1 == args.FindIndex(d => d.Name == definition.Name)) {
                        var e = new ArgumentException($"Invalid type: expected {string.Join(" | ", definition.Accepted.Select(t => t.ToString()))}", definition.Name);
                        e.Data["TokenIndex"] = i - 1;
                        throw e;
                    } else if (definition.Required) throw new ArgumentNullException(null, $"Missing argument: {definition.Name} (expects {string.Join(" | ", definition.Accepted.Select(t => t.ToString()))})");
                }

            }
        }

    }
}
