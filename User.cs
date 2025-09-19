using Ash3.Economy;
using Ash3.Groups;
using Discord;
using Discord.WebSocket;
using System.ComponentModel;

namespace Ash3 {
    public class User(int id) {
        public static Database Database;

        internal static void Init() => Database = new("Users", typeof(User));

        public readonly static Event<User> OnCreate = new();
        public readonly static Event<User> OnDelete = new();

        [DbColumn]
        public int Id { get; } = id;

        [DbColumn("INTEGER")]
        public DateTime CreationTimestamp { get => DateTime.FromBinary(Database.GetLong(Id) ?? default); }

        [DbColumn("INTEGER")]
        public long? DiscordId {
            get => Database.GetLong(Id);
            set => Database.Set(Id, value);
        }

        [DbColumn("TEXT")]
        public string DisplayName {
            get => Database.GetString(Id) ?? $"{Id}u";
            set => Database.Set(Id, value);
        }

        [DbColumn("INTEGER")]
        public Color Color {
            get => Color.FromInt(Database.GetInt(Id) ?? default);
            set => Database.Set(Id, value.ToInt());
        }

        [DbColumn("TEXT")]
        public string? CachedDiscordAvatar {
            get => Database.GetString(Id);
            set => Database.Set(Id, value);
        }
        public void UpdateFromDiscord(SocketGuildUser user) {
            DisplayName = user.DisplayName;
            CachedDiscordAvatar = user.GetDisplayAvatarUrl(ImageFormat.Png, 256);
            Color = Color.FromInt((int)user.Roles.OrderByDescending(role => role.Position).First().Color.RawValue);
        }

        [DbColumn("INTEGER")]
        public long? SteamId {
            get => Database.GetLong(Id);
            set => Database.Set(Id, value);
        }

        [DbColumn("INTEGER")]
        public DateTime VerifiedTimestamp {
            get => DateTime.FromBinary(Database.GetLong(Id) ?? default);
            set => Database.Set(Id, value.ToBinary());
        }

        public List<PersonalAccount> Accounts { get => PersonalAccount.GetAll().Where(account => Equals(account.Owner)).ToList(); }

        public List<Account> GetAccounts() => Account.GetAll().Where(account => account.UserHasPermission(this, AccountPermission.Use)).ToList();

        public List<Faction> GetFactions() => Faction.GetAll().Where(faction => faction.GetMember(this) != null).ToList();

        public List<Nation> GetNations() => Nation.GetAll().Where(nation => nation.GetMember(this) != null).ToList();

        [DbColumn("INTEGER")]
        public PersonalAccount? PrimaryAccount {
            get {
                var id = Database.GetInt(Id);
                return id != null && new PersonalAccount(id.Value).Exists() ? new(id.Value) : null;
            }
            set => Database.Set(Id, value?.Id);
        }

        public bool Exists() => Database.Exists(Id);

        public void Delete() { // DON'T USE THIS !!!
            throw new NotImplementedException();
            // other cleanup logic here
            Database.Delete(Id);
        }

        public static User Create() {
            using var reader = Database.ExecuteReader($"INSERT INTO {Database.TableName} (CreationTimestamp) VALUES ({DateTime.UtcNow.ToBinary()}); SELECT last_insert_rowid()");
            reader.Read();
            var user = new User(reader.GetInt32(0));
            OnCreate.Fire(user);
            return user;
        }

        public static User? Get(int id) {
            if (!Database.Exists(id)) return null;
            return new(id);
        }

        public static List<User> GetAll() {
            using var reader = Database.ExecuteReader($"SELECT id FROM {Database.TableName}");
            var list = new List<User>();
            while (reader.Read()) list.Add(new(reader.GetInt32(0)));
            return list;
        }

        public static User? FromDiscordId(long id) {
            var userId = Database.SelectMatching("DiscordId", id.ToString());
            return userId.Count > 0 ? new(userId.First()) : null;
        }
        public static User? FromSteamId(long id) {
            var userId = Database.SelectMatching("SteamId", id.ToString());
            return userId.Count > 0 ? new(userId.First()) : null;
        }

        public override bool Equals(object? obj) => obj is User user && Id == user.Id;
    }
}