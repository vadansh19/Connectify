using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

class RemoteHost
{
    #region Private Fields

    const uint MOUSEEVENTF_MOVE = 0x0001;
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;
    const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    const uint KEYBOARDEVENTF_DOWN = 0u;
    const uint KEYBOARDEVENTF_UP = 0x0002u;
    static TcpClient client;
    static TcpListener listener;
    static NetworkStream stream;

    #endregion Private Fields

    #region DLL Imports

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    #endregion DLL Imports

    #region Public Methods

    public static void Main()
    {
        SetProcessDPIAware();

        listener = new TcpListener(IPAddress.Any, 8888);
        listener.Start();
        Console.WriteLine("Waiting for client...");

        client = listener.AcceptTcpClient();
        Console.WriteLine("Client connected!");
        stream = client.GetStream();

        Thread receiveInputThread = new Thread(ReceiveInputLoop);
        receiveInputThread.IsBackground = true;
        receiveInputThread.Start();

        while (true)
        {
            Bitmap bmp = CaptureScreen();
            byte[] jpegData = BitmapToJpeg(bmp);
            bmp.Dispose();

            try
            {
                byte[] lenBytes = BitConverter.GetBytes(jpegData.Length);
                stream.Write(lenBytes, 0, 4);
                stream.Write(jpegData, 0, jpegData.Length);
            }
            catch { break; }

            Thread.Sleep(100);
        }

        stream.Close();
        client.Close();
        listener.Stop();
    }

    #endregion Public Methods

    #region Private Methods

    static byte[] BitmapToJpeg(Bitmap bmp)
    {
        using (var ms = new System.IO.MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }
    }

    static Bitmap CaptureScreen()
    {
        Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
        Bitmap bmp = new Bitmap(bounds.Width, bounds.Height);

        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
        }

        return bmp;
    }

    static void ReceiveInputLoop()
    {
        try
        {
            while (true)
            {
                int packetType = stream.ReadByte();
                if (packetType == -1) return;

                switch (packetType)
                {
                    case 0x01: // Mouse event
                        byte[] mouseBuffer = new byte[5];
                        int read = 0;
                        while (read < 5)
                        {
                            int r = stream.Read(mouseBuffer, read, 5 - read);
                            if (r == 0) return;
                            read += r;
                        }

                        byte eventType = mouseBuffer[0];
                        short x = BitConverter.ToInt16(mouseBuffer, 1);
                        short y = BitConverter.ToInt16(mouseBuffer, 3);

                        if (eventType == 0) // move
                            SetCursorPos(x, y);
                        else if (eventType == 1) // left click
                        {
                            mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, 0);
                            mouse_event(MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
                        }
                        else if (eventType == 2) // right click
                        {
                            const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
                            const uint MOUSEEVENTF_RIGHTUP = 0x0010;
                            mouse_event(MOUSEEVENTF_RIGHTDOWN, (uint)x, (uint)y, 0, 0);
                            mouse_event(MOUSEEVENTF_RIGHTUP, (uint)x, (uint)y, 0, 0);
                        }
                        break;

                    case 0x02: // Keyboard event
                        byte[] kbBuffer = new byte[2];
                        if (stream.Read(kbBuffer, 0, 2) < 2) return;
                        byte keyCode = kbBuffer[0];
                        byte keyState = kbBuffer[1];
                        const uint KEYEVENTF_KEYUP = 0x0002;
                        uint flags = (keyState == 1) ? KEYEVENTF_KEYUP : 0;
                        keybd_event(keyCode, 0, flags, UIntPtr.Zero);
                        break;
                }
            }
        }
        catch { }
    }



    #endregion Private Methods
}
