using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using RemoteDesktop.Shared;

namespace RemoteDesktop.Server
{
    public class InputHandler
    {
        public void HandleMouseEvent(byte[] payload)
        {
            var eventType = (CommunicationProtocol.MouseEventType)payload[0];
            short x = BitConverter.ToInt16(payload, 1);
            short y = BitConverter.ToInt16(payload, 3);

            SetCursorPos(x, y);

            switch (eventType)
            {
                case CommunicationProtocol.MouseEventType.LeftClick:
                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
                    break;
                case CommunicationProtocol.MouseEventType.RightClick:
                    mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, (uint)x, (uint)y, 0, 0);
                    break;
                case CommunicationProtocol.MouseEventType.DoubleClick:
                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
                    Task.Delay(150).Wait(); // Block for double click interval
                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
                    break;
            }
        }

        public void HandleKeyboardEvent(byte[] payload)
        {
            byte keyCode = payload[0];
            var eventType = (CommunicationProtocol.KeyboardEventType)payload[1];
            uint flags = (eventType == CommunicationProtocol.KeyboardEventType.KeyUp) ? KEYEVENTF_KEYUP : 0;
            keybd_event(keyCode, 0, flags, UIntPtr.Zero);
        }

        public void HandleMouseScrollEvent(byte[] payload)
        {
            int delta = BitConverter.ToInt32(payload, 0);
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, 0);
        }

        #region P/Invoke User32
        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        #endregion
    }
}