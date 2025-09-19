using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ash3 {
    public class Event<T> {
        internal readonly List<EventConnection<T>> _connections = new();

        public void Fire(T arg) {
            foreach (var connection in _connections) connection._action(arg);
        }

        public EventConnection<T> Connect(Action<T> action) {
            var connection = new EventConnection<T>(this, action);
            _connections.Add(connection);
            return connection;
        }

        public EventConnection<T> Once(Action<T> action) {
            EventConnection<T>? connection = null;
            connection = Connect(arg => {
                action(arg);
                connection?.Disconnect();
            });
            return connection;
        }

        public async Task<T> Wait() {
            var tcs = new TaskCompletionSource<T>();
            Once(tcs.SetResult);
            return await tcs.Task;
        }

        public static readonly object Empty = new();
    }

    public class EventConnection<T>(Event<T> self, Action<T> action) {
        internal Event<T> _self = self;
        internal Action<T> _action = action;
        public void Disconnect() => _self._connections.Remove(this);
    }

    public class Event {
        internal readonly List<EventConnection> _connections = new();

        public void Fire() {
            foreach (var connection in _connections) connection._action();
        }

        public EventConnection Connect(Action action) {
            var connection = new EventConnection(this, action);
            _connections.Add(connection);
            return connection;
        }

        public EventConnection Once(Action action) {
            EventConnection? connection = null;
            connection = Connect(() => {
                action();
                connection?.Disconnect();
            });
            return connection;
        }

        public async Task Wait() {
            var tcs = new TaskCompletionSource();
            Once(tcs.SetResult);
            await tcs.Task;
        }
    }

    public class EventConnection(Event self, Action action) {
        internal Event _self = self;
        internal Action _action = action;
        public void Disconnect() => _self._connections.Remove(this);
    }
}
