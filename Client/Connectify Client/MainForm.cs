using RemoteDesktop.Shared;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace RemoteDesktop.Client
{
    public partial class MainForm : Form
    {
        private readonly RemoteClient _client;
        private Size _hostImageSize;

        public MainForm(RemoteClient client)
        {
            _client = client;
            InitializeComponent();
            this.Load += MainForm_Load;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            _client.ImageReceived += Client_ImageReceived;
            _client.Disconnected += Client_Disconnected;
            try
            {
                await _client.ConnectAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to the host: {ex.Message}");
                Application.Exit();
            }
        }

        private void Client_ImageReceived(object sender, byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                throw new ArgumentException("Image data is invalid.");
            }

            using (var ms = new MemoryStream(imageData))
            {
                var image = Image.FromStream(ms);
                _hostImageSize = image.Size;
                this.BeginInvoke((MethodInvoker)delegate
                {
                    pictureBox.Image = image;
                    //image?.Dispose();
                });
            }
        }

        private void Client_Disconnected(object sender, EventArgs e)
        {
            MessageBox.Show("Disconnected from the host.");
            Application.Exit();
        }

        private void pictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                var (scaledX, scaledY) = ScaleCoordinates(e);
                _client.SendMouseEventAsync(CommunicationProtocol.MouseEventType.Move, (short)scaledX, (short)scaledY);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing mouse move: {ex.Message}");
            }
        }

        private void pictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            var (scaledX, scaledY) = ScaleCoordinates(e);
            var eventType = e.Button == MouseButtons.Left
                ? CommunicationProtocol.MouseEventType.LeftClick
                : CommunicationProtocol.MouseEventType.RightClick;
            _client.SendMouseEventAsync(eventType, (short)scaledX, (short)scaledY);
        }

        private void pictureBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var (scaledX, scaledY) = ScaleCoordinates(e);
            _client.SendMouseEventAsync(CommunicationProtocol.MouseEventType.DoubleClick, (short)scaledX, (short)scaledY);
        }

        private void MainForm_MouseWheel(object sender, MouseEventArgs e)
        {
            _client.SendMouseScrollAsync(e.Delta);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            _client.SendKeyboardEventAsync((byte)e.KeyCode, CommunicationProtocol.KeyboardEventType.KeyDown);
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            _client.SendKeyboardEventAsync((byte)e.KeyCode, CommunicationProtocol.KeyboardEventType.KeyUp);
        }

        private (int, int) ScaleCoordinates(MouseEventArgs point)
        {
            if (_hostImageSize.IsEmpty) return (0, 0);
            var scaledX = point.X * _hostImageSize.Width / pictureBox.Width;
            var scaledY = point.Y * _hostImageSize.Height / pictureBox.Height;
            return (scaledX, scaledY);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _client.Disconnect();
            base.OnFormClosing(e);
        }

        #region Windows Form Designer generated code

        private System.Windows.Forms.PictureBox pictureBox;

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
            this.pictureBox.Size = new System.Drawing.Size(800, 450);
            this.pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox.TabIndex = 0;
            this.pictureBox.TabStop = false;
            this.pictureBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.pictureBox_MouseClick);
            this.pictureBox.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.pictureBox_MouseDoubleClick);
            this.pictureBox.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pictureBox_MouseMove);
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.pictureBox);
            this.KeyPreview = true;
            this.Name = "MainForm";
            this.Text = "Remote Desktop Client";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.MainForm_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.MainForm_KeyUp);
            this.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.MainForm_MouseWheel);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
    }
}