using System;
using System.Threading.Tasks;

namespace RemoteDesktop.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "Remote Desktop Server";
            // We now only need a single port for all communication.
            const int port = 8888;
            var server = new RemoteServer(port);
            await server.StartAsync();
        }
    }
}