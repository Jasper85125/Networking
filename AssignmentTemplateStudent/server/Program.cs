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
        int latestMsgId = 0;

        while (true)
        {
            try
            {
                byte[] buffer = new byte[1024];
                EndPoint remoteEndPoint = clientEndPoint;
                int bytesReceived = listener.ReceiveFrom(buffer, ref remoteEndPoint);

                string receivedMessageJson = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                Message? receivedMessage = JsonSerializer.Deserialize<Message>(receivedMessageJson);

                if (receivedMessage != null)
                {
                    Console.WriteLine($"Received message: {receivedMessage.MsgType}");
                    latestMsgId = receivedMessage.MsgId;

                    switch (receivedMessage.MsgType)
                    {
                        case MessageType.Hello:
                            latestMsgId = SendWelcomeMessage(listener, remoteEndPoint, receivedMessage.MsgId);
                            break;
                        case MessageType.DNSLookup:
                            listener.ReceiveTimeout = 1000;
                            latestMsgId = HandleDNSLookup(listener, remoteEndPoint, receivedMessage);
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
            catch (SocketException)
            {
                SendEndMessage(listener, clientEndPoint, latestMsgId);
                listener.ReceiveTimeout = 0;
                
            }
        }
    }

    private int SendWelcomeMessage(Socket listener, EndPoint remoteEndPoint, int msgId)
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
        Console.WriteLine("Sent WELCOME message to client.\n");
        return msgId + 1;
    }

    private int HandleDNSLookup(Socket listener, EndPoint remoteEndPoint, Message dnsLookupMessage)
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
                Console.WriteLine("Sent DNSLookupReply message to client.\n");
                return dnsLookupMessage.MsgId + 1;
            }
            else
            {
                Console.WriteLine("lookupname was not found in DNSrecords.json.\n");
                Message dnsLookupReplyMessage = new()
                {
                    MsgId = dnsLookupMessage.MsgId + 1,
                    MsgType = MessageType.Error,
                    Content = "Error: Record not found"
                };

                string dnsLookupReplyMessageJson = JsonSerializer.Serialize(dnsLookupReplyMessage);
                byte[] dnsLookupReplyMessageBytes = Encoding.ASCII.GetBytes(dnsLookupReplyMessageJson);

                listener.SendTo(dnsLookupReplyMessageBytes, remoteEndPoint);
                Console.WriteLine("Sent Error Reply message to client.\n");
                return dnsLookupMessage.MsgId + 1;
            }
        }
        else
        {
            Console.WriteLine("lookupname or DNSrecords.json is empty.\n");
        }
        return dnsLookupMessage.MsgId;
    }

    private void SendEndMessage(Socket listener, EndPoint remoteEndPoint, int msgId)
    {
        Message endMessage = new()
        {
            MsgId = msgId + 1,
            MsgType = MessageType.End,
            Content = "No Lookups anymore"
        };

        string endMessageJSON = JsonSerializer.Serialize(endMessage);
        byte[] endMessageBytes = Encoding.ASCII.GetBytes(endMessageJSON);

        listener.SendTo(endMessageBytes, remoteEndPoint);
        Console.WriteLine("Sent END message to client.\n");
    }
}