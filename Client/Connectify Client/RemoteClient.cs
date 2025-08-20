using RemoteDesktop.Shared;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteDesktop.Client
{
    public class RemoteClient
    {
        private readonly string _host;
        private readonly int _imagePort;
        private readonly int _inputPort;
        private TcpClient _imageClient;
        private TcpClient _inputClient;
        private NetworkStream _imageStream;
        private NetworkStream _inputStream;

        public event EventHandler<byte[]> ImageReceived;
        public event EventHandler Disconnected;

        public RemoteClient(string host, int imagePort, int inputPort)
        {
            _host = host;
            _imagePort = imagePort;
            _inputPort = inputPort;
        }

        public async Task ConnectAsync()
        {
            _imageClient = new TcpClient();
            _inputClient = new TcpClient();
            await _imageClient.ConnectAsync(_host, _imagePort);
            await _inputClient.ConnectAsync(_host, _inputPort);
            _imageStream = _imageClient.GetStream();
            _inputStream = _inputClient.GetStream();

            Task.Run(ReceiveImagesAsync);
            Task.Run(ReceiveClipboardUpdatesAsync);
            Task.Run(() => SyncClipboardAsync(_inputStream, new CancellationToken()));
        }

        private async Task ReceiveImagesAsync()
        {
            try
            {
                while (true)
                {
                    // Read 4 bytes for length (handle partial reads)
                    var lenBytes = new byte[4];
                    int read = 0;
                    while (read < 4)
                    {
                        int r = await _imageStream.ReadAsync(lenBytes, read, 4 - read);
                        if (r == 0) return; // disconnected
                        read += r;
                    }

                    int len = BitConverter.ToInt32(lenBytes, 0);

                    // Read the image data
                    var buffer = new byte[len];
                    read = 0;
                    while (read < len)
                    {
                        int r = await _imageStream.ReadAsync(buffer, read, len - read);
                        if (r == 0) return; // disconnected
                        read += r;
                    }

                    // Process the image
                    ImageReceived?.Invoke(this, buffer);
                }
            }
            catch
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task ReceiveClipboardUpdatesAsync()
        {
            try
            {
                while (true)
                {
                    var packetType = _inputStream.ReadByte();
                    if (packetType == CommunicationProtocol.ClipboardText)
                    {
                        var lenBytes = new byte[4];
                        await _inputStream.ReadAsync(lenBytes, 0, 4);
                        var len = BitConverter.ToInt32(lenBytes, 0);
                        var buffer = new byte[len];
                        await _inputStream.ReadAsync(buffer, 0, len);
                        var text = System.Text.Encoding.UTF8.GetString(buffer);
                        ClipboardHelper.SetText(text);
                    }
                }
            }
            catch
            {
                // Handle disconnection
            }
        }

        public async Task SendMouseEventAsync(CommunicationProtocol.MouseEventType eventType, short x, short y)
        {
            if (_inputStream != null && _inputStream.CanWrite) // fixed condition to check if CanWrite is true
            {
                var data = new byte[6];
                data[0] = CommunicationProtocol.MouseEvent;
                data[1] = (byte)eventType;
                BitConverter.GetBytes(x).CopyTo(data, 2);
                BitConverter.GetBytes(y).CopyTo(data, 4);
                await _inputStream.WriteAsync(data, 0, data.Length);
            }
        }

        public async Task SendKeyboardEventAsync(byte keyCode, CommunicationProtocol.KeyboardEventType eventType)
        {
            var data = new byte[3];
            data[0] = CommunicationProtocol.KeyboardEvent;
            data[1] = keyCode;
            data[2] = (byte)eventType;
            await _inputStream.WriteAsync(data, 0, data.Length);
        }

        public async Task SendMouseScrollAsync(int delta)
        {
            var data = new byte[5];
            data[0] = CommunicationProtocol.MouseScrollEvent;
            BitConverter.GetBytes(delta).CopyTo(data, 1);
            await _inputStream.WriteAsync(data, 0, data.Length);
        }

        private async Task SyncClipboardAsync(NetworkStream stream, CancellationToken token)
        {
            var lastClipboardText = string.Empty;
            while (!token.IsCancellationRequested)
            {
                var currentClipboardText = ClipboardHelper.GetText();
                if (currentClipboardText != lastClipboardText)
                {
                    lastClipboardText = currentClipboardText;
                    await SendClipboardUpdateAsync(stream, currentClipboardText, token);
                }
                await Task.Delay(1000, token);
            }
        }

        private async Task SendClipboardUpdateAsync(NetworkStream stream, string text, CancellationToken token)
        {
            var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
            var lengthBytes = BitConverter.GetBytes(textBytes.Length);
            var message = new byte[1 + 4 + textBytes.Length];
            message[0] = CommunicationProtocol.ClipboardText;
            Buffer.BlockCopy(lengthBytes, 0, message, 1, 4);
            Buffer.BlockCopy(textBytes, 0, message, 5, textBytes.Length);

            try
            {
                await stream.WriteAsync(message, 0, message.Length, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send clipboard update: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            _imageClient?.Close();
            _inputClient?.Close();
        }
    }
}