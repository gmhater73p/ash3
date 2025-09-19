using Ash3.Groups;

namespace Ash3.Economy {
    public enum AccountType {
        Personal,
        Nation,
        Faction
    }

    public enum AccountPermission {
        Use,
        Rename,
        Delete
    }

    public abstract class Account(int id) {
        public static Database Database;

        public static Event<Account> OnCreate = new();
        public static Event<Account> OnDelete = new();

        internal static void Init() {
            Database = new("Accounts", typeof(Account), typeof(PersonalAccount));
            Transaction.Init();
        }

        [DbColumn]
        public int Id { get; } = id;

        [DbColumn("INTEGER")]
        public DateTime CreationTimestamp { get => DateTime.FromBinary(Database.GetLong(Id) ?? default); }

        [DbColumn("INTEGER")]
        public abstract AccountType Type { get; }

        [DbColumn("TEXT")]
        public string Name {
            get => Database.GetString(Id) ?? "";
            set => Database.Set(Id, value);
        }

        [DbColumn("INTEGER")]
        public int Balance {
            get => Database.GetInt(Id) ?? default;
            set => Database.Set(Id, value);
        }

        public abstract List<User> GetUsersWithPermission(params AccountPermission[] permissions);
        public abstract bool UserHasPermission(User user, AccountPermission permission);

        public List<Transaction> Transactions { get => Transaction.Database.SelectMatching("SourceAccount", Id).Concat(Transaction.Database.SelectMatching("TargetAccount", Id)).Select(id => new Transaction(id)).ToList(); }

        public bool Exists() => Database.Exists(Id);

        public void Delete() {
            OnDelete.Fire(this);
            Database.Delete(Id);
        }

        public static Account? Get(int id) {
            var type = Database.GetInt(id, "Type");
            if (type == null) return null;
            return (AccountType) type switch {
                AccountType.Personal => new PersonalAccount(id),
                AccountType.Faction => new FactionAccount(id),
                AccountType.Nation => new NationAccount(id)
            };
        }
        public static List<Account> GetAll() {
            using var reader = Database.ExecuteReader($"SELECT id, type FROM {Database.TableName}");
            var list = new List<Account>();
            while (reader.Read()) {
                var id = reader.GetInt32(0);
                var type = (AccountType) reader.GetInt32(1);
                list.Add(type switch {
                    AccountType.Personal => new PersonalAccount(id),
                    AccountType.Faction => new FactionAccount(id),
                    AccountType.Nation => new NationAccount(id)
                });
            }
            return list;
        }

        public override bool Equals(object? obj) => obj is Account account && Id == account.Id;
    }
}