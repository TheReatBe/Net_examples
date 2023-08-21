using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public static class WifiSignalStrength
{
    //Строка в выводе, обозначающую сигнал
    private static string[] wlanSignalNameFields = { "Сигнал", "Signal" };

    /// <summary>
    /// Вызывает утилиту windows - netsh, для получения параметров сети. Часто не вызывать
    /// </summary>
    /// <returns>Сигнал 0-100</returns>
    public static int GetSignalStrength()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(Environment.SystemDirectory, "netsh.exe"),
                Arguments = "wlan show interfaces",
                UseShellExecute = false,
                RedirectStandardOutput = true, CreateNoWindow = true,StandardOutputEncoding = Encoding.GetEncoding(866)
            };

            var p = Process.Start(startInfo);
            //p.WaitForExit();
            var output = p.StandardOutput.ReadToEnd();

            foreach (var signalNameField in wlanSignalNameFields)
            {
                if (output.Contains(signalNameField))
                {
                    var signalValueStr = output
                        .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries))
                        .Where(split => split[0].Contains(signalNameField))
                        .Select(split => split[1].Trim()).First().TrimEnd('%');
                    var signalValue = Convert.ToInt32(signalValueStr);
                    return signalValue;
                }
            }
        }

        return 0;
    }
}