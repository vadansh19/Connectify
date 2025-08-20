using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RemoteDesktop.Shared;

namespace RemoteDesktop.Server
{
    public class PacketSender
    {
        private readonly NetworkStream _stream;
        private string _lastClipboardText = string.Empty;

        public PacketSender(NetworkStream stream)
        {
            _stream = stream;
        }

        public async Task SendDataAsync(CancellationToken token)
        {
            // Start two concurrent loops: one for screen updates, one for clipboard.
            var screenTask = SendScreenUpdatesAsync(token);
            var clipboardTask = SendClipboardUpdatesAsync(token);
            await Task.WhenAll(screenTask, clipboardTask);
        }

        private async Task SendScreenUpdatesAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var bmp = CaptureScreen())
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Jpeg);
                        byte[] buffer = ms.ToArray();
                        await WritePacketAsync(CommunicationProtocol.ScreenImage, buffer, token);
                    }
                    await Task.Delay(100, token); // Adjust for desired frame rate
                }
                catch
                {
                    Console.WriteLine("Client disconnected. Stopping screen updates.");
                    break;
                }
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
                        await WritePacketAsync(CommunicationProtocol.ClipboardText, textBytes, token);
                    }
                    await Task.Delay(500, token); // Poll clipboard every 500ms
                }
                catch
                {
                    Console.WriteLine("Client disconnected. Stopping clipboard sync.");
                    break;
                }
            }
        }

        private async Task WritePacketAsync(byte packetType, byte[] payload, CancellationToken token)
        {
            byte[] lengthBytes = BitConverter.GetBytes(payload.Length);
            await _stream.WriteAsync(new[] { packetType }, 0, 1, token);
            await _stream.WriteAsync(lengthBytes, 0, 4, token);
            await _stream.WriteAsync(payload, 0, payload.Length, token);
        }

        private Bitmap CaptureScreen()
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            var bmp = new Bitmap(bounds.Width, bounds.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
            }
            return bmp;
        }
    }
}