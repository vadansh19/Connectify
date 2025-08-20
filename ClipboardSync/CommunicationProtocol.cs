using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteDesktop.Shared
{
    /// <summary>
    /// Defines the communication protocol constants between the client and server.
    /// All communication is now on a single port.
    /// </summary>
    public static class CommunicationProtocol
    {
        // Packet types sent from Client to Server
        public const byte MouseEvent = 0x01;
        public const byte KeyboardEvent = 0x02;
        public const byte MouseScrollEvent = 0x03;

        // Packet types sent in both directions
        public const byte ClipboardText = 0x04;

        // Packet type sent from Server to Client
        public const byte ScreenImage = 0x05;

        public enum MouseEventType : byte
        {
            Move = 0,
            LeftClick = 1,
            RightClick = 2,
            DoubleClick = 3
        }

        public enum KeyboardEventType : byte
        {
            KeyDown = 0,
            KeyUp = 1
        }
    }
}