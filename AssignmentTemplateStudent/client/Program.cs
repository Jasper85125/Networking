using System.Collections.Immutable;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibData;

// SendTo();
class Program
{
    static void Main(string[] args)
    {
        ClientUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ClientUDP
{

    //TODO: [Deserialize Setting.json]
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);


    public static void start()
    {

        //TODO: [Create endpoints and socket]
        if (setting == null || string.IsNullOrEmpty(setting.ClientIPAddress) || string.IsNullOrEmpty(setting.ServerIPAddress))
        {
            throw new InvalidOperationException("Invalid settings in configuration file.");
        }

        IPAddress clientIPAddress = IPAddress.Parse(setting.ClientIPAddress);
        int clientPortNumber = setting.ClientPortNumber;
        IPEndPoint clientEndPoint = new IPEndPoint(clientIPAddress, clientPortNumber);

        IPAddress serverIPAddress = IPAddress.Parse(setting.ServerIPAddress);
        int serverPortNumber = setting.ServerPortNumber;
        IPEndPoint serverEndPoint = new IPEndPoint(serverIPAddress, serverPortNumber);


        //TODO: [Create and send HELLO]
        UdpClient client = new UdpClient(clientEndPoint);
        byte[] hello = Encoding.ASCII.GetBytes("HELLO");
        client.Send(hello, hello.Length, serverEndPoint);


        //TODO: [Receive and print Welcome from server]
        byte[] welcome = client.Receive(ref serverEndPoint);
        Console.WriteLine(Encoding.ASCII.GetString(welcome));

        // TODO: [Create and send DNSLookup Message]


        //TODO: [Receive and print DNSLookupReply from server]


        //TODO: [Send Acknowledgment to Server]

        // TODO: [Send next DNSLookup to server]
        // repeat the process until all DNSLoopkups (correct and incorrect onces) are sent to server and the replies with DNSLookupReply

        //TODO: [Receive and print End from server]





    }
}