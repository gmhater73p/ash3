using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tommy;

namespace Ash3 {
    public class Configuration(string name) {
        private const string PATH = "config.toml";

        private static TomlTable _data = new();

        internal static void Init() {
            // create table if not exists
            //Database.ExecuteNonQuery($"CREATE TABLE IF NOT EXISTS Configuration(key TEXT PRIMARY KEY, value TEXT) WITHOUT ROWID");

            if (!File.Exists(PATH)) using (_ = File.Create(PATH)) { };
            using var reader = File.OpenText(PATH);
            
            _data = TOML.Parse(reader);
        }

        public static void Commit() {
            using var writer = File.CreateText(PATH);
            _data.WriteTo(writer);
            writer.Flush();
        }

        public readonly string Name = name;

        public void Declare(string key, string defaultValue) { if (!_data[Name].HasKey(key)) _data[Name][key] = defaultValue; }
        public void Declare(string key, int defaultValue) { if (!_data[Name].HasKey(key)) _data[Name][key] = defaultValue; }

        public string GetString(string key) => _data[Name][key];
        public int GetInt(string key) => _data[Name][key];

        public void Set(string key, string value) {
            _data[Name][key] = value;
            Commit();
        }
        public void Set(string key, int value) {
            _data[Name][key] = value;
            Commit();
        }
    }
}
