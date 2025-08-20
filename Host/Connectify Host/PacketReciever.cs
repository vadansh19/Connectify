using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RemoteDesktop.Shared;

namespace RemoteDesktop.Server
{
    public class PacketReceiver
    {
        private readonly NetworkStream _stream;
        private readonly InputHandler _inputSimulator = new InputHandler();

        public PacketReceiver(NetworkStream stream)
        {
            _stream = stream;
        }

        public async Task ReceiveDataAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int packetType = _stream.ReadByte();
                    if (packetType == -1) break;

                    byte[] lengthBytes = new byte[4];
                    await _stream.ReadAsync(lengthBytes, 0, 4, token);
                    int payloadLength = BitConverter.ToInt32(lengthBytes, 0);

                    byte[] payload = new byte[payloadLength];
                    await _stream.ReadAsync(payload, 0, payloadLength, token);

                    switch (packetType)
                    {
                        case CommunicationProtocol.MouseEvent:
                            _inputSimulator.HandleMouseEvent(payload);
                            break;
                        case CommunicationProtocol.KeyboardEvent:
                            _inputSimulator.HandleKeyboardEvent(payload);
                            break;
                        case CommunicationProtocol.MouseScrollEvent:
                            _inputSimulator.HandleMouseScrollEvent(payload);
                            break;
                        case CommunicationProtocol.ClipboardText:
                            HandleClipboardUpdate(payload);
                            break;
                    }
                }
            }
            catch (IOException)
            {
                Console.WriteLine("Client disconnected.");
            }
            catch (OperationCanceledException) { /* Server is shutting down */ }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving data: {ex.Message}");
            }
        }

        private void HandleClipboardUpdate(byte[] payload)
        {
            string text = Encoding.UTF8.GetString(payload);
            ClipboardService.SetText(text);
        }
    }
}