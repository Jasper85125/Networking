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
    // Check if a specific port is already in use
    private bool IsPortInUse(int port)
    {
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                return false;
            }
        }
        catch (SocketException)
        {
            return true;
        }
    }

    private readonly Setting? setting; // Server and client settings
    private readonly List<DNSRecord>? records; // List of DNS records

    public ServerUDP()
    {
        // Load server and client settings from Setting.json
        string configFile = Path.Combine(AppContext.BaseDirectory, "../../../../Setting.json");
        string configContent = File.ReadAllText(configFile);
        setting = JsonSerializer.Deserialize<Setting>(configContent);

        if (setting != null)
        {
            // Ensure client and server do not use the same port
            if (setting.ClientPortNumber == setting.ServerPortNumber)
            {
                setting.ClientPortNumber += 1;
            }

            // Find an available port for the server
            while (IsPortInUse(setting.ServerPortNumber))
            {
                Console.WriteLine($"Server port {setting.ServerPortNumber} is in use. Trying next port...");
                setting.ServerPortNumber += 1;
            }

            // Find an available port for the client
            while (IsPortInUse(setting.ClientPortNumber))
            {
                Console.WriteLine($"Client port {setting.ClientPortNumber} is in use. Trying next port...");
                setting.ClientPortNumber += 1;
            }

            Console.WriteLine($"Server running on port {setting.ServerPortNumber}");
            Console.WriteLine($"Client will use port {setting.ClientPortNumber}");
        }

        // Load DNS records from DNSrecords.json
        string recordsFile = Path.Combine(AppContext.BaseDirectory, "../../../DNSrecords.json");
        string recordsContent = File.ReadAllText(recordsFile);
        records = JsonSerializer.Deserialize<List<DNSRecord>>(recordsContent);
    }

    public void Start()
    {
        try
        {
            // Validate settings
            if (setting == null || string.IsNullOrEmpty(setting.ClientIPAddress) || string.IsNullOrEmpty(setting.ServerIPAddress))
            {
                throw new InvalidOperationException("Invalid settings in configuration file.");
            }

            // Create server and client endpoints
            IPEndPoint serverEndPoint = new(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
            IPEndPoint clientEndPoint = new(IPAddress.Parse(setting.ClientIPAddress), setting.ClientPortNumber);

            // Create a UDP socket and bind it to the server endpoint
            using Socket listener = new(serverEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            listener.Bind(serverEndPoint);

            int latestMsgId = 0; // Track the latest message ID
            while (true)
            {
                try
                {
                    // Receive data from the client
                    byte[] buffer = new byte[1024];
                    EndPoint remoteEndPoint = clientEndPoint;
                    int bytesReceived = listener.ReceiveFrom(buffer, ref remoteEndPoint);

                    // Deserialize the received message
                    string receivedMessageJson = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                    Message? receivedMessage = JsonSerializer.Deserialize<Message>(receivedMessageJson);

                    if (receivedMessage != null)
                    {
                        Console.WriteLine($"Received message: {receivedMessage.MsgType}");
                        Console.WriteLine($"Message ID: {receivedMessage.MsgId}");

                        // Handle the message based on its type
                        switch (receivedMessage.MsgType)
                        {
                            case MessageType.Hello:
                                latestMsgId = SendWelcomeMessage(listener, remoteEndPoint, receivedMessage.MsgId);
                                break;
                            case MessageType.DNSLookup:
                                listener.ReceiveTimeout = 1000; // Set timeout for DNS lookup so when it stops receiving, it sends an END message to the client
                                latestMsgId = HandleDNSLookup(listener, remoteEndPoint, receivedMessage, latestMsgId);
                                break;
                            case MessageType.Ack:
                                Console.WriteLine("Received ACK from client.\n");
                                break;
                            default:
                                Console.WriteLine("Received an invalid or unexpected message.");
                                break;
                        }
                    }
                }
                catch (SocketException)
                {
                    // Send an END message to the client when a timeout occurs
                    SendEndMessage(listener, clientEndPoint, latestMsgId);
                    listener.ReceiveTimeout = 0; // Reset the timeout to 0 to stop sending END messages
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            throw new InvalidOperationException("Invalid settings in configuration file.");
        }
    }

    // Send a welcome message to the client
    private int SendWelcomeMessage(Socket listener, EndPoint remoteEndPoint, int msgId)
    {
        if (msgId == 1)
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
            return welcomeMessage.MsgId;
        }
        else
        {
            Console.WriteLine("Received an invalid or unexpected message.\n");
            Message errorMessage = new()
            {
                MsgId = msgId + 1,
                MsgType = MessageType.Error,
                Content = "Error: Invalid message ID"
            };

            string errorMessageJson = JsonSerializer.Serialize(errorMessage);
            byte[] errorMessageBytes = Encoding.ASCII.GetBytes(errorMessageJson);

            listener.SendTo(errorMessageBytes, remoteEndPoint);
            Console.WriteLine("Sent Error message to client.\n");
            return errorMessage.MsgId;
        }
    }

    // Convert a JSON string to a DNSRecord object
    public static DNSRecord ConvertToDNSRecord(string message)
    {
        try
        {
            DNSRecord? record = JsonSerializer.Deserialize<DNSRecord>(message);
            return record;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deserializing DNSRecord");
            return null;
        }
    }

    // Handle a DNS lookup request from the client
    private int HandleDNSLookup(Socket listener, EndPoint remoteEndPoint, Message dnsLookupMessage, int msgId)
    {
        // checks if the message ID is one more than the last message ID
        if (msgId + 1 == dnsLookupMessage.MsgId)
        {
            string jsonContent = JsonSerializer.Serialize(dnsLookupMessage.Content);
            
            //Checks if the content is a valid DNSRecord object
            DNSRecord? record = ConvertToDNSRecord(jsonContent);
            if (record == null)
            {
                //Sends an error message to the client if the content is not a valid DNSRecord object
                Console.WriteLine("Invalid content\n");
                Message dnsLookupReplyMessage = new()
                {
                    MsgId = dnsLookupMessage.MsgId,
                    MsgType = MessageType.Error,
                    Content = "Error: Invalid content"
                };

                string dnsLookupReplyMessageJson = JsonSerializer.Serialize(dnsLookupReplyMessage);
                byte[] dnsLookupReplyMessageBytes = Encoding.ASCII.GetBytes(dnsLookupReplyMessageJson);

                listener.SendTo(dnsLookupReplyMessageBytes, remoteEndPoint);
                Console.WriteLine("Sent Error Reply message to client.\n");
                return dnsLookupMessage.MsgId;
            }

            //Checks if there is a valid DNSRecord that shares the name and type with the one in the content
            string? lookupName = record.Name?.ToString();
            string? lookupType = record.Type?.ToString();
            if (!string.IsNullOrEmpty(lookupName) && records != null)
            {
                DNSRecord? foundRecord = records.FirstOrDefault(record => record.Name.Equals(lookupName, StringComparison.OrdinalIgnoreCase)
                                                                && record.Type.Equals(lookupType, StringComparison.OrdinalIgnoreCase));

                if (foundRecord != null)
                {
                    Message dnsLookupReplyMessage = new()
                    {
                        MsgId = dnsLookupMessage.MsgId,
                        MsgType = MessageType.DNSLookupReply,
                        Content = JsonSerializer.Serialize(foundRecord)
                    };

                    string dnsLookupReplyMessageJson = JsonSerializer.Serialize(dnsLookupReplyMessage);
                    byte[] dnsLookupReplyMessageBytes = Encoding.ASCII.GetBytes(dnsLookupReplyMessageJson);

                    listener.SendTo(dnsLookupReplyMessageBytes, remoteEndPoint);
                    Console.WriteLine("Sent DNSLookupReply message to client.\n");
                    return dnsLookupMessage.MsgId;
                }
                else
                {
                    Console.WriteLine("lookupname was not found in DNSrecords.json.\n");
                    Message dnsLookupReplyMessage = new()
                    {
                        MsgId = dnsLookupMessage.MsgId,
                        MsgType = MessageType.Error,
                        Content = "Error: Record not found"
                    };

                    string dnsLookupReplyMessageJson = JsonSerializer.Serialize(dnsLookupReplyMessage);
                    byte[] dnsLookupReplyMessageBytes = Encoding.ASCII.GetBytes(dnsLookupReplyMessageJson);

                    listener.SendTo(dnsLookupReplyMessageBytes, remoteEndPoint);
                    Console.WriteLine("Sent Error Reply message to client.\n");
                    return dnsLookupMessage.MsgId;
                }
            }
            else
            {
                Console.WriteLine("lookupname or DNSrecords.json is empty.\n");
            }
            return dnsLookupMessage.MsgId;
        }
        else
        {
            Console.WriteLine($"Received an invalid or unexpected message. The message ID should be {msgId + 1}\n");
            Message errorMessage = new()
            {
                MsgId = msgId + 1,
                MsgType = MessageType.Error,
                Content = "Error: Invalid message ID"
            };
            string dnsLookupReplyErrorMessageJson = JsonSerializer.Serialize(errorMessage);
            byte[] dnsLookupReplyErrorMessageBytes = Encoding.ASCII.GetBytes(dnsLookupReplyErrorMessageJson);
            listener.SendTo(dnsLookupReplyErrorMessageBytes, remoteEndPoint);
            Console.WriteLine("Sent Error message to client.\n");
        }
        return msgId + 1;
    }

    // Send an END message to the client
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