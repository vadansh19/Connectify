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
    static TcpClient imageclient;
    static TcpClient inputclient;
    static TcpListener imageListener;
    static TcpListener inputListener;
    static NetworkStream imagestream;
    static NetworkStream inputstream;

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

        inputListener = new TcpListener(IPAddress.Any, 8889);
        inputListener.Start();

        imageListener = new TcpListener(IPAddress.Any, 8888);
        imageListener.Start();
        Console.WriteLine("Waiting for client...");

        inputclient = inputListener.AcceptTcpClient();
        Console.WriteLine("Input Client connected!");
        inputstream = inputclient.GetStream();

        imageclient = imageListener.AcceptTcpClient();
        Console.WriteLine("Image Client connected!");
        imagestream = imageclient.GetStream();

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
                imagestream.Write(lenBytes, 0, 4);
                imagestream.Write(jpegData, 0, jpegData.Length);
            }
            catch { break; }

            Thread.Sleep(66);
        }

        imagestream.Close();
        imageclient.Close();
        imageListener.Stop();

        inputstream.Close();
        inputclient.Close();
        inputListener.Stop();
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
                int packetType = inputstream.ReadByte();
                if (packetType == -1) return;

                switch (packetType)
                {
                    case 0x01: // Mouse event
                        byte[] mouseBuffer = new byte[5];
                        int read = 0;
                        while (read < 5)
                        {
                            int r = inputstream.Read(mouseBuffer, read, 5 - read);
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
                        else if (eventType == 3) // double-click
                        {
                            mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, 0);
                            mouse_event(MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
                            Thread.Sleep(50);
                            mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, 0);
                            mouse_event(MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
                        }
                        break;

                    case 0x02: // Keyboard event
                        byte[] kbBuffer = new byte[2];
                        if (inputstream.Read(kbBuffer, 0, 2) < 2) return;
                        byte keyCode = kbBuffer[0];
                        byte keyState = kbBuffer[1];
                        const uint KEYEVENTF_KEYUP = 0x0002;
                        uint flags = (keyState == 1) ? KEYEVENTF_KEYUP : 0;
                        keybd_event(keyCode, 0, flags, UIntPtr.Zero);
                        break;

                    case 0x03: // Mouse scroll
                        byte[] scrollBuffer = new byte[4];
                        if (inputstream.Read(scrollBuffer, 0, 4) < 4) return;
                        int delta = BitConverter.ToInt32(scrollBuffer, 0);
                        const uint MOUSEEVENTF_WHEEL = 0x0800;
                        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, 0);
                        break;
                }
            }
        }
        catch { }
    }

    #endregion Private Methods
}
