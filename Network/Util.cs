using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

public static class Util
{
    public static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }

        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    public static List<string> GetListIPAddresses()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        return host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).Select(ip=>ip.ToString()).ToList();
    }

    public static bool ValidateIPv4(string ipString)
    {
        if (String.IsNullOrWhiteSpace(ipString))
        {
            return false;
        }

        string[] splitValues = ipString.Split('.');
        if (splitValues.Length != 4)
        {
            return false;
        }

        byte tempForParsing;

        return splitValues.All(r => byte.TryParse(r, out tempForParsing));
    }
}