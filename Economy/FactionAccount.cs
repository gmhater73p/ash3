using Ash3.Groups;

namespace Ash3.Economy {
    public class FactionAccount(int id) : Account(id) {
        public override AccountType Type { get => AccountType.Faction; }

        [DbColumn("INTEGER")]
        public Faction Owner {
            get => new(Database.GetInt(Id) ?? default);
            set => Database.Set(Id, value.Id);
        }

        public override List<User> GetUsersWithPermission(params AccountPermission[] permissions) {
            var users = new List<User>();
            foreach (var permission in permissions) {
                switch (permission) {
                    case AccountPermission.Use:
                        users.AddRange(Owner.Members.Where(member => member.Permissions.HasFlag(FactionPermission.UseAccount)).Select(member => member.User));
                        break;
                    case AccountPermission.Rename:
                        users.AddRange(Owner.Members.Where(member => member.Permissions.HasFlag(FactionPermission.RenameAccount)).Select(member => member.User));
                        break;
                    case AccountPermission.Delete:
                        users.AddRange(Owner.Members.Where(member => member.Permissions.HasFlag(FactionPermission.DeleteAccount)).Select(member => member.User));
                        break;
                }
            }
            return users.DistinctBy(u => u.Id).ToList();
        }
        public override bool UserHasPermission(User user, AccountPermission permission) {
            var member = Owner.GetMember(user);
            if (member == null) return false;
            return permission switch {
                AccountPermission.Use => member.Permissions.HasFlag(FactionPermission.UseAccount),
                AccountPermission.Rename => member.Permissions.HasFlag(FactionPermission.RenameAccount),
                AccountPermission.Delete => member.Permissions.HasFlag(FactionPermission.DeleteAccount),
                _ => false,
            };
        }

        public static new List<FactionAccount> GetAll() => Database.SelectMatching("Type", (int)AccountType.Faction).Select(id => new FactionAccount(id)).ToList();

        public static FactionAccount Create(Faction owner) {
            using var reader = Database.ExecuteReader($"INSERT INTO {Database.TableName} (Type, CreationTimestamp, Owner) VALUES ({(int)AccountType.Faction}, {DateTime.UtcNow.ToBinary()}, {owner.Id}); SELECT last_insert_rowid()");
            reader.Read();
            var account = new FactionAccount(reader.GetInt32(0));
            OnCreate.Fire(account);
            return account;
        }
        public static FactionAccount Create(Faction owner, int id, bool force) {
            try {
                Database.ExecuteNonQuery($"INSERT{(force ? " OR REPLACE" : "")} INTO {Database.TableName} (Type, CreationTimestamp, Owner) VALUES ({(int)AccountType.Faction}, {DateTime.UtcNow.ToBinary()}, {owner.Id})");
                var account = new FactionAccount(id);
                OnCreate.Fire(account);
                return account;
            } catch {
                throw new InvalidOperationException($"Account {id} already exists");
            }
        }
    }
}
