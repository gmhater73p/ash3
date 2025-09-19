namespace Ash3.Groups {
    public abstract class GroupMember(int id) {
        public abstract Database _database { get; }

        [DbColumn]
        public int Id { get; } = id;

        [DbColumn("INTEGER")]
        public DateTime CreationTimestamp { get => DateTime.FromBinary(_database.GetLong(Id) ?? default); }

        [DbColumn("INTEGER")]
        public abstract Group Group { get; }

        //[DBColumn("INTEGER")]
        //public abstract GroupRank Rank { get; set; }

        //public abstract Enum Permissions { get; }

        [DbColumn("INTEGER")]
        public User User {
            get => new(_database.GetInt(Id) ?? default);
            set => _database.Set(Id, value.Id);
        }

        public bool Exists() => _database.Exists(Id);

        public abstract void Delete();

        public override bool Equals(object? obj) => obj is GroupMember member && Id == member.Id && Group == member.Group;
    }

    public class FactionMember(int id) : GroupMember(id) {
        public static Database Database;
        public override Database _database { get => Database; }

        internal static void Init() => Database = new("FactionMembers", typeof(GroupMember), typeof(FactionMember));

        public override Faction Group { get => new(_database.GetInt(Id) ?? default); }

        [DbColumn("INTEGER")]
        public FactionRank Rank {
            get => new(_database.GetInt(Id) ?? default);
            set => _database.Set(Id, value.Id);
        }

        public FactionPermission Permissions { get => Rank.Permissions; }

        public override void Delete() {
            if (Equals(Group.Owner)) throw new InvalidOperationException("Cannot delete Owner of Group");
            _database.Delete(Id);
        }

        public static FactionMember Create(User user, Faction group) {
            using var reader = Database.ExecuteReader($"INSERT INTO {Database.TableName} (\"Group\", User, CreationTimestamp, Rank) VALUES ({group.Id}, {user.Id}, {DateTime.UtcNow.ToBinary()}, {group.Ranks.OrderBy(rank => rank.Order).First().Id}); SELECT last_insert_rowid()");
            reader.Read();
            return new FactionMember(reader.GetInt32(0));
        }
    }

    public class NationMember(int id) : GroupMember(id) {
        public static Database Database;
        public override Database _database { get => Database; }

        internal static void Init() => Database = new("NationMembers", typeof(GroupMember), typeof(NationMember));

        public override Nation Group { get => new(_database.GetInt(Id) ?? default); }

        [DbColumn("INTEGER")]
        public NationRank Rank {
            get => new(_database.GetInt(Id) ?? default);
            set => _database.Set(Id, value.Id);
        }

        public NationPermission Permissions { get => Rank.Permissions; }

        public override void Delete() {
            if (Equals(Group.Owner)) throw new InvalidOperationException("Cannot delete Owner of Group");
            _database.Delete(Id);
        }

        public static NationMember Create(User user, Nation group) {
            using var reader = Database.ExecuteReader($"INSERT INTO {Database.TableName} (\"Group\", User, CreationTimestamp, Rank) VALUES ({group.Id}, {user.Id}, {DateTime.UtcNow.ToBinary()}, {group.Ranks.OrderBy(rank => rank.Order).First().Id}); SELECT last_insert_rowid()");
            reader.Read();
            return new NationMember(reader.GetInt32(0));
        }
    }
}
