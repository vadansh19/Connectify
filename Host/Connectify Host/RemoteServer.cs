using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RemoteDesktop.Shared;

namespace RemoteDesktop.Server
{
    public class RemoteServer
    {
        private readonly TcpListener _imageListener;
        private readonly TcpListener _inputListener;

        public RemoteServer(int imagePort, int inputPort)
        {
            _imageListener = new TcpListener(IPAddress.Any, imagePort);
            _inputListener = new TcpListener(IPAddress.Any, inputPort);
        }

        public async Task StartAsync()
        {
            _imageListener.Start();
            _inputListener.Start();
            Console.WriteLine("Server started. Waiting for connections...");

            while (true) // keep server alive forever
            {
                Console.WriteLine("Waiting for image client...");
                var imageClient = await _imageListener.AcceptTcpClientAsync();
                Console.WriteLine("Image client connected.");

                Console.WriteLine("Waiting for input client...");
                var inputClient = await _inputListener.AcceptTcpClientAsync();
                Console.WriteLine("Input client connected.");

                var cancellationTokenSource = new CancellationTokenSource();

                // Run session tasks
                var screenTask = Task.Run(() => SendScreenUpdatesAsync(imageClient, cancellationTokenSource.Token));
                var inputTask = Task.Run(() => ReceiveInputAsync(inputClient, cancellationTokenSource.Token));
                var clipboardTask = Task.Run(() => SyncClipboardAsync(inputClient.GetStream(), cancellationTokenSource.Token));

                // Wait until one task finishes (likely a disconnect)
                await Task.WhenAny(screenTask, inputTask, clipboardTask);

                Console.WriteLine("Client session ended. Cleaning up...");
                cancellationTokenSource.Cancel();

                imageClient.Close();
                inputClient.Close();
                // Loop will continue and accept the next client pair
            }
        }

        private async Task SendScreenUpdatesAsync(TcpClient client, CancellationToken token)
        {
            using (var stream = client.GetStream())
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using (var bmp = CaptureScreen())
                        using (var ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Jpeg);
                            var buffer = ms.ToArray();
                            var lengthBytes = BitConverter.GetBytes(buffer.Length);
                            await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, token);
                            await stream.WriteAsync(buffer, 0, buffer.Length, token);
                        }
                        await Task.Delay(66, token); // ~15 FPS
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Client disconnected.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending screen updates: {ex.Message}");
                        break;
                    }
                }
            }
        }

        private async Task ReceiveInputAsync(TcpClient client, CancellationToken token)
        {
            using (var stream = client.GetStream())
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var packetType = stream.ReadByte();
                        if (packetType == -1) break;

                        switch (packetType)
                        {
                            case CommunicationProtocol.MouseEvent:
                                await InputHandler.HandleMouseEventAsync(stream);
                                break;
                            case CommunicationProtocol.KeyboardEvent:
                                await InputHandler.HandleKeyboardEventAsync(stream);
                                break;
                            case CommunicationProtocol.MouseScrollEvent:
                                await InputHandler.HandleMouseScrollEventAsync(stream);
                                break;
                            case CommunicationProtocol.ClipboardText:
                                await HandleClipboardUpdateAsync(stream);
                                break;
                        }
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Client disconnected.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error receiving input: {ex.Message}");
                        break;
                    }
                }
            }
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

        private async Task HandleClipboardUpdateAsync(NetworkStream stream)
        {
            var lengthBytes = new byte[4];
            await stream.ReadAsync(lengthBytes, 0, 4);
            var length = BitConverter.ToInt32(lengthBytes, 0);

            var buffer = new byte[length];
            await stream.ReadAsync(buffer, 0, length);
            var text = System.Text.Encoding.UTF8.GetString(buffer);
            ClipboardHelper.SetText(text);
        }

        private Bitmap CaptureScreen()
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var bmp = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
            }
            return bmp;
        }
    }
}