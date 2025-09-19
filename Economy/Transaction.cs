using Ash3.AshDiscord;
using Ash3.Groups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ash3.Economy {
    public class Transaction(int id) {
        public static Database Database;

        internal static void Init() => Database = new("Transactions", typeof(Transaction));

        public readonly static Event<Transaction> OnTransact = new();

        [DbColumn]
        public int Id { get; } = id;

        [DbColumn("INTEGER")]
        public DateTime CreationTimestamp { get => DateTime.FromBinary(Database.GetLong(Id) ?? default); }

        [DbColumn("INTEGER")]
        public Account? SourceAccount {
            get => Account.Get(Database.GetInt(Id) ?? default)!;
            set {
                Database.Set(Id, value?.Id);
                Database.Set(Id, value?.Name, "CachedSourceAccountName");
            }
        }

        [DbColumn("INTEGER")]
        public Account? TargetAccount {
            get => Account.Get(Database.GetInt(Id) ?? default)!;
            set {
                Database.Set(Id, value?.Id);
                Database.Set(Id, value?.Name, "CachedTargetAccountName");
            }
        }
        // cache in case of source account deletion
        [DbColumn("TEXT")]
        public string CachedSourceAccountName { get => Database.GetString(Id) ?? ""; }
        public int CachedSourceAccountId { get => Database.GetInt(Id, "SourceAccount") ?? default; }

        // cache in case of target account deletion
        [DbColumn("TEXT")]
        public string CachedTargetAccountName { get => Database.GetString(Id) ?? ""; }
        public int CachedTargetAccountId { get => Database.GetInt(Id, "TargetAccount") ?? default; }

        [DbColumn("INTEGER")]
        public int Amount {
            get => Database.GetInt(Id) ?? default;
            set => Database.Set(Id, value);
        }

        [DbColumn("TEXT")]
        public string? Note {
            get => Database.GetString(Id);
            set => Database.Set(Id, value);
        }

        [DbColumn("TEXT")]
        public string? CustomNote {
            get => Database.GetString(Id);
            set => Database.Set(Id, value);
        }

        [DbColumn("TEXT")]
        public List<User> InvolvedUsers { get => (Database.GetJson<List<int>>(Id) ?? []).Select(userId => new User(userId)).ToList(); }
        public void AddUser(User user) {
            var users = InvolvedUsers;
            users.Add(user);
            Database.SetJson(Id, users.DistinctBy(u => u.Id).Select(user => user.Id).ToList(), "Users");
        }

        public bool Exists() => Database.Exists(Id);

        public void Delete() { // DON'T USE THIS !!!
            throw new NotImplementedException();
            // other cleanup logic here
            Database.Delete(Id);
        }

        public static Transaction Perform(Account source, Account target, int amount, string? note = null) {
            if (source.Equals(target)) throw new InvalidOperationException("Source account and target account are the same: cannot transact");
            source.Balance -= amount;
            target.Balance += amount;

            using var reader = Database.ExecuteReader($"INSERT INTO {Database.TableName} (CreationTimestamp) VALUES ({DateTime.UtcNow.ToBinary()}); SELECT last_insert_rowid()");
            reader.Read();

            var self = new Transaction(reader.GetInt32(0));

            self.SourceAccount = source;
            self.TargetAccount = target;
            self.Amount = amount;
            self.Note = note;

            OnTransact.Fire(self);

            return self;
        }

        public string FormatFor(Account account) {
            if (account.Id.Equals(CachedSourceAccountId)) {
                var user = InvolvedUsers.First();
                return user != null
                    ? $"📤 {(user.DiscordId != null ? $"<@{user.DiscordId}>" : user.DisplayName)} transferred {Amount:N0} {Bot.Configuration.GetString("CurrencySymbol")} to account #{CachedTargetAccountId}"
                    : $"📤 Transferred {Amount:N0} {Bot.Configuration.GetString("CurrencySymbol")} to account #{CachedTargetAccountId}";
            } else return $"📥 Received {Amount:N0} {Bot.Configuration.GetString("CurrencySymbol")} from account #{CachedSourceAccountId}";
        }

        public override bool Equals(object? obj) => obj is Transaction transaction && Id == transaction.Id;
    }
}
