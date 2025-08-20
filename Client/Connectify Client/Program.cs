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

            Console.Write("Enter the IP Address of the host: ");
            var host = "192.168.1.154";
            if (string.IsNullOrWhiteSpace(host))
            {
                Console.WriteLine("Invalid IP address.");
                return;
            }

            var remoteClient = new RemoteClient(host, 8888, 8889);
            var mainForm = new MainForm(remoteClient);
            Application.Run(mainForm);
        }
    }
}