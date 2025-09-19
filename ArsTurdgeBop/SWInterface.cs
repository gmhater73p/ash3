using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

namespace Ash3.ArsTurdgeBop {
    internal class SWInterface {
        private readonly Queue<Instruction> _queue = new();
        private readonly Dictionary<int, Instruction> _pendingAck = new();

        public Event<SWInterfaceStats> OnStatsUpdate = new();
        public Event OnTick = new();

        private int _lastResponseTime = DateTime.UtcNow.Millisecond;

        private HttpListener _httpServer { get; set; }

        public SWInterface(int port) {
            _httpServer = new();
            _httpServer.Prefixes.Add($"http://localhost:{port}/");
        }

        public void Start() {
            if (_httpServer.IsListening) return;
            Task.Run(() => {
                _httpServer.Start();
                Console.WriteLine("HTTP server started");

                // Listen for requests
                while (_httpServer.IsListening) {
                    var context = _httpServer.GetContext();
                    var request = context.Request;

                    Console.WriteLine(request.Url);

                    var url = request.Url.AbsolutePath;

                    if (!url.StartsWith("/bop")) continue;

                    var urlParams = request.QueryString;

                    OnStatsUpdate.Fire(new SWInterfaceStats(
                        urlParams["players"] != null ? int.Parse(urlParams["players"]!) : 0,
                        urlParams["hour"] != null ? int.Parse(urlParams["hour"]!) : 0,
                        urlParams["minute"] != null ? int.Parse(urlParams["minute"]!) : 0,
                        urlParams["tick"] != null ? int.Parse(urlParams["tick"]!) : 0,
                        urlParams["uptime"] != null ? int.Parse(urlParams["uptime"]!) : 0,
                        _lastResponseTime
                    ));
                    _lastResponseTime = DateTime.UtcNow.Millisecond;

                    if (urlParams["data"] != null) {
                        var data = urlParams["data"];
                        Console.WriteLine(data);
                    }

                    OnTick.Fire();

                    if (_queue.Count > 0) {
                        Console.WriteLine("-");
                        Console.WriteLine($"TX Tick {urlParams["tick"]}");
                        while (_queue.Count > 0) {
                            var instruction = _queue.Dequeue();
                        }
                    }
                }

                // Stop the HTTP server
                //_httpServer.Stop();
                Console.WriteLine("HTTP server stopped");
            });
        }

        public void Stop() => _httpServer.Stop();
    }

    internal record SWInterfaceStats(
        int PlayerCount,
        int Hour,
        int Minute,
        int Tick,
        int Uptime,
        int LastResponseTime
    );
}
