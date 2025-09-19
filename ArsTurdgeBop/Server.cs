using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ash3.ArsTurdgeBop {
    internal class Server {
        private SWInterface _swInterface;

        public Event<bool> OnConnected = new();
        public Event OnTick { get => _swInterface.OnTick; }

        public int Latency { get; private set; }
        public int Tps { get; private set; }
        public int PlayerCount { get; private set; }
        public int Uptime { get; private set; }
        public int Tick { get; private set; }
        public int Hour { get; private set; }
        public int Minute { get; private set; }
        public double Time { get; private set; }

        private readonly byte[] _averageTps = new byte[10];

        public Server(SWInterface swInterface) {
            _swInterface = swInterface;

            _swInterface.OnStatsUpdate.Connect(stats => {
                Latency = DateTime.UtcNow.Millisecond - stats.LastResponseTime;

                for (int i = _averageTps.Length - 1; i > 0; i--) _averageTps[i] = _averageTps[i - 1]; // shift to the right by 1
                _averageTps[0] = (byte)(-(Tick - stats.Tick) / (stats.Uptime - Uptime) * 1000);
                Tps = _averageTps.Sum(x => x) / _averageTps.Length;

                PlayerCount = stats.PlayerCount;
                Uptime = stats.Uptime;
                Hour = stats.Hour;
                Minute = stats.Minute;
                Time = stats.Hour + (stats.Minute / 60.0);

                if (stats.Tick < Tick) OnConnected.Fire(Tick < 60);

                Tick = stats.Tick;
            });
        }

        public Server(int port) : this(new SWInterface(port)) { }
    }
}
