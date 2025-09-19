using Ash3.Economy;

namespace Ash3.Groups {
    public abstract class GroupRank(int id) {
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

        [DbColumn("INTEGER")]
        public int Order {
            get => _database.GetInt(Id) ?? default;
            set => _database.Set(Id, value);
        }

        [DbColumn("INTEGER")]
        public abstract Group Group { get; }

        public abstract dynamic Members { get; }//public abstract List<GroupMember> Members { get; }

        //public abstract Enum Permissions { get; }

        public bool Exists() => _database.Exists(Id);

        public abstract void Delete();

        public override bool Equals(object? obj) => obj is GroupRank rank && Id == rank.Id && Group == rank.Group;
    }

    public class FactionRank(int id) : GroupRank(id) {
        public static Database Database;
        public override Database _database { get => Database; }

        internal static void Init() => Database = new("FactionRanks", typeof(GroupRank), typeof(FactionRank));

        public override Faction Group { get => new(_database.GetInt(Id) ?? default); }

        public override List<FactionMember> Members { get => Group.Members.Where(member => Equals(member.Rank)).ToList(); }

        [DbColumn("INTEGER")]
        public FactionPermission Permissions {
            get => (FactionPermission) (_database.GetInt(Id) ?? default);
            set => _database.Set(Id, (int) value);
        }

        public override void Delete() {
            if (Group.Ranks.Count <= 1) throw new InvalidOperationException("Cannot delete last Rank in Group");
            foreach (var member in Members) member.Rank = Group.Ranks.OrderBy(rank => rank.Order).First();
            _database.Delete(Id);
        }

        public static FactionRank Create(Group group) {
            using var reader = Database.ExecuteReader($"INSERT INTO {Database.TableName} (\"Group\", CreationTimestamp, \"Order\") VALUES ({group.Id}, {DateTime.UtcNow.ToBinary()}, 1); SELECT last_insert_rowid()");
            reader.Read();
            return new FactionRank(reader.GetInt32(0));
        }
    }

    public class NationRank(int id) : GroupRank(id) {
        public static Database Database;
        public override Database _database { get => Database; }

        internal static void Init() => Database = new("NationRanks", typeof(GroupRank), typeof(NationRank));

        public override Nation Group { get => new(_database.GetInt(Id) ?? default); }

        public override List<NationMember> Members { get => Group.Members.Where(member => Equals(member.Rank)).ToList(); }

        [DbColumn("INTEGER")]
        public NationPermission Permissions {
            get => (NationPermission)(_database.GetInt(Id) ?? default);
            set => _database.Set(Id, (int)value);
        }

        public override void Delete() {
            if (Group.Ranks.Count <= 1) throw new InvalidOperationException("Cannot delete last Rank in Group");
            foreach (var member in Members) member.Rank = Group.Ranks.OrderBy(rank => rank.Order).First();
            _database.Delete(Id);
        }

        public static NationRank Create(Group group) {
            using var reader = Database.ExecuteReader($"INSERT INTO {Database.TableName} (\"Group\", CreationTimestamp, \"Order\") VALUES ({group.Id}, {DateTime.UtcNow.ToBinary()}, 1); SELECT last_insert_rowid()");
            reader.Read();
            return new NationRank(reader.GetInt32(0));
        }
    }
}
