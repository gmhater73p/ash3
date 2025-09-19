namespace Ash3.Economy {
    public class PersonalAccount(int id) : Account(id) {
        public override AccountType Type { get => AccountType.Personal; }

        [DbColumn("INTEGER")]
        public User Owner {
            get => new(Database.GetInt(Id) ?? default);
            set => Database.Set(Id, value.Id);
        }

        [DbColumn("TEXT")]
        public List<User> Users { get => (Database.GetJson<List<int>>(Id) ?? []).Select(userId => new User(userId)).ToList(); }
        public void AddUser(User user) {
            var users = Users;
            users.Add(user);
            Database.SetJson(Id, users.DistinctBy(u => u.Id).Select(user => user.Id).ToList(), "Users");
        }
        public void RemoveUser(User user) {
            var users = Users;
            users.Remove(user);
            Database.SetJson(Id, users.Select(user => user.Id).ToList(), "Users");
        }

        public override List<User> GetUsersWithPermission(params AccountPermission[] permissions) {
            var users = new List<User>();
            foreach (var permission in permissions) {
                switch (permission) {
                    case AccountPermission.Use:
                        users.Add(Owner);
                        users.AddRange(Users);
                        break;
                    case AccountPermission.Rename:
                        users.Add(Owner);
                        break;
                    case AccountPermission.Delete:
                        users.Add(Owner);
                        break;
                }
            }
            return users.DistinctBy(u => u.Id).ToList();
        }
        public override bool UserHasPermission(User user, AccountPermission permission) {
            return permission switch {
                AccountPermission.Use => Owner.Equals(user) || Users.Contains(user),
                AccountPermission.Rename => Owner.Equals(user),
                AccountPermission.Delete => Owner.Equals(user),
                _ => false,
            };
        }

        public static new List<PersonalAccount> GetAll() => Database.SelectMatching("Type", (int)AccountType.Personal).Select(id => new PersonalAccount(id)).ToList();

        public static PersonalAccount Create(User owner) {
            using var reader = Database.ExecuteReader($"INSERT INTO {Database.TableName} (Type, CreationTimestamp, Owner) VALUES ({(int)AccountType.Personal}, {DateTime.UtcNow.ToBinary()}, {owner.Id}); SELECT last_insert_rowid()");
            reader.Read();
            var account = new PersonalAccount(reader.GetInt32(0));
            OnCreate.Fire(account);
            return account;
        }
        public static PersonalAccount Create(User owner, int id, bool force) {
            try {
                Database.ExecuteNonQuery($"INSERT{(force ? " OR REPLACE" : "")} INTO {Database.TableName} (Type, CreationTimestamp, Owner) VALUES ({(int)AccountType.Personal}, {DateTime.UtcNow.ToBinary()}, {owner.Id})");
                var account = new PersonalAccount(id);
                OnCreate.Fire(account);
                return account;
            } catch {
                throw new InvalidOperationException($"Account {id} already exists");
            }
        }
    }
}
