using System;
using System.Windows.Forms;

namespace RemoteDesktop.Client
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string host = "192.168.1.154"; // Replace with a prompt or config
            const int port = 8888;

            var clientForm = new MainForm(host, port);
            Application.Run(clientForm);
        }
    }
}