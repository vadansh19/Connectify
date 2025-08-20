using System;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace RemoteDesktop.Shared
{
    public static class ClipboardHelper
    {
        public static void SetText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            var thread = new Thread(() =>
            {
                try
                {
                    Clipboard.SetText(text, TextDataFormat.UnicodeText);
                }
                catch (Exception ex)
                {
                    // Log the exception
                    Console.WriteLine($"Clipboard SetText error: {ex.Message}");
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        public static string GetText()
        {
            string result = string.Empty;
            var thread = new Thread(() =>
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        result = Clipboard.GetText(TextDataFormat.UnicodeText);
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception
                    Console.WriteLine($"Clipboard GetText error: {ex.Message}");
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result;
        }
    }
}
