using Ash3.Economy;

namespace Ash3.Groups {
    [Flags]
    public enum NationPermission {
        None = 0,
        UseAccount = 1,
        DeleteAccount = 2,
        RenameAccount = 4,
        ManageMembers = 8,
        EditDetails = 16
    }

    public class Nation(int id) : Group(id) {
        public static Database Database;
        public override Database _database { get => Database; }

        public static Event<Nation> OnCreate = new();
        public static Event<Nation> OnDelete = new();

        internal static void Init() {
            Database = new("Nations", typeof(Group), typeof(Nation));
            NationRank.Init();
            NationMember.Init();
        }

        [DbColumn("INTEGER")]
        public NationMember Owner {
            get => new(_database.GetInt(Id) ?? default);
            set => _database.Set(Id, value.Id);
        }

        public override List<NationMember> Members { get => NationMember.Database.SelectMatching("Group", Id).Select(id => new NationMember(id)).ToList(); }

        public override List<NationRank> Ranks { get => NationRank.Database.SelectMatching("Group", Id).Select(id => new NationRank(id)).ToList(); }

        public override NationMember? GetMember(User user) => Members.Find(member => member.User.Equals(user));

        public override NationMember AddMember(User user) {
            if (GetMember(user) != null) throw new InvalidOperationException("Member already exists");
            return NationMember.Create(user, this);
        }

        public override NationRank AddRank() => NationRank.Create(this);

        public override List<NationAccount> Accounts { get => NationAccount.GetAll().Where(account => Equals(account.Owner)).ToList(); }

        //public List<Faction> Factions { get => Faction.GetAll().Find(faction => Equals(faction.Nation)); }
        public List<Faction> Factions { get => Faction.Database.SelectMatching("Nation", Id).Select(id => new Faction(id)).ToList(); }

        // configuration (primary account id)
        [DbColumn("INTEGER")]
        public NationAccount? PrimaryAccount {
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

        public static Nation Create(User user) {
            using var reader = Database.ExecuteReader($"INSERT INTO {Database.TableName} (CreationTimestamp) VALUES ({DateTime.UtcNow.ToBinary()}); SELECT last_insert_rowid()");
            reader.Read();
            var nation = new Nation(reader.GetInt32(0));

            var memberRank = nation.AddRank();
            memberRank.Name = "Member";
            memberRank.Order = 1;

            var vipRank = nation.AddRank();
            vipRank.Name = "VIP";
            vipRank.Order = 2;

            var managerRank = nation.AddRank();
            managerRank.Name = "Manager";
            managerRank.Order = 3;
            managerRank.Permissions = NationPermission.UseAccount;

            var administratorRank = nation.AddRank();
            administratorRank.Name = "Administrator";
            administratorRank.Order = 4;
            administratorRank.Permissions = (NationPermission)0xFFFFFF;

            var member = nation.AddMember(user);
            member.Rank = administratorRank;
            nation.Owner = member;

            OnCreate.Fire(nation);

            return nation;
        }

        public static Nation? Get(int id) {
            if (!Database.Exists(id)) return null;
            return new(id);
        }
        public static List<Nation> GetAll() {
            using var reader = Database.ExecuteReader($"SELECT id FROM {Database.TableName}");
            var list = new List<Nation>();
            while (reader.Read()) list.Add(new(reader.GetInt32(0)));
            return list;
        }
    }
}
