using Ash3.Economy;
using Ash3.AshDiscord;
using Discord;
using Discord.WebSocket;
using Ash3.SWLink;
using System.Windows.Forms;
using System.Linq;
using System.Security.Principal;

namespace Ash3.Groups {
    [Flags]
    public enum FactionPermission {
        None = 0,
        UseAccount = 1,
        DeleteAccount = 2,
        RenameAccount = 4,
        ManageMembers = 8,
        EditDetails = 16
    }

    public class Faction(int id) : Group(id) {
        public static Database Database;
        public override Database _database { get => Database; }

        public static Event<Faction> OnCreate = new();
        public static Event<Faction> OnDelete = new();

        internal static void Init() {
            Database = new ("Factions", typeof(Group), typeof(Faction));
            FactionRank.Init();
            FactionMember.Init();
        }

        [DbColumn("INTEGER")]
        public FactionMember Owner {
            get => new(_database.GetInt(Id) ?? default);
            set => _database.Set(Id, value.Id);
        }

        public override List<FactionMember> Members { get => FactionMember.Database.SelectMatching("Group", Id).Select(id => new FactionMember(id)).ToList(); }

        public override List<FactionRank> Ranks { get => FactionRank.Database.SelectMatching("Group", Id).Select(id => new FactionRank(id)).ToList(); }
        
        public override FactionMember? GetMember(User user) => Members.Find(member => member.User.Equals(user));

        public override FactionMember AddMember(User user) {
            if (GetMember(user) != null) throw new InvalidOperationException("Member already exists");
            return FactionMember.Create(user, this);
        }

        public override FactionRank AddRank() => FactionRank.Create(this);

        public Nation? Nation {
            get {
                var id = _database.GetInt(Id);
                return id != null && new Nation(id.Value).Exists() ? new(id.Value) : null;
            }
            set => _database.Set(Id, value?.Id);
        }

        public override List<FactionAccount> Accounts { get => FactionAccount.GetAll().Where(account => Equals(account.Owner)).ToList(); }
        
        // configuration (primary account id)
        [DbColumn("INTEGER")]
        public FactionAccount? PrimaryAccount {
            get {
                var id = _database.GetInt(Id);
                return id != null ? new(id.Value) : Accounts.FirstOrDefault();
            }
            set {
                if (value == null) { _database.Set<int?>(Id, null); return; }
                if (!value.Owner.Equals(this)) throw new InvalidOperationException("PrimaryAccount must be owned by this Group");
                _database.Set(Id, value.Id);
            }
        }

        public void Delete() {
            throw new NotImplementedException();
            // other cleanup logic here
            OnDelete.Fire(this);
            _database.Delete(Id);
        }

        public static Faction Create(User user) {
            using var reader = Database.ExecuteReader($"INSERT INTO {Database.TableName} (CreationTimestamp) VALUES ({DateTime.UtcNow.ToBinary()}); SELECT last_insert_rowid()");
            reader.Read();
            var faction = new Faction(reader.GetInt32(0));

            var memberRank = faction.AddRank();
            memberRank.Name = "Member";
            memberRank.Order = 1;

            var vipRank = faction.AddRank();
            vipRank.Name = "VIP";
            vipRank.Order = 2;

            var managerRank = faction.AddRank();
            managerRank.Name = "Manager";
            managerRank.Order = 3;
            managerRank.Permissions = FactionPermission.UseAccount;

            var administratorRank = faction.AddRank();
            administratorRank.Name = "Administrator";
            administratorRank.Order = 4;
            administratorRank.Permissions = (FactionPermission) 0xFFFFFF;

            var member = faction.AddMember(user);
            member.Rank = administratorRank;
            faction.Owner = member;

            OnCreate.Fire(faction);

            return faction;
        }

        public static Faction? Get(int id) {
            if (!Database.Exists(id)) return null;
            return new(id);
        }
        public static List<Faction> GetAll() {
            using var reader = Database.ExecuteReader($"SELECT id FROM {Database.TableName}");
            var list = new List<Faction>();
            while (reader.Read()) list.Add(new(reader.GetInt32(0)));
            return list;
        }

        // todo in the future could probably accept the user sending a message with faction id or name too
        public static readonly Activity<Task<Faction?>> SelectActivity = new() {
            Id = "SelectFaction1",
            Args = [
                new ("Message", CommandArgumentType.String) { Required = false }
            ],
            Execute = async delegate (ActivityContext context, CommandArgumentCollection args) {
                var component = new ComponentBuilder();

                var ticket = new ComponentYieldTicket((long)context.User.Id);

                var userFactions = User.FromDiscordId((long)context.User.Id)?.GetFactions();
                if (userFactions != null && userFactions.Count > 0) component.WithSelectMenu(new SelectMenuBuilder {
                    CustomId = ticket.CustomId + "m",
                    Placeholder = $"My factions ({userFactions.Count})",
                    Options = userFactions.OrderBy(f => f.Name).Select(faction => new SelectMenuOptionBuilder {
                        Value = faction.Id.ToString(),
                        Label = faction.Name,
                        Description = faction.ShortDescription,
                        Emote = new Emoji("⭐")
                    }).ToList()
                });

                foreach (var chunk in GetAll().Where(f => userFactions == null || !userFactions.Contains(f)).OrderBy(f => f.Name).Chunk(25)) {
                    component.WithSelectMenu(new SelectMenuBuilder {
                        CustomId = ticket.CustomId + chunk[0].Id, // needs to be unique so i'll just pick the id of the first faction in the chunk (guaranteed unique)
                        Placeholder = $"{chunk[0].Name[0..3]}... to {chunk[^1].Name[0..3]}... ({chunk.Length})",
                        Options = chunk.Select(faction => new SelectMenuOptionBuilder {
                            Value = faction.Id.ToString(),
                            Label = faction.Name,
                            Description = faction.ShortDescription
                        }).ToList()
                    });
                }

                await context.Reply(new Presentation {
                    Text = args.Get<string>("Message") ?? "Please select a faction.",
                    Components = component.Build()
                });

                var result = (SocketMessageComponent)await context.WaitingFor(ticket);

                return Get(int.Parse(result.Data.Values.First()));
            }
        };
    }
}