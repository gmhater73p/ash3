using Ash3.Economy;

namespace Ash3.Groups {
    public abstract class Group(int id) {
        public abstract Database _database { get; }

        [DbColumn]
        public int Id { get; } = id;

        [DbColumn("INTEGER")]
        public DateTime CreationTimestamp { get => DateTime.FromBinary(_database.GetLong(Id) ?? default); }

        [DbColumn("TEXT")]
        public string Name {
            get => _database.GetString(Id) ?? "";
            set => _database.Set(Id, value);
        }

        [DbColumn("TEXT")]
        public string Description {
            get => _database.GetString(Id) ?? "";
            set => _database.Set(Id, value);
        }

        [DbColumn("TEXT")]
        public string ShortDescription {
            get => _database.GetString(Id) ?? "";
            set => _database.Set(Id, value);
        }

        [DbColumn("INTEGER")]
        public Color Color {
            get => Color.FromInt(_database.GetInt(Id) ?? default);
            set => _database.Set(Id, value.ToInt());
        }

        [DbColumn("BLOB")]
        public Stream? DisplayImage {
            get => _database.GetBlob(Id);
            set => _database.SetBlob(Id, value);
        }

        [DbColumn("TEXT")]
        public List<User> InvitedUsers { get => (_database.GetJson<List<int>>(Id) ?? []).Select(userId => new User(userId)).ToList(); }
        public void InviteUser(User user) {
            if (GetMember(user) != null) throw new InvalidOperationException("Cannot invite User that is already Member of Group");
            var invitedUsers = InvitedUsers;
            invitedUsers.Add(user);
            _database.SetJson(Id, invitedUsers.DistinctBy(u => u.Id).Select(user => user.Id).ToList(), "InvitedUsers");
        }
        public void UninviteUser(User user) {
            var invitedUsers = InvitedUsers;
            invitedUsers.Remove(user);
            _database.SetJson(Id, invitedUsers.Select(user => user.Id).ToList(), "InvitedUsers");
        }

        //[DBColumn("INTEGER")]
        //public abstract GroupMember Owner { get; set; }

        // these fields have to be IEnumerable<> instead of List<> because Lists do not project covariance
        // note that in derived classes, the field can still return a List instead of an IEnumerable
        // https://learn.microsoft.com/en-us/dotnet/standard/generics/covariance-and-contravariance
        public abstract IEnumerable<Account> Accounts { get; }

        public abstract IEnumerable<GroupMember> Members { get; }

        public abstract IEnumerable<GroupRank> Ranks { get; }

        // not implemented because properties can only be covariant if they are read-only
        //public abstract Account? PrimaryAccount { get; set; }

        public abstract GroupMember? GetMember(User user);

        public abstract GroupMember AddMember(User user);

        public abstract GroupRank AddRank();

        public bool Exists() => _database.Exists(Id);

        public override bool Equals(object? obj) => obj is Group group && Id == group.Id && _database == group._database;
    }
}
