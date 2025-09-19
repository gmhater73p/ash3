using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ash3 {
    internal class Registry<T> {
        private readonly Dictionary<string, T> _objects = new();

        public readonly Event<T> NewEntry = new();

        public void Set(string id, T obj) {
            if (id.Any(c => !char.IsLetterOrDigit(c) && c != '_')) throw new ArgumentException("Id can only contain letters, numbers, and underscores");
            if (_objects.ContainsKey(id)) throw new InvalidOperationException("Object is already registered with id");
            _objects[id] = obj;
            NewEntry.Fire(obj);
        }

        public T? Get(string id) {
            if (id.Any(c => !char.IsLetterOrDigit(c) && c != '_')) throw new ArgumentException("Id can only contain letters, numbers, and underscores");
            return _objects.TryGetValue(id, out var obj) ? obj : default;
        }

        public string GetId(T obj) {
            return _objects.FirstOrDefault(x => x.Value!.Equals(obj)).Key;
        }
    }
}
