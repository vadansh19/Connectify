using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteDesktop.Server
{
    public class RemoteServer
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public RemoteServer(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine($"Server started on port {_listener.LocalEndpoint}. Waiting for a client to connect...");

            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                Console.WriteLine("Client connected.");

                var stream = client.GetStream();
                var token = _cancellationTokenSource.Token;

                // Run concurrent tasks for sending and receiving data on the same stream.
                var sendingTask = new PacketSender(stream).SendDataAsync(token);
                var receivingTask = new PacketReceiver(stream).ReceiveDataAsync(token);

                // Wait for either task to complete (which indicates a disconnection or error).
                await Task.WhenAny(sendingTask, receivingTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            finally
            {
                Stop();
            }
        }

        public void Stop()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
            _listener.Stop();
            Console.WriteLine("Server stopped.");
        }
    }
}