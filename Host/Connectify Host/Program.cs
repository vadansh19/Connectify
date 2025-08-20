using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteDesktop.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Remote Desktop Server...");
            var server = new RemoteServer(8888, 8889);
            await server.StartAsync();
        }
    }
}
