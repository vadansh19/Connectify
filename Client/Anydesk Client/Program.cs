using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

class RemoteClient : Form
{
    #region Private Fields

    Size hostImageSize;
    PictureBox pb;

    TcpClient imageclient;
    TcpClient inputclient;
    NetworkStream imagestream;
    NetworkStream inputstream;
    //TcpClient client;
    //NetworkStream stream;

    #endregion Private Fields

    #region DLL Imports

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    #endregion DLL Imports

    #region Public Constructors

    public RemoteClient()
    {
        InitializeComponents();
    }

    #endregion Public Constructors

    #region Public Methods

    public void Start(string host)
    {
        imageclient = new TcpClient();
        imageclient.Connect(host, 8888);
        imagestream = imageclient.GetStream();

        inputclient = new TcpClient();
        inputclient.Connect(host, 8889);
        inputstream = inputclient.GetStream();

        Thread receiveThread = new Thread(ReceiveScreenLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Application.Run(this);
    }

    #endregion Public Methods

    #region Private Methods

    private void InitializeComponents()
    {
        this.Width = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
        this.Height = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
        pb = new PictureBox();
        pb.Padding = Padding.Empty;
        pb.Margin = Padding.Empty;
        pb.Width = this.Width;
        pb.Height = this.Height;
        pb.Dock = DockStyle.Fill;
        pb.SizeMode = PictureBoxSizeMode.StretchImage;
        pb.BackgroundImageLayout = ImageLayout.Stretch;
        this.KeyPreview = true;
        //this.KeyDown += RemoteClient_KeyDown;
        //this.KeyUp += RemoteClient_KeyUp;
        pb.MouseMove += RemoteClient_MouseMove;
        pb.MouseClick += RemoteClient_MouseClick;
        this.Controls.Add(pb);
    }

    [STAThread]
    static void Main(string[] args)
    {
        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var client = new RemoteClient();
        client.Start("192.168.1.75");
    }

    void ReceiveScreenLoop()
    {
        try
        {
            while (true)
            {
                byte[] lenBytes = new byte[4];
                int read = 0;
                while (read < 4)
                {
                    int r = imagestream.Read(lenBytes, read, 4 - read);
                    if (r == 0) return;
                    read += r;
                }
                int len = BitConverter.ToInt32(lenBytes, 0);
                byte[] imgBytes = new byte[len];
                read = 0;
                while (read < len)
                {
                    int r = imagestream.Read(imgBytes, read, len - read);
                    if (r == 0) return;
                    read += r;
                }
                using (var ms = new MemoryStream(imgBytes))
                {
                    Image img = Image.FromStream(ms);
                    hostImageSize = img.Size;
                    this.Invoke((MethodInvoker)(() =>
                    {
                        pb.BackgroundImage?.Dispose();
                        pb.BackgroundImage = new Bitmap(img);
                    }));
                }
            }
        }
        catch { }
    }

    private void RemoteClient_MouseClick(object sender, MouseEventArgs e)
    {
        int scaledX, scaledY;
        SyncMouse(e, out scaledX, out scaledY);

        if (e.Button == MouseButtons.Left)
            SendMouseEvent(1, (short)scaledX, (short)scaledY); // left click
        else if (e.Button == MouseButtons.Right)
            SendMouseEvent(2, (short)scaledX, (short)scaledY); // right click
    }

    private void RemoteClient_MouseMove(object sender, MouseEventArgs e)
    {
        int scaledX, scaledY;
        SyncMouse(e, out scaledX, out scaledY);
        SendMouseEvent(0, (short)scaledX, (short)scaledY);
    }

    void SendMouseEvent(byte eventType, short x, short y)
    {
        if (inputstream == null) return;

        byte[] data = new byte[6];
        data[0] = 0x01; // Mouse packet
        data[1] = eventType;
        BitConverter.GetBytes(x).CopyTo(data, 2);
        BitConverter.GetBytes(y).CopyTo(data, 4);

        try { inputstream.Write(data, 0, data.Length); } catch { }
    }

    void SendKeyboardEvent(byte keyCode, bool isKeyUp)
    {
        if (inputstream == null) return;

        byte[] data = new byte[3];
        data[0] = 0x02; // Keyboard packet
        data[1] = keyCode;
        data[2] = (byte)(isKeyUp ? 1 : 0);

        try { inputstream.Write(data, 0, data.Length); } catch { }
    }

    private void SyncMouse(MouseEventArgs e, out int scaledX, out int scaledY)
    {
        scaledX = 0;
        scaledY = 0;
        try
        {
            int clientX = e.X;
            int clientY = e.Y;
            int displayWidth = pb.Width;
            int displayHeight = pb.Height;
            int hostScreenWidth = hostImageSize.Width;
            int hostScreenHeight = hostImageSize.Height;

            scaledX = clientX * hostScreenWidth / displayWidth;
            scaledY = clientY * hostScreenHeight / displayHeight;
        }
        catch { }

    }

    private void RemoteClient_KeyDown(object sender, KeyEventArgs e)
    {
        SendKeyboardEvent((byte)e.KeyValue, false); // key down
    }

    private void RemoteClient_KeyUp(object sender, KeyEventArgs e)
    {
        //SendKeyboardEvent((byte)e.KeyValue, true); // key up
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        byte vk = (byte)keyData;
        SendKeyboardEvent(vk, false); // Key down
        SendKeyboardEvent(vk, true);  // Key up
        return base.ProcessCmdKey(ref msg, keyData);
    }

    #endregion Private Methods
}
