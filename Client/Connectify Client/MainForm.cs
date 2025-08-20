using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RemoteDesktop.Shared;

namespace RemoteDesktop.Client
{
    public partial class MainForm : Form
    {
        private readonly RemoteClient _desktopManager;
        private Size _hostImageSize;

        public MainForm(string host, int port)
        {
            InitializeComponent();
            _desktopManager = new RemoteClient(host, port);
            _desktopManager.ImageReceived += OnImageReceived;
            _desktopManager.Disconnected += OnDisconnected;
        }

        private async void ClientForm_Load(object sender, EventArgs e)
        {
            try
            {
                await _desktopManager.ConnectAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        private void OnImageReceived(object sender, byte[] imageData)
        {
            try
            {
                using (var ms = new MemoryStream(imageData))
                {
                    var image = Image.FromStream(ms);
                    _hostImageSize = image.Size;
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        pictureBox.Image?.Dispose();
                        pictureBox.Image = image;
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing image: {ex.Message}");
            }
        }

        private void OnDisconnected(object sender, EventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate
            {
                MessageBox.Show("Disconnected from the host.", "Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            });
        }

        private (int, int) ScaleCoordinates(Point point)
        {
            if (_hostImageSize.IsEmpty || pictureBox.Width == 0 || pictureBox.Height == 0)
                return (0, 0);

            int scaledX = point.X * _hostImageSize.Width / pictureBox.Width;
            int scaledY = point.Y * _hostImageSize.Height / pictureBox.Height;
            return (scaledX, scaledY);
        }

        #region UI Event Handlers

        private void pictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            var (x, y) = ScaleCoordinates(e.Location);
            var payload = new byte[5];
            payload[0] = (byte)CommunicationProtocol.MouseEventType.Move;
            BitConverter.GetBytes((short)x).CopyTo(payload, 1);
            BitConverter.GetBytes((short)y).CopyTo(payload, 3);
            _desktopManager.SendPacketAsync(CommunicationProtocol.MouseEvent, payload);
        }

        private void pictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            var (x, y) = ScaleCoordinates(e.Location);
            var eventType = e.Button == MouseButtons.Left ? CommunicationProtocol.MouseEventType.LeftClick : CommunicationProtocol.MouseEventType.RightClick;
            var payload = new byte[5];
            payload[0] = (byte)eventType;
            BitConverter.GetBytes((short)x).CopyTo(payload, 1);
            BitConverter.GetBytes((short)y).CopyTo(payload, 3);
            _desktopManager.SendPacketAsync(CommunicationProtocol.MouseEvent, payload);
        }

        private void pictureBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var (x, y) = ScaleCoordinates(e.Location);
            var payload = new byte[5];
            payload[0] = (byte)CommunicationProtocol.MouseEventType.DoubleClick;
            BitConverter.GetBytes((short)x).CopyTo(payload, 1);
            BitConverter.GetBytes((short)y).CopyTo(payload, 3);
            _desktopManager.SendPacketAsync(CommunicationProtocol.MouseEvent, payload);
        }

        private void ClientForm_MouseWheel(object sender, MouseEventArgs e)
        {
            var payload = BitConverter.GetBytes(e.Delta);
            _desktopManager.SendPacketAsync(CommunicationProtocol.MouseScrollEvent, payload);
        }

        private void ClientForm_KeyDown(object sender, KeyEventArgs e)
        {
            var payload = new byte[] { (byte)e.KeyCode, (byte)CommunicationProtocol.KeyboardEventType.KeyDown };
            _desktopManager.SendPacketAsync(CommunicationProtocol.KeyboardEvent, payload);
        }

        private void ClientForm_KeyUp(object sender, KeyEventArgs e)
        {
            var payload = new byte[] { (byte)e.KeyCode, (byte)CommunicationProtocol.KeyboardEventType.KeyUp };
            _desktopManager.SendPacketAsync(CommunicationProtocol.KeyboardEvent, payload);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _desktopManager.Disconnect();
            base.OnFormClosing(e);
        }

        #endregion

        #region Windows Form Designer generated code

        private PictureBox pictureBox;
        private void InitializeComponent()
        {
            this.pictureBox = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox
            // 
            this.pictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox.Location = new System.Drawing.Point(0, 0);
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.Size = new System.Drawing.Size(800, 600);
            this.pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox.TabIndex = 0;
            this.pictureBox.TabStop = false;
            this.pictureBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.pictureBox_MouseClick);
            this.pictureBox.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.pictureBox_MouseDoubleClick);
            this.pictureBox.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pictureBox_MouseMove);
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Controls.Add(this.pictureBox);
            this.KeyPreview = true;
            this.Name = "MainForm";
            this.Text = "Remote Desktop Viewer";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.ClientForm_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ClientForm_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.ClientForm_KeyUp);
            this.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.ClientForm_MouseWheel);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.ResumeLayout(false);
        }
        #endregion
    }
}