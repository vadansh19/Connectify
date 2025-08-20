using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RemoteDesktop.Shared;

namespace RemoteDesktop.Client
{
    public class RemoteClient
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cancellationTokenSource;
        private string _lastClipboardText = string.Empty;

        public event EventHandler<byte[]> ImageReceived;
        public event EventHandler Disconnected;

        public RemoteClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task ConnectAsync()
        {
            _client = new TcpClient();
            _cancellationTokenSource = new CancellationTokenSource();
            await _client.ConnectAsync(_host, _port);
            _stream = _client.GetStream();

            var token = _cancellationTokenSource.Token;
            Task.Run(() => ReceiveDataAsync(token), token);
            Task.Run(() => SendClipboardUpdatesAsync(token), token);
        }

        private async Task ReceiveDataAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int packetType = _stream.ReadByte();
                    if (packetType == -1) break;

                    byte[] lenBytes = new byte[4];
                    await _stream.ReadAsync(lenBytes, 0, 4, token);
                    int len = BitConverter.ToInt32(lenBytes, 0);

                    byte[] payload = new byte[len];
                    int bytesRead = 0;
                    while (bytesRead < len)
                    {
                        bytesRead += await _stream.ReadAsync(payload, bytesRead, len - bytesRead, token);
                    }

                    if (packetType == CommunicationProtocol.ScreenImage)
                    {
                        ImageReceived?.Invoke(this, payload);
                    }
                    else if (packetType == CommunicationProtocol.ClipboardText)
                    {
                        string text = Encoding.UTF8.GetString(payload);
                        ClipboardService.SetText(text);
                        _lastClipboardText = text;
                    }
                }
            }
            catch
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task SendClipboardUpdatesAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    string currentClipboardText = ClipboardService.GetText();
                    if (currentClipboardText != _lastClipboardText)
                    {
                        _lastClipboardText = currentClipboardText;
                        byte[] textBytes = Encoding.UTF8.GetBytes(currentClipboardText);
                        await SendPacketAsync(CommunicationProtocol.ClipboardText, textBytes);
                    }
                    await Task.Delay(500, token);
                }
                catch
                {
                    break;
                }
            }
        }

        public async Task SendPacketAsync(byte packetType, byte[] payload)
        {
            if (_stream == null || !_stream.CanWrite) return;
            try
            {
                byte[] lengthBytes = BitConverter.GetBytes(payload.Length);
                await _stream.WriteAsync(new[] { packetType }, 0, 1);
                await _stream.WriteAsync(lengthBytes, 0, 4);
                await _stream.WriteAsync(payload, 0, payload.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send packet: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            _cancellationTokenSource?.Cancel();
            _client?.Close();
        }
    }
}