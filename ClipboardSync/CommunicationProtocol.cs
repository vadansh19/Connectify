using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteDesktop.Shared
{
    public static class CommunicationProtocol
    {
        public const byte MouseEvent = 0x01;
        public const byte KeyboardEvent = 0x02;
        public const byte MouseScrollEvent = 0x03;
        public const byte ClipboardText = 0x04;

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