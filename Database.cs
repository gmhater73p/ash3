using Microsoft.Data.Sqlite;
using System.Reflection;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Ash3 {

    public class Database {
        
        public static SqliteConnection Connection;
        
        internal static void Init(string dataSource) {
            Connection = new SqliteConnection($"Data Source={dataSource}");
            Connection.Open();
            ExecuteNonQuery("PRAGMA journal_mode = WAL");
            ExecuteNonQuery("PRAGMA synchronous = NORMAL");
            ExecuteNonQuery("PRAGMA optimize=0x10002;"); // Recommended by https://www.sqlite.org/pragma.html#pragma_optimize
        }

        public static void ExecuteNonQuery(string query) { using (var command = new SqliteCommand(query, Connection)) command.ExecuteNonQuery(); }
        public static SqliteDataReader ExecuteReader(string query) => new SqliteCommand(query, Connection).ExecuteReader();

        public readonly string TableName;

        private class ColumnInfo {
            public string Name { get; }
            public string Type { get; }

            public SqliteCommand Set { get; }
            public SqliteCommand Get { get; }

            public ColumnInfo(string name, string type, string tableName) {
                Name = name;
                Type = type;
                Set = new($"UPDATE {tableName} SET \"{name}\" = $value WHERE id = $id", Connection);
                Set.Parameters.AddWithValue("$value", "");
                Set.Parameters.AddWithValue("$id", 0);
                Get = new($"SELECT \"{name}\" FROM {tableName} WHERE id = $id", Connection);
                Get.Parameters.AddWithValue("$id", 0);
            }

            public override string ToString() => $"\"{Name}\" {Type}";
        }
        private readonly Dictionary<string, ColumnInfo> _columns;

        public Database(string tableName, params Type[] types) {
            TableName = tableName;

            // create columns from types (reflection)
            _columns = types.SelectMany(type =>
                type.GetProperties()
                    .Where(prop => prop.IsDefined(typeof(DbColumnAttribute), false))
                    .Select(prop => new ColumnInfo(prop.Name, prop.GetCustomAttribute<DbColumnAttribute>()!.Type, TableName))
                ).DistinctBy(column => column.Name).ToDictionary(column => column.Name);

            // create table if not exists
            ExecuteNonQuery($"CREATE TABLE IF NOT EXISTS {TableName}({string.Join(", ", _columns.Values.Select(x => x.ToString()))})");

            // add columns if not in table
            var existingColumns = new List<string>();
            using var reader = ExecuteReader($"PRAGMA table_info({TableName})");
            while (reader.Read()) existingColumns.Add(reader.GetString(1));
            
            foreach (var column in _columns.Values.Where(col => !existingColumns.Contains(col.Name))) ExecuteNonQuery($"ALTER TABLE {TableName} ADD COLUMN {column.Name} {column.Type}");
        }
        public Database(params Type[] types) : this(types[0].Name, types) { }

        public void PurgeColumns() {
            using var reader = ExecuteReader($"PRAGMA table_info({TableName})");
            while (reader.Read()) {
                var columnName = reader.GetString(1);
                if (!_columns.Values.Select(attr => attr.Name).Contains(columnName)) ExecuteNonQuery($"ALTER TABLE {TableName} DROP COLUMN {columnName};");
            }
        }

        public void Set<T>(int id, T value, [CallerMemberName] string columnName = "") {
            var command = _columns[columnName].Set;
            command.Parameters[0].Value = value == null ? DBNull.Value : value;
            command.Parameters[1].Value = id;
            command.ExecuteNonQuery();
        }

        public string? GetString(int id, [CallerMemberName] string columnName = "") {
            var command = _columns[columnName].Get;
            command.Parameters[0].Value = id;
            using var reader = command.ExecuteReader();
            //if (!reader.HasRows) return null;
            reader.Read();
            return reader.IsDBNull(0) ? null : reader.GetString(0);
        }
        public int? GetInt(int id, [CallerMemberName] string columnName = "") {
            var command = _columns[columnName].Get;
            command.Parameters[0].Value = id;
            using var reader = command.ExecuteReader();
            if (!reader.HasRows) return null;
            reader.Read();
            return reader.IsDBNull(0) ? null : reader.GetInt32(0);
        }
        public long? GetLong(int id, [CallerMemberName] string columnName = "") {
            var command = _columns[columnName].Get;
            command.Parameters[0].Value = id;
            using var reader = command.ExecuteReader();
            //if (!reader.HasRows) return null;
            reader.Read();
            return reader.IsDBNull(0) ? null : reader.GetInt64(0);
        }
        public bool? GetBool(int id, [CallerMemberName] string columnName = "") {
            var command = _columns[columnName].Get;
            command.Parameters[0].Value = id;
            using var reader = command.ExecuteReader();
            //if (!reader.HasRows) return null;
            reader.Read();
            return reader.IsDBNull(0) ? null : reader.GetBoolean(0);
        }

        public void SetBlob(int id, Stream? value, [CallerMemberName] string columnName = "") {
            if (value == null) {
                var command = _columns[columnName].Set;
                command.Parameters[0].Value = DBNull.Value;
                command.Parameters[1].Value = id;
                command.ExecuteNonQuery();
            } else {
                var command = new SqliteCommand($"UPDATE {TableName} SET \"{columnName}\" = zeroblob($value) WHERE id = $id", Connection);
                command.Parameters.AddWithValue("$value", value.Length);
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();
                using var dbStream = new SqliteBlob(Connection, TableName, "DisplayImage", id);
                value.CopyTo(dbStream);
            }
        }
        public Stream? GetBlob(int id, [CallerMemberName] string columnName = "") {
            var command = _columns[columnName].Get;
            command.Parameters[0].Value = id;
            using var reader = command.ExecuteReader();
            reader.Read();
            return reader.IsDBNull(0) ? null : reader.GetStream(0);
        }

        public void SetJson<T>(int id, T? value, [CallerMemberName] string columnName = "") where T : class {
            var command = _columns[columnName].Set;
            command.Parameters[0].Value = value == null ? DBNull.Value : JsonSerializer.Serialize(value);
            command.Parameters[1].Value = id;
            command.ExecuteNonQuery();
        }
        public T? GetJson<T>(int id, [CallerMemberName] string columnName = "") where T : class {
            var command = _columns[columnName].Get;
            command.Parameters[0].Value = id;
            using var reader = command.ExecuteReader();
            //if (!reader.HasRows) return null;
            reader.Read();
            return reader.IsDBNull(0) ? null : JsonSerializer.Deserialize<T>(reader.GetString(0));
        }

        public int Insert() {
            using var reader = ExecuteReader($"INSERT INTO {TableName} DEFAULT VALUES; SELECT last_insert_rowid()");
            reader.Read();
            return reader.GetInt32(0);
        }

        public void Delete(int id) => ExecuteNonQuery($"DELETE FROM {TableName} WHERE id = {id}");

        public bool Exists(int id) {
            using var reader = ExecuteReader($"SELECT EXISTS(SELECT 1 FROM {TableName} WHERE id = {id})");
            reader.Read();
            return reader.GetBoolean(0);
        }

        public List<int> SelectMatching(string matchColumn, string match) {
            using var reader = ExecuteReader($"SELECT id FROM {TableName} WHERE \"{matchColumn}\" = {match}");
            var list = new List<int>();
            //if (!reader.HasRows) return null;
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }
        public List<int> SelectMatching(string matchColumn, int match) => SelectMatching(matchColumn, match.ToString());

        public List<int> SearchColumn(string query, string column) {
            using var command = new SqliteCommand($"SELECT id FROM {TableName} WHERE {column} LIKE $query ESCAPE '\\'", Connection);
            command.Parameters.AddWithValue("$query", '%' + query.Replace("%", "\\%") + '%');
            using var reader = command.ExecuteReader();

            var list = new List<int>();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal class DbColumnAttribute : Attribute {
        public string Type;

        public DbColumnAttribute(string type = "PRIMARY") {
            type = type.ToUpper();
            Type = type == "PRIMARY" ? "INTEGER PRIMARY KEY AUTOINCREMENT" : type;
        }
    }
}
