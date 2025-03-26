using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LibData;

class Program
{
    static void Main(string[] args)
    {
        ServerUDP server = new ServerUDP();
        server.Start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ServerUDP
{
    private readonly Setting? setting;
    private readonly List<DNSRecord>? records;

    public ServerUDP()
    {
        string configFile = Path.Combine(AppContext.BaseDirectory, "../../../../Setting.json");
        string configContent = File.ReadAllText(configFile);
        setting = JsonSerializer.Deserialize<Setting>(configContent);

        string recordsFile = Path.Combine(AppContext.BaseDirectory, "../../../DNSrecords.json");
        string recordsContent = File.ReadAllText(recordsFile);
        records = JsonSerializer.Deserialize<List<DNSRecord>>(recordsContent);
    }

    public void Start()
    {
        if (setting == null || string.IsNullOrEmpty(setting.ClientIPAddress) || string.IsNullOrEmpty(setting.ServerIPAddress))
        {
            throw new InvalidOperationException("Invalid settings in configuration file.");
        }

        IPEndPoint serverEndPoint = new(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
        IPEndPoint clientEndPoint = new(IPAddress.Parse(setting.ClientIPAddress), setting.ClientPortNumber);

        using Socket listener = new(serverEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        listener.Bind(serverEndPoint);

        while (true)
        {
            byte[] buffer = new byte[1024];
            EndPoint remoteEndPoint = clientEndPoint;
            int bytesReceived = listener.ReceiveFrom(buffer, ref remoteEndPoint);

            string receivedMessageJson = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
            Message? receivedMessage = JsonSerializer.Deserialize<Message>(receivedMessageJson);

            if (receivedMessage != null)
            {
                Console.WriteLine($"Received message: {receivedMessage.MsgType}");

                switch (receivedMessage.MsgType)
                {
                    case MessageType.Hello:
                        SendWelcomeMessage(listener, remoteEndPoint, receivedMessage.MsgId);
                        break;
                    case MessageType.DNSLookup:
                        HandleDNSLookup(listener, remoteEndPoint, receivedMessage);
                        break;
                    case MessageType.Ack:
                        Console.WriteLine("Received ACK from client.");
                        break;
                    default:
                        Console.WriteLine("Received an invalid or unexpected message.");
                        break;
                }
            }
        }
    }

    private void SendWelcomeMessage(Socket listener, EndPoint remoteEndPoint, int msgId)
    {
        Message welcomeMessage = new()
        {
            MsgId = msgId + 1,
            MsgType = MessageType.Welcome,
            Content = "Welcome to the server!"
        };

        string welcomeMessageJson = JsonSerializer.Serialize(welcomeMessage);
        byte[] welcomeMessageBytes = Encoding.ASCII.GetBytes(welcomeMessageJson);

        listener.SendTo(welcomeMessageBytes, remoteEndPoint);
        Console.WriteLine("Sent WELCOME message to client.");
    }

    private void HandleDNSLookup(Socket listener, EndPoint remoteEndPoint, Message dnsLookupMessage)
    {
        string? lookupName = dnsLookupMessage.Content?.ToString();
        if (!string.IsNullOrEmpty(lookupName) && records != null)
        {
            DNSRecord? foundRecord = records.FirstOrDefault(record => record.Name.Equals(lookupName, StringComparison.OrdinalIgnoreCase));

            if (foundRecord != null)
            {
                Message dnsLookupReplyMessage = new()
                {
                    MsgId = dnsLookupMessage.MsgId + 1,
                    MsgType = MessageType.DNSLookupReply,
                    Content = JsonSerializer.Serialize(foundRecord)
                };

                string dnsLookupReplyMessageJson = JsonSerializer.Serialize(dnsLookupReplyMessage);
                byte[] dnsLookupReplyMessageBytes = Encoding.ASCII.GetBytes(dnsLookupReplyMessageJson);

                listener.SendTo(dnsLookupReplyMessageBytes, remoteEndPoint);
                Console.WriteLine("Sent DNSLookupReply message to client.");
            }
        }
    }
}