using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RemoteDesktop.Server
{
    public static class InputHandler
    {
        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static async Task HandleMouseEventAsync(Stream stream)
        {
            byte[] mouseBuffer = new byte[5];
            int read = 0;
            while (read < 5)
            {
                int r = await stream.ReadAsync(mouseBuffer, read, 5 - read);
                if (r == 0) return; // disconnected
                read += r;
            }

            byte eventType = mouseBuffer[0];
            short x = BitConverter.ToInt16(mouseBuffer, 1);
            short y = BitConverter.ToInt16(mouseBuffer, 3);

            SetCursorPos(x, y);

            switch (eventType)
            {
                case 0: // Move
                    break;
                case 1: // Left Click
                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
                    break;
                case 2: // Right Click
                    mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, (uint)x, (uint)y, 0, 0);
                    break;
                case 3: // Double Click
                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
                    await Task.Delay(150);
                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
                    break;
            }
        }

        public static async Task HandleKeyboardEventAsync(Stream stream)
        {
            var buffer = new byte[2];
            await stream.ReadAsync(buffer, 0, buffer.Length);
            var keyCode = buffer[0];
            var keyState = buffer[1];
            var flags = (keyState == 1) ? KEYEVENTF_KEYUP : 0;
            keybd_event(keyCode, 0, flags, UIntPtr.Zero);
        }

        public static async Task HandleMouseScrollEventAsync(Stream stream)
        {
            var buffer = new byte[4];
            await stream.ReadAsync(buffer, 0, buffer.Length);
            var delta = BitConverter.ToInt32(buffer, 0);
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, 0);
        }
    }
}